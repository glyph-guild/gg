using System.Text.Json;
using Gg.Cli;

namespace Gg.Cli.Tests;

/// <summary>
/// A drafting agent can pull the airspace, through the verb a person would
/// type.
/// </summary>
/// <remarks>
/// <para>
/// <b>IT GRANTS THE AGENT NOTHING IT DID NOT HAVE.</b> A drafting session is
/// attended and launched with <c>--allowedTools</c>, which is an
/// auto-approval list rather than a tool restriction — <c>--tools</c> is the
/// restricting flag and <c>PtyDraftSession</c> does not pass it. So the agent
/// has a shell and <c>gg</c> is on the path: it can run <c>gg airspace
/// pull</c> today by guessing at the command line. This is
/// <c>DocumentTool</c>'s argument, one verb over — <i>"Why a tool rather than
/// letting the agent write the file. Not to stop it"</i> — and what the tool
/// buys is the same: a described channel, the right root, and gg's own
/// refusals relayed as themselves.
/// </para>
/// <para>
/// <b>A CHILD RATHER THAN A CLIENT, and that is what keeps the server what it
/// is.</b> <c>PlatformToolServer</c> holds no <c>HttpClient</c>, no session
/// store and no control-plane client, and
/// <c>TheProposalToolActsOnNothingTests</c> says why: it runs as a child of a
/// process treated as compromised, so what an injected agent can reach
/// through it is the whole question. Re-execing gg under a verb keeps the
/// credential and the network in the process whose job they are — the same
/// boundary <c>SelfInvocation.Under</c> exists to cross, and the same one the
/// runner is spawned across.
/// </para>
/// <para>
/// <b>The dirty-tree refusal is why this is safe to grant rather than
/// prompt.</b> <c>AirspacePullAsync</c> throws <c>DirtyWorkingCopyException</c>
/// as its first statement, before it touches the network, so a pull cannot
/// bury uncommitted work — including drafts the agent has just written. What
/// it must not do is read as a wall: an agent that takes a refusal for a
/// failure retries it or works around it, which is the failure
/// <c>WorkItemProposalTool</c>'s description was written against. So the
/// refusal is in the description before it is ever hit.
/// </para>
/// </remarks>
public class TheAgentCanPullTheAirspaceTests
{
    /// <summary>The wire name, spelled as the agent sees it.</summary>
    private const string Tool = "pull_airspace";

