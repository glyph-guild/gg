using Gg.Contracts;
using Gg.Runner.Execution;

namespace Gg.Runner.Tests;

/// <summary>
/// A tool server this runner configured, that did not come up.
/// </summary>
/// <remarks>
/// <para>
/// <b>The failure this closes was silent, and it disabled a whole tier.</b> The
/// platform's own server is started as a child over stdio. When the command was
/// wrong the child died at startup, the agent was launched anyway with the tool
/// missing from its list, and NOTHING said so - the runner's log carried not a
/// word. Only the stream's own opening record knew, as
/// <c>{"name":"gg","status":"failed"}</c>.
/// </para>
/// <para>
/// <b>Found by reading a transcript to explain an agent's behaviour.</b> Slice
/// twenty-five's walk watched a real agent do half a job and report `completed`
/// without asking, and that was read as the model choosing not to ask. It had
/// nothing to ask with. A control that reports the agent's judgement wrongly is
/// worse than no control.
/// </para>
/// <para>
/// <b>Refused before anything is spent</b>, which is
/// <see cref="NominationTool.Unservable"/>'s own answer to the same shape one
/// step earlier: that one catches a server this runner cannot NAME, and this
/// one catches a server it named that did not START. The init record is the
/// first line of the stream, so the refusal costs a process launch and no
/// turns.
/// </para>
/// <para>
/// <b>Anything but connected, not just failed.</b> Under this runner's flags -
/// setting sources cleared, strict tool configuration - the only servers in
/// that list are the ones it put there, so any of them not answering is a tool
/// the agent was told about and cannot call. Enumerating the bad statuses would
/// be a list to keep in step with somebody else's vocabulary.
/// </para>
/// </remarks>
public class AToolServerThatDidNotStartTests
{
    /// <summary>
    /// The opening record, as the stream really writes it.
    /// </summary>
    /// <remarks>
    /// Built here rather than captured, because a real capture of this line
    /// carries the machine that made it - see <c>FixtureCleanlinessTests</c>.
    /// The shape is copied from one; the values are not.
    /// </remarks>
    private static string Init(params (string Name, string Status)[] servers) =>
        "{\"type\":\"system\",\"subtype\":\"init\",\"cwd\":\"/work/tree\","
      + "\"model\":\"a-model\",\"mcp_servers\":["
      + string.Join(",", servers.Select(s =>
            $"{{\"name\":\"{s.Name}\",\"status\":\"{s.Status}\"}}"))
      + "],\"tools\":[\"Read\",\"Edit\"]}";

    [Test]
    public async Task A_server_that_connected_is_not_a_refusal()
    {
        await Assert.That(ToolServers.Unstarted(Init(("gg", "connected")))).IsNull()
            .Because("the ordinary launch is this one, and a check that fired on it would "
                   + "ground every flight in the estate.");
    }

    [Test]
    public async Task A_server_that_failed_is_named_with_its_status()
    {
        var refusal = ToolServers.Unstarted(Init(("gg", "failed")));

        await Assert.That(refusal).IsNotNull()
            .Because("an agent told a tool exists and given one that does not answer spends "
                   + "its turns calling nothing - and a loop that cannot ask for a decision "
                   + "cannot say it is stuck.");
        await Assert.That(refusal!).Contains("gg")
            .Because("which server, because a launch can configure more than one and the fix "
                   + "differs per server.");
        await Assert.That(refusal).Contains("failed")
            .Because("and what it said about itself, rather than this runner's paraphrase.");
    }

    [Test]
    public async Task Only_the_server_that_did_not_come_up_is_named()
    {
        // A TRACKER READER AND THE PLATFORM'S OWN SERVER are two different
        // problems with two different fixes, and a refusal naming both when one
        // is healthy sends somebody to look at the wrong one.
        var refusal = ToolServers.Unstarted(Init(("tracker", "connected"), ("gg", "failed")));

        await Assert.That(refusal!).Contains("gg");
        await Assert.That(refusal).DoesNotContain("tracker")
            .Because("tracker came up. Naming it would be this runner reporting a fault it "
                   + "did not observe.");
    }

