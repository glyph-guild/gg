namespace Gg.Console.Tests;

/// <summary>
/// A view that needs the screen takes all of it, and what is open reads as
/// tabs on the title line.
/// </summary>
/// <remarks>
/// <para>
/// <b>SIX PANES OVER ONE REGION, and the model had to keep them apart by
/// turning each other off.</b> Evidence, live, browse, repositories, the
/// checklist and the envelope all drew into the same half of the right-hand
/// side, so every toggle closed the others - <c>BrowseToggled</c> cleared four
/// flags - and the keymap carried five booleans to work out which of them had
/// the region. A person who opened the envelope to check a rule lost the
/// checklist they were comparing it against.
/// </para>
/// <para>
/// <b>So a view takes the whole screen and the open ones are tabs.</b> Only the
/// active tab draws, which is what makes it safe for six of them to be open at
/// once; the queue is one of the tabs rather than a strip beside them. What was
/// mutual exclusion in the model becomes one field - which tab is showing - and
/// the flags go back to meaning what they say: this view is open.
/// </para>
/// </remarks>
public class TabsTakeTheWholeScreenTests
{
    [Test]
    public async Task Opening_a_view_makes_it_the_tab_that_is_showing()
    {
        var state = Reducer.Reduce(new AppState(), Command.ToggleEvidence);

        await Assert.That(state.ActiveTab).IsEqualTo(TabId.Evidence)
            .Because("a key that opens a view and leaves the queue on screen is a key that "
                   + "did nothing a person can see.");
        await Assert.That(state.EvidenceVisible).IsTrue();
    }

    [Test]
    public async Task A_second_view_does_not_close_the_first()
    {
        // THE WHOLE POINT. Under one shared region this was impossible, and the
        // reducer enforced it by clearing the other flags.
        var state = Reducer.Reduce(new AppState(), Command.ToggleEvidence);
        state = Reducer.Reduce(state, Command.ToggleLive);

        await Assert.That(state.EvidenceVisible).IsTrue()
            .Because("opening the live view is not a reason to throw away the evidence "
                   + "somebody was reading beside it.");
        await Assert.That(state.LiveVisible).IsTrue();
        await Assert.That(state.ActiveTab).IsEqualTo(TabId.Live)
            .Because("the one just opened is the one showing.");
    }

    [Test]
    public async Task Only_the_tab_that_is_showing_is_drawn()
    {
        // "Takes over all the panes", as the invariant the view is built from
        // rather than as a sentence in a comment. Six panes drawn over one
        // region is what this replaces.
        var state = Reducer.Reduce(new AppState(), Command.ToggleEvidence);
        state = Reducer.Reduce(state, Command.ToggleLive);

        var drawn = Enum.GetValues<TabId>().Where(tab => Tabs.Showing(state, tab)).ToList();

        await Assert.That(drawn).IsEquivalentTo((TabId[])[TabId.Live])
            .Because("exactly one tab is on the screen. Found: " + string.Join(", ", drawn));
    }

    [Test]
    public async Task The_queue_keeps_the_screen_only_while_it_is_the_tab_showing()
    {
        await Assert.That(Tabs.Showing(new AppState(), TabId.Queue)).IsTrue()
            .Because("the queue is where a console opens, and it is a tab like the others.");

        var state = Reducer.Reduce(new AppState(), Command.ToggleBrowse);

        await Assert.That(Tabs.Showing(state, TabId.Queue)).IsFalse()
            .Because("a view that took the screen took the queue's half of it too.");
    }

    [Test]
    public async Task Tab_moves_to_the_next_open_tab_and_comes_back_round()
    {
        var state = Reducer.Reduce(new AppState(), Command.ToggleEvidence);
        state = Reducer.Reduce(state, Command.ToggleLive);

        // Live is showing; the open set is queue, evidence, live.
        state = Reducer.Reduce(state, Command.FocusNextPane);
        await Assert.That(state.ActiveTab).IsEqualTo(TabId.Queue);

        state = Reducer.Reduce(state, Command.FocusNextPane);
        await Assert.That(state.ActiveTab).IsEqualTo(TabId.Evidence);

        state = Reducer.Reduce(state, Command.FocusNextPane);
        await Assert.That(state.ActiveTab).IsEqualTo(TabId.Live)
            .Because("three open tabs and tab pressed three times is where it started.");
    }

    [Test]
    public async Task Tab_never_lands_on_a_view_nobody_opened()
    {
        var state = new AppState();

        for (var press = 0; press < 4; press++)
        {
            state = Reducer.Reduce(state, Command.FocusNextPane);

            await Assert.That(state.ActiveTab).IsEqualTo(TabId.Queue)
                .Because("nothing else is open, so there is nowhere else to go - and a tab "
                       + "showing a view that was never opened would render an empty pane.");
        }
    }

    [Test]
    public async Task A_views_own_key_closes_it_and_the_queue_comes_back()
    {
        var state = Reducer.Reduce(new AppState(), Command.ToggleEvidence);
        state = Reducer.Reduce(state, Command.ToggleEvidence);

        await Assert.That(state.EvidenceVisible).IsFalse();
        await Assert.That(state.ActiveTab).IsEqualTo(TabId.Queue)
            .Because("closing the tab a person is looking at has to leave them somewhere, and "
                   + "the queue is the one view that is always open.");
    }

    [Test]
    public async Task A_views_own_key_switches_to_it_rather_than_closing_it_from_elsewhere()
    {
        // The key means "show me this", and only means "close it" when it is
        // already what you are looking at. Pressing `v` while reading the live
        // view should not silently discard the evidence tab.
        var state = Reducer.Reduce(new AppState(), Command.ToggleEvidence);
        state = Reducer.Reduce(state, Command.ToggleLive);
        state = Reducer.Reduce(state, Command.ToggleEvidence);

        await Assert.That(state.ActiveTab).IsEqualTo(TabId.Evidence);
        await Assert.That(state.EvidenceVisible).IsTrue()
            .Because("it was open and somebody asked for it, so it is showing rather than "
                   + "gone.");
        await Assert.That(state.LiveVisible).IsTrue()
            .Because("and the one they were on is still open behind it.");
    }

    [Test]
    public async Task The_bar_marks_the_one_showing_and_names_the_rest()
    {
        var state = Reducer.Reduce(new AppState(), Command.ToggleEvidence);
        state = Reducer.Reduce(state, Command.ToggleLive);

        var bar = Tabs.Bar(state);

        await Assert.That(bar).Contains("[ Live ]", StringComparison.Ordinal)
            .Because("the one showing is marked, the way the help page's own pages are.");
        await Assert.That(bar).Contains("Queue", StringComparison.Ordinal);
        await Assert.That(bar).Contains("Evidence", StringComparison.Ordinal);
        await Assert.That(bar).DoesNotContain("Envelope", StringComparison.Ordinal)
            .Because("a tab for a view nobody opened is a tab that shows an empty pane.");
    }

    [Test]
    public async Task The_bar_says_nothing_about_tabs_when_only_the_queue_is_open()
    {
        // A bar with one tab on it is decoration: there is nowhere to switch to
        // and the title line is the most expensive line on the screen.
        await Assert.That(Tabs.Bar(new AppState())).IsEmpty();
    }
}
