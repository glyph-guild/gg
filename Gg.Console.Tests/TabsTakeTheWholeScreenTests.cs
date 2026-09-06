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

        // BrowseToggled rather than Reduce, and the difference is real: showing
        // that pane is a READ, so the shell calls the reducer directly and
        // Reduce has no arm for the command at all. A test that went through
        // Reduce would have asserted nothing.
        var state = Reducer.BrowseToggled(new AppState());

        await Assert.That(Tabs.Showing(state, TabId.Queue)).IsFalse()
            .Because("a view that took the screen took the queue's half of it too.");
    }

    [Test]
    public async Task Tab_moves_to_the_next_tab_in_the_order_the_bar_shows_them()
    {
        // AMENDED WHEN EVERY TAB WENT ON THE BAR. It walked the OPEN tabs, so
        // this pressed tab four times and expected to come back round; it walks
        // all eight now, in the order the enum declares them, which is the
        // order of the bar. ReducerTests.TabWalksEveryTabAndComesBackRound is
        // the full circuit; this is the order.
        var state = Reducer.Reduce(new AppState(), Command.ToggleEvidence);

        await Assert.That(state.ActiveTab).IsEqualTo(TabId.Evidence);

        state = Reducer.Reduce(state, Command.FocusNextPane);
        await Assert.That(state.ActiveTab).IsEqualTo(TabId.Live)
            .Because("live is the tab after evidence on the bar.");

        state = Reducer.Reduce(state, Command.FocusNextPane);
        await Assert.That(state.ActiveTab).IsEqualTo(TabId.Browse);
    }

    [Test]
    public async Task A_tab_a_person_lands_on_says_what_it_is_waiting_for()
    {
        // AMENDED TWICE, and this is the third subject. It asserted tab stays
        // on the queue from a bare console; then that it walks the two
        // permanent tabs; and now every tab is on the bar, so landing on a view
        // nobody has fetched is a thing that HAPPENS - and what makes that all
        // right is the pane saying so rather than drawing blank.
        var state = new AppState();

        for (var press = 0; press < Tabs.All.Count; press++)
        {
            state = Reducer.Reduce(state, Command.FocusNextPane);

            if (Tabs.HasRead(state, state.ActiveTab))
            {
                continue;
            }

            await Assert.That(PaneText.ForTab(state, state.ActiveTab)).IsNotEmpty()
                .Because($"{state.ActiveTab} has read nothing yet, and a pane that draws "
                       + "blank is indistinguishable from a broken one.");
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

        // WAS ABOUT A STRING IN THE TITLE, which is what the bar used to be.
        // The component marks the one showing itself, so what the model owes it
        // is which tab that is - and Tabs.Showing is where that is asserted,
        // one test up. What is left here is the titles, which are the model's.
        await Assert.That(Tabs.Title(state, TabId.Live)).Contains("Live", StringComparison.Ordinal);
        await Assert.That(Tabs.Title(state, TabId.Envelope))
            .Contains("Envelope", StringComparison.Ordinal)
            .Because("every tab is on the bar now, including the views nobody has opened - "
                   + "the bar's job is to say what there is.");
        await Assert.That(Tabs.Showing(state, TabId.Live)).IsTrue();
    }

    [Test]
    public async Task A_tab_that_has_read_nothing_says_so_rather_than_hiding()
    {
        // WAS about which tabs the bar names, twice over: first "nothing when
        // only the queue is open", then "the two that are always there". Every
        // tab is on the bar now, so what is left to say is how a person can
        // tell the difference between a view with something in it and one that
        // will fetch when they go there.
        var bare = new AppState();

        await Assert.That(Tabs.HasRead(bare, TabId.Queue)).IsTrue();
        await Assert.That(Tabs.HasRead(bare, TabId.Repositories)).IsFalse();

        await Assert.That(Tabs.Title(bare, TabId.Repositories))
            .IsNotEqualTo(Tabs.Title(
                bare with { RepositoriesVisible = true }, TabId.Repositories))
            .Because("a tab holding nothing yet and a tab holding something read the same "
                   + "otherwise, and one of them costs a read to visit.");
    }
}
