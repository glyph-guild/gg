using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// `m' in the flight modal moves the flight on the screen to another model
/// root lists (slice seventy-six).
/// </summary>
/// <remarks>
/// <para>
/// <b>From the one place Ground is offered, for its reason.</b> Every other
/// key acts on the row under the cursor; a flight is moved from the modal
/// where it is named on the screen, so a person who scrolled while reading
/// cannot move one they were not looking at.
/// </para>
/// <para>
/// <b>The picker offers root's list and nothing else</b>, so the keystroke
/// cannot name what the door would refuse. The write is the shell's: the
/// session ends, the loop sends, and the answer is in the model the next
/// session draws.
/// </para>
/// </remarks>
public class AFlightIsMovedFromTheConsoleTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);

    private static Envelope Root(IReadOnlyList<string>? models) => new()
    {
        Context = new ContextBinding { Scope = "**", Constitution = "1.0.0" },
        Obligations = [],
        Loops = [],
        Destinations = [],
        Models = models,
    };

    private static AppState Looking(IReadOnlyList<string>? models = null) => new()
    {
        Mode = UiMode.FlightDetail,
        ActiveTab = TabId.Flights,
        Envelope = models is null
            ? null
            : new EnvelopeState
            {
                Version = "v21",
                Envelope = Root(models),
                UpdatedAt = T0,
                UpdatedBy = "kdee",
            },
        Flights = new FlightList
        {
            Flights =
            [
                new FlightSummary
                {
                    FlightId = "01a0792a-5e1f-7030-a5d8-52fd66e510b0",
                    FlightNumber = "GG-54",
                    Name = "investigate",
                    Intent = new FlightIntent { Kind = FlightIntentKinds.Text, Text = "work" },
                    CreatedAt = T0,
                    RunnerProtocolVersion = 1,
                    FactVocabularyVersion = "0.44.0",
                    ConstitutionVersion = "1.0.0",
                    EnvelopeVersion = "v21",
                    Attempts = 1,
                    State = FlightStates.Open,
                    Facts = [],
                },
            ],
        },
    };

    private static readonly string[] Two = ["claude-opus-5-5", "claude-sonnet-5-5"];

    [Test]
    public async Task The_flight_modal_is_where_it_starts_and_nowhere_else()
    {
        await Assert.That(Keymap.Resolve(KeyStroke.Char('m'), new KeymapContext(UiMode.FlightDetail)))
            .IsEqualTo(Command.ChooseModel)
            .Because("`m' is free in the flight modal, and it is the letter the verb starts with.");

        var elsewhere = (from mode in Enum.GetValues<UiMode>()
                         from showing in Enum.GetValues<TabId>()
                         from frozen in (bool[])[false, true]
                         from takeable in (bool[])[false, true]
                         from handedBack in (bool[])[false, true]
                         select new KeymapContext(mode, showing, frozen, takeable, handedBack))
            .Where(context => context.Mode != UiMode.FlightDetail)
            .SelectMany(Keymap.Bindings)
            .Where(binding => binding.Command == Command.ChooseModel)
            .Select(binding => $"{binding.Key.Name} in {binding.Command}")
            .Distinct()
            .ToList();
        await Assert.That(elsewhere).IsEmpty()
            .Because("every other key acts on the row under the cursor, which is wrong for "
                   + "moving a flight somebody may not be looking at.");
    }

    [Test]
    public async Task Asking_opens_the_picker_on_roots_list_with_the_cursor_at_the_top()
    {
        var asked = Reducer.Reduce(Looking(Two), Command.ChooseModel);
        await Assert.That(asked.Mode).IsEqualTo(UiMode.ModelChoice);
        await Assert.That(asked.ModelSelected).IsEqualTo(0);
        await Assert.That(ModelChoices.Rows(asked)).IsEquivalentTo(Two)
            .Because("root's models: is the list, and nothing else is offered.");
        await Assert.That(ModelChoices.Chosen(asked)).IsEqualTo("claude-opus-5-5");

        var down = Reducer.Reduce(asked, Command.SelectNext);
        await Assert.That(ModelChoices.Chosen(down)).IsEqualTo("claude-sonnet-5-5");
        await Assert.That(ModelChoices.Chosen(Reducer.Reduce(down, Command.SelectNext)))
            .IsEqualTo("claude-sonnet-5-5")
            .Because("the cursor clamps at the end rather than wrapping or running off.");
        await Assert.That(ModelChoices.Chosen(Reducer.Reduce(down, Command.SelectPrevious)))
            .IsEqualTo("claude-opus-5-5");

        await Assert.That(ModelChoices.Chosen(Looking(Two))).IsNull()
            .Because("a cursor is not an answer: outside the picker nothing is chosen.");
    }

    [Test]
    public async Task The_body_names_the_flight_and_lists_the_models_with_the_cursor()
    {
        var asked = Reducer.Reduce(Looking(Two), Command.ChooseModel);
        var body = PaneText.Modal(asked);
        await Assert.That(body).Contains("GG-54");
        await Assert.That(body).Contains("claude-opus-5-5").And.Contains("claude-sonnet-5-5");
        await Assert.That(body.IndexOf("> claude-opus-5-5", StringComparison.Ordinal)).IsGreaterThanOrEqualTo(0)
            .Because("the cursor is drawn in the text, so the choice is visible before enter. Body:\n" + body);
        await Assert.That(PaneText.ModalTitle(UiMode.ModelChoice)).Contains("model");

        var unlisted = PaneText.Modal(Reducer.Reduce(Looking([]), Command.ChooseModel) with
        {
            Envelope = null,
        });
        await Assert.That(unlisted).Contains("models:")
            .Because("a root that lists nothing has not enabled this, and the body says so "
                   + "rather than offering an empty list. Body:\n" + unlisted);
    }

    [Test]
    public async Task Enter_is_the_shells_and_escape_is_the_way_out()
    {
        var picker = new KeymapContext(UiMode.ModelChoice);
        await Assert.That(Keymap.Resolve(KeyStroke.EnterKey, picker)).IsEqualTo(Command.MoveFlight);
        await Assert.That(Keymap.Resolve(KeyStroke.Esc, picker)).IsEqualTo(Command.CloseModal);
        await Assert.That(ShellCommands.Handled).Contains(Command.MoveFlight)
            .Because("a write happens between sessions, like grounding and sending a credential.");
        await Assert.That(Keymap.Resolve(KeyStroke.Char('j'), picker)).IsEqualTo(Command.SelectNext);
        await Assert.That(Keymap.Resolve(KeyStroke.Char('k'), picker)).IsEqualTo(Command.SelectPrevious);
    }

    [Test]
    public async Task The_loop_sends_the_flight_it_was_reading_to_the_model_under_the_cursor()
    {
        var sent = new List<(string Flight, string? Model)>();
        var picked = Reducer.Reduce(Reducer.Reduce(Looking(Two), Command.ChooseModel), Command.SelectNext);
        var ui = new ScriptedUi(
            state => new UiOutcome(Command.MoveFlight, state),
            state => new UiOutcome(Command.Quit, state));
        var final = new ConsoleLoop(
            ui,
            new Says(""),
            moveFlight: state =>
            {
                sent.Add((PaneText.Detailed(state)!.FlightNumber, ModelChoices.Chosen(state)));
                return state with { Mode = UiMode.Normal, LastMoved = "GG-54 was moved to claude-sonnet-5-5." };
            })
            .Run(picked);

        await Assert.That(sent).IsEquivalentTo(new[] { ("GG-54", (string?)"claude-sonnet-5-5") })
            .Because("the flight the modal was about and the model the cursor was on, read off "
                   + "the state before the mode closes.");
        await Assert.That(final.Mode).IsEqualTo(UiMode.Normal);
        await Assert.That(ConsoleLoop.Said(new AppState(), final)).IsEqualTo(final.LastMoved)
            .Because("each arm records its outcome in its own field and Said takes whichever changed.");
    }

    [Test]
    public async Task A_console_that_cannot_move_a_flight_says_so_rather_than_blinking()
    {
        var picked = Reducer.Reduce(Looking(Two), Command.ChooseModel);
        var ui = new ScriptedUi(
            state => new UiOutcome(Command.MoveFlight, state),
            state => new UiOutcome(Command.Quit, state));
        var final = new ConsoleLoop(ui, new Says("")).Run(picked);
        await Assert.That(final.LastMoved).IsNotNull();
        await Assert.That(final.Mode).IsEqualTo(UiMode.Normal);
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
