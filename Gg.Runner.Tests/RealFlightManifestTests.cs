using System.Text.Json;
using Gg.Contracts;
using Gg.Contracts.Description;
using Gg.Runner.Execution;
using Gg.Runner.Vcs;

namespace Gg.Runner.Tests;

/// <summary>
/// One real implement-intent flight, all the way through the runner's own loop,
/// and what its <c>change.manifest</c> actually said.
/// </summary>
/// <remarks>
/// <para>
/// <b>Excluded from CI by name</b>, on the same terms as
/// <see cref="AgainstRealAgentTests"/>: it needs the executor binary, a credential
/// and the network. What it produces is recorded by hand in the slice notes, which
/// is this repository's practice for facts that cost a real invocation.
/// </para>
/// <para>
/// The lease is shaped exactly as the control plane sends one - provider, slug and
/// pinned ref, and no base ref, because nothing control-plane-side populates that
/// member. The tree is a real clone, the agent is a real agent, and the manifest
/// is whatever <see cref="RunnerLoop"/> shipped.
/// </para>
/// </remarks>
[Category("RealAgent")]
public class RealFlightManifestTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 13, 12, 0, 0, TimeSpan.Zero);

    private static string Binary =>
        Environment.GetEnvironmentVariable("GG_EXECUTOR_BINARY")
        ?? throw new InvalidOperationException(
            "GG_EXECUTOR_BINARY is not set. This flies a real flight, and skipping it would leave "
          + "the manifest's real reading unrecorded.");

    /// <summary>A repository shaped like the one an implement intent arrives about.</summary>
    private sealed class FlightRepoFixture : IDisposable
    {
        internal string Directory { get; }

        internal string BarePath { get; }

        internal FlightRepoFixture()
        {
            Directory = Path.Combine(Path.GetTempPath(), "gg-real-flight", Guid.NewGuid().ToString("n"));
            System.IO.Directory.CreateDirectory(Directory);

            BarePath = Path.Combine(Directory, "widgets.git");
            var work = Path.Combine(Directory, "work");

            GitFixture.Run(Directory, "init", "--bare", "--initial-branch=main", BarePath);
            GitFixture.Run(Directory, "clone", BarePath, work);

            Write(work, "ISSUE.md",
                "# Issue 1: orders need a discount\n\n"
              + "Orders should carry a discount. Add the column to the schema, and read it "
              + "where orders are loaded.\n\n"
              + "Schema changes live in `migrations/`. This project has not shipped yet, so "
              + "amend the existing migration in place instead of adding a new one.\n");
            Write(work, "src/orders.py",
                "def load_order(row):\n"
              + "    return {\"id\": row[0], \"total\": row[1]}\n");
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

    private static LeaseGranted ALease(FlightRepoFixture fixture) => new()
    {
        LeaseId = "lease-1",
        Generation = 1,
        FlightId = "flight-1",
        FlightNumber = FlightRef.Format(1042),
        Repos =
        [
            // EXACTLY WHAT THE CONTROL PLANE SENDS. LeaseEndpoints populates provider,
            // slug, pinned ref and continues-from, and nothing populates BaseRef - so a
            // fixture that set one would be flying a flight this product cannot produce.
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
        ExpiresAt = T0.AddMinutes(30),
        RenewWithinSeconds = 5,
        IntentUri = "https://forge.example/acme/widgets/issues/1",
        Loop = new LeaseLoop
        {
            LoopId = "implement",
            Executor = ExecutorRungs.Frontier,
            Moves = [LoopMoves.Read, LoopMoves.Edit],
            WallClockSeconds = 600,
            OnExhaustion = "handoff-to-human",
        },
    };

    [Test]
    public async Task What_one_real_implement_flight_shipped_as_its_change_manifest()
    {
        using var fixture = new FlightRepoFixture();
        using var trees = new ScratchTreeRoot();

        var clock = new MovableClock(T0);
        var protocol = new FakeProtocol();
        protocol.Claims.Enqueue(new ClaimResult.Granted(ALease(fixture)));
        var observer = new RecordingObserver();

        var stopping = new CancellationTokenSource();
        var seen = 0;
        observer.OnEvent = _ =>
        {
            if (Interlocked.Increment(ref seen) >= 2)
            {
                stopping.Cancel();
            }
        };

        var loop = new RunnerLoop(
            protocol, clock,
            (span, token) =>
            {
                token.ThrowIfCancellationRequested();
                clock.Advance(span);
                return Task.CompletedTask;
            },
            observer, new NoCredentialResolver(),
            trees.Workspace(new LocalVcsAdapter(fixture.Directory)),
            new ClaudeCodeExecutor(Binary))
        {
            HoldFor = TimeSpan.FromSeconds(3),
        };

        await loop.RunAsync("runner-1", ["local"], stopping.Token);

        // THE TREE, held rather than released, because nothing landed. This is the
        // same tree the manifest was measured from.
        var held = Directory.EnumerateDirectories(trees.Handoff.Path, "*", SearchOption.AllDirectories)
            .FirstOrDefault(d => Directory.Exists(Path.Combine(d, ".git")));

        var shipped = protocol.ShippedFacts.SelectMany(b => b.Items).ToList();
        var manifest = shipped.FirstOrDefault(f => f.Kind == FactKinds.ChangeManifest);

        var record = new System.Text.StringBuilder()
            .AppendLine("=== what the loop reported ===")
            .AppendJoin('\n', observer.Events.Select(e => "  " + e)).AppendLine()
            .AppendLine("=== the real diff, from the tree the manifest was measured from ===")
            .AppendLine(held is null
                ? "  (no tree was held)"
                : "  git status --porcelain:\n" + GitFixture.Run(held, "status", "--porcelain")
                + "  git diff --numstat (tracked files only):\n" + GitFixture.Run(held, "diff", "--numstat"))
            .AppendLine("=== what the agent said it did (loop.digest) ===")
            .AppendLine(shipped.FirstOrDefault(f => f.Kind == FactKinds.LoopDigest) is not { } d
                ? "  (no digest)"
                : JsonSerializer.Serialize(d.LoopDigest, new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    }))
            .AppendLine("=== every fact the flight shipped ===")
            .AppendJoin('\n', shipped.Select(f => "  " + f.Kind)).AppendLine()
            .AppendLine("=== change.manifest, verbatim ===")
            .AppendLine(manifest is null
                ? "  (no change.manifest fact was shipped)"
                : JsonSerializer.Serialize(manifest.Change, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                }))
            .ToString();

        Console.WriteLine(record);

        // Written out as well as printed. A recording nobody can read is a
        // recording that did not happen.
        if (Environment.GetEnvironmentVariable("GG_FLIGHT_RECORD") is { Length: > 0 } into)
        {
            File.WriteAllText(into, record);
        }

        // AND THE MANIFEST ITSELF, in the shape the control plane's attachment
        // fixture reads. That fixture used to hold a diff run by hand between two
        // commits; this is the runner's own extraction from a real flight, which
        // is the thing the criterion always meant.
        if (Environment.GetEnvironmentVariable("GG_MANIFEST_CAPTURE") is { Length: > 0 } capture
            && manifest?.Change is { } change)
        {
            File.WriteAllText(capture, JsonSerializer.Serialize(
                new
                {
                    capturedFrom = "RealFlightManifestTests, the whole RunnerLoop against a "
                                 + "local bare repository",
                    capturedOn = "2026-08-13",
                    executor = "claude-code, headless, moves read+edit",
                    prompt = File.ReadAllText(Path.Combine(fixture.Directory, "work", "ISSUE.md")),
                    baseCommit = change.BaseCommit,
                    headCommit = change.HeadCommit,
                    paths = change.Paths.Select(p => new
                    {
                        path = p.Path,
                        change = p.Change,
                        linesAdded = p.LinesAdded,
                        linesRemoved = p.LinesRemoved,
                    }),
                },
                new JsonSerializerOptions { WriteIndented = true }));
        }

        await Assert.That(shipped).IsNotEmpty()
            .Because("a flight that shipped nothing would make this recording meaningless.");
    }
}
