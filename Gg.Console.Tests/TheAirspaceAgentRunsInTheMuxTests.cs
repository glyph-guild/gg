using Gg.Local;

namespace Gg.Console.Tests;

/// <summary>
/// The airspace agent is a mux agent: started from the column's "+ new agent" menu, running beside
/// gg, opening on a prompt that names its tools. The airspace modal's agent key, which took the
/// whole terminal, is gone (owner's call, 2026-10-07).
/// </summary>
public class TheAirspaceAgentRunsInTheMuxTests
{
    [Test]
    public async Task The_airspace_modal_no_longer_starts_an_agent()
    {
        var bindings = Keymap.Bindings(new KeymapContext(UiMode.AirspaceActions, TabId.Envelope));

        await Assert.That(bindings.Select(b => b.Command)).DoesNotContain(Command.DraftEstate)
            .Because("the agent that took over the terminal is replaced by the one beside gg.");
    }

    [Test]
    public async Task A_in_the_new_agent_menu_asks_for_an_airspace_agent()
    {
        using var fixture = new MuxFixture(columns: 100, rows: 20);

        var showing = fixture.Showing(MuxTab.New);
        await Assert.That(MuxFixture.Until(() => fixture.Terminal.Painted.Contains("airspace", StringComparison.Ordinal)))
            .IsTrue()
            .Because("the menu says the airspace agent is there before anybody presses for it.");

        fixture.Terminal.Type("a");

        await Assert.That(await showing.WaitAsync(TimeSpan.FromSeconds(20))).IsEqualTo(MuxLeave.Airspace);
    }

    [Test]
    public async Task The_loop_starts_the_airspace_agent_it_was_asked_for()
    {
        using var fixture = new MuxFixture(columns: 100, rows: 20);
        var started = 0;

        var ui = new Scripted(
            s => new UiOutcome(Command.ShowNewAgent, s),
            s => new UiOutcome(Command.Quit, s),
            s => new UiOutcome(Command.Quit, s));

        var running = Task.Factory.StartNew(
            () => new ConsoleLoop(ui, new NoEditor(), draftEstate: s =>
            {
                Interlocked.Increment(ref started);
                return s;
            }, mux: fixture.Mux).Run(new AppState()),
            CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

        await Assert.That(MuxFixture.Until(() => fixture.Terminal.Painted.Contains("airspace", StringComparison.Ordinal)))
            .IsTrue();
        fixture.Terminal.Type("a");

        await Assert.That(MuxFixture.Until(() => Volatile.Read(ref started) == 1)).IsTrue()
            .Because("choosing it in the menu starts it, as `l` there starts a plan agent.");
        await running.WaitAsync(TimeSpan.FromSeconds(20));
    }

    [Test]
    public async Task The_airspace_opening_names_every_tool_and_that_apply_asks_first()
    {
        var prompt = AgentOpening.Airspace();

        foreach (var tool in (string[])[AirspaceContextTool.Qualified, AirspacePullTool.Qualified,
                     DocumentTool.Qualified, AirspaceDiffTool.Qualified, AirspaceApplyTool.Qualified])
        {
            await Assert.That(prompt).Contains(tool);
        }

        await Assert.That(prompt).DoesNotContain("\n");
    }

    [Test]
    public async Task The_launch_opens_on_the_prompt_grants_diff_and_never_apply()
    {
        using var terminal = new HostedTerminal { Columns = 80, Rows = 24 };
        var tree = Directory.CreateTempSubdirectory("gg-airspace-agent-");
        var agent = Path.Combine(tree.FullName, "agent.sh");
        File.WriteAllText(agent, $"printf '%s\\n' \"$@\" > '{tree.FullName}/argv'\n");

        try
        {
            new PtyDraftSession($"/bin/sh {agent}", () => terminal,
                    self: new SelfInvocation("/usr/local/bin/gg", ["runner", "tools"]))
                .Draft(tree.FullName);

            var argv = await File.ReadAllLinesAsync(Path.Combine(tree.FullName, "argv"));

            await Assert.That(argv[0]).IsEqualTo(AgentOpening.Airspace())
                .Because("the prompt comes before every flag, or --allowedTools swallows it.");
            await Assert.That(argv).Contains(AirspaceDiffTool.Qualified)
                .Because("reading what would change is safe to grant.");
            await Assert.That(argv).DoesNotContain(AirspaceApplyTool.Qualified)
                .Because("applying is asked for every time: Claude Code's prompt is the confirmation.");
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    private sealed class Scripted(params Func<AppState, UiOutcome>[] script) : IUiSession
    {
        private readonly Queue<Func<AppState, UiOutcome>> _script = new(script);

        public UiOutcome Run(AppState state) => _script.Dequeue()(state);
    }

    private sealed class NoEditor : IEditorSession
    {
        public string Edit(string initialText) => initialText;
    }
}