    private static async Task<IReadOnlyList<JsonDocument>> RecordingAsync(
        string? documentRoot, RunPull? pull, params string[] lines)
    {
        var output = new StringWriter();
        await PlatformToolServer.RunAsync(
            new StringReader(string.Join('\n', lines)), output,
            intentPath: null, documentRoot: documentRoot, pull: pull);

        return output.ToString()
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => JsonDocument.Parse(line))
            .ToList();
    }

    private static string Call() => JsonSerializer.Serialize(new
    {
        jsonrpc = "2.0",
        id = 11,
        method = "tools/call",
        @params = new { name = Tool, arguments = new Dictionary<string, string>() },
    });

    private static string Listing() =>
        """{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}""";

    private static string Said(JsonDocument answer) =>
        answer.RootElement.GetProperty("result").GetProperty("content")[0]
            .GetProperty("text").GetString() ?? "";

    private static bool Failed(JsonDocument answer) =>
        answer.RootElement.GetProperty("result").TryGetProperty("isError", out var flag)
        && flag.GetBoolean();

    private static JsonElement Declared(IReadOnlyList<JsonDocument> answers, string name) =>
        answers[0].RootElement.GetProperty("result").GetProperty("tools")
            .EnumerateArray()
            .Single(tool => tool.GetProperty("name").GetString() == name);

    [Test]
    public async Task The_server_offers_it()
    {
        var answers = await RecordingAsync(null, null, Listing());

        var names = answers[0].RootElement.GetProperty("result").GetProperty("tools")
            .EnumerateArray()
            .Select(tool => tool.GetProperty("name").GetString())
            .ToList();

        await Assert.That(names).Contains(Tool)
            .Because("declared: " + string.Join(", ", names));
    }

    [Test]
    public async Task Its_description_says_a_dirty_tree_is_refused()
    {
        var described = Declared(await RecordingAsync(null, null, Listing()), Tool)
            .GetProperty("description").GetString() ?? "";

        await Assert.That(described).Contains("uncommitted", StringComparison.OrdinalIgnoreCase)
            .Because("the refusal an agent will actually hit is the one it has to be told "
                   + "about first, or it reads a wall and routes around it. Description: "
                   + described);
    }

    [Test]
    public async Task Without_a_working_copy_it_refuses()
    {
        var answers = await RecordingAsync(null, _ => throw new InvalidOperationException(
            "nothing should be run when there is no working copy to run it on."), Call());

        await Assert.That(Failed(answers[0])).IsTrue()
            .Because("no root means this is not a drafting session, and a pull into a "
                   + "directory nobody named is the silent-write hazard GG_AIRSPACE was "
                   + "added to close.");
    }

    [Test]
    public async Task It_pulls_into_the_working_copy_it_was_handed()
    {
        // THE DETAIL THAT WOULD GO WRONG SILENTLY. `gg airspace pull` resolves
        // its root through EstateRoot(), which falls back to the current
        // directory - and the tool server's directory is whatever the client
        // started it in. Handed the wrong root, the verb succeeds and writes a
        // tree somewhere nobody is looking.
        var asked = new List<string>();

        await RecordingAsync("/tmp/some-airspace", root =>
        {
            asked.Add(root);
            return new PullReport { Started = true, ExitCode = 0, Said = "up to date" };
        }, Call());

        await Assert.That(asked).IsEquivalentTo((string[])["/tmp/some-airspace"])
            .Because("the working copy this session is drafting in is the only one this may "
                   + "write to. Asked for: " + string.Join(", ", asked));
    }

    [Test]
    public async Task What_gg_said_is_what_the_agent_reads()
    {
        var answers = await RecordingAsync("/tmp/some-airspace", _ => new PullReport
        {
            Started = true,
            ExitCode = 0,
            Said = "Pulled 4 documents into /tmp/some-airspace.",
        }, Call());

        await Assert.That(Failed(answers[0])).IsFalse();
        await Assert.That(Said(answers[0])).Contains("Pulled 4 documents", StringComparison.Ordinal)
            .Because("one renderer. A second wording of what a pull did would be a second "
                   + "thing to keep in agreement with the verb.");
    }

    [Test]
    public async Task A_refusal_arrives_as_itself_and_as_an_error()
    {
        var answers = await RecordingAsync("/tmp/some-airspace", _ => new PullReport
        {
            Started = true,
            ExitCode = 1,
            Said = "The working copy has uncommitted changes: airspace/narrowings/pci.yaml",
        }, Call());

        await Assert.That(Failed(answers[0])).IsTrue()
            .Because("a pull that did not happen must not read as one that did, or the next "
                   + "thing the agent does is draft against a tree it believes is fresh.");

        await Assert.That(Said(answers[0])).Contains("pci.yaml", StringComparison.Ordinal)
            .Because("the verb names the files that are dirty, and naming them is the whole "
                   + "value of the refusal. Said: " + Said(answers[0]));
    }

    [Test]
    public async Task A_server_that_cannot_start_gg_says_so()
    {
        var answers = await RecordingAsync("/tmp/some-airspace", _ => new PullReport
        {
            Started = false,
            ExitCode = -1,
            Said = "gg cannot name its own executable here.",
        }, Call());

        await Assert.That(Failed(answers[0])).IsTrue()
            .Because("SelfInvocation.Current answers null rather than guessing a path, and "
                   + "a tool that fails silently there leaves an agent waiting on a pull "
                   + "that never ran.");
    }

    [Test]
    public async Task An_empty_tree_is_told_which_tool_fills_it()
    {
        var tree = Directory.CreateDirectory(Path.Combine(
            Path.GetTempPath(), "gg-pull-test-" + Guid.NewGuid().ToString("N")[..8]));
        try
        {
            var answers = await RecordingAsync(tree.FullName, null, JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                id = 12,
                method = "tools/call",
                @params = new
                {
                    name = "describe_airspace",
                    arguments = new Dictionary<string, string>(),
                },
            }));

            await Assert.That(Said(answers[0])).Contains(Tool, StringComparison.Ordinal)
                .Because("the two tools meet here: nothing pulled is the one situation "
                       + "describe_airspace cannot answer usefully, and naming the verb "
                       + "rather than the tool leaves the agent guessing at a command line. "
                       + "Said: " + Said(answers[0]));
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }
}
