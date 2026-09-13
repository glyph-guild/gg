namespace Gg.Console.Views;

/// <summary>What should hold the keyboard focus after a render.</summary>
public enum FocusTarget
{
    /// <summary>Whoever has it now. A person put it there.</summary>
    LeaveAlone,

    /// <summary>The modal, which owns the keyboard while it is open.</summary>
    Modal,

    /// <summary>
    /// The log inside the runner modal, which is the part of it somebody
    /// scrolls.
    /// </summary>
    /// <remarks>
    /// The flight log's reason, one modal over: a modal made of widgets has to
    /// say WHICH widget, and what a person opened this to watch is a runner
    /// coming up - which is the log, live, while it does.
    /// </remarks>
    /// <summary>
    /// Whichever of the runner modal's three views is showing.
    /// </summary>
    /// <remarks>
    /// <b>Was <c>RunnerLog</c>, and the rename is the defect.</b> The modal has
    /// three views now, and a landing that always named the log made the other
    /// two unreachable: Terminal.Gui's <c>Tabs</c> follows FOCUS, so a focused
    /// log pane assigns <c>Value</c> back to itself every render and the bar
    /// snapped away from whatever a person had just turned it to.
    /// </remarks>
    RunnerView,

    /// <summary>
    /// The log inside the flight modal, which is the part of it with a cursor.
    /// </summary>
    /// <remarks>
    /// A modal made of widgets has to say WHICH widget, and the frame is not an
    /// answer - Terminal.Gui would pick the first focusable child, which is the
    /// intent, and an arrow key there scrolls a document instead of walking the
    /// history a person opened the modal to walk.
    /// </remarks>
    FlightLog,

    /// <summary>
    /// The airspace path field, which is not a modal and still owns the
    /// keyboard.
    /// </summary>
    /// <remarks>
    /// A field that accepts keystrokes has to hold the focus for as long as
    /// somebody is typing into it, and it sits inside a tab rather than in a
    /// dialog - so it is neither <see cref="Modal"/> nor <see cref="Tab"/>.
    /// </remarks>
    AirspacePath,

    /// <summary>
    /// The document beside the airspace tree, which is the half a person
    /// scrolls.
    /// </summary>
    /// <remarks>
    /// The two logs' reason, on a tab rather than in a modal: a pane made of
    /// two halves has to say WHICH half, and the tab's own landing names the
    /// tree. Without this the document was reachable by clicking and had
    /// nothing to hand the keyboard back.
    /// </remarks>
    AirspaceDocument,

    /// <summary>
    /// Whichever of the help modal's pages is showing.
    /// </summary>
    /// <remarks>
    /// <b>The runner view's reason, one modal over.</b> A modal made of pages
    /// has to say WHICH page: a Dialog stops at itself and hands the keyboard
    /// to its first focusable child, which since this modal grew a tab bar is
    /// the bar. And the page can turn while the modal keeps focus, so "the
    /// modal has it" is not an answer either - each page is a different widget
    /// with its own cursor, and the arrows belong to the one in front.
    /// </remarks>
    HelpPage,

    /// <summary>The tab on screen, wherever that tab says focus lands.</summary>
    Tab,
}

