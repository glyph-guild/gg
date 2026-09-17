using System.Text.Json;
using Gg.Contracts;
using Gg.Local;
using Gg.Runner.Execution;
using Gg.Runner.Sweeps;

namespace Gg.Runner.Tests;

/// <summary>
/// A sweep's agent is started with exactly the servers its served moves name:
/// the watch's tracker, bound to the watch's query, and gg in sweep mode.
/// </summary>
/// <remarks>
/// <para>
/// <b>S39.3-01.</b> The moves are the <c>sweep</c> kind's composition as the
/// control plane served them, and nothing a watch says widens them. Asserted
/// over the argument list for <c>NominateToolLaunchTests</c>' reason: a server
/// configured and never passed, or a tool granted without its move, is
/// invisible to a test that stops at the configuration.
/// </para>
/// <para>
/// <b>A credential goes only to a host this machine's operator paired it
/// with.</b> A watch names a host and a credential, and both arrive over the
/// wire. A runner that joined the two because a document asked would send a
/// customer's secret wherever the document pointed, so the pair must already
/// be declared here, in <c>GG_INTENT_HOSTS</c>, the same line a flight's
/// reader comes from. The launch then names the credential and never carries
/// it: the reader resolves it itself, from this machine's store.
/// </para>
/// <para>
/// <b>Nobody to ask.</b> A sweep has no flight in front of it and no person
/// behind it, so it is not given the tool for asking one, and its prompt does
/// not tell it to use one.
/// </para>
/// </remarks>
public class TheSweepLauncherAttachesTheWatchsServersTests
{
    private const string Host = "https://tracker.example/acme";

    private const string Paired = "local:acme/board";

    private const string Declared = "board=" + Host + "|" + Paired;

    private const string Filter =
        "SELECT [System.Id] FROM WorkItems WHERE [System.Tags] CONTAINS 'triage'";

    private const string Skill = "Nominate every untriaged item that names a customer.";

    private static readonly SelfInvocation Self = SelfInvocation.For("/opt/gg/gg", null)!;

    private static readonly DateTimeOffset Noon = new(2026, 9, 17, 12, 0, 0, TimeSpan.Zero);

    internal static WatchDocument AWatch(string host = Host, string credential = Paired) => new()
    {
        Shape = WatchShapes.WorkItems,
        Trigger = new WatchTrigger { Every = "1h" },
        Host = host,
        Credential = credential,
        Filter = Filter,
        Repository = "payments",
        Skill = ".goodgrief/skills/triage.md",
        Ref = "refs/heads/main",
        Mapping = new WatchMapping { Subject = "id", Version = "revision", IntentKey = "url" },
        PullPoint = PullPoints.ResidentRunner,
        Nominates = new Destination
        {
            Id = "what-a-sweep-opens",
            Kind = DestinationKinds.Flight,
            Requires = [],
            Opens = ["review", "triage"],
        },
    };

    internal static SweepRequest ASweep(
        IReadOnlyList<string>? moves = null, WatchDocument? watch = null) => new()
    {
        Action = new WatchAction
        {
            ActionId = Guid.CreateVersion7(Noon),
            Watch = "nightly-triage",
            WatchVersion = "nightly-triage@v3",
            Document = watch ?? AWatch(),
            Executor = WatchExecutors.Instructions,
            Moves = moves ?? [LoopMoves.Read, LoopMoves.Propose],
            Skill = new LeaseRepoRef
            {
                Provider = "a-forge",
                Slug = "acme/payments",
                PinnedRef = new string('a', 40),
            },
            DecidedAt = Noon.AddMinutes(-5),
        },
        Skill = Skill,
        TranscriptPath = "/state/transcripts/sweeps/nightly-triage/a.ndjson",
    };

    private static SweepLaunch Plan(
        SweepRequest sweep, string declared = Declared, SelfInvocation? self = null) =>
        SweepLauncher.Plan(
            sweep,
            IntentConfiguration.ServedTrackers(declared),
            self ?? Self,
            workingDirectory: "/scratch/sweeps/a",
            wallClock: TimeSpan.FromMinutes(15));

    private static SweepLaunch.Ready Ready(SweepRequest sweep) =>
        Plan(sweep) as SweepLaunch.Ready
        ?? throw new InvalidOperationException($"The plan refused: {Plan(sweep)}");

