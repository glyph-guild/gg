using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// A flight is grounded from the modal that is about it, and from nowhere
/// else.
/// </summary>
/// <remarks>
/// <para>
/// <b>An ending is offered where the thing being ended is on the screen.</b>
/// Every other key in this console acts on "the row under the cursor", which
/// is fine for opening a flight and wrong for stopping one: a person who
/// scrolled while reading would ground a flight they were not looking at. The
/// modal names its flight at the top and shows what happened to it, which is
/// exactly the state somebody should be in before they end it.
/// </para>
/// <para>
/// <b>It is the shell's, twice over.</b> It writes, and it asks for a reason
/// first - the reason is required on the wire, and the editor is how this
/// console asks for a sentence. Both are things a UI session may not do.
/// </para>
/// <para>
/// <b>Nothing typed grounds nothing.</b> The wire refuses a blank reason, but
/// a person who opened the editor and closed it has not decided to stop
/// anything, and sending their empty buffer to find out would be the console
/// deciding for them.
/// </para>
/// </remarks>
public class GroundingIsOfferedOnlyWhereTheFlightIsTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 7, 9, 0, 0, TimeSpan.Zero);

    private static AppState Looking() => new()
    {
        Mode = UiMode.FlightDetail,
        ActiveTab = TabId.Flights,
        Flights = new FlightList
        {
            Flights =
            [
                new FlightSummary
                {
                    FlightId = "01a0792a-5e1f-7030-a5d8-52fd66e510b0",
                    FlightNumber = "GG-54",
                    Name = "implement",
                    Intent = new FlightIntent { Kind = FlightIntentKinds.Text, Text = "work" },
                    CreatedAt = T0,
                    RunnerProtocolVersion = 1,
                    FactVocabularyVersion = "0.25.0",
                    ConstitutionVersion = "1.0.0",
                    EnvelopeVersion = "v6",
                    Attempts = 0,
                    State = FlightStates.Open,
                    Facts = [],
                },
            ],
        },
    };

    [Test]
    public async Task The_flight_modal_offers_it()
    {
        var key = Keymap.Resolve(KeyStroke.Char('x'), new KeymapContext(UiMode.FlightDetail));

        await Assert.That(key).IsEqualTo(Command.GroundFlight)
            .Because("`x' stops the thing the modal is about, which is what it already means "
                   + "in the runner's modal - one letter, one idea, in the two places a "
                   + "modal is about something that can be stopped.");
    }

    [Test]
    public async Task And_nowhere_else_does()
    {
        var elsewhere = from mode in Enum.GetValues<UiMode>()
                        where mode != UiMode.FlightDetail
                        from showing in Enum.GetValues<TabId>()
                        from frozen in (bool[])[false, true]
                        from takeable in (bool[])[false, true]
                        from handedBack in (bool[])[false, true]
                        select new KeymapContext(mode, showing, frozen, takeable, handedBack);

        var offered = elsewhere
            .SelectMany(Keymap.Bindings)
            .Where(binding => binding.Command == Command.GroundFlight)
            .Select(binding => binding.Key.Name)
            .Distinct()
            .ToList();

        await Assert.That(offered).IsEmpty()
            .Because("every other key acts on the row under the cursor, and a person who "
                   + $"scrolled would end a flight they were not reading. Found: "
                   + string.Join(", ", offered));
    }

    [Test]
    public async Task It_is_the_shells_because_it_writes_and_asks_first()
    {
        await Assert.That(ShellCommands.Handled).Contains(Command.GroundFlight);

        await Assert.That(Reducer.Reduce(Looking(), Command.GroundFlight).Mode)
            .IsEqualTo(UiMode.FlightDetail)
            .Because("a reducer arm that ended the flight locally would end it whether or "
                   + "not the control plane did.");
    }

    [Test]
    public async Task The_loop_asks_for_a_reason_and_sends_the_flight_it_was_reading()
    {
        var asked = 0;
        var sent = new List<(string Flight, string Because)>();

        var ui = new ScriptedUi(
            state => new UiOutcome(Command.GroundFlight, state),
            state => new UiOutcome(Command.Quit, state));

        var final = new ConsoleLoop(
            ui,
            new Says("the fleet cannot serve this yet"),
            groundFlight: (state, ask) =>
            {
                asked++;
                sent.Add((PaneText.Detailed(state)!.FlightNumber, ask()));
                return state with { LastGrounded = "GG-54 was grounded." };
            })
            .Run(Looking());

        await Assert.That(asked).IsEqualTo(1);
        await Assert.That(sent).IsEquivalentTo(
            new[] { ("GG-54", "the fleet cannot serve this yet") })
            .Because("the flight the modal was about, and the reason a person typed.");

        await Assert.That(final.LastGrounded).IsNotNull();
        await Assert.That(ConsoleLoop.Said(new AppState(), final)).IsEqualTo(final.LastGrounded)
            .Because("each arm records its outcome in its own field and Said takes whichever "
                   + "changed, so a new arm cannot forget to say anything.");
    }

    [Test]
    public async Task A_console_that_cannot_ground_says_so_rather_than_blinking()
    {
        var ui = new ScriptedUi(
            state => new UiOutcome(Command.GroundFlight, state),
            state => new UiOutcome(Command.Quit, state));

        var final = new ConsoleLoop(ui, new Says("")).Run(Looking());

        await Assert.That(final.LastGrounded).IsNotNull()
            .Because("the port not being passed is what `y' looked like for two slices.");
    }

    private sealed class ScriptedUi(params Func<AppState, UiOutcome>[] script) : IUiSession
    {
        private readonly Queue<Func<AppState, UiOutcome>> _script = new(script);

        public UiOutcome Run(AppState state) => _script.Dequeue()(state);
    }

    private sealed class Says(string what) : IEditorSession
    {
        public string Edit(string initialText) => what;
    }
}
