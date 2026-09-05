using Gg.Local;
using Gg.Contracts;
using Gg.Runner.Execution;

namespace Gg.Runner.Tests;

/// <summary>
/// The tool for asking a person is granted to every flight, and no envelope can
/// take it away.
/// </summary>
/// <remarks>
/// <para>
/// <b>Rule 5, and it is the opposite disposition from every other tool on this
/// server.</b> A move bounds what an agent may do to a customer's code; asking
/// a question touches nothing. An envelope able to withhold this would be an
/// envelope that makes a stuck agent silent, which is the failure the whole
/// slice exists to fix - so it is not a move, there is nothing to declare, and
/// the grant does not consult the loop.
/// </para>
/// <para>
/// <b>Which means the server is now started for every flight.</b> It used to
/// start only when a loop declared <c>propose</c>, because the one tool on it
/// was withholdable. One channel, two tools, and the always-granted one decides
/// when the channel opens.
/// </para>
/// <para>
/// <b>Asserted over the argument list rather than over a comment</b>, because a
/// server this runner configured and never passed, and a tool allowed without
/// the move that permits it, are both invisible to a test that stops at the
/// configuration.
/// </para>
/// </remarks>
public class HelpToolLaunchTests
{
    private static readonly IntentReader Tracker = new(
        "jira", "jira-mcp", ["--stdio"], "JIRA_TOKEN", null);

    private static readonly SelfInvocation Self = SelfInvocation.For("/bin/gg", null)!;

    private static ExecutorRequest Request(
        IReadOnlyList<string> moves, string? provider = null) => new()
    {
        WorkingDirectory = "/work/flight",
        LoopId = "implement",
        Moves = moves,
        IntentProvider = provider,
        WallClock = TimeSpan.FromMinutes(30),
        TranscriptPath = "/work/flight/transcript.ndjson",
    };

    private static int Count(IReadOnlyList<string> arguments, string flag) =>
        arguments.Count(a => string.Equals(a, flag, StringComparison.Ordinal));

    private static string ConfigIn(IReadOnlyList<string> arguments)
    {
        var at = arguments.ToList().IndexOf("--mcp-config");
        return at < 0 ? "" : arguments[at + 1];
    }

    // ---- S25.2-01 ----

    [Test]
    public async Task Every_flight_is_granted_the_tool_whatever_its_moves_declare()
    {
        // THREE ENVELOPES WITH NOTHING IN COMMON: read-only, a writer, and one
        // that may nominate. Swept in one test rather than as arguments,
        // because a move vocabulary is not a constant expression and an
        // attribute cannot hold one.
        foreach (var moves in (string[][])
                 [[LoopMoves.Read],
                  [LoopMoves.Read, LoopMoves.Edit, LoopMoves.Write],
                  [LoopMoves.Read, LoopMoves.Propose]])
        {
            var arguments = ClaudeCodeExecutor.ArgumentsFor(Request(moves), [], self: Self);

            await Assert.That(arguments).Contains(HelpTool.Qualified)
                .Because("an envelope able to withhold this would be an envelope that makes "
                       + "a stuck agent silent, and this one declares ["
                       + string.Join(", ", moves) + "].");
            await Assert.That(arguments).DoesNotContain($"mcp__{HelpTool.Server}")
                .Because("the whole tool, never the server's prefix - a prefix grant would "
                       + "retroactively grant every tool this platform later adds to its own "
                       + "server, for every envelope in force.");
        }
    }

    [Test]
    public async Task The_server_starts_for_a_flight_that_declares_no_propose()
    {
        // The change this step makes to the launch. The server used to start
        // only when a loop declared `propose`, because the one tool on it was
        // withholdable; the always-granted one is what opens the channel now.
        var arguments = ClaudeCodeExecutor.ArgumentsFor(
            Request([LoopMoves.Read]), [], self: Self);

        await Assert.That(Count(arguments, "--mcp-config")).IsEqualTo(1)
            .Because("a grant whose server was never configured tells the agent a tool "
                   + "exists and then spends its turns on something that is not there.");
        await Assert.That(ConfigIn(arguments)).Contains(HelpTool.Server);
    }

    [Test]
    public async Task A_runner_that_cannot_name_itself_grants_nothing_rather_than_lying()
    {
        // The launch must not promise what it cannot serve, and this is the
        // path where it could: no self path, so no server, so no grant.
        var arguments = ClaudeCodeExecutor.ArgumentsFor(
            Request([LoopMoves.Read]), [], self: null);

        await Assert.That(arguments).DoesNotContain(HelpTool.Qualified);
        await Assert.That(Count(arguments, "--mcp-config")).IsEqualTo(0);
    }

    // ---- S25.2-06 ----