    private static IReadOnlyList<string> Arguments(SweepLaunch.Ready ready) =>
        ClaudeCodeExecutor.ArgumentsFor(ready.Request, [ready.Reader], self: ready.Self);

    private static JsonElement Server(IReadOnlyList<string> arguments, string key)
    {
        var at = arguments.ToList().IndexOf("--mcp-config");
        using var config = JsonDocument.Parse(arguments[at + 1]);
        return config.RootElement.GetProperty("mcpServers").GetProperty(key).Clone();
    }

    private static string[] Strings(JsonElement array) =>
        [.. array.EnumerateArray().Select(a => a.GetString()!)];

    private static IReadOnlyList<string> Granted(IReadOnlyList<string> arguments) =>
        [.. arguments.SkipWhile(a => a != "--allowedTools").Skip(1)];

    [Test]
    public async Task The_tracker_reader_is_the_one_its_operator_paired_with_the_watchs_host()
    {
        var ready = Ready(ASweep());

        var reader = Server(Arguments(ready), "board");

        await Assert.That(Strings(reader.GetProperty("args"))).IsEquivalentTo(
            (string[])
            [
                "runner", "read",
                "--provider", "board",
                "--host", Host,
                "--credential", Paired,
                "--query", Filter,
            ],
            TUnit.Assertions.Enums.CollectionOrdering.Matching)
            .Because("the watch's query is bound when the reader starts, so the agent can page "
                   + "through what was reviewed and ask for nothing else.");
        await Assert.That(reader.GetProperty("command").GetString()).IsEqualTo(Self.Command);
        await Assert.That(reader.TryGetProperty("env", out _)).IsFalse()
            .Because("the reader resolves the credential it is named, so there is nothing for "
                   + "a launch argument - which every `ps` on the host can read - to carry.");
        await Assert.That(ready.Reader.EnvironmentVariable).IsNull();
        await Assert.That(ready.Reader.Locator).IsNull();
    }

    [Test]
    public async Task The_gg_server_is_started_in_sweep_mode()
    {
        var ours = Server(Arguments(Ready(ASweep())), NominationTool.Server);

        await Assert.That(Strings(ours.GetProperty("args"))).IsEquivalentTo(
            (string[])["runner", "tools", NominationTool.Sweep.Flag],
            TUnit.Assertions.Enums.CollectionOrdering.Matching)
            .Because("in sweep mode the server offers the nomination a sweep makes - a subject "
                   + "and a version - and nothing a flight's agent is given.");
        await Assert.That(ours.TryGetProperty("env", out _)).IsFalse();
    }

    [Test]
    public async Task The_grant_is_the_sweeps_moves_and_nothing_else()
    {
        var granted = Granted(Arguments(Ready(ASweep())));

        await Assert.That(granted).Contains("mcp__board")
            .Because("reading the tracker is reading, and the sweep was granted `read`.");
        await Assert.That(granted).Contains(NominationTool.Qualified);
        await Assert.That(granted).DoesNotContain(HelpTool.Qualified)
            .Because("a sweep has nobody behind it to ask.");

        foreach (var withheld in (string[])
                 ["Edit", "Write", "Bash", "Grep", WorkItemProposalTool.Qualified, "mcp__gg"])
        {
            await Assert.That(granted).DoesNotContain(withheld);
        }
    }

    [Test]
    public async Task A_sweep_granted_no_read_is_not_launched()
    {
        var refused = Plan(ASweep(moves: [LoopMoves.Propose]));

        await Assert.That(refused).IsTypeOf<SweepLaunch.Refused>()
            .Because("an agent that may not look at its tracker can only report that it found "
                   + "nothing, which is the one report a sweep must never make blind.");
        await Assert.That(((SweepLaunch.Refused)refused).Diagnosis).Contains($"'{LoopMoves.Read}'");
    }

    [Test]
    public async Task A_host_this_runner_never_declared_is_refused()
    {
        var refused = Plan(ASweep(watch: AWatch(host: "https://elsewhere.example/acme")));

        await Assert.That(refused).IsTypeOf<SweepLaunch.Refused>();

        var diagnosis = ((SweepLaunch.Refused)refused).Diagnosis;
        await Assert.That(diagnosis).Contains("https://elsewhere.example/acme");
        await Assert.That(diagnosis).Contains(IntentConfiguration.ServedVariable)
            .Because("the fix is a line on this machine, and the sentence names where it goes.");
    }

