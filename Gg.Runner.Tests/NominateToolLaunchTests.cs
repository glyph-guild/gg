using Gg.Contracts;
using Gg.Runner.Execution;

namespace Gg.Runner.Tests;

/// <summary>
/// What the agent is actually handed when a loop may nominate a work kind.
/// </summary>
/// <remarks>
/// <para>
/// <b>Asserted over the argument list, not over the configuration.</b> A server
/// this runner configured and never passed, or a tool allowed without the move
/// that permits it, are both invisible to a test that stops at the config -
/// which is why <c>ArgumentsFor</c> exists and why every assertion here goes
/// through it.
/// </para>
/// <para>
/// <b>ONE <c>--mcp-config</c>, and that is a deliberate refusal to depend on
/// somebody else's parser.</b> The flag is variadic and documented
/// space-separated; whether a SECOND occurrence appends or replaces is a detail
/// of the vendor's argument library that nobody here has measured. A launch
/// that relied on it would work until the day it did not, and the failure would
/// be the tracker reader silently disappearing - so both servers go in one
/// document.
/// </para>
/// <para>
/// <b>And the tool is granted whole.</b> The reader is granted by its
/// <c>mcp__&lt;key&gt;</c> prefix because a tracker's server offers tools this
/// runner does not enumerate. The platform's own server is ours, so a prefix
/// grant here would silently widen what <c>propose</c> permits on the day a
/// second tool joins it.
/// </para>
/// </remarks>
public class NominateToolLaunchTests
{
    private static readonly IntentReader Tracker = new(
        "jira", "jira-mcp", ["--stdio"], "JIRA_TOKEN", null);

    private static ExecutorRequest Request(
        IReadOnlyList<string> moves, string? provider = null) => new()
    {
        WorkingDirectory = "/work/flight",
        LoopId = "classify",
        Moves = moves,
        IntentProvider = provider,
        WallClock = TimeSpan.FromMinutes(10),
        TranscriptPath = "/work/flight/transcript.ndjson",
    };

    private static string ConfigIn(IReadOnlyList<string> arguments)
    {
        var at = arguments.ToList().IndexOf("--mcp-config");
        return at < 0 ? "" : arguments[at + 1];
    }

    private static int Count(IReadOnlyList<string> arguments, string flag) =>
        arguments.Count(a => string.Equals(a, flag, StringComparison.Ordinal));

    [Test]
    public async Task A_loop_that_may_nominate_is_granted_the_whole_tool()
    {
        var arguments = ClaudeCodeExecutor.ArgumentsFor(
            Request([LoopMoves.Read, LoopMoves.Propose]), [], self: SelfInvocation.For("/bin/gg", null));

        await Assert.That(arguments).Contains(NominationTool.Qualified);
        await Assert.That(arguments).DoesNotContain($"mcp__{NominationTool.Server}")
            .Because("a prefix grant would widen what `propose` permits the day a second tool "
                   + "joins this platform's own server, for every envelope in force, with "
                   + "nothing in the record marking the change.");
    }

    [Test]
    public async Task A_loop_that_may_not_nominate_is_granted_nothing_and_starts_no_server()
    {
        var arguments = ClaudeCodeExecutor.ArgumentsFor(
            Request([LoopMoves.Read]), [], self: SelfInvocation.For("/bin/gg", null));

        await Assert.That(arguments).DoesNotContain(NominationTool.Qualified);
        await Assert.That(Count(arguments, "--mcp-config")).IsEqualTo(0)
            .Because("a server nobody may call is a child process started for nothing - and "
                   + "the probe runs with read alone, so it must configure none.");
    }

    [Test]
    public async Task Both_servers_ride_one_config_argument()
    {
        var arguments = ClaudeCodeExecutor.ArgumentsFor(
            Request([LoopMoves.Read, LoopMoves.Propose], provider: "jira"),
            [Tracker],
            secret: "shhh",
            self: SelfInvocation.For("/bin/gg", null));

        await Assert.That(Count(arguments, "--mcp-config")).IsEqualTo(1)
            .Because("two occurrences of a variadic flag may append or replace and nobody has "
                   + "measured which; a launch that depended on it would lose the tracker "
                   + "reader silently.");

        var config = ConfigIn(arguments);
        await Assert.That(config).Contains("\"jira\"");
        await Assert.That(config).Contains($"\"{NominationTool.Server}\"");
        await Assert.That(config).Contains("runner");
        await Assert.That(config).Contains("nominate");
    }