/// <summary>
/// Whether a render should move the focus, and where.
/// </summary>
/// <remarks>
/// <para>
/// <b>Pure, because the screen cannot be constructed and this has been wrong
/// twice.</b> First it moved focus on every render, which a once-a-second
/// countdown turned into a button that could be reached and not held. Then it
/// left focus alone whenever the tab's pane already had it - which is true the
/// moment a pane is shown, so arriving on a tab landed nowhere and Terminal.Gui
/// picked the first focusable child instead of the one the tab means.
/// </para>
/// <para>
/// <b>The question is whether the tab is new, not who holds the focus.</b> The
/// two readings agree on the render case and disagree on the arrival, which is
/// why the second version passed the first version's tests.
/// </para>
/// </remarks>
public static class FocusChange
{
    /// <param name="landed">
    /// The tab focus was last placed on, or null if it has not been placed -
    /// which is also how a modal holding it is recorded, so that closing one
    /// counts as a change again.
    /// </param>
    /// <param name="modalHasFocus">
    /// Whether the modal already holds it. A modal owns the keyboard, and
    /// re-asserting that every second would move a cursor inside it exactly as
    /// it moved one behind it.
    /// </param>
    /// <param name="pathHasFocus">
    /// Whether the airspace field already holds it. The same argument as its
    /// neighbour and a sharper case: re-asserting focus on a field somebody is
    /// typing into puts the cursor back at the start of the line, once a
    /// second, while they type.
    /// </param>
    /// <param name="readingTheDocument">
    /// Which half of the airspace tab the model says has the keyboard.
    /// </param>
    /// <param name="landedReading">
    /// Which half it had when focus was last placed. <b>The pair is what makes
    /// this a CHANGE rather than a standing instruction</b> - the same job
    /// <paramref name="landed"/> does for the tab. Comparing the model against
    /// the widget instead would re-assert focus once a second and drag it back
    /// out of whichever half a person had just clicked into.
    /// </param>
    /// <param name="helpPage">Which help page the model says is showing.</param>
    /// <param name="landedHelpPage">
    /// Which one it was showing when focus was last placed. <b>The pair, exactly
    /// as <paramref name="landedReading"/> is the pair for the airspace tab's
    /// two halves</b> - comparing the model against the widget instead would
    /// re-place focus once a second and scroll a reader back to the top.
    /// </param>
    public static FocusTarget Wanted(
        UiMode mode,
        TabId showing,
        TabId? landed,
        bool modalHasFocus,
        bool pathHasFocus = false,
        bool readingTheDocument = false,
        bool landedReading = false,
        RunnerView runnerView = RunnerView.Log,
        RunnerView landedRunnerView = RunnerView.Log,
        HelpPage helpPage = HelpPage.Keys,
        HelpPage landedHelpPage = HelpPage.Keys) => (mode, landed) switch
    {
        // THE FIELD FIRST, because it is not a modal and the arms below would
        // hand it to one that is not on screen.
        (UiMode.AirspacePath, _) when pathHasFocus => FocusTarget.LeaveAlone,
        (UiMode.AirspacePath, _) => FocusTarget.AirspacePath,

        // THE RUNNER MODAL IS MADE OF THREE AND THE OTHERS ARE NOT, so it
        // answers before the guard below. That guard says "the modal has focus,
        // so nothing needs moving", which is true of a modal with one place for
        // the keyboard to be and false of this one: the view can turn while the
        // modal keeps focus, and the keyboard has to follow because it is what
        // decides which tab the bar shows.
        //
        // THE PAIR, exactly as landedReading is the pair for the airspace tab's
        // two halves - see that parameter's remark. Comparing the model against
        // the widget instead would re-place focus once a second.
        (UiMode.Runner, _) when modalHasFocus && landedRunnerView == runnerView
            => FocusTarget.LeaveAlone,
        (UiMode.Runner, _) => FocusTarget.RunnerView,

        // THE HELP MODAL IS MADE OF PAGES, for the reason the runner modal is
        // made of views, and it answers before the same guard. While its two
        // text pages were Labels there was nothing on them to move and this was
        // invisible; now that they scroll, a page reached with the keyboard
        // still on the page behind it cannot be scrolled at all.
        (UiMode.Help, _) when modalHasFocus && landedHelpPage == helpPage
            => FocusTarget.LeaveAlone,
        (UiMode.Help, _) => FocusTarget.HelpPage,

        (not UiMode.Normal, _) when modalHasFocus => FocusTarget.LeaveAlone,

        // WHICH WIDGET, for the two modals that are made of several. The rest
        // are a few lines and two keys, and the frame is the whole of them.
        (UiMode.FlightDetail, _) => FocusTarget.FlightLog,
        (not UiMode.Normal, _) => FocusTarget.Modal,

        // NOTHING MOVED, so nothing is moved. The tab is the one focus was
        // placed on AND the same half of it still wants the keyboard.
        (_, { } already) when already == showing && landedReading == readingTheDocument
            => FocusTarget.LeaveAlone,

        // THE HALF TURNED. Crossing to the document is its own target; crossing
        // back is the tab's own landing, which already names the tree.
        _ when readingTheDocument && showing == TabId.Envelope
            => FocusTarget.AirspaceDocument,

        _ => FocusTarget.Tab,
    };
}