    [Test]
    public async Task A_credential_its_operator_did_not_pair_with_that_host_is_refused()
    {
        var refused = Plan(ASweep(watch: AWatch(credential: "local:acme/admin")));

        await Assert.That(refused).IsTypeOf<SweepLaunch.Refused>()
            .Because("a document naming a host and a credential must not be able to send the "
                   + "second to the first unless this machine already does.");

        var diagnosis = ((SweepLaunch.Refused)refused).Diagnosis;
        await Assert.That(diagnosis).Contains("local:acme/admin");
        await Assert.That(diagnosis).DoesNotContain(Paired)
            .Because("which credential this machine pairs with the host is its operator's "
                   + "configuration, and the diagnosis travels to the control plane.");
    }

    [Test]
    public async Task A_runner_that_cannot_name_itself_launches_nothing()
    {
        var refused = SweepLauncher.Plan(
            ASweep(), IntentConfiguration.ServedTrackers(Declared), self: null,
            workingDirectory: "/scratch/sweeps/a", wallClock: TimeSpan.FromMinutes(15));

        await Assert.That(refused).IsTypeOf<SweepLaunch.Refused>()
            .Because("both servers are this binary started again, and a sweep whose agent "
                   + "cannot nominate can only report nothing.");
    }

    [Test]
    public async Task The_prompt_is_the_sweep_and_then_its_skill()
    {
        var prompt = ClaudeCodeExecutor.PromptFor(Ready(ASweep()).Request);

        await Assert.That(prompt).Contains($"mcp__board__{QueryTool.Name}");
        await Assert.That(prompt).Contains(NominationTool.Qualified);
        await Assert.That(prompt).Contains("'revision'")
            .Because("the watch's mapping says which field of each row is the version, and "
                   + "the agent is told rather than left to guess.");
        await Assert.That(prompt).Contains("'url'");
        await Assert.That(prompt).Contains("review, triage")
            .Because("the kinds it may nominate are the bound's menu.");
        await Assert.That(prompt.IndexOf(Skill, StringComparison.Ordinal))
            .IsGreaterThan(prompt.IndexOf(NominationTool.Qualified, StringComparison.Ordinal))
            .Because("the agent reads what the job is before the words that say how to do it.");
    }

    [Test]
    public async Task The_prompt_never_sends_it_looking_for_a_person_or_a_code_change()
    {
        var prompt = ClaudeCodeExecutor.PromptFor(Ready(ASweep()).Request);

        await Assert.That(prompt).DoesNotContain(HelpTool.Qualified)
            .Because("the tool is not granted, and an agent told a tool exists spends turns "
                   + "calling nothing.");
        await Assert.That(prompt).DoesNotContain("Make the code changes");
        await Assert.That(prompt).DoesNotContain("Work on this");
    }

    [Test]
    public async Task The_sweep_runs_with_nobody_to_ask_and_the_runners_own_clock()
    {
        var sweep = ASweep();
        var request = Ready(sweep).Request;

        await Assert.That(request.CanAskAPerson).IsFalse();
        await Assert.That(request.WallClock).IsEqualTo(TimeSpan.FromMinutes(15));
        await Assert.That(request.TranscriptPath).IsEqualTo(sweep.TranscriptPath);
        await Assert.That(request.WorkingDirectory).IsEqualTo("/scratch/sweeps/a");
        await Assert.That(request.IntentProvider).IsEqualTo("board");
        await Assert.That(request.Moves).IsEquivalentTo(sweep.Action.Moves);
    }

    [Test]
    public async Task A_served_host_line_reads_as_its_three_parts()
    {
        var trackers = IntentConfiguration.ServedTrackers(
            "board=https://tracker.example/acme|local:acme/board, open=https://open.example");

        await Assert.That(trackers).IsEquivalentTo(
            (ServedTracker[])
            [
                new("board", "https://tracker.example/acme", "local:acme/board"),
                new("open", "https://open.example", null),
            ],
            TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }
}