    [Test]
    public async Task Any_status_but_connected_is_refused()
    {
        // NOT A LIST OF BAD STATUSES. Under this runner's flags the only
        // servers in that record are the ones it configured, so a status it
        // does not recognise is a server that is not answering - and a list
        // would be somebody else's vocabulary to keep in step with.
        await Assert.That(ToolServers.Unstarted(Init(("gg", "needs-auth")))).IsNotNull();
        await Assert.That(ToolServers.Unstarted(Init(("gg", "pending")))).IsNotNull();
    }

    [Test]
    public async Task A_line_that_says_nothing_about_servers_is_not_a_refusal()
    {
        // THE COMMON CASE, and it must not throw. A flight with no tracker and
        // no platform tool configures no server; a line that is not the init
        // record has no such list; and a line that will not parse is the
        // transcript's business, not this function's.
        await Assert.That(ToolServers.Unstarted(Init())).IsNull();
        await Assert.That(ToolServers.Unstarted("{\"type\":\"assistant\"}")).IsNull();
        await Assert.That(ToolServers.Unstarted("not json at all")).IsNull();
        await Assert.That(ToolServers.Unstarted("")).IsNull();
    }

    [Test]
    public async Task The_result_record_is_not_read_for_this()
    {
        // THE WORSE LIE. The result record carries the same list at the END of a
        // run, so a server that connected and later dropped would turn a run
        // that actually happened into a refusal saying nothing was spent. What
        // is being asked is whether the agent STARTED without a tool it was
        // offered, and only the opening line answers that.
        var ended =
            "{\"type\":\"result\",\"subtype\":\"success\",\"is_error\":false,"
          + "\"mcp_servers\":[{\"name\":\"gg\",\"status\":\"failed\"}]}";

        await Assert.That(ToolServers.Unstarted(ended)).IsNull()
            .Because("by then the turns are spent, and a refusal claiming otherwise would be "
                   + "this runner reporting a run that happened as one that did not.");
    }

    // ---- and the executor acts on it ----

    [Test]
    [Category("RealAgent")]
    public async Task A_run_whose_server_cannot_start_is_refused_before_the_agent_works()
    {
        // THE WIRING, against the real binary. The rule above is a statement
        // about a line; this is the statement that the executor reads it, and
        // it is the half the silent failure lived in.
        //
        // The server is named as a path that is not there, which is exactly what
        // the defect produced: an invocation that looks well-formed from this
        // side and starts nothing on the other.
        var binary = Environment.GetEnvironmentVariable("GG_EXECUTOR_BINARY")
            ?? throw new InvalidOperationException(
                "GG_EXECUTOR_BINARY is not set. This drives the real executor, because what "
              + "is under test is what it does with a real stream's first line.");

        var root = Path.Combine(Path.GetTempPath(), "gg-unstarted-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Path.Combine(root, "tree"));
        File.WriteAllText(Path.Combine(root, "tree", "ISSUE.md"), "# Issue\n\nDo nothing.\n");

        var run = await new ClaudeCodeExecutor(
                binary,
                self: SelfInvocation.For(Path.Combine(root, "no-such-gg"), null))
            .ExecuteAsync(
                new ExecutorRequest
                {
                    WorkingDirectory = Path.Combine(root, "tree"),
                    LoopId = "implement",
                    Moves = [LoopMoves.Read],
                    CanAskAPerson = true,
                    WallClock = TimeSpan.FromMinutes(2),
                    TranscriptPath = Path.Combine(root, "state", "transcript.ndjson"),
                },
                CancellationToken.None);

        await Assert.That(run.Outcome).IsEqualTo(LoopOutcomes.Failed)
            .Because("a runner that cannot offer the tool it configured has a declared "
                   + "capability gap, and a gap answered is better than a flight that runs "
                   + "blind. It said: " + run.Reason);
        await Assert.That(run.Reason).Contains("gg")
            .Because("naming the server is what makes the refusal actionable.");
        await Assert.That(run.MovesUsed).IsEmpty()
            .Because("nothing was spent - the init record is the first line of the stream, so "
                   + "the refusal costs a process launch and no turns.");
    }
}