    [Test]
    public async Task The_credential_still_goes_only_in_the_readers_own_environment()
    {
        // THE PROPERTY THE SECOND SERVER MUST NOT BREAK. Joining two servers in
        // one document puts them in one JSON object, and a secret written at
        // the wrong nesting would be handed to the platform's own server - or
        // worse, to the agent.
        var config = ConfigIn(ClaudeCodeExecutor.ArgumentsFor(
            Request([LoopMoves.Read, LoopMoves.Propose], provider: "jira"),
            [Tracker],
            secret: "shhh",
            self: SelfInvocation.For("/bin/gg", null)));

        var ours = config.IndexOf($"\"{NominationTool.Server}\"", StringComparison.Ordinal);
        var theirs = config.IndexOf("\"jira\"", StringComparison.Ordinal);
        var secret = config.IndexOf("shhh", StringComparison.Ordinal);

        await Assert.That(secret).IsGreaterThan(-1);
        await Assert.That(ours).IsGreaterThan(-1);
        await Assert.That(theirs).IsGreaterThan(-1);
        await Assert.That(secret > theirs && secret < ours || theirs > ours && secret > theirs)
            .IsTrue()
            .Because("the secret sits inside the reader's own object and nowhere else.");
    }

    [Test]
    public async Task A_launch_with_only_a_tracker_is_unchanged()
    {
        // THE COEXISTENCE ASSERTED AS AN ABSENCE OF CHANGE, which is stronger
        // than asserting the joined shape: every flight in the air today has
        // exactly this launch, and this slice must not have touched it.
        var before = ClaudeCodeExecutor.ArgumentsFor(
            Request([LoopMoves.Read], provider: "jira"), [Tracker], secret: "shhh");

        var after = ClaudeCodeExecutor.ArgumentsFor(
            Request([LoopMoves.Read], provider: "jira"), [Tracker], secret: "shhh",
            self: SelfInvocation.For("/bin/gg", null));

        await Assert.That(after).IsEquivalentTo(before);
    }

    [Test]
    public async Task A_runner_that_cannot_find_itself_serves_no_tool()
    {
        // NOT A SILENT ABSENCE. The refusal belongs before anything is spent -
        // see the capability check beside it - but the launch must not invent a
        // command either: a server configured with a path that is not this
        // binary is a child that fails at startup, and the agent would be told
        // the tool exists.
        var arguments = ClaudeCodeExecutor.ArgumentsFor(
            Request([LoopMoves.Read, LoopMoves.Propose]), [], self: null);

        await Assert.That(arguments).DoesNotContain(NominationTool.Qualified);
        await Assert.That(Count(arguments, "--mcp-config")).IsEqualTo(0);
    }

    [Test]
    public async Task A_loop_that_may_nominate_on_a_runner_that_cannot_serve_it_is_refused()
    {
        // ARTICLE XI, BEFORE ANYTHING IS SPENT - the shape the unreadable-tracker
        // refusal already has. A flight whose loop declares a move this runner
        // cannot serve is refused with a reason, rather than handed to an agent
        // that will establish the same thing slowly and report it as prose.
        await Assert.That(NominationTool.Unservable([LoopMoves.Read, LoopMoves.Propose], null))
            .IsNotNull();
        await Assert.That(NominationTool.Unservable([LoopMoves.Read], null)).IsNull()
            .Because("a loop that never asked to nominate is not blocked by a tool nobody "
                   + "needs.");
        await Assert.That(NominationTool.Unservable(
            [LoopMoves.Propose], SelfInvocation.For("/bin/gg", null))).IsNull();
    }

    [Test]
    public async Task An_operator_reader_keyed_like_the_platforms_own_server_is_refused()
    {
        // THE COLLISION. The key IS the tool-name prefix, so a reader declared
        // under ours would shadow the platform's own server - and the failure
        // would be an agent granted `mcp__gg__nominate_work_kind` against
        // somebody else's process.
        var refused = Assert.Throws<InvalidOperationException>(() =>
            IntentConfiguration.FromEnvironment($"{NominationTool.Server}=some-tracker --stdio"));

        await Assert.That(refused.Message).Contains(NominationTool.Server);
    }

    [Test]
    public async Task This_binary_names_itself_in_both_deployment_shapes()
    {
        // AN APPHOST names itself; UNDER `dotnet` the process path is the host
        // and the entry assembly is the dll. Getting this wrong means a server
        // command of `dotnet runner nominate`, which starts nothing.
        var apphost = SelfInvocation.For("/usr/local/bin/gg", "/usr/local/bin/gg");
        await Assert.That(apphost!.Command).IsEqualTo("/usr/local/bin/gg");
        await Assert.That(apphost.Arguments).IsEquivalentTo(
            new[] { "runner", "nominate" });

        var hosted = SelfInvocation.For("/usr/share/dotnet/dotnet", "/app/gg.dll");
        await Assert.That(hosted!.Command).IsEqualTo("/usr/share/dotnet/dotnet");
        await Assert.That(hosted.Arguments).IsEquivalentTo(
            new[] { "/app/gg.dll", "runner", "nominate" });

        await Assert.That(SelfInvocation.For(null, null)).IsNull()
            .Because("a process that cannot name its own executable must say so rather than "
                   + "guess, because the guess starts a server that is not this binary.");
    }
}
