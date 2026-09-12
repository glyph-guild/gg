using Gg.Console;
using Gg.Console.Views;

namespace Gg.Console.Tests;

/// <summary>
/// A key moves the keyboard between the airspace tree and the document beside
/// it, in both directions.
/// </summary>
/// <remarks>
/// <para>
/// <b>REPORTED FROM USE: the document is a one-way door.</b> Clicking into the
/// pane on the right gives it the keyboard - which is what makes a long
/// composed envelope scrollable - and nothing brings the keyboard back to the
/// tree. The arrow keys then move a cursor in the document while the row the
/// document is ABOUT stays where it was, so the tab looks frozen.
/// </para>
/// <para>
/// <b>A key rather than the mouse, because the mouse was never the way in
/// either.</b> The tab is driven from the keyboard - <c>j</c> and <c>k</c>
/// walk the tree, <c>v</c> turns the views - and a half of it reachable only
/// by clicking is a half most people will never reach.
/// </para>
/// <para>
/// <b>Which half has the keyboard is MODEL state, not a view's private
/// business.</b> Focus in this console is decided by <c>FocusChange</c> from
/// <c>AppState</c>, for the reason that file records: the screen cannot be
/// constructed, so anything it decides alone is a decision nothing can check.
/// </para>
/// <para>
/// <b>And it is applied once, not every frame.</b> Re-asserting focus on a
/// timer is what this console already learned not to do - it made a button
/// reachable and not holdable, and it puts a cursor back to the start of a
/// line somebody is typing on. So the landing is remembered WITH which half it
/// landed on, and a render that changes neither leaves the keyboard alone.
/// </para>
/// </remarks>
public class FocusCrossesBetweenTheTreeAndTheDocumentTests
{
    [Test]
    public async Task The_key_turns_the_keyboard_round()
    {
        var onTheTree = new AppState { ActiveTab = TabId.Envelope };

        var onTheDocument = Reducer.Reduce(onTheTree, Command.NextAirspacePane);

        await Assert.That(onTheDocument.AirspaceReading).IsTrue()
            .Because("the first press hands the keyboard to the document, which is what "
                   + "makes it scrollable.");

        await Assert.That(
                Reducer.Reduce(onTheDocument, Command.NextAirspacePane).AirspaceReading)
            .IsFalse()
            .Because("and the second brings it back to the tree, which is the half that "
                   + "was one-way.");
    }

    [Test]
    public async Task The_keyboard_goes_where_the_model_says()
    {
        // ARRIVING ON THE TAB lands on the tree, as it always has.
        await Assert.That(FocusChange.Wanted(
                UiMode.Normal, TabId.Envelope, landed: null, modalHasFocus: false))
            .IsEqualTo(FocusTarget.Tab);

        // THE FLAG TURNS, so the keyboard crosses.
        await Assert.That(FocusChange.Wanted(
                UiMode.Normal, TabId.Envelope, landed: TabId.Envelope, modalHasFocus: false,
                readingTheDocument: true, landedReading: false))
            .IsEqualTo(FocusTarget.AirspaceDocument);

        // AND BACK.
        await Assert.That(FocusChange.Wanted(
                UiMode.Normal, TabId.Envelope, landed: TabId.Envelope, modalHasFocus: false,
                readingTheDocument: false, landedReading: true))
            .IsEqualTo(FocusTarget.Tab);
    }

    [Test]
    public async Task A_render_that_changes_nothing_leaves_the_keyboard_alone()
    {
        // THE COUNTDOWN'S LESSON. Render runs once a second; a focus decision
        // that re-asserted itself would fight a person's own click and undo
        // whatever they had scrolled to.
        await Assert.That(FocusChange.Wanted(
                UiMode.Normal, TabId.Envelope, landed: TabId.Envelope, modalHasFocus: false,
                readingTheDocument: true, landedReading: true))
            .IsEqualTo(FocusTarget.LeaveAlone);

        await Assert.That(FocusChange.Wanted(
                UiMode.Normal, TabId.Envelope, landed: TabId.Envelope, modalHasFocus: false,
                readingTheDocument: false, landedReading: false))
            .IsEqualTo(FocusTarget.LeaveAlone);
    }

    [Test]
    public async Task The_key_is_offered_only_where_there_is_a_document_to_read()
    {
        // ARTICLE XI. A folder row has no document, so the pane beside it is a
        // sentence rather than something to scroll, and a key that moved the
        // keyboard onto it would move it nowhere a person can see.
        var overADocument = new KeymapContext
        {
            Showing = TabId.Envelope,
            OverADocument = true,
        };

        var overAFolder = overADocument with { OverADocument = false };

        await Assert.That(Keymap.Resolve(UiMode.Normal, KeyStroke.Char('w'), overADocument))
            .IsEqualTo(Command.NextAirspacePane);

        await Assert.That(Keymap.Resolve(UiMode.Normal, KeyStroke.Char('w'), overAFolder))
            .IsNotEqualTo(Command.NextAirspacePane);
    }

    [Test]
    public async Task The_hint_says_which_way_it_goes()
    {
        // THE SAME CONTEXT DISPATCH ANSWERS BOTH, so the line cannot advertise
        // a direction the key does not take.
        var onTheTree = new KeymapContext
        {
            Showing = TabId.Envelope,
            OverADocument = true,
        };

        await Assert.That(Keymap.Hints(onTheTree))
            .Contains("document", StringComparison.Ordinal)
            .Because("from the tree, the key goes to the document.");

        await Assert.That(Keymap.Hints(onTheTree with { ReadingTheDocument = true }))
            .Contains("tree", StringComparison.Ordinal)
            .Because("and from the document it comes back - which is the half nobody could "
                   + "find.");
    }
}
