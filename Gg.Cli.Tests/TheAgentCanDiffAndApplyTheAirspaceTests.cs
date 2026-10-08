using System.Text.Json;
using Gg.Cli;
using Gg.Local;

namespace Gg.Cli.Tests;

/// <summary>
/// An airspace agent can ask what applying would change, and apply - each through the verb a
/// person would type, re-execed as a child (owner's call, 2026-10-07: "expose more of gg's
/// management layer into mcp").
/// </summary>
/// <remarks>
/// <b>A child, never a client</b>, for <c>pull_airspace</c>'s reason: this server holds no session
/// and no control-plane client, so the credential and the network stay in the process whose job
/// they are. Apply is declared here and never granted at launch - Claude Code asks the person.
/// </remarks>
public class TheAgentCanDiffAndApplyTheAirspaceTests
{
    private sealed class Ran
    {
        public List<(IReadOnlyList<string> Verb, string Root)> Calls { get; } = [];

        public PullReport Answer { get; set; } = new() { Started = true, ExitCode = 0, Said = "2 changes" };

        public PullReport Run(IReadOnlyList<string> verb, string root)
        {
            Calls.Add((verb, root));
            return Answer;
        }
    }

    private static async Task<IReadOnlyList<JsonDocument>> RecordingAsync(
        string? documentRoot, RunAirspace? airspace, params string[] lines)
    {
        var output = new StringWriter();
        await PlatformToolServer.RunAsync(
            new StringReader(string.Join('\n', lines)), output,
            intentPath: null, documentRoot: documentRoot, airspace: airspace);

        return output.ToString()
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => JsonDocument.Parse(line))
            .ToList();
    }

    private static string Call(string tool, object? arguments = null) => JsonSerializer.Serialize(new
    {
        jsonrpc = "2.0",
        id = 12,
        method = "tools/call",
        @params = new { name = tool, arguments = arguments ?? new Dictionary<string, object>() },
    });

    private static string Said(JsonDocument answer) =>
        answer.RootElement.GetProperty("result").GetProperty("content")[0]
            .GetProperty("text").GetString() ?? "";

    private static bool Failed(JsonDocument answer) =>
        answer.RootElement.GetProperty("result").TryGetProperty("isError", out var flag)
        && flag.GetBoolean();

    [Test]
    public async Task A_drafting_session_is_offered_both()
    {
        var answers = await RecordingAsync(
            "/tmp/tree", null, """{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}""");

        var names = answers[0].RootElement.GetProperty("result").GetProperty("tools")
            .EnumerateArray().Select(tool => tool.GetProperty("name").GetString()).ToList();

        await Assert.That(names).Contains(AirspaceDiffTool.Name);
        await Assert.That(names).Contains(AirspaceApplyTool.Name);
    }

    [Test]
    public async Task Diff_runs_the_diff_verb_on_the_working_copy()
    {
        var ran = new Ran();

        var answers = await RecordingAsync("/tmp/tree", ran.Run, Call(AirspaceDiffTool.Name));

        await Assert.That(ran.Calls.Count).IsEqualTo(1);
        await Assert.That(string.Join(" ", ran.Calls[0].Verb)).IsEqualTo("diff");
        await Assert.That(ran.Calls[0].Root).IsEqualTo("/tmp/tree");
        await Assert.That(Said(answers[0])).Contains("2 changes");
        await Assert.That(Failed(answers[0])).IsFalse();
    }

    [Test]
    public async Task Apply_runs_the_apply_verb_and_declares_names_only_when_asked()
    {
        var ran = new Ran();

        await RecordingAsync("/tmp/tree", ran.Run,
            Call(AirspaceApplyTool.Name),
            Call(AirspaceApplyTool.Name, new Dictionary<string, object> { [AirspaceApplyTool.DeclareNamesArgument] = true }));

        await Assert.That(ran.Calls.Select(c => string.Join(" ", c.Verb)))
            .IsEquivalentTo((string[])["apply", "apply --declare-names"]);
    }

    [Test]
    public async Task A_refusal_comes_back_as_an_error_in_gg_s_own_words()
    {
        var ran = new Ran { Answer = new() { Started = true, ExitCode = 1, Said = "These documents name things this airspace has not declared" } };

        var answers = await RecordingAsync("/tmp/tree", ran.Run, Call(AirspaceApplyTool.Name));

        await Assert.That(Failed(answers[0])).IsTrue();
        await Assert.That(Said(answers[0])).Contains("has not declared");
    }

    [Test]
    public async Task Without_a_working_copy_or_a_way_to_run_it_nothing_runs()
    {
        var ran = new Ran();

        var noTree = await RecordingAsync(null, ran.Run, Call(AirspaceDiffTool.Name));
        var noRunner = await RecordingAsync("/tmp/tree", null, Call(AirspaceApplyTool.Name));

        await Assert.That(ran.Calls).IsEmpty();
        await Assert.That(Failed(noTree[0])).IsTrue();
        await Assert.That(Failed(noRunner[0])).IsTrue();
    }
}
