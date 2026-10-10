using Gg.Client;
using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Console.Tests;

/// <summary>
/// The runner modal shows what a runner has flown and the agent sessions it holds
/// (slice seventy-one, S71.4-01, part one: display only).
/// </summary>
/// <remarks>
/// <para>
/// <b>Two panes after the three that were there.</b> The log, the environments and
/// the members answer what a runner said, what it runs and what runs beside it. The
/// question Remote Control adds is what it has DONE and what it is HOLDING: the
/// flights it claimed, and the ad hoc agent sessions on the machine.
/// </para>
/// <para>
/// <b>The sessions cost no request; the flights cost one.</b> A runner's sessions
/// arrive with the fleet read, on every refresh, so the pane draws them from the row
/// the modal is open on. Its recent flights are a read of their own, asked for when
/// the pane is shown - the facts tab's arrangement one modal over, and for its
/// reason: a request per runner at boot would be paid by everybody for a pane almost
/// nobody opens.
/// </para>
/// <para>
/// <b>No keys yet.</b> The session actions arrive in a follow-up, so the absence must
/// not name a key nothing binds - the facts tab's "press for its facts" was exactly
/// that defect.
/// </para>
/// </remarks>
public class RemoteControlShowsARunnerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 10, 9, 0, 0, TimeSpan.Zero);

    private static AgentSessionStanding Session(
        string id, bool alive, string? directory = "/home/kev/src/gg", int startedMinutes = 0) => new()
        {
            SessionId = id,
            Directory = directory,
            StartedAt = T0.AddMinutes(startedMinutes),
            Alive = alive,
            EndedAt = alive ? null : T0.AddMinutes(startedMinutes + 30),
        };

    private static RunnerSummary Runner(
        string label, bool? accepts = null, IReadOnlyList<AgentSessionStanding>? sessions = null) => new()
        {
            RunnerId = "runner-" + label,
            Label = label,
            State = RunnerStates.Idle,
            LastHeartbeatAt = T0,
            Labels = [],
            AcceptsAgentSessions = accepts,
            AgentSessions = sessions,
        };

    /// <summary>The modal open on the second runner of three, on the given pane.</summary>
    private static AppState Open(RunnerView view, params RunnerSummary[] fleet) => new()
    {
        Mode = UiMode.Runner,
        ActiveTab = TabId.Runners,
        RunnerView = view,
        RunnerSelected = 1,
        Runners = new RunnerList
        {
            Runners = fleet.Length > 0
                ? fleet
                :
                [
                    Runner("a", accepts: true, sessions: [Session("aaaaaaaa-1111", alive: true)]),
                    Runner("b", accepts: true, sessions:
                    [
                        Session("bbbbbbbb-ended", alive: false, directory: "/srv/old", startedMinutes: 0),
                        Session("cccccccc-live", alive: true, directory: "/srv/new", startedMinutes: 60),
                    ]),
                    Runner("c"),
                ],
        },
    };

    private static RunnerFlightList Flown(params RunnerFlight[] flights) => new() { Flights = flights };

    private static RunnerFlight AFlight(int number, string state = FlightStates.Landed) => new()
    {
        FlightId = AConsolePlane.Id(number),
        Number = number,
        Kind = "implement",
        State = state,
        ClaimedAt = T0,
        EndedAt = state == FlightStates.Open ? null : T0.AddMinutes(5),
    };

    // --- the panes ---

    [Test]
    public async Task Flights_and_sessions_come_after_the_three_that_were_there()
    {
        await Assert.That(RunnerViews.All)
            .IsEquivalentTo((RunnerView[])
            [
                RunnerView.Log, RunnerView.Environments, RunnerView.Members,
                RunnerView.Flights, RunnerView.Sessions,
            ]);

        await Assert.That(RunnerViews.All[0]).IsEqualTo(RunnerView.Log)
            .Because("nothing changes for somebody who opens a runner and presses nothing.");

        await Assert.That(RunnerViews.Title(RunnerView.Flights)).IsEqualTo("flights");
        await Assert.That(RunnerViews.Title(RunnerView.Sessions)).IsEqualTo("sessions");
    }

    [Test]
    public async Task V_turns_through_all_five_and_comes_back_round()
    {
        var at = RunnerView.Log;

        foreach (var expected in (RunnerView[])
                 [RunnerView.Environments, RunnerView.Members, RunnerView.Flights,
                  RunnerView.Sessions, RunnerView.Log])
        {
            at = Reducer.Reduce(Open(at), Command.NextRunnerView).RunnerView;
            await Assert.That(at).IsEqualTo(expected);
        }
    }

    // --- sessions ---

    [Test]
    public async Task The_sessions_are_the_selected_runners_and_the_live_one_leads()
    {
        var rows = RunnerActivity.Sessions(Open(RunnerView.Sessions));

        await Assert.That(rows.Select(r => r.SessionId))
            .IsEquivalentTo((string[])["cccccccc-live", "bbbbbbbb-ended"])
            .Because("the cursor is on runner b, and runner a's session is not b's business.");

        await Assert.That(rows[0].SessionId).IsEqualTo("cccccccc-live")
            .Because("a running session is the one a person came to reach.");

        await Assert.That(rows[0].Session).IsEqualTo("cccccccc")
            .Because("eight characters tell two sessions apart; the whole id is carried for the "
                   + "actions that will need it.");
        await Assert.That(rows[0].State).IsEqualTo("running");
        await Assert.That(rows[0].Directory).IsEqualTo("/srv/new");
        await Assert.That(rows[0].Ended).IsEmpty();

        await Assert.That(rows[1].State).IsEqualTo("ended");
        await Assert.That(rows[1].Ended).IsNotEmpty();
    }

    [Test]
    public async Task The_session_cursor_names_the_session_it_is_on()
    {
        var state = Reducer.Pointed(Open(RunnerView.Sessions), 1);

        await Assert.That(state.RunnerSessionSelected).IsEqualTo(1);
        await Assert.That(state.RunnerSelected).IsEqualTo(1)
            .Because("pointing inside the modal must not change which runner it is open on.");

        await Assert.That(RunnerActivity.SelectedSession(state)?.SessionId).IsEqualTo("bbbbbbbb-ended")
            .Because("the row under the cursor is the session the follow-up's keys will act on.");

        var past = Reducer.Pointed(Open(RunnerView.Sessions), 99);

        await Assert.That(past.RunnerSessionSelected).IsEqualTo(1)
            .Because("two sessions, so the last row is one.");
    }

    [Test]
    public async Task A_sessions_text_is_stripped_before_it_is_drawn()
    {
        var state = Open(RunnerView.Sessions,
            Runner("a"),
            Runner("b", accepts: true, sessions:
                [Session("dddddddd-evil\u001b[2J", alive: true, directory: "/tmp/\u001b]0;owned\u0007x")]));

        var row = RunnerActivity.Sessions(state).Single();

        await Assert.That(row.Directory).DoesNotContain("\u001b");
        await Assert.That(row.SessionId).DoesNotContain("\u001b")
            .Because("a session's id and directory are a machine's to say, and console text from "
                   + "elsewhere is cleaned at the doorway.");
    }

    [Test]
    public async Task A_runner_that_does_not_take_sessions_says_its_configuration_does_not()
    {
        foreach (var accepts in (bool?[])[false, null])
        {
            var state = Open(RunnerView.Sessions, Runner("a"), Runner("b", accepts: accepts));

            await Assert.That(RunnerActivity.Sessions(state)).IsEmpty();

            await Assert.That(RunnerActivity.SessionsAbsence(state))
                .Contains("accept-agent-sessions", StringComparison.Ordinal)
                .Because("null and false both mean it will not answer, and the remedy is the "
                       + "machine's own file.");
        }
    }

    [Test]
    public async Task A_runner_that_takes_sessions_and_holds_none_says_so_and_names_no_key()
    {
        var state = Open(RunnerView.Sessions, Runner("a"), Runner("b", accepts: true, sessions: []));

        var said = RunnerActivity.SessionsAbsence(state);

        await Assert.That(said).IsEqualTo("No sessions on this machine yet.");
        await Assert.That(said).DoesNotContain("accept-agent-sessions", StringComparison.Ordinal);

        await Assert.That(RunnerActivity.SessionsAbsence(Open(RunnerView.Sessions))).IsEmpty()
            .Because("rows are the answer once there are any.");
    }

    // --- flights ---

    [Test]
    public async Task Showing_the_flights_pane_is_what_asks_for_them()
    {
        await Assert.That(RunnerActivity.FlightsOwed(Open(RunnerView.Flights))).IsEqualTo("runner-b")
            .Because("a pane showing flights it has not read owes the read.");

        await Assert.That(RunnerActivity.FlightsOwed(Open(RunnerView.Members))).IsNull()
            .Because("only the pane that shows them asks.");

        await Assert.That(RunnerActivity.FlightsOwed(Open(RunnerView.Flights) with { ReadInFlight = true }))
            .IsNull()
            .Because("one read runs at a time and a second abandons the first.");

        var held = Open(RunnerView.Flights) with
        {
            RunnerFlights = Flown(AFlight(42)), RunnerFlightsFor = "runner-b",
        };

        await Assert.That(RunnerActivity.FlightsOwed(held)).IsNull()
            .Because("flights already held for this runner are not asked for again.");

        await Assert.That(RunnerActivity.FlightsOwed(held with { RunnerFlightsFor = "runner-a" }))
            .IsEqualTo("runner-b")
            .Because("holding another runner's flights is holding none of this one's.");
    }

    [Test]
    public async Task It_is_a_read_rather_than_a_session_ending_command()
    {
        await Assert.That(ShellCommands.Reads).Contains(Command.ShowRunnerFlights);
        await Assert.That(ShellCommands.Handled).DoesNotContain(Command.ShowRunnerFlights);
    }

    [Test]
    public async Task The_read_asks_for_the_runners_flights_and_folds_them_into_the_table()
    {
        var (data, plane) = AConsolePlane.Console();

        var read = ConsoleRunnerFlights.Read(data, Open(RunnerView.Flights));

        await Assert.That(plane.Paths).Contains("/v1/runners/runner-b/flights");
        await Assert.That(read.RunnerFlightsFor).IsEqualTo("runner-b");

        var rows = RunnerActivity.Flights(read);

        await Assert.That(rows.Select(r => r.Flight)).IsEquivalentTo((string[])[FlightRef.Format(42)]);
        await Assert.That(rows[0].Kind).IsEqualTo("implement");
        await Assert.That(rows[0].State).IsEqualTo(FlightStates.Landed);
        await Assert.That(rows[0].Claimed).IsNotEmpty();
        await Assert.That(rows[0].Ended).IsNotEmpty();
    }

    [Test]
    public async Task The_fold_goes_through_the_one_projection_and_cleans_what_it_stores()
    {
        var flown = Flown(AFlight(7, FlightStates.Open) with { Kind = "impl\u001b[31mement" });

        var state = ConsoleProjection.Apply(
            Open(RunnerView.Flights), new VerbResult.RunnerFlights("runner-b", flown));

        await Assert.That(state.RunnerFlightsFor).IsEqualTo("runner-b");

        var row = RunnerActivity.Flights(state).Single();

        await Assert.That(row.Flight).IsEqualTo(FlightRef.Format(7));
        await Assert.That(row.Kind).DoesNotContain("\u001b");
        await Assert.That(row.Ended).IsEmpty()
            .Because("a flight still in the air has not ended, and a placeholder would say it had.");
    }

    [Test]
    public async Task Another_runners_flights_are_never_drawn_under_this_one()
    {
        var state = Open(RunnerView.Flights) with
        {
            RunnerFlights = Flown(AFlight(42)), RunnerFlightsFor = "runner-a",
        };

        await Assert.That(RunnerActivity.Flights(state)).IsEmpty()
            .Because("runner a's flights are not something runner b flew.");
    }

    [Test]
    public async Task Three_absences_three_sentences()
    {
        var coming = Open(RunnerView.Flights) with { ReadInFlight = true };

        await Assert.That(RunnerActivity.FlightsAbsence(coming))
            .Contains("still coming", StringComparison.Ordinal);

        var failed = Open(RunnerView.Flights) with { Diagnosis = "the control plane said 503" };

        await Assert.That(RunnerActivity.FlightsAbsence(failed))
            .Contains("could not be read", StringComparison.Ordinal);
        await Assert.That(RunnerActivity.FlightsAbsence(failed))
            .Contains("the control plane said 503", StringComparison.Ordinal)
            .Because("the one absence with a reason should say it.");

        var none = Open(RunnerView.Flights) with
        {
            RunnerFlights = Flown(), RunnerFlightsFor = "runner-b",
        };

        await Assert.That(RunnerActivity.FlightsAbsence(none))
            .IsEqualTo("This runner has flown nothing.");

        var some = none with { RunnerFlights = Flown(AFlight(1)) };

        await Assert.That(RunnerActivity.FlightsAbsence(some)).IsEmpty();
    }

    [Test]
    public async Task Pointing_at_a_flight_moves_its_own_cursor()
    {
        var state = Open(RunnerView.Flights) with
        {
            RunnerFlights = Flown(AFlight(3), AFlight(2), AFlight(1)), RunnerFlightsFor = "runner-b",
        };

        var pointed = Reducer.Pointed(state, 2);

        await Assert.That(pointed.RunnerFlightSelected).IsEqualTo(2);
        await Assert.That(pointed.RunnerSessionSelected).IsEqualTo(0)
            .Because("each pane keeps its own cursor; their row counts differ.");
        await Assert.That(pointed.RunnerSelected).IsEqualTo(1);
    }

    // --- the screen, read from its source because it cannot be built without a terminal ---

    [Test]
    public async Task The_screen_draws_both_tables_at_their_own_cursors()
    {
        var screen = Sources.Read("Gg.Console", "Views", "ConsoleScreen.cs");

        foreach (var (table, cursor) in (ValueTuple<string, string>[])
                 [("_runnerFlights", "State.RunnerFlightSelected"),
                  ("_runnerSessions", "State.RunnerSessionSelected")])
        {
            var at = screen.IndexOf($"Fill({table},", StringComparison.Ordinal);

            await Assert.That(at).IsGreaterThan(-1);

            var call = screen[at..screen.IndexOf(");", at, StringComparison.Ordinal)];

            await Assert.That(call).Contains(cursor, StringComparison.Ordinal);

            await Assert.That(screen)
                .Contains($"{table}.ValueChanged += OnModalRowPointedAt", StringComparison.Ordinal);
            await Assert.That(screen)
                .Contains($"{table}.ValueChanged -= OnModalRowPointedAt", StringComparison.Ordinal);
        }
    }

    [Test]
    public async Task The_screen_asks_and_the_reader_answers()
    {
        var screen = Sources.Read("Gg.Console", "Views", "ConsoleScreen.cs");

        await Assert.That(screen).Contains("RunnerActivity.FlightsOwed(State)", StringComparison.Ordinal);
        await Assert.That(screen).Contains("Asked(Command.ShowRunnerFlights)", StringComparison.Ordinal);

        var program = Sources.Read("Gg.Cli", "Program.cs");

        await Assert.That(program).Contains("Gg.Console.Command.ShowRunnerFlights =>", StringComparison.Ordinal)
            .Because("a command in Reads with no arm in the reader throws when it is asked.");
    }

    [Test]
    public async Task Focus_lands_on_whichever_of_the_five_is_showing()
    {
        var screen = Sources.Read("Gg.Console", "Views", "ConsoleScreen.cs");

        var at = screen.IndexOf("case FocusTarget.RunnerView:", StringComparison.Ordinal);
        var arm = screen[at..screen.IndexOf("return;", at, StringComparison.Ordinal)];

        await Assert.That(arm).Contains("RunnerView.Flights when _runnerFlights.Visible", StringComparison.Ordinal);
        await Assert.That(arm).Contains("RunnerView.Sessions when _runnerSessions.Visible", StringComparison.Ordinal);
    }

    [Test]
    public async Task A_new_view_is_never_given_another_views_table()
    {
        // THE BINARY TERNARY THAT BUILT THE PANES gave every view that was not
        // environments the members table, so a fourth view would have drawn the
        // members under its own title.
        var screen = Sources.Read("Gg.Console", "Views", "ConsoleScreen.cs");

        await Assert.That(screen)
            .DoesNotContain("view == RunnerView.Environments ? _runnerEnvironments : _runnerMembers",
                StringComparison.Ordinal);
    }
}
