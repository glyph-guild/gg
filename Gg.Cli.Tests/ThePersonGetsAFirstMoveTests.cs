using System.Text.Json;
using Gg.Cli;

namespace Gg.Cli.Tests;

/// <summary>
/// A drafting session opens with a first move the person can pick, rather than
/// a blank line.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE HALF THE TOOLS DO NOT ADDRESS.</b> <c>describe_airspace</c> and
/// <c>pull_airspace</c> are for the agent. The PERSON still arrives at an
/// empty prompt in a session they opened by pressing one key, with no
/// indication of what this agent is for or what a good opening looks like -
/// and <c>PtyDraftSession</c> passes no prompt, deliberately, because a
/// prompt gg wrote would start the agent working before anybody said what
/// they wanted.
/// </para>
/// <para>
/// <b>A PROMPT IS THE CHANNEL THAT SPLITS THAT DIFFERENCE.</b> An MCP server
/// may declare prompts, and a client surfaces them as commands a person
/// chooses: gg writes the wording, the person picks it, edits it if they
/// like, and sends it. The person still drives - nothing is sent for them -
/// and they no longer start from nothing. It is the same distinction the
/// console already makes for `n`: the key opens a question rather than
/// composing an answer.
/// </para>
/// <para>
/// <b>Orient, do not act.</b> The opening move reads the airspace and reports;
/// it explicitly writes nothing. Somebody who opened a drafting session has
/// not yet said what they want changed, and an agent that guessed would spend
/// the first turn undoing the guess.
/// </para>
/// </remarks>
public class ThePersonGetsAFirstMoveTests
{
    /// <summary>The name as a person sees it in their client.</summary>
    private const string Prompt = "start_drafting";

    private static async Task<IReadOnlyList<JsonDocument>> RecordingAsync(params string[] lines)
    {
        var output = new StringWriter();
        await PlatformToolServer.RunAsync(
            new StringReader(string.Join('\n', lines)), output, intentPath: null);

        return output.ToString()
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => JsonDocument.Parse(line))
            .ToList();
    }

    [Test]
    public async Task The_server_says_it_has_prompts()
    {
        // DECLARED AT INITIALIZE OR NEVER ASKED FOR. A client reads
        // capabilities before it reads anything else, and a server offering
        // prompts it did not declare is one whose prompts/list is never
        // called.
        var answers = await RecordingAsync(
            """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05"}}""");

        var capabilities = answers[0].RootElement
            .GetProperty("result").GetProperty("capabilities");

        await Assert.That(capabilities.TryGetProperty("prompts", out _)).IsTrue()
            .Because("undeclared is unasked-for: the client decides what to call from this "
                   + "object, so a prompt behind an undeclared capability is one nobody "
                   + "ever sees.");
    }

    [Test]
    public async Task It_offers_one_first_move()
    {
        var answers = await RecordingAsync(
            """{"jsonrpc":"2.0","id":2,"method":"prompts/list","params":{}}""");

        var prompts = answers[0].RootElement.GetProperty("result").GetProperty("prompts");

        var names = prompts.EnumerateArray()
            .Select(one => one.GetProperty("name").GetString())
            .ToList();

        await Assert.That(names).IsEquivalentTo((string?[])[Prompt])
            .Because("one, and a second is a decision somebody has to argue for - the rule "
                   + "this server already applies to its tools. Found: "
                   + string.Join(", ", names));

        await Assert.That(prompts[0].GetProperty("description").GetString()).IsNotEmpty()
            .Because("the description is the whole of what a person sees in the list they "
                   + "pick from.");
    }

    [Test]
    public async Task Picking_it_asks_the_agent_to_read_before_it_writes()
    {
        var answers = await RecordingAsync(JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = 3,
            method = "prompts/get",
            @params = new { name = Prompt },
        }));

        var result = answers[0].RootElement.GetProperty("result");
        var messages = result.GetProperty("messages");

        await Assert.That(messages.GetArrayLength()).IsEqualTo(1)
            .Because("it is one opening turn, in the person's voice, and a conversation gg "
                   + "invented both halves of would be gg talking to itself.");

        await Assert.That(messages[0].GetProperty("role").GetString()).IsEqualTo("user")
            .Because("the person is sending this. An assistant turn here would be gg "
                   + "putting words in the agent's mouth before it has read anything.");

        var text = messages[0].GetProperty("content").GetProperty("text").GetString() ?? "";

        await Assert.That(text).Contains("describe_airspace", StringComparison.Ordinal)
            .Because("the one thing a person cannot know to ask for, and the reason the "
                   + "opening move is worth writing at all. Text: " + text);

        await Assert.That(text).Contains("not", StringComparison.OrdinalIgnoreCase)
            .Because("orient, do not act: an agent that starts drafting before the person "
                   + "has said what they want spends the next turn undoing it. Text: "
                   + text);
    }

    [Test]
    public async Task A_prompt_nobody_declared_is_refused_rather_than_guessed()
    {
        var answers = await RecordingAsync(JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = 4,
            method = "prompts/get",
            @params = new { name = "something_else" },
        }));

        await Assert.That(answers[0].RootElement.TryGetProperty("error", out _)).IsTrue()
            .Because("answering an unknown name with the one prompt there is would make "
                   + "every mistyped command look like it worked.");
    }

    [Test]
    public async Task The_bar_tells_the_person_it_is_there()
    {
        // A COMMAND NOBODY IS TOLD ABOUT IS A COMMAND NOBODY HAS. gg owns the
        // top row of this session and already spends it on what ends the
        // session; what STARTS it belongs there too.
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Gg.sln")))
        {
            root = root.Parent;
        }

        var session = await File.ReadAllTextAsync(Path.Combine(
            root!.FullName, "Gg.Console", "PtyDraftSession.cs"));

        await Assert.That(session).Contains(Prompt, StringComparison.Ordinal)
            .Because("the bar is the only thing a person reads before they type, so a "
                   + "first move they are not told about is one they never use.");
    }
}
