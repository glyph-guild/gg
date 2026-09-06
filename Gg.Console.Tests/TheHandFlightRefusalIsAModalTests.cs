using System.Diagnostics;
using Gg.Contracts;
using Gg.Local;

namespace Gg.Console.Tests;

/// <summary>
/// A hand-flight that did not happen says so in a modal, not in a strip along
/// the bottom.
/// </summary>
/// <remarks>
/// <para>
/// <b>The activity line is one line and the refusal is three sentences.</b>
/// It reads "Nothing was created: this flight requires environment=dev. This
/// machine does not advertise 'environment=dev'. Bring that environment up
/// here, or fly it on the fleet, which ha" - the remedy, which is the half a
/// person can act on, runs off the edge of the screen. A modal is the shape
/// this console already uses for something that has to be read before anything
/// else happens.
/// </para>
/// <para>
/// <b>Only when it did not fly.</b> A flight that was flown by hand was watched
/// by the person who flew it, at a prompt the child owned; a modal telling them
/// what they just did is a keypress in the way. The receipt on the activity
/// line is enough for that one.
/// </para>
/// <para>
/// <b>And the model carries which, rather than the words being read back.</b>
/// Deciding whether it worked by looking for a phrase in the sentence would
/// make the wording load-bearing, and the wording is the part most likely to be
/// improved by somebody who does not know that.
/// </para>
/// </remarks>
public class TheHandFlightRefusalIsAModalTests
{
    private static readonly SelfInvocation Self = new("/usr/local/bin/gg", []);

    private static Checklist Needing(params string[] labels) => new()
    {
        EnvelopeVersion = "v6",
        RequiredLabels = labels,
        Items =
        [
            .. labels.Select(label => new ChecklistItem
            {
                Requirement = label,
                Verification = "a runner advertises it",
                Satisfier = ChecklistSatisfiers.MatchingRunner,
                Disposition = LabelDispositions.Stated,
            }),
        ],
    };

    private static AppState Pressed(Func<AppState, Func<string>, AppState> fly, AppState from)
    {
        var ui = new ScriptedUi(
            state => new UiOutcome(Command.FlyByHand, state),
            state => new UiOutcome(Command.Quit, state));

        new ConsoleLoop(ui, new NoEditor(), flyByHand: fly).Run(from);

        return ui.StatesSeen[1];
    }

    [Test]
    public async Task A_refusal_opens_a_modal_over_the_console()
    {
        var after = Pressed(
            (state, ask) => ConsoleHandFlight.Fly(
                state,
                plan: () => Needing("environment=dev"),
                advertised: [],
                ask: ask,
                self: Self,
                start: _ => 0),
            new AppState());

        await Assert.That(after.Mode).IsEqualTo(UiMode.HandFlight);

        var modal = PaneText.Modal(after);

        await Assert.That(modal).Contains("environment=dev")
            .Because("the label first, because it is the actionable half.");
        await Assert.That(modal).Contains("fly it on the fleet")
            .Because("the remedy is the half that runs off the edge of the activity line, "
                   + $"which is why this modal exists. Modal:\n{modal}");
    }

    [Test]
    public async Task A_flight_that_flew_opens_nothing()
    {
        var after = Pressed(
            (state, ask) => ConsoleHandFlight.Fly(
                state,
                plan: () => Needing(),
                advertised: [],
                ask: () => "make the report say who was idle",
                self: Self,
                start: _ => 0),
            new AppState());

        await Assert.That(after.Mode).IsEqualTo(UiMode.Normal)
            .Because("they watched it happen at a prompt the child owned; a modal telling them "
                   + "what they just did is a keypress in the way.");
        await Assert.That(after.LastAction).IsNotNull()
            .Because("the receipt on the activity line is enough for that one.");
    }

    [Test]
    public async Task A_console_that_cannot_fly_by_hand_at_all_says_so_in_the_modal_too()
    {
        // THE NULL PORT, which is what a person saw as a blink before the port
        // was wired and is the same class of answer: nothing happened, and the
        // reason is a sentence somebody has to read.
        var ui = new ScriptedUi(
            state => new UiOutcome(Command.FlyByHand, state),
            state => new UiOutcome(Command.Quit, state));

        new ConsoleLoop(ui, new NoEditor()).Run(new AppState());

        await Assert.That(ui.StatesSeen[1].Mode).IsEqualTo(UiMode.HandFlight);
    }

    [Test]
    public async Task Esc_is_the_way_out_and_the_only_one()
    {
        var context = new KeymapContext(UiMode.HandFlight);

        await Assert.That(Keymap.EscapeHatch(context)).IsEqualTo(KeyStroke.Esc);
        await Assert.That(Keymap.Resolve(KeyStroke.Char('y'), context)).IsNull()
            .Because("the modal owns the keyboard: pressing the key again from inside a "
                   + "refusal would be the second attempt nobody asked for.");
        await Assert.That(Reducer.Reduce(
            new AppState { Mode = UiMode.HandFlight }, Command.CloseModal).Mode)
            .IsEqualTo(UiMode.Normal);
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
        public string Edit(string initialText) => "";
    }
}
