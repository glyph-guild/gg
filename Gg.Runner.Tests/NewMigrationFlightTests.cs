using System.Text.Json;
using Gg.Contracts;
using Gg.Contracts.Description;
using Gg.Runner.Execution;
using Gg.Runner.Vcs;

namespace Gg.Runner.Tests;

/// <summary>
/// The case the gate was built for: a flight that creates a new migration.
/// </summary>
/// <remarks>
/// <para>
/// <b>It has never happened.</b> <c>when: change.manifest touches migrations/**</c>
/// is slice three's whole subject and it has not fired in production once, for
/// three independent reasons: no manifest crossed at all, uncommitted work was
/// invisible to the extractor, and no move in the vocabulary granted creating a
/// file. The first two were fixed in earlier steps. This one needed a word.
/// </para>
/// <para>
/// <b>Through the product path</b>, for the reason the injection test is: a test
/// that assembled its own loop would measure the components again, and the thing
/// in question is whether a flight - the thing a person starts - produces a
/// manifest a condition can attach to.
/// </para>
/// <para>
/// This is the runner half. The obligation attaching, the gate opening and a
/// person deciding are the control plane's, and they read the manifest this
/// produces.
/// </para>
/// </remarks>
[Category("RealAgent")]
public class NewMigrationFlightTests
{
    /// <summary>A repository whose convention is one new file per schema change.</summary>
    private sealed class MigrationRepoFixture : IDisposable
    {
        internal string Directory { get; }

        internal string BarePath { get; }

        internal MigrationRepoFixture()
        {
            Directory = Path.Combine(Path.GetTempPath(), "gg-newmig", Guid.NewGuid().ToString("n"));
            System.IO.Directory.CreateDirectory(Directory);

            BarePath = Path.Combine(Directory, "widgets.git");
            var work = Path.Combine(Directory, "work");

            GitFixture.Run(Directory, "init", "--bare", "--initial-branch=main", BarePath);
            GitFixture.Run(Directory, "clone", BarePath, work);

            Write(work, "ISSUE.md",
                "# Issue 1: orders need a discount\n\n"
              + "Orders should carry a discount. Add the column to the schema, and read it "
              + "where orders are loaded.\n\n"
              + "Schema changes live in `migrations/`, one NEW numbered file per change - "
              + "never by editing an existing one.\n");
            Write(work, "src/orders.py",
                "def load_order(row):\n    return {\"id\": row[0], \"total\": row[1]}\n");
            Write(work, "migrations/0001_init.sql",
                "create table orders (id integer primary key, total numeric);\n");

            GitFixture.Run(work, "add", ".");
            GitFixture.Run(work, "commit", "-m", "base");
            GitFixture.Run(work, "push", "origin", "main");
        }

        private static void Write(string root, string relative, string content)
        {
            var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
            System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }

        public void Dispose()
        {
            if (System.IO.Directory.Exists(Directory))
            {
                System.IO.Directory.Delete(Directory, recursive: true);
            }
        }
    }

    [Test]
    public async Task A_flight_declaring_write_produces_a_manifest_that_touches_migrations()
    {
        using var fixture = new MigrationRepoFixture();
        using var trees = new ScratchTreeRoot();
        await using var surface = new StubRunnerSurface();

        surface.Grant = new LeaseGranted
        {
            LeaseId = "lease-newmig",
            Generation = 1,
            FlightId = "flight-newmig",
            FlightNumber = FlightRef.Format(88),
            Repos =
            [
                new LeaseRepoRef
                {
                    Provider = LocalVcsAdapter.ProviderKey,
                    Slug = fixture.BarePath,
                    PinnedRef = "refs/heads/main",
                },
            ],
            Credentials = [],
            ClassificationCeiling = Classifications.Internal,
            ClassificationRules = ClassificationRules.Default,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30),
            RenewWithinSeconds = 30,
            IntentUri = "https://forge.example/acme/widgets/issues/1",
            Loop = new LeaseLoop
            {
                LoopId = "implement",
                Executor = ExecutorRungs.Frontier,
                // THE WORD THAT DID NOT EXIST. Without it this flight produces the
                // same manifest every earlier one did - the code edit alone, and a
                // condition on migrations/** that never fires.
                Moves = [LoopMoves.Read, LoopMoves.Edit, LoopMoves.Write],
                WallClockSeconds = 600,
                OnExhaustion = "handoff-to-human",
            },
        };