    [Test]
    public async Task Both_servers_ride_one_config_and_strictness_is_unchanged()
    {
        var arguments = ClaudeCodeExecutor.ArgumentsFor(
            Request([LoopMoves.Read], provider: "jira"), [Tracker], secret: "shhh", self: Self);

        await Assert.That(Count(arguments, "--mcp-config")).IsEqualTo(1)
            .Because("the flag is variadic and whether a SECOND occurrence appends or "
                   + "replaces is a detail of somebody else's parser that nobody here has "
                   + "measured. A launch that relied on it would work until the day the "
                   + "tracker reader silently disappeared.");

        var config = ConfigIn(arguments);
        await Assert.That(config).Contains(Tracker.Key);
        await Assert.That(config).Contains(HelpTool.Server);

        await Assert.That(arguments).Contains("--strict-mcp-config")
            .Because("clearing the operator's own servers is the whole point, and it has to "
                   + "survive there being two of ours rather than one.");
    }

    // ---- S25.2-02 ----

    [Test]
    public async Task Asking_is_never_reported_as_a_move_the_envelope_withheld()
    {
        // WITHOUT THIS, every flight that asks for help also reports that its
        // agent reached outside its envelope. A successful call is already
        // safe - refused means never once got through - so the case that bites
        // is a call the tool REFUSED, which is undeclared and always-failing
        // and would be reported as a move nobody granted.
        var refused =
            "{\"type\":\"assistant\",\"message\":{\"content\":[{\"type\":\"tool_use\","
          + "\"id\":\"q1\",\"name\":\"" + HelpTool.Qualified + "\",\"input\":{}}]}}\n"
          + "{\"type\":\"user\",\"message\":{\"content\":[{\"type\":\"tool_result\","
          + "\"tool_use_id\":\"q1\",\"is_error\":true,\"content\":[{\"type\":\"text\","
          + "\"text\":\"Refused: a question needs words in it.\"}]}]}}";

        var digest = TranscriptDigest.Extract(
            refused, "implement", ["/work/flight"], LoopOutcomes.Completed,
            declared: [ClaudeCodeExecutor.ToolFor(LoopMoves.Read)]);

        await Assert.That(digest.RefusedMoves).DoesNotContain(HelpTool.Qualified)
            .Because("it is not a move. Reporting it as one would tell a person their agent "
                   + "reached outside its envelope because it tried to ask them a question.");
    }

    [Test]
    public async Task An_actually_undeclared_tool_is_still_reported()
    {
        // The liveness twin, on this assertion's own axis: a RefusedMoves that
        // reported nothing at all would satisfy the line above and would hide
        // the case it exists for.
        var reached =
            "{\"type\":\"assistant\",\"message\":{\"content\":[{\"type\":\"tool_use\","
          + "\"id\":\"w1\",\"name\":\"" + ClaudeCodeExecutor.ToolFor(LoopMoves.Write)
          + "\",\"input\":{\"file_path\":\"/work/flight/a.py\"}}]}}\n"
          + "{\"type\":\"user\",\"message\":{\"content\":[{\"type\":\"tool_result\","
          + "\"tool_use_id\":\"w1\",\"is_error\":true,\"content\":[{\"type\":\"text\","
          + "\"text\":\"not permitted\"}]}]}}";

        var digest = TranscriptDigest.Extract(
            reached, "implement", ["/work/flight"], LoopOutcomes.Completed,
            declared: [ClaudeCodeExecutor.ToolFor(LoopMoves.Read)]);

        await Assert.That(digest.RefusedMoves)
            .Contains(ClaudeCodeExecutor.ToolFor(LoopMoves.Write));
    }

    // ---- S25.2-03 ----

    [Test]
    public async Task The_tool_answers_from_the_arguments_and_holds_nothing_open()
    {
        // Rule 4: ask, stop, release. A tool that waited for a person would put
        // every question on the takeover path, because a lease held across
        // human latency is a lease that expires. Asserted by the server being a
        // pure function of one message - it reaches no lease, no clock and no
        // control plane, so there is nothing for it to hold.
        var source = File.ReadAllText(PlatformSource("PlatformToolServer.cs"));

        foreach (var holding in (string[])
            ["HttpClient", "Renew", "Lease", "Task.Delay", "Timer", "Socket"])
        {
            await Assert.That(source.Contains(holding, StringComparison.Ordinal)).IsFalse()
                .Because($"'{holding}' in the server is something that waits or something "
                       + "that reaches, and this tool does neither: it takes a question and "
                       + "returns a receipt.");
        }
    }

    private static string PlatformSource(string file)
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);
        while (here is not null && !Directory.Exists(Path.Combine(here.FullName, "Gg.Cli")))
        {
            here = here.Parent;
        }

        return Path.Combine(here!.FullName, "Gg.Cli", file);
    }
}
