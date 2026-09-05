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
