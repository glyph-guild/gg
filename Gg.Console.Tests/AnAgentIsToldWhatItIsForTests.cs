using Gg.Local;

namespace Gg.Console.Tests;

/// <summary>
/// An agent gg launches for a flight or a plan opens on a prompt that says what the session is
/// for and which of gg's tools carry the answer (owner's call, 2026-10-07).
/// </summary>
/// <remarks>
/// <para>
/// <b>Before every flag, and that is not style.</b> Claude Code's <c>--allowedTools</c> and
/// <c>--mcp-config</c> each take a LIST, so a prompt placed after them is read as one more tool
/// name and the session opens on nothing.
/// </para>
/// <para>
/// <b>One line.</b> It is an argument on a command line, and the fake agents these tests use
/// print one argument per line.
/// </para>
/// </remarks>
public class AnAgentIsToldWhatItIsForTests
{
    [Test]
    public async Task The_compose_prompt_names_the_tool_and_what_was_chosen()
    {
        var prompt = AgentOpening.Compose(new ComposeBrief("implement", ["glyph-guild/gg"]));

        await Assert.That(prompt).Contains(IntentTool.Qualified)
            .Because("the tool is the only way the intent reaches gg.");
        await Assert.That(prompt).Contains("'implement'");
        await Assert.That(prompt).Contains("glyph-guild/gg");
        await Assert.That(prompt).DoesNotContain("\n");
    }

    [Test]
    public async Task A_compose_with_nothing_chosen_says_so_rather_than_naming_nothing()
    {
        var prompt = AgentOpening.Compose(new ComposeBrief(null, []));

        await Assert.That(prompt).Contains(IntentTool.Qualified);
        await Assert.That(prompt).DoesNotContain("''");
        await Assert.That(prompt).DoesNotContain("against .");
    }

    [Test]
    public async Task The_plan_prompt_names_every_planning_tool_and_its_draft()
    {
        var prompt = AgentOpening.Plan("console-2");

        foreach (var tool in PlanningTool.All)
        {
            await Assert.That(prompt).Contains(PlanningTool.Qualified(tool))
                .Because($"'{tool}' is one of the tools the plan is made with.");
        }

        await Assert.That(prompt).Contains("'console-2'");
        await Assert.That(prompt).DoesNotContain("\n");
    }

    [Test]
    public async Task The_compose_session_opens_on_its_prompt_before_any_flag()
    {
        using var terminal = new HostedTerminal { Columns = 80, Rows = 24 };
        var compose = Directory.CreateTempSubdirectory("gg-opening-");
        var agent = Path.Combine(compose.FullName, "agent.sh");
        File.WriteAllText(agent, $"printf '%s\\n' \"$@\" > '{compose.FullName}/argv'\n");

        try
        {
            new PtyAgentSession(
                    $"/bin/sh {agent}", () => terminal, self: new SelfInvocation("/usr/local/bin/gg", ["runner", "tools"]),
                    composeIn: compose.FullName)
                .Compose("", new ComposeBrief("implement", ["glyph-guild/gg"]));

            var argv = await File.ReadAllLinesAsync(Path.Combine(compose.FullName, "argv"));

            await Assert.That(argv[0]).IsEqualTo(AgentOpening.Compose(new ComposeBrief("implement", ["glyph-guild/gg"])))
                .Because("the prompt comes before every flag, or a list flag swallows it. Passed: "
                       + string.Join(" | ", argv));
        }
        finally
        {
            compose.Delete(recursive: true);
        }
    }

    [Test]
    public async Task The_plan_session_opens_on_its_prompt_before_any_flag()
    {
        using var session = new PlanSessionFixture();

        session.Run();

        await Assert.That(session.Arguments[0]).IsEqualTo(AgentOpening.Plan("console"))
            .Because("the prompt comes before every flag, or --allowedTools swallows it.");
    }

    [Test]
    public async Task The_loop_tells_the_compose_agent_what_was_chosen()
    {
        var seen = new List<ComposeBrief>();
        var compose = new Briefed(seen);
        using var fixture = new MuxFixture();

        var state = new AppState { FlyingWith = ["glyph-guild/gg"] };
        var ui = new Scripted(
            s => new UiOutcome(Command.ComposeWithAgent, s with { Mode = UiMode.ComposeChoice }),
            s => new UiOutcome(Command.Quit, s),
            s => new UiOutcome(Command.Quit, s));

        new ConsoleLoop(ui, new Briefed([]), compose: compose, mux: fixture.Mux).Run(state);

        await Assert.That(MuxFixture.Until(() => seen.Count > 0)).IsTrue();
        await Assert.That(seen[0].Against).IsEquivalentTo(["glyph-guild/gg"]);
    }

    private sealed class Briefed(List<ComposeBrief> seen) : IEditorSession
    {
        public string Edit(string initialText) => "";

        public string Compose(string initialText, ComposeBrief brief)
        {
            lock (seen)
            {
                seen.Add(brief);
            }

            return "";
        }
    }

    private sealed class Scripted(params Func<AppState, UiOutcome>[] script) : IUiSession
    {
        private readonly Queue<Func<AppState, UiOutcome>> _script = new(script);

        public UiOutcome Run(AppState state) => _script.Dequeue()(state);
    }
}
