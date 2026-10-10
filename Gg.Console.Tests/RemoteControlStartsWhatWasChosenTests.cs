using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// The runner modal's sessions view acts on what is chosen in it: a new session, the one
/// under the cursor attached to or resumed, one ended session forgotten, or all of them
/// (slice seventy-one, S71.4-02).
/// </summary>
/// <remarks>
/// <para>
/// <b>Keys, not buttons, and only where they can work.</b> The four are offered on the
/// sessions view of a machine whose heartbeat says it takes sessions, and nowhere else
/// in the modal: <c>n</c> and <c>D</c> over any such machine, <c>enter</c> over a row,
/// and <c>d</c> only over a session that ended, because a machine refuses to forget one
/// that runs.
/// </para>
/// <para>
/// <b>Between sessions.</b> Reaching a machine is a control-plane introduction and a
/// channel, which a UI session may not open, so the commands are the shell's. The loop
/// hands one act to the composition root's delegate and the runner modal is still open
/// when the console comes back.
/// </para>
/// <para>
/// <b>Remote Control in the mux hands off to the console.</b> Choosing a machine there
/// opens this modal on that machine's sessions, so there is one place sessions are listed
/// and acted on, not two that drift.
/// </para>
/// </remarks>
public class RemoteControlStartsWhatWasChosenTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 10, 9, 0, 0, TimeSpan.Zero);

    private static readonly RemoteMachine Vm3 = new("runner-vm3", "vmlinux003");

    private static AgentSessionStanding Session(string id, bool alive) => new()
    {
        SessionId = id,
        Directory = "/srv/" + id,
        StartedAt = alive ? T0.AddHours(1) : T0,
        Alive = alive,
        EndedAt = alive ? null : T0.AddMinutes(30),
    };

    private static RunnerSummary Machine(bool? accepts, params AgentSessionStanding[] sessions) => new()
    {
        RunnerId = Vm3.Id,
        Label = Vm3.Name,
        State = RunnerStates.Idle,
        LastHeartbeatAt = T0,
        Labels = [],
        AcceptsAgentSessions = accepts,
        AgentSessions = sessions,
    };

    /// <summary>The modal open on vmlinux003; the live session sorts first, the ended one second.</summary>
    private static AppState Open(
        RunnerView view = RunnerView.Sessions, int session = 0, bool? accepts = true) => new()
        {
            Mode = UiMode.Runner,
            ActiveTab = TabId.Runners,
            RunnerView = view,
            RunnerSelected = 0,
            RunnerSessionSelected = session,
            Runners = new RunnerList
            {
                Runners = [Machine(accepts, Session("ended-1", alive: false), Session("live-1", alive: true))],
            },
        };

    private static Command? Pressed(KeyStroke key, AppState state) =>
        Keymap.Resolve(key, KeymapContext.For(state));

    // --- the keys ---

    [Test]
    public async Task Over_a_live_session_n_enter_and_D_are_offered_and_d_is_not()
    {
        var live = Open(session: 0);

        await Assert.That(RunnerActivity.SelectedSession(live)?.SessionId).IsEqualTo("live-1");

        await Assert.That(Pressed(KeyStroke.Char('n'), live)).IsEqualTo(Command.StartRemoteSession);
        await Assert.That(Pressed(KeyStroke.EnterKey, live)).IsEqualTo(Command.OpenRemoteSession);
        await Assert.That(Pressed(KeyStroke.Char('D'), live)).IsEqualTo(Command.ForgetEndedRemoteSessions);
        await Assert.That(Pressed(KeyStroke.Char('d'), live)).IsNull()
            .Because("the machine refuses to forget a session that runs, so the key is not offered.");
    }

    [Test]
    public async Task Over_an_ended_session_d_forgets_it_and_enter_resumes_it()
    {
        var ended = Open(session: 1);

        await Assert.That(RunnerActivity.SelectedSession(ended)?.SessionId).IsEqualTo("ended-1");

        await Assert.That(Pressed(KeyStroke.Char('d'), ended)).IsEqualTo(Command.ForgetRemoteSession);
        await Assert.That(Pressed(KeyStroke.EnterKey, ended)).IsEqualTo(Command.OpenRemoteSession);

        var line = Keymap.Hints(KeymapContext.For(ended));

        foreach (var said in (string[])["resume it", "forget it", "start a session here", "forget every ended session"])
        {
            await Assert.That(line).Contains(said, StringComparison.Ordinal)
                .Because("a modal with no buttons teaches its keys on its line. Line: " + line);
        }

        await Assert.That(Keymap.Hints(KeymapContext.For(Open(session: 0))))
            .Contains("attach to it", StringComparison.Ordinal);
    }

    [Test]
    public async Task On_any_other_view_or_a_machine_that_takes_none_the_keys_are_not_there()
    {
        foreach (var state in (AppState[])
                 [Open(view: RunnerView.Flights), Open(view: RunnerView.Log),
                  Open(accepts: false), Open(accepts: null)])
        {
            foreach (var key in (KeyStroke[])
                     [KeyStroke.Char('n'), KeyStroke.EnterKey, KeyStroke.Char('d'), KeyStroke.Char('D')])
            {
                await Assert.That(Pressed(key, state)).IsNull()
                    .Because($"{key.Name} on the {state.RunnerView} view of a machine that "
                           + $"accepts={state.Runners!.Runners[0].AcceptsAgentSessions} would act on nothing.");
            }
        }
    }

    [Test]
    public async Task On_an_empty_list_n_and_D_are_offered_and_enter_and_d_are_not()
    {
        var empty = Open() with
        {
            Runners = new RunnerList { Runners = [Machine(accepts: true)] },
        };

        await Assert.That(Pressed(KeyStroke.Char('n'), empty)).IsEqualTo(Command.StartRemoteSession);
        await Assert.That(Pressed(KeyStroke.EnterKey, empty)).IsNull();
        await Assert.That(Pressed(KeyStroke.Char('d'), empty)).IsNull();
    }

    [Test]
    public async Task No_session_key_carries_a_label()
    {
        foreach (var session in (int[])[0, 1])
        {
            var labelled = Keymap.Bindings(KeymapContext.For(Open(session: session)))
                .Where(b => b.Label is { Length: > 0 })
                .Select(b => b.Description)
                .ToList();

            await Assert.That(labelled).IsEmpty()
                .Because("the runner modal is keys only. Labelled: " + string.Join(", ", labelled));
        }
    }

    [Test]
    public async Task The_four_are_the_shells_and_the_reducer_leaves_them()
    {
        foreach (var command in Acts)
        {
            await Assert.That(ShellCommands.Handled).Contains(command);

            var before = Open(session: 1);
            await Assert.That(Reducer.Reduce(before, command)).IsEqualTo(before);
        }
    }

    private static readonly Command[] Acts =
    [
        Command.StartRemoteSession, Command.OpenRemoteSession,
        Command.ForgetRemoteSession, Command.ForgetEndedRemoteSessions,
    ];

    // --- the loop ---

    [Test]
    [Arguments(Command.StartRemoteSession, 0, RemoteSessionKind.Start, null, false)]
    [Arguments(Command.OpenRemoteSession, 0, RemoteSessionKind.Open, "live-1", true)]
    [Arguments(Command.OpenRemoteSession, 1, RemoteSessionKind.Open, "ended-1", false)]
    [Arguments(Command.ForgetRemoteSession, 1, RemoteSessionKind.Forget, "ended-1", false)]
    [Arguments(Command.ForgetEndedRemoteSessions, 0, RemoteSessionKind.ForgetEnded, null, false)]
    public async Task Each_key_hands_the_root_one_act_and_the_modal_stays_open(
        Command command, int session, RemoteSessionKind kind, string? sessionId, bool alive)
    {
        var acts = new List<RemoteSessionAct>();
        AppState? next = null;

        var ui = new Scripted(
            s => new UiOutcome(command, s),
            s =>
            {
                next = s;
                return new UiOutcome(Command.Quit, s);
            });

        new ConsoleLoop(ui, new NoEditor(), remoteSession: (s, act) =>
        {
            acts.Add(act);
            return s with { LastRunner = "done" };
        }).Run(Open(session: session));

        var act = acts.Single();

        await Assert.That(act.Kind).IsEqualTo(kind);
        await Assert.That(act.RunnerId).IsEqualTo(Vm3.Id);
        await Assert.That(act.RunnerLabel).IsEqualTo(Vm3.Name);
        await Assert.That(act.SessionId).IsEqualTo(sessionId);
        await Assert.That(act.Alive).IsEqualTo(alive);

        await Assert.That(next!.Mode).IsEqualTo(UiMode.Runner)
            .Because("the runner modal is still open when the person comes back to gg.");
        await Assert.That(next.RunnerView).IsEqualTo(RunnerView.Sessions);
        await Assert.That(next.LastRunner).IsEqualTo("done");
    }

    [Test]
    public async Task A_console_that_cannot_reach_machines_says_so()
    {
        AppState? next = null;
        var ui = new Scripted(
            s => new UiOutcome(Command.StartRemoteSession, s),
            s =>
            {
                next = s;
                return new UiOutcome(Command.Quit, s);
            });

        new ConsoleLoop(ui, new NoEditor()).Run(Open());

        await Assert.That(next!.LastRunner).Contains("cannot reach", StringComparison.Ordinal);
        await Assert.That(next.Mode).IsEqualTo(UiMode.Runner);
    }

    [Test]
    public async Task Forgetting_reads_the_fleet_again_so_the_pane_redraws()
    {
        var reloads = 0;
        var ui = new Scripted(
            s => new UiOutcome(Command.ForgetRemoteSession, s),
            s => new UiOutcome(Command.Quit, s));

        new ConsoleLoop(
            ui, new NoEditor(),
            reload: s =>
            {
                reloads++;
                return s;
            },
            remoteSession: (s, _) => s).Run(Open(session: 1));

        await Assert.That(reloads).IsGreaterThanOrEqualTo(1);
    }

    // --- what the root's delegate does with the mux ---

    [Test]
    public async Task A_forget_says_the_list_follows_the_machines_next_heartbeat()
    {
        using var fixture = new MuxFixture(columns: 120, rows: 20);
        var link = Forgetting();
        fixture.Mux.Reaching(() => [Vm3], _ => new RemoteReach(link, null));

        var after = ConsoleRemoteSession.Act(
            fixture.Mux, Open(session: 1),
            new RemoteSessionAct(RemoteSessionKind.Forget, Vm3.Id, Vm3.Name, "ended-1"));

        await Assert.That(after.LastRunner).Contains("forgotten", StringComparison.Ordinal);
        await Assert.That(after.LastRunner).Contains("next heartbeat", StringComparison.Ordinal)
            .Because("the fleet's list lags a heartbeat, and the row is still drawn until it arrives.");
        await Assert.That(after.Mode).IsEqualTo(UiMode.Runner);
    }

    [Test]
    public async Task An_open_puts_the_session_on_a_row_and_asks_the_mux_to_show_it()
    {
        using var fixture = new MuxFixture(columns: 120, rows: 20);
        var link = FakeLink.Machine();
        fixture.Mux.Reaching(() => [Vm3], _ => new RemoteReach(link, null));

        var after = ConsoleRemoteSession.Act(
            fixture.Mux, Open(), new RemoteSessionAct(RemoteSessionKind.Start, Vm3.Id, Vm3.Name));

        await Assert.That(MuxFixture.Until(() => fixture.Mux.Rows().Count == 1)).IsTrue();
        await Assert.That(fixture.Mux.TakeWanted()).IsEqualTo(MuxTab.Agent(1))
            .Because("the next turn of the loop shows it, and ctrl-g 0 comes back to this modal.");
        await Assert.That(after.Mode).IsEqualTo(UiMode.Runner);
    }

    [Test]
    public async Task A_refused_open_is_said_where_the_person_is_looking()
    {
        using var fixture = new MuxFixture(columns: 120, rows: 20);
        fixture.Mux.Reaching(() => [Vm3], _ => new RemoteReach(null, "vmlinux003 is offline."));

        var after = ConsoleRemoteSession.Act(
            fixture.Mux, Open(), new RemoteSessionAct(RemoteSessionKind.Start, Vm3.Id, Vm3.Name));

        await Assert.That(after.LastRunner).IsEqualTo("vmlinux003 is offline.");
        await Assert.That(fixture.Mux.Rows()).IsEmpty();
    }

    // --- the mux's forget ---

    /// <summary>A machine that forgets an ended session and refuses a live one, as a runner does.</summary>
    private static FakeLink Forgetting() => new()
    {
        Answers = frame => frame switch
        {
            ForgetAgentSession { SessionId: "live-1" } =>
                [new AgentSessionRefused { Because = "live-1 is running; end it before forgetting it." }],
            ForgetAgentSession => [new AgentSessionList { Sessions = [] }],
            _ => [],
        },
    };

    [Test]
    public async Task Forgetting_one_sends_its_id_and_lets_the_channel_go()
    {
        var mux = new Mux(terminal: () => null);
        var link = Forgetting();
        mux.Reaching(() => [Vm3], _ => new RemoteReach(link, null));

        await Assert.That(mux.ForgetRemote(Vm3, "ended-1")).IsNull();
        await Assert.That(link.Sent.OfType<ForgetAgentSession>().Single().SessionId).IsEqualTo("ended-1");
        await Assert.That(link.Disposed).IsTrue()
            .Because("a forget holds no session open, so its channel is not kept.");
    }

    [Test]
    public async Task Forgetting_every_ended_one_sends_no_id()
    {
        var mux = new Mux(terminal: () => null);
        var link = Forgetting();
        mux.Reaching(() => [Vm3], _ => new RemoteReach(link, null));

        await Assert.That(mux.ForgetRemote(Vm3, null)).IsNull();
        await Assert.That(link.Sent.OfType<ForgetAgentSession>().Single().SessionId).IsNull()
            .Because("the contract's null forgets every ended session.");
    }

    [Test]
    public async Task A_live_one_is_refused_in_the_machines_words()
    {
        var mux = new Mux(terminal: () => null);
        mux.Reaching(() => [Vm3], _ => new RemoteReach(Forgetting(), null));

        await Assert.That(mux.ForgetRemote(Vm3, "live-1")).IsEqualTo("live-1 is running; end it before forgetting it.");
    }

    // --- Remote Control in the mux hands off ---

    [Test]
    public async Task Choosing_a_machine_in_the_mux_hands_it_to_the_console()
    {
        using var fixture = new MuxFixture(columns: 120, rows: 20);
        var reached = 0;
        fixture.Mux.Reaching(
            () => [Vm3, new RemoteMachine("runner-vm2", "vmlinux002")],
            _ =>
            {
                reached++;
                return new RemoteReach(null, "not reached here");
            });

        var showing = fixture.Showing(MuxTab.New);
        await Assert.That(MuxFixture.Until(() => fixture.Terminal.Painted.Contains("Remote Control", StringComparison.Ordinal))).IsTrue();
        await Assert.That(fixture.Choose("Remote Control")).IsTrue();
        await Assert.That(MuxFixture.Until(() => fixture.Terminal.Painted.Contains("▸ vmlinux003", StringComparison.Ordinal))).IsTrue();

        fixture.Terminal.Type("\r");

        await Assert.That(await showing.WaitAsync(TimeSpan.FromSeconds(20))).IsEqualTo(MuxLeave.RemoteControl);
        await Assert.That(fixture.Mux.TakeRemoteControl()).IsEqualTo(Vm3.Id);
        await Assert.That(fixture.Mux.TakeRemoteControl()).IsNull()
            .Because("taken once, like what the shell was asked to show.");
        await Assert.That(reached).IsEqualTo(0)
            .Because("the console reaches the machine when a key on its sessions asks, not the menu.");
    }

    [Test]
    public async Task The_console_opens_that_machines_sessions()
    {
        var fleet = new AppState
        {
            ActiveTab = TabId.Queue,
            Runners = new RunnerList
            {
                Runners = [Machine(true) with { RunnerId = "runner-other", Label = "other" }, Machine(true)],
            },
        };

        var opened = ConsoleRemoteSession.Opened(fleet, Vm3.Id);

        await Assert.That(opened).IsNotNull();
        await Assert.That(opened!.Mode).IsEqualTo(UiMode.Runner);
        await Assert.That(opened.ActiveTab).IsEqualTo(TabId.Runners);
        await Assert.That(opened.RunnerView).IsEqualTo(RunnerView.Sessions);
        await Assert.That(Rows.Selected(opened)?.Id).IsEqualTo(Vm3.Id);

        await Assert.That(ConsoleRemoteSession.Opened(fleet, "runner-gone")).IsNull()
            .Because("a machine the fleet does not list cannot be opened, and the loop says so.");
    }

    [Test]
    public async Task The_loop_has_an_arm_for_the_hand_off()
    {
        var loop = Sources.Read("Gg.Console", "ConsoleLoop.cs");

        await Assert.That(loop).Contains("MuxLeave.RemoteControl", StringComparison.Ordinal);
        await Assert.That(loop).Contains("TakeRemoteControl()", StringComparison.Ordinal);
    }

    [Test]
    public async Task The_sessions_table_hands_its_keys_to_the_modal()
    {
        // ENTER ON A TABLE IS THE TABLE'S unless its keys go to the modal first.
        var screen = Sources.Read("Gg.Console", "Views", "ConsoleScreen.cs");

        await Assert.That(screen).Contains("_runnerSessions.KeyDown += OnModalKeyDown", StringComparison.Ordinal);
        await Assert.That(screen).Contains("_runnerSessions.KeyDown -= OnModalKeyDown", StringComparison.Ordinal);
    }

    [Test]
    public async Task The_root_passes_the_delegate_by_name()
    {
        var program = Sources.Read("Gg.Cli", "Program.cs");

        await Assert.That(program).Contains("remoteSession:", StringComparison.Ordinal);
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
