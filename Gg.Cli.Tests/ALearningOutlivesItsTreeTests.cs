using System.Text.Json;
using Gg.Cli;

namespace Gg.Cli.Tests;

/// <summary>
/// An agent is told, where it will read it, that a file is not a carrier.
/// </summary>
/// <remarks>
/// <para>
/// <b>Measured twice, on GG-329 and GG-327.</b> The learning rehearsal worked —
/// it found four concrete facts about a real environment, each pointing at
/// something that had happened — and wrote every one of them to
/// <c>scratch/notes.md</c> inside the tree. The tree is deleted when the flight
/// lands. They survive only because the <c>Write</c> tool's input is in the
/// transcript and somebody dug it out.
/// </para>
/// <para>
/// <b>The wording pointed at the filesystem.</b> The brief said <i>"the branch is
/// thrown away"</i> — true, and narrower than the truth, because the notes were
/// not in the branch, so the sentence did not reach them. The instruction led
/// with the verb <i>"write it down"</i> and named the tool in a subordinate
/// clause. Both readings satisfy "write it down", and the filesystem is the
/// reflex.
/// </para>
/// <para>
/// <b>Fixed once in a tenant's own document, and it belongs here.</b>
/// <c>learn-work-kind@v4</c> said it and the fix worked outright — the tool was
/// loaded in the flight's first search and not one scratch file was written. But
/// the rule is general: <b>when an agent has a granted write move and a tool that
/// is the only real way out, the prompt must say which one carries.</b> A tenant
/// that writes its own learning kind gets no such sentence unless the platform
/// says it.
/// </para>
/// <para>
/// <b>In the tool's own description, because that is the channel proven to
/// arrive.</b> The tool list is in context before the agent's first token.
/// </para>
/// </remarks>
public class ALearningOutlivesItsTreeTests
{
    private static async Task<string> LearningToolAsync()
    {
        var output = new StringWriter();

        await PlatformToolServer.RunAsync(
            new StringReader("""{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}"""),
            // NO documentRoot AND NO intentPath: propose_document is offered on
            // the FLIGHT branch. Drafting reads the airspace and hands a document
            // back, which is not what a rehearsal does.
            output, intentPath: null, documentRoot: null, inForce: null);

        using var answer = JsonDocument.Parse(output.ToString().Trim().Split('\n')[0]);

        return answer.RootElement.GetProperty("result").GetProperty("tools")
            .EnumerateArray()
            .Single(tool => tool.GetProperty("name").GetString() == "propose_document")
            .GetProperty("description").GetString() ?? "";
    }

    [Test]
    public async Task The_tool_says_a_file_is_not_how_a_lesson_leaves()
    {
        var asking = await LearningToolAsync();

        await Assert.That(asking).Contains("a file", StringComparison.OrdinalIgnoreCase)
            .Because("the reflex is the filesystem, and an instruction that does not name it "
                   + "is one an agent satisfies by writing a note. Description: " + asking);
        await Assert.That(asking).Contains("deleted", StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task It_says_which_instrument_carries_rather_than_only_what_to_do()
    {
        // THE GENERAL RULE the two lost rehearsals produced: when an agent has a
        // granted write move AND a tool that is the only real way out, the prompt
        // must say WHICH ONE CARRIES. "Write it down" is satisfied by both.
        var asking = await LearningToolAsync();

        await Assert.That(asking).Contains("only way", StringComparison.OrdinalIgnoreCase)
            .Because("naming the tool is not enough - GG-329 loaded this tool's schema and "
                   + "then reached for Write anyway, twice. Description: " + asking);
    }

    [Test]
    public async Task And_forbids_pointing_a_reader_at_a_path()
    {
        // GG-329's loop.outcome referred whoever read it to scratch/notes.md, a
        // path that by then existed nowhere. A pointer to a deleted file is worse
        // than silence: it reads as though the work was recorded.
        var asking = await LearningToolAsync();

        await Assert.That(asking).Contains("path", StringComparison.OrdinalIgnoreCase)
            .Because("a reader sent to a path that no longer exists has been told the lesson "
                   + "was kept. Description: " + asking);
    }
}