        using var stopping = new CancellationTokenSource();
        surface.OnFacts = stopping.Cancel;

        await RunnerHost.RunAsync(
            new Uri(surface.BaseAddress),
            "runner-newmig",
            "token",
            ["local"],
            TimeSpan.FromSeconds(1),
            new NoCredentialResolver(),
            trees.Workspace(new LocalVcsAdapter(fixture.Directory)),
            stopping.Token,
            executor: ExecutorConfiguration.FromEnvironment());

        var shipped = string.Join("\n", surface.FactBodies);
        var change = Fact(shipped, FactKinds.ChangeManifest)!.Value.GetProperty("change");
        var paths = change.GetProperty("paths").EnumerateArray()
            .Select(p => p.GetProperty("path").GetString()!)
            .ToList();

        if (Environment.GetEnvironmentVariable("GG_FLIGHT_RECORD") is { Length: > 0 } into)
        {
            File.WriteAllText(into, JsonSerializer.Serialize(
                JsonDocument.Parse(shipped.Split('\n')[0]).RootElement,
                new JsonSerializerOptions { WriteIndented = true }));
        }

        // THE CRITERION. A path under migrations/ that the agent created, in a
        // manifest a real flight shipped - which is what `when: touches
        // migrations/**` has been waiting for since it was written.
        await Assert.That(paths.Any(p => p.StartsWith("migrations/", StringComparison.Ordinal)
                                      && p != "migrations/0001_init.sql")).IsTrue()
            .Because("a NEW migration, which is the commonest real case for this gate and the "
                   + "one no flight could produce until the vocabulary had a word for it. "
                   + $"Paths: {string.Join(", ", paths)}");

        // ADDED, not modified. The distinction is the whole point: the earlier
        // capture had to amend an existing migration because creating one was
        // impossible, and `added` is the shape a reviewer expects.
        var migration = change.GetProperty("paths").EnumerateArray()
            .First(p => p.GetProperty("path").GetString()!
                .StartsWith("migrations/0002", StringComparison.Ordinal));

        await Assert.That(migration.GetProperty("change").GetString()).IsEqualTo(ChangeKinds.Added);
        await Assert.That(migration.GetProperty("linesAdded").GetInt32()).IsGreaterThan(0)
            .Because("and it has contents, so something really was written.");

        // AND IT IS UNCOMMITTED, which is the earlier repair still holding: the
        // agent is told not to commit, so a manifest that could only see commits
        // would see none of this.
        await Assert.That(change.GetProperty("baseCommit").GetString())
            .IsEqualTo(change.GetProperty("headCommit").GetString());

        // Nothing was refused, because the envelope declared what the work needed.
        var digest = Fact(shipped, FactKinds.LoopDigest)!.Value.GetProperty("loopDigest");
        await Assert.That(digest.GetProperty("refusedMoves").EnumerateArray()).IsEmpty()
            .Because("declaring `write` is what makes this flight different from every one "
                   + "before it, and a refusal here would mean it had not taken effect.");
    }

    private static JsonElement? Fact(string bodies, string kind)
    {
        foreach (var body in bodies.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            using var document = JsonDocument.Parse(body);
            foreach (var fact in document.RootElement.GetProperty("facts").EnumerateArray())
            {
                if (fact.GetProperty("kind").GetString() == kind)
                {
                    return fact.Clone();
                }
            }
        }

        return null;
    }
}
