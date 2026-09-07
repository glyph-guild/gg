using Gg.Local;
using System.Text;
using System.Text.Json;
using Gg.Cli;
using Gg.Runner.Execution;

namespace Gg.Cli.Tests;

/// <summary>
/// The platform's own tool server: the channel an agent declares a value
/// through, rather than writing prose about it.
/// </summary>
/// <remarks>
/// <para>
/// <b>STDOUT IS THE PROTOCOL, which is the hazard this file exists for.</b> One
/// stray line of narration and the agent sees a dead server - not a failed
/// tool, a server that never initialized - and the flight produces no
/// nomination for a reason nothing records. So every assertion here parses
/// EVERY line the server wrote, rather than looking for the lines it expected.
/// </para>
/// <para>
/// <b>Hand-written rather than an SDK, and the reason is the binary.</b>
/// <c>Gg.Runner</c> carries no package references and the CLI is published
/// AOT; the official server library is DI- and reflection-shaped. What is
/// needed here is four methods over line-delimited JSON, which is the shape
/// the launch's own config writer already uses and for the same stated reason.
/// </para>
/// <para>
/// <b>It holds nothing open and needs nothing.</b> No credential, no session,
/// no control-plane call - it validates two strings and returns a receipt. That
/// is what makes it safe to launch as a child of a process the threat model
/// treats as hostile: an injected agent that reaches it can at most record a
/// request that admission will refuse.
/// </para>
/// </remarks>
public class PlatformToolServerTests
{
    /// <summary>A conversation with the server, with somewhere to record an intent.</summary>
    /// <remarks>
    /// <b>The path is an ARGUMENT, not an environment read, and that is what
    /// keeps this type a function of what it is handed.</b> Reading
    /// <c>GG_INTENT_PATH</c> in here would make the server depend on process
    /// state, and would make every test that exercised it mutate something
    /// global while the rest of the suite ran beside it in parallel. The verb
    /// that starts the server reads the environment; the server is told.
    /// </remarks>
    private static async Task<IReadOnlyList<JsonDocument>> RecordingAsync(
        string? intentPath, params string[] lines)
    {
        var output = new StringWriter();
        await PlatformToolServer.RunAsync(
            new StringReader(string.Join('\n', lines)), output, intentPath);

        return output.ToString()
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => JsonDocument.Parse(line))
            .ToList();
    }

    /// <summary>A directory this test owns, for an intent to land in.</summary>
    private static DirectoryInfo Somewhere() => Directory.CreateDirectory(Path.Combine(
        Path.GetTempPath(), "gg-intent-test-" + Guid.NewGuid().ToString("N")[..8]));

    /// <summary>A tools/call for the intent tool, named from the declaration.</summary>
    /// <remarks>
    /// The NAME comes from <see cref="IntentTool"/> rather than being typed here,
    /// so a test cannot go on passing against a tool the server has renamed.
    /// </remarks>
    private static string CallIntent(string intent) =>
        """
        {"jsonrpc":"2.0","id":7,"method":"tools/call","params":{"name":"TOOL","arguments":{"intent":"SAID"}}}
        """
        .Replace("TOOL", IntentTool.Name, StringComparison.Ordinal)
        .Replace("SAID", intent, StringComparison.Ordinal);

    private static bool IsError(JsonDocument answer) =>
        answer.RootElement.TryGetProperty("result", out var result)
        && result.TryGetProperty("isError", out var flag)
        && flag.GetBoolean();

    private static string Said(JsonDocument answer) =>
        answer.RootElement.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString()!;

    [Test]
    public async Task One_declaration_owns_the_intent_tools_three_spellings()
    {
        // THE THIRD INSTANCE OF THIS HAZARD IN THIS FEATURE AREA, and every
        // one so far has been silent rather than loud. The launch's allow-list
        // uses the qualified name, tools/list declares the bare one, and
        // whatever reads the result looks for the qualified one - so a
        // disagreement means either an agent granted a tool that does not
        // exist, or gg waiting for a call the agent was never offered.
        await Assert.That(IntentTool.Qualified)
            .IsEqualTo($"mcp__{IntentTool.Server}__{IntentTool.Name}")
            .Because("the qualified name is composed from the other two rather than typed "
                   + "beside them, which is the only arrangement that cannot drift.");

        await Assert.That(IntentTool.Server).IsEqualTo(NominationTool.Server)
            .Because("three tools on ONE server. A second server key would shadow the first "
                   + "if an operator ever configured a reader under it.");
    }

    [Test]
    public async Task The_intent_tool_is_declared_beside_the_other_two()
    {
        // Declared always, granted never by default: the server's own note says
        // the grant is decided in the launch's allow-list rather than by varying
        // tools/list, because a list that varied by envelope would be a second
        // place the same rule lives.
        var answers = await RecordingAsync(intentPath: null,
            """{"jsonrpc":"2.0","id":1,"method":"tools/list"}""");

        var declared = answers[0].RootElement
            .GetProperty("result").GetProperty("tools").EnumerateArray()
            .Select(tool => tool.GetProperty("name").GetString())
            .ToList();

        await Assert.That(declared).Contains(IntentTool.Name)
            .Because("an agent cannot call a tool it was never offered, however the launch "
                   + "granted it. Found: " + string.Join(", ", declared));
    }

    [Test]
    public async Task An_intent_arrives_as_a_file_written_whole()
    {
        // THE HAND-BACK. The tool call is the structured channel - the moment an
        // agent SAYS it is done - and the file is transport between two gg
        // processes rather than something gg has to guess the meaning of.
        var notes = Somewhere();
        var path = Path.Combine(notes.FullName, "intent.txt");

        try
        {
            var answers = await RecordingAsync(path, CallIntent("Make the pool decider stop scanning"));

            await Assert.That(IsError(answers[0])).IsFalse()
                .Because("a receipt, not a refusal: " + Said(answers[0]));

            await Assert.That(File.Exists(path)).IsTrue();
            await Assert.That(await File.ReadAllTextAsync(path))
                .IsEqualTo("Make the pool decider stop scanning")
                .Because("what the agent composed is what gg reads - no wrapper, no format to "
                       + "version, the same bytes the editor path would have produced.");

            // WRITTEN BY RENAME, which is why gg may watch it. A write in place
            // is visible half-finished, and gg watching would read a truncated
            // intent and open a flight with it. The observable consequence is
            // that nothing is left lying beside it.
            await Assert.That(notes.GetFiles().Select(f => f.Name)).IsEquivalentTo((string[])["intent.txt"])
                .Because("a temp file left behind is the tell that the write was not a rename.");
        }
        finally
        {
            notes.Delete(recursive: true);
        }
    }

    [Test]
    public async Task A_session_with_nowhere_to_record_says_so()
    {
        // S33.3-09. An agent that cannot submit must not look like one that
        // chose not to - and this is the ordinary case on the fleet path, where
        // the server runs with no composing session at all.
        var answers = await RecordingAsync(intentPath: null, CallIntent("something worth doing"));

        await Assert.That(IsError(answers[0])).IsTrue()
            .Because("silence here is indistinguishable from an agent that declined.");
        await Assert.That(Said(answers[0])).Contains("Nothing was recorded")
            .Because("the same words the other two tools use for the same fact.");
    }

    [Test]
    public async Task An_empty_intent_is_refused_rather_than_written()
    {
        // Borrowed from FlightIntent.Validate rather than invented here: an
        // intent of kind text whose text is empty is already refused on the
        // wire, so accepting one would only move the refusal somewhere less
        // helpful.
        var notes = Somewhere();
        var path = Path.Combine(notes.FullName, "intent.txt");

        try
        {
            var answers = await RecordingAsync(path, CallIntent("   "));

            await Assert.That(IsError(answers[0])).IsTrue();
            await Assert.That(File.Exists(path)).IsFalse()
                .Because("a flight opened with nothing in it is worse than no flight.");
        }
        finally
        {
            notes.Delete(recursive: true);
        }
    }

    [Test]
    public async Task The_last_intent_before_the_session_ends_is_the_one_that_counts()
    {
        // A person correcting themselves is the ordinary case, not an error, so
        // the second call replaces the first rather than being refused.
        var notes = Somewhere();
        var path = Path.Combine(notes.FullName, "intent.txt");

        try
        {
            var answers = await RecordingAsync(path,
                CallIntent("first thought"), CallIntent("what I actually meant"));

            await Assert.That(answers.Count).IsEqualTo(2);
            await Assert.That(IsError(answers[1])).IsFalse();
            await Assert.That(await File.ReadAllTextAsync(path)).IsEqualTo("what I actually meant");
        }
        finally
        {
            notes.Delete(recursive: true);
        }
    }

    private static async Task<IReadOnlyList<JsonDocument>> ExchangeAsync(params string[] lines)
    {
        var output = new StringWriter();
        await PlatformToolServer.RunAsync(new StringReader(string.Join('\n', lines)), output);

        // EVERY LINE, parsed. A server that wrote one unparseable line among
        // valid ones is a dead server, and a test that looked only for what it
        // expected would not notice.
        return output.ToString()
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => JsonDocument.Parse(line))
            .ToList();
    }

    // BUILT BY CONCATENATION rather than interpolated: this is JSON, and a raw
    // interpolated literal ending in three closing braces is a brace-counting
    // exercise that the compiler loses too.
    private static string Initialize(int id = 0) =>
        "{\"jsonrpc\":\"2.0\",\"id\":" + id + ",\"method\":\"initialize\",\"params\":"
      + "{\"protocolVersion\":\"2024-11-05\",\"capabilities\":{},"
      + "\"clientInfo\":{\"name\":\"probe\",\"version\":\"1\"}}}";

    private static string Call(int id, string arguments) =>
        "{\"jsonrpc\":\"2.0\",\"id\":" + id + ",\"method\":\"tools/call\",\"params\":"
      + "{\"name\":\"" + NominationTool.Name + "\",\"arguments\":" + arguments + "}}";

    [Test]
    public async Task Everything_it_writes_is_json_rpc()
    {
        var answers = await ExchangeAsync(
            Initialize(),
            """{"jsonrpc":"2.0","method":"notifications/initialized"}""",
            """{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}""",
            Call(2, """{"work_kind":"research","reason":"the item asks a question"}"""));

        await Assert.That(answers.Count).IsGreaterThan(0);

        foreach (var answer in answers)
        {
            await Assert.That(answer.RootElement.GetProperty("jsonrpc").GetString()).IsEqualTo("2.0");
        }
    }

    [Test]
    public async Task It_answers_initialize_with_the_protocol_the_client_asked_for()
    {
        var answers = await ExchangeAsync(Initialize());

        await Assert.That(answers.Count).IsEqualTo(1);

        var result = answers[0].RootElement.GetProperty("result");
        await Assert.That(result.GetProperty("protocolVersion").GetString()).IsEqualTo("2024-11-05")
            .Because("echoed rather than declared, so the version this server claims is one the "
                   + "client has already said it speaks.");
        await Assert.That(result.GetProperty("serverInfo").GetProperty("name").GetString())
            .IsEqualTo(NominationTool.Server)
            .Because("the server key is half the identity of every tool it hosts, so it comes "
                   + "from the one place that names it.");
    }

    [Test]
    public async Task A_notification_is_answered_with_nothing()
    {
        // A RESPONSE TO A NOTIFICATION IS A PROTOCOL ERROR, and one written to
        // this stream is a line the client cannot match to a request.
        var answers = await ExchangeAsync(
            """{"jsonrpc":"2.0","method":"notifications/initialized"}""");

        await Assert.That(answers).IsEmpty();
    }

    [Test]
    public async Task It_declares_one_tool_taking_a_work_kind_and_a_reason()
    {
        // TWO NOW, AND THE OLD REASON WAS THE WRONG ONE. This asserted one tool
        // because "a second on this server would be granted by the same move" -
        // and the second is granted on the OPPOSITE terms. Nominating a work
        // kind is the whole output of one kind of work, so an envelope that
        // never declares `propose` has no business granting it; asking for a
        // decision is not a move at all and no envelope may withhold it,
        // because one able to would be one that makes a stuck agent silent.
        //
        // Which is why the count is asserted rather than the absence: a THIRD
        // tool has to make its own argument, and neither of the two above is
        // it.
        var answers = await ExchangeAsync(
            """{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}""");

        var tools = answers[0].RootElement.GetProperty("result").GetProperty("tools");
        await Assert.That(tools.GetArrayLength()).IsEqualTo(2)
            .Because("one channel, two tools, granted on opposite terms. A third is a "
                   + "decision somebody has to argue for.");

        var listed = tools.EnumerateArray()
            .Select(t => t.GetProperty("name").GetString()!).ToList();
        await Assert.That(listed).IsEquivalentTo(
            new[] { NominationTool.Name, HelpTool.Name });

        var tool = tools[0];
        await Assert.That(tool.GetProperty("name").GetString()).IsEqualTo(NominationTool.Name);

        var properties = tool.GetProperty("inputSchema").GetProperty("properties");
        await Assert.That(properties.TryGetProperty("work_kind", out _)).IsTrue();
        await Assert.That(properties.TryGetProperty("reason", out _)).IsTrue();

        var required = tool.GetProperty("inputSchema").GetProperty("required")
            .EnumerateArray().Select(r => r.GetString()!).ToList();
        await Assert.That(required).IsEquivalentTo(new[] { "work_kind", "reason" })
            .Because("a reason is required, because a nomination with none is a decision with "
                   + "no record of what it rested on.");
    }

    [Test]
    public async Task A_call_is_answered_with_a_receipt_naming_what_was_taken()
    {
        var answers = await ExchangeAsync(
            Call(1, """{"work_kind":"research","reason":"nobody has diagnosed it yet"}"""));

        var result = answers[0].RootElement.GetProperty("result");
        var text = result.GetProperty("content")[0].GetProperty("text").GetString()!;

        await Assert.That(text).Contains("research")
            .Because("echoed back, so an agent can see what was taken rather than assume.");
        await Assert.That(result.TryGetProperty("isError", out var flag) && flag.GetBoolean())
            .IsFalse();
    }

    [Test]
    public async Task The_receipt_says_it_grants_nothing()
    {
        // THE WORDING IS LOAD-BEARING. An agent that believed calling this had
        // opened a flight would stop waiting for one, or would try again. What
        // it is told is that a request was recorded and the work is over.
        var answers = await ExchangeAsync(
            Call(1, """{"work_kind":"research","reason":"nobody has diagnosed it yet"}"""));

        var text = answers[0].RootElement.GetProperty("result")
            .GetProperty("content")[0].GetProperty("text").GetString()!;

        await Assert.That(text).Contains("grants nothing");
    }

    [Test]
    public async Task A_call_missing_an_argument_is_an_error_result_rather_than_a_receipt()
    {
        // AN ERROR RESULT, NOT A PROTOCOL ERROR. The call reached the tool and
        // the tool refused it - which is a thing the agent can read and fix,
        // and a thing the extractor must not read as a nomination.
        foreach (var arguments in (string[])
            ["""{"reason":"no kind"}""", """{"work_kind":"research"}""", "{}"])
        {
            var answers = await ExchangeAsync(Call(1, arguments));
            var result = answers[0].RootElement.GetProperty("result");

            await Assert.That(result.GetProperty("isError").GetBoolean()).IsTrue()
                .Because($"'{arguments}' is not a nomination, and a receipt for it would be a "
                       + "value the runner then had to invent half of.");
        }
    }

    [Test]
    public async Task A_method_nobody_declared_is_an_error_carrying_the_id()
    {
        var answers = await ExchangeAsync(
            """{"jsonrpc":"2.0","id":7,"method":"resources/list","params":{}}""");

        await Assert.That(answers[0].RootElement.GetProperty("id").GetInt32()).IsEqualTo(7)
            .Because("a client matching responses to requests needs the id back even on an "
                   + "error, or it waits for ever.");
        await Assert.That(answers[0].RootElement.TryGetProperty("error", out _)).IsTrue();
    }

    [Test]
    public async Task A_line_that_will_not_parse_does_not_stop_the_server()
    {
        // A DEAD SERVER IS THE WORST OUTCOME, worse than a skipped line: the
        // agent loses the tool for the rest of the session and the flight
        // produces no nomination for a reason nothing records.
        var answers = await ExchangeAsync(
            "{not json",
            """{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}""");

        await Assert.That(answers.Count).IsEqualTo(1);
        await Assert.That(answers[0].RootElement.GetProperty("id").GetInt32()).IsEqualTo(1);
    }

    [Test]
    public async Task It_returns_when_the_input_ends()
    {
        // HOLDS NOTHING OPEN. The agent's process owns this one's lifetime, so
        // a server that kept a handle after stdin closed would be a child the
        // runner has to reap.
        await Assert.That(await PlatformToolServer.RunAsync(
            new StringReader(string.Empty), new StringWriter())).IsEqualTo(0);
    }
}
