using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// The boot reads what the opening screen shows. A log arrives when somebody
/// asks for it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Fifty-two flights, fifty-two logs, and forty-eight of them for flights
/// that ended.</b> The boot fetched one per flight so the queue could count
/// lease expiries and so the enter key would cost nothing. Both of the queue's
/// log-derived reasons are about a flight that is still running - a lease that
/// expired twice, a runner that went offline holding it - and a flight that has
/// landed needs nobody. So the logs the boot spent most of its time on could
/// not have changed a single row.
/// </para>
/// <para>
/// <b>And enter reads its own.</b> That is the trade and it is worth naming: a
/// flight's detail used to be free because the boot had already paid for every
/// one of them. Now the boot pays for the few that can matter and the one
/// somebody actually opens costs one request, on a keypress, with the terminal
/// released - which is the same shape as every tab in this console.
/// </para>
/// </remarks>
public class TheBootReadsWhatItShowsTests
{
    private const int Flights = 24;
    private const int InTheAir = 3;

    [Test]
    public async Task A_log_is_read_only_for_a_flight_still_in_the_air()
    {
        var (data, plane) = AConsolePlane.Console(Flights, InTheAir);

        await ConsoleStart.LoadAsync(data, "somebody");

        await Assert.That(plane.LogsRead.Count).IsEqualTo(InTheAir)
            .Because($"{Flights} flights and {InTheAir} of them still flying. The other "
                   + $"{Flights - InTheAir} have landed and their logs cannot put a row in the "
                   + $"queue. Read {plane.LogsRead.Count}.");
    }

    [Test]
    public async Task The_flights_still_in_the_air_do_get_theirs()
    {
        // THE ANCHOR, and it is the half that keeps the queue working. Reading
        // fewer is only right while the ones that can produce a row are still
        // among them.
        var (data, _) = AConsolePlane.Console(Flights, InTheAir);

        var loaded = await ConsoleStart.LoadAsync(data, "somebody");

        await Assert.That(loaded.Logs.Count).IsEqualTo(InTheAir);

        foreach (var open in loaded.Flights!.Flights.Where(f => f.State == FlightStates.Open))
        {
            await Assert.That(loaded.Logs.ContainsKey(open.FlightId)).IsTrue()
                .Because($"{open.FlightNumber} is still flying, so the queue needs its log.");
        }
    }

    [Test]
    public async Task Opening_a_flight_reads_the_log_the_boot_did_not()
    {
        // ENTER IS A READ NOW, so it is the loop's like every other read in this
        // console. A UI session may not make a request; the session ends, the
        // loop asks for one flight's log, and the next session opens the modal
        // over an answer.
        var (data, plane) = AConsolePlane.Console(Flights, InTheAir);

        var booted = await ConsoleStart.LoadAsync(data, "somebody");

        // The newest flight, which is the one the cursor starts on and which
        // landed - so the boot deliberately skipped its log.
        var newest = PaneText.Detailed(booted)!;
        await Assert.That(newest.State).IsEqualTo(FlightStates.Landed);
        await Assert.That(booted.Logs.ContainsKey(newest.FlightId)).IsFalse()
            .Because("the boot does not read a landed flight's log.");

        var before = plane.LogsRead.Count;

        var ui = new ScriptedUi(
            state => new UiOutcome(Command.ShowFlight, state),
            state => new UiOutcome(Command.Quit, state));

        var final = new ConsoleLoop(
            ui,
            new NoEditor(),
            flightLog: current => ConsoleFlightLog.Read(data, current))
            .Run(booted);

        await Assert.That(plane.LogsRead.Count).IsEqualTo(before + 1)
            .Because("one flight, one request, and only when somebody asked.");

        await Assert.That(final.Logs.ContainsKey(newest.FlightId)).IsTrue()
            .Because("and the answer is in the model the next session renders.");

        await Assert.That(ui.StatesSeen[1].Mode).IsEqualTo(UiMode.FlightDetail)
            .Because("the modal opens over the log rather than before it.");

        await Assert.That(PaneText.Modal(ui.StatesSeen[1])).Contains("read-on-demand")
            .Because("and what it shows is what was just read.");
    }

    private sealed class ScriptedUi(params Func<AppState, UiOutcome>[] script) : IUiSession
    {
        private readonly Queue<Func<AppState, UiOutcome>> _script = new(script);

        public List<AppState> StatesSeen { get; } = [];

        public UiOutcome Run(AppState state)
        {
            StatesSeen.Add(state);
            return _script.Dequeue()(state);
        }
    }

    private sealed class NoEditor : IEditorSession
    {
        public string Edit(string initialText) =>
            throw new InvalidOperationException("nothing here hands over the terminal.");
    }
}
