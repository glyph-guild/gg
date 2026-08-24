using System.Text.Json;
using Gg.Contracts;
using Gg.Contracts.Description;
using Gg.Runner.Execution;
using Gg.Runner.Vcs;

namespace Gg.Runner.Tests;

/// <summary>
/// A real agent handed a seed acts on what only the seed carries, and does not
/// redo what the record rules out.
/// </summary>
/// <remarks>
/// <para>
/// <b>What this proves that no fake can.</b> <c>ResumptionContextTests</c>
/// proves the seed reaches the request and <c>ResumptionPromptTests</c> proves
/// the words around it; neither proves an agent CONSUMED it. Here the intent
/// demands a value that exists in exactly one place - the prior attempt's own
/// account inside the seed - so the tree can only end up correct if the agent
/// received the record and read it. Slice seven's walk proved delivery across
/// hosts and said out loud that no executor ran; this is the executor running.
/// </para>
/// <para>
/// <b>It goes through <see cref="RunnerHost"/> and takes its executor from
/// <see cref="ExecutorConfiguration"/></b> - the product path, probe included -
/// for the same reason <c>InjectedRepositoryTests</c> does: a proof that
/// constructed its own executor would measure the component again.
/// </para>
/// </remarks>
[Category("RealAgent")]
public class ResumedFlightTests
{
    /// <summary>
    /// The value the intent demands, present nowhere in the repository.
    /// </summary>
    /// <remarks>
    /// It reaches the agent through one channel only: the prior attempt's
    /// account inside the seed. A guard assertion below re-checks that the
    /// fixture never contains it, because the moment somebody writes it into a
    /// fixture file this whole test starts passing on a run that read nothing.
    /// </remarks>
    private const string Calibration = "6083";

    private static string Binary =>
        Environment.GetEnvironmentVariable(ExecutorConfiguration.BinaryVariable)
        ?? throw new InvalidOperationException(
            $"{ExecutorConfiguration.BinaryVariable} is not set. This drives a real agent "
          + "against a resumed flight; skipping it would leave the consumption half of the "
          + "agent-to-agent claim proven by a string a test wrote.");

    /// <summary>The rendered record of the attempt this flight resumes.</summary>
    /// <remarks>
    /// Composed and rendered by the real composer - the same rendering the
    /// control plane sends on a lease - so what the agent reads here is what an
    /// agent on a second machine would read.
    /// </remarks>
    private static string Seed() => TakeSeedComposer.Render(TakeSeedComposer.Compose(
        "GG-88", "flight-resumed",
        new LoopDigest
        {
            LoopId = "implement",
            FilesReadNotEdited = ["src/config.py"],
            FilesEdited = ["src/orders.py"],
            Searches = ["calibration"],
            Errors = [],
            RefusedMoves = [],
            Attempts = 1,
            StopReason = "exhausted",
        },
        account: "I measured the calibration before the clock ran out: the value is "
               + Calibration + ". src/orders.py is already updated to read it; do not "
               + "change that file again."));

    /// <summary>A repository whose issue can only be finished with the record.</summary>
    private sealed class ResumableRepoFixture : IDisposable
    {
        internal string Directory { get; }

        internal string BarePath { get; }

