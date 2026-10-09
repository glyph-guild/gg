using System.Text.Json;
using Gg.Cli;
using Gg.Local;

namespace Gg.Cli.Tests;

/// <summary>
/// <c>gg manage tools</c>: gg's management layer as MCP tools, each running the verb a person
/// would type (owner's call, 2026-10-08).
/// </summary>
/// <remarks>
/// <b>A child, never a client</b>, for <c>pull_airspace</c>'s reason: the credential and the
/// network stay in the process whose job they are, and gg's refusals come back in gg's words.
/// </remarks>
public class TheManageToolServerTests
{
    private sealed class Ran
    {
        public List<IReadOnlyList<string>> Calls { get; } = [];

        public PullReport Answer { get; set; } = new() { Started = true, ExitCode = 0, Said = "{\"ok\":true}" };

        public PullReport Run(IReadOnlyList<string> arguments)
        {
            Calls.Add(arguments);
            return Answer;
        }
    }

    private static async Task<IReadOnlyList<JsonDocument>> RecordingAsync(Ran ran, params string[] lines)
    {
        var output = new StringWriter();
        await ManageToolServer.RunAsync(new StringReader(string.Join('\n', lines)), output, ran.Run);

        return output.ToString()
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => JsonDocument.Parse(line))
            .ToList();
    }

    private static string Call(string tool, Dictionary<string, object>? arguments = null) =>
        JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = 5,
            method = "tools/call",
            @params = new { name = tool, arguments = arguments ?? [] },
        });

    private static bool Failed(JsonDocument answer) =>
        answer.RootElement.GetProperty("result").TryGetProperty("isError", out var flag) && flag.GetBoolean();

    private static string Said(JsonDocument answer) =>
        answer.RootElement.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString() ?? "";

    [Test]
    public async Task It_initializes_and_lists_every_read_and_every_act()
    {
        var answers = await RecordingAsync(new Ran(),
            """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05"}}""",
            """{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}""");

        await Assert.That(answers[0].RootElement.GetProperty("result").GetProperty("serverInfo")
            .GetProperty("name").GetString()).IsEqualTo(ManageTool.Server);

        var names = answers[1].RootElement.GetProperty("result").GetProperty("tools").EnumerateArray()
            .Select(tool => tool.GetProperty("name").GetString()!).ToList();

        await Assert.That(names).IsEquivalentTo([.. ManageTool.Reads, .. ManageTool.Acts]);
    }

    [Test]
    [Arguments("whoami", "", "whoami --json")]
    [Arguments("list_gates", "", "gates --json")]
    [Arguments("list_board", "", "board --json")]
    [Arguments("list_flights", "", "flights --json")]
    [Arguments("show_flight", "flight=GG-42", "show GG-42 --json")]
    [Arguments("flight_log", "flight=GG-42", "log GG-42 --json")]
    [Arguments("why_flight", "flight=GG-42", "why GG-42 --json")]
    [Arguments("why_flight", "flight=GG-42;obligation=in-scope", "why GG-42 in-scope --json")]
    [Arguments("list_runners", "", "runners --json")]
    [Arguments("list_watches", "", "watches --json")]
    [Arguments("list_itineraries", "", "itineraries --json")]
    [Arguments("list_itineraries", "intent=ado#18678", "itineraries --intent ado#18678 --json")]
    [Arguments("show_itinerary", "itinerary=ITN-63", "itinerary show ITN-63 --json")]
    [Arguments("decide_gate", "flight=GG-42;obligation=widen-root;outcome=approved;reason=looks right", "decide GG-42 widen-root approved looks right --json")]
    [Arguments("decide_gate", "flight=GG-42;obligation=widen-root;outcome=rejected", "decide GG-42 widen-root rejected --json")]
    [Arguments("answer_nomination", "nomination=01a0792a-0000-0000-0000-000000000000;answer=open;because=it blocks the release", "board open 01a0792a-0000-0000-0000-000000000000 it blocks the release --json")]
    [Arguments("fly", "intent=Fix the login page", "fly Fix the login page --json")]
    [Arguments("fly", "intent=Fix the login page;work_kind=implement", "fly Fix the login page --work-kind implement --json")]
    [Arguments("ground", "flight=GG-42;because=the fleet cannot serve this yet", "ground GG-42 the fleet cannot serve this yet --json")]
    [Arguments("declare_name", "role=work-kind;name=triage", "airspace name work-kind triage --json")]
    [Arguments("declare_name", "role=work-kind;name=triage;under=root", "airspace name work-kind triage --under root --json")]
    [Arguments("retire_name", "name=triage", "airspace retire triage --json")]
    public async Task Each_tool_runs_the_verb_a_person_would_type(string tool, string given, string expected)
    {
        var ran = new Ran();
        var arguments = given.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .ToDictionary(pair => pair[0], pair => (object)pair[1]);

        var answers = await RecordingAsync(ran, Call(tool, arguments));

        await Assert.That(Failed(answers[0])).IsFalse().Because(Said(answers[0]));
        await Assert.That(ran.Calls.Count).IsEqualTo(1);
        await Assert.That(string.Join(" ", ran.Calls[0])).IsEqualTo(expected);
    }

    [Test]
    [Arguments("show_flight", "")]
    [Arguments("decide_gate", "flight=GG-42;obligation=widen-root;outcome=maybe")]
    [Arguments("answer_nomination", "nomination=x;answer=supersede;because=why")]
    [Arguments("answer_nomination", "nomination=x;answer=open")]
    [Arguments("ground", "flight=GG-42")]
    [Arguments("fly", "intent=--json")]
    [Arguments("show_flight", "flight=--all")]
    [Arguments("declare_name", "role=work-kind;name=-x")]
    public async Task A_call_that_is_incomplete_or_reads_as_a_flag_runs_nothing(string tool, string given)
    {
        var ran = new Ran();
        var arguments = given.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .ToDictionary(pair => pair[0], pair => (object)pair[1]);

        var answers = await RecordingAsync(ran, Call(tool, arguments));

        await Assert.That(Failed(answers[0])).IsTrue();
        await Assert.That(ran.Calls).IsEmpty()
            .Because("a value that starts with '-' would be read as a flag, and a missing one as a different verb.");
    }

    [Test]
    public async Task A_refusal_comes_back_as_an_error_in_gg_s_own_words()
    {
        var ran = new Ran { Answer = new() { Started = true, ExitCode = 1, Said = "GG-42 is not waiting on widen-root" } };

        var answers = await RecordingAsync(ran, Call(ManageTool.DecideGate, new()
        {
            ["flight"] = "GG-42", ["obligation"] = "widen-root", ["outcome"] = "approved",
        }));

        await Assert.That(Failed(answers[0])).IsTrue();
        await Assert.That(Said(answers[0])).Contains("not waiting on widen-root");
    }

    [Test]
    public async Task An_unknown_tool_is_refused()
    {
        var ran = new Ran();

        var answers = await RecordingAsync(ran, Call("credential_send"));

        await Assert.That(Failed(answers[0]) || answers[0].RootElement.TryGetProperty("error", out _)).IsTrue();
        await Assert.That(ran.Calls).IsEmpty();
    }

    [Test]
    public async Task The_verb_is_parsed_and_on_the_usage()
    {
        await Assert.That(CliArgs.Parse(["manage", "tools"])).IsTypeOf<CliAction.ManageTools>();
        await Assert.That(((CliAction.Unknown)CliArgs.Parse(["nonsense"])).Message).Contains("gg manage tools");
    }
}