        internal ResumableRepoFixture()
        {
            Directory = Path.Combine(Path.GetTempPath(), "gg-resumed", Guid.NewGuid().ToString("n"));
            System.IO.Directory.CreateDirectory(Directory);

            BarePath = Path.Combine(Directory, "widgets.git");
            var work = Path.Combine(Directory, "work");

            GitFixture.Run(Directory, "init", "--bare", "--initial-branch=main", BarePath);
            GitFixture.Run(Directory, "clone", BarePath, work);

            Write(work, "ISSUE.md",
                "# Issue 88\n\nSet CALIBRATION in src/config.py to the measured value.\n\n"
              + "A previous automated attempt at this flight may have measured it already. "
              + "If you were handed that attempt's handoff record, its agent's own account "
              + "states the number - use exactly that number. Do not edit any file the "
              + "record lists as already changed. If you have no such record, leave "
              + "CALIBRATION at 0 and stop.\n");
            Write(work, "src/config.py",
                "# Calibration is measured, never guessed.\nCALIBRATION = 0\n");
            Write(work, "src/orders.py",
                "from .config import CALIBRATION\n\n\ndef total(amount):\n"
              + "    return round(amount * CALIBRATION)\n");

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
    public async Task A_real_agent_acts_on_what_only_the_seed_carries_and_leaves_the_ruled_out_file_alone()
    {
        _ = Binary;

        using var fixture = new ResumableRepoFixture();
        using var trees = new ScratchTreeRoot();
        await using var surface = new StubRunnerSurface();

        // ---- 0. THE GUARD. The value must be reachable through the seed and
        // nothing else, or every assertion below passes on a run that read nothing.
        foreach (var file in Directory.EnumerateFiles(fixture.Directory, "*", SearchOption.AllDirectories)
                     .Where(f => !f.Contains(".git", StringComparison.Ordinal)))
        {
            await Assert.That(File.ReadAllText(file)).DoesNotContain(Calibration)
                .Because($"{file} must not carry the calibration value - the seed is the only "
                       + "channel, and a fixture leak would prove nothing.");
        }

        surface.Grant = new LeaseGranted
        {
            LeaseId = "lease-resumed",
            Generation = 1,
            FlightId = "flight-resumed",
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
            IntentUri = "https://forge.example/acme/widgets/issues/88",
            Loop = new LeaseLoop
            {
                LoopId = "implement",
                Executor = ExecutorRungs.Frontier,
                Moves = [LoopMoves.Read, LoopMoves.Edit],
                WallClockSeconds = 600,
                OnExhaustion = ExhaustionPolicies.HandoffToAgent,
                // THE DELIVERED RECORD, exactly as a second machine's lease
                // carries it. Everything this test proves hangs off this member.
                ResumesFrom = Seed(),
            },
        };

        using var stopping = new CancellationTokenSource();
        surface.OnFacts = stopping.Cancel;

        await RunnerHost.RunAsync(
            new Uri(surface.BaseAddress),
            "runner-resumed",
            "token",
            ["local"],
            TimeSpan.FromSeconds(1),
            new NoCredentialResolver(),
            trees.Workspace(new LocalVcsAdapter(fixture.Directory)),
            stopping.Token,
            executor: ExecutorConfiguration.FromEnvironment());

        var tree = Directory
            .EnumerateDirectories(trees.Handoff.Path, "*", SearchOption.AllDirectories)
            .FirstOrDefault(d => Directory.Exists(Path.Combine(d, ".git")));

        await Assert.That(tree).IsNotNull()
            .Because("the flight did not land, so its tree is held - and the tree is what "
                   + "the consumption assertions read.");

        var shipped = string.Join("\n", surface.FactBodies);
        var digest = Facts(shipped, FactKinds.LoopDigest)!.Value.GetProperty("loopDigest");
        var edited = digest.GetProperty("filesEdited").EnumerateArray()
            .Select(x => x.GetString()).ToList();

        // ---- 1. RECEIVED AND ACTED ON. The value exists in one place: the prior
        // attempt's account inside the seed. It is now in the tree.
        var config = File.ReadAllText(Path.Combine(tree!, "src", "config.py"));
        await Assert.That(config).Contains(Calibration)
            .Because("the calibration value is reachable only through the delivered seed, so "
                   + "its presence in the tree is the consumption, measured.");

        // ---- 2. CARRIED ON RATHER THAN STARTED OVER. The record lists
        // src/orders.py as already changed, and the intent says not to touch what
        // the record rules out. The digest is the extractor's account, not the
        // agent's.
        await Assert.That(edited).Contains("src/config.py");
        await Assert.That(edited).DoesNotContain("src/orders.py")
            .Because("redoing what the record rules out is starting over with extra steps, "
                   + "and it is exactly what the seed exists to prevent.");

        // ---- 3. THE LOOP FINISHED THE WORK. A completed outcome, because the
        // record plus the tree was enough - the resumed half did not exhaust again.
        var outcome = Facts(shipped, FactKinds.LoopOutcome)!.Value.GetProperty("loop");
        await Assert.That(outcome.GetProperty("outcome").GetString())
            .IsEqualTo(LoopOutcomes.Completed);

        // ---- 4. NOTHING MACHINE-SHAPED IN THE FACTS A SEED IS COMPOSED FROM.
        // Scoped to the loop facts rather than the whole batch, because the
        // local provider's slug IS a path by design (the deployment names the
        // subtree, so the control plane can only ask for what it was told) -
        // but the digest and the outcome are what a NEXT seed renders, and a
        // machine detail there would ride every handoff after this one.
        var outcomeFact = Facts(shipped, FactKinds.LoopOutcome)!.Value.ToString();
        foreach (var crossing in (string[])[digest.ToString(), outcomeFact])
        {
            await Assert.That(crossing).DoesNotContain(fixture.Directory);
            await Assert.That(crossing).DoesNotContain(tree!)
                .Because("the next seed is composed from these facts, and a path in them "
                       + "re-couples the flight to the machine the slice decoupled it from.");
        }
    }

    /// <summary>The first fact of a kind, from what really went on the wire.</summary>
    private static JsonElement? Facts(string bodies, string kind)
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
