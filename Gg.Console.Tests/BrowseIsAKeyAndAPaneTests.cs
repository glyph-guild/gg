using Gg.Local;
using Gg.Console;

namespace Gg.Console.Tests;

/// <summary>
/// The key that shows the work, and where the reading happens.
/// </summary>
/// <remarks>
/// <para>
/// <b>S29.4-04, and the shape is decided rather than chosen.</b> Showing this
/// pane means ASKING a reader, and asking a reader means starting a child
/// process. <c>CLAUDE.md</c> is unambiguous: a UI session may read a local file
/// and nothing else — <i>"it may not make a network call, resolve a credential,
/// or spawn a process"</i>. So this cannot be an in-session toggle like
/// <c>ToggleEvidence</c>. It has to end the session and let the loop do the
/// reading, which is exactly what <c>ShellCommands.Handled</c> is for.
/// </para>
/// <para>
/// <b>That is also the whole answer to paging, and it is not a good one.</b>
/// The terminal-release shape gets a first page honestly. A SECOND page through
/// the same door would tear the session down per keystroke, which is
/// <c>S29.4-03</c> and is not satisfied here. Slice twenty-eight has confirmed
/// it is not building a data port, so mid-session I/O is a decision above both
/// slices; leaving the criterion open is more honest than a key that redraws by
/// rebuilding the world.
/// </para>
/// </remarks>
public class BrowseIsAKeyAndAPaneTests
{
    private static KeymapContext Normal(bool browsing = false) =>
        new(UiMode.Normal, browsing ? TabId.Browse : TabId.Queue);

    [Test]
    public async Task The_key_is_bound_and_advertised_in_the_same_breath()
    {
        // Bindings is the single source: Resolve looks up in it and Hints
        // renders it, so a key that works and is not advertised cannot happen.
        await Assert.That(Keymap.Resolve(KeyStroke.Char('b'), Normal()))
            .IsEqualTo(Command.ToggleBrowse);
        await Assert.That(Keymap.Hints(Normal())).Contains("b ");
    }

    [Test]
    public async Task The_hint_says_which_way_the_key_goes()
    {
        // ToggleLive's shape. A key labelled "browse" while you are looking at
        // the browser reads as a key that will do nothing.
        //
        // READ OFF THE BINDING RATHER THAN THE HINT LINE, because the line no
        // longer carries this key: browse is a tab, its key is on the tab, and
        // the line keeps only the keys with nowhere else to be advertised. The
        // description is still what the help page renders, so what this test is
        // about - the word changing with what the key will do - is unmoved.
        //
        // "close" rather than "hide" since a view took the whole screen: from
        // any other tab the key BRINGS THIS ONE FORWARD rather than hiding it,
        // and the word only changes where the behaviour does.
        string Description(KeymapContext context) => Keymap.Bindings(context)
            .Single(b => b.Command == Command.ToggleBrowse).Description;

        await Assert.That(Description(Normal(browsing: false))).IsEqualTo("browse");
        await Assert.That(Description(Normal(browsing: true))).IsEqualTo("close browse");
    }

    [Test]
    public async Task It_is_the_shells_because_a_session_may_not_start_a_process()
    {
        // THE RULE, not a preference. A UI session may read a local file and
        // nothing else; a reader is a child process holding a credential's
        // name. So the session ends and the loop asks.
        await Assert.That(ShellCommands.Handled).Contains(Command.ToggleBrowse)
            .Because("a toggle handled inside the session would have to spawn the reader "
                   + "from inside the session, which is the one thing a session may not do.");
    }

    [Test]
    public async Task A_browse_key_in_a_modal_does_nothing()
    {
        // TENANT-LEVEL AND VIEW-LEVEL KEYS ALIKE STAY OUT OF MODALS. A modal
        // holds the keyboard for one question, and a key that tore the session
        // down mid-decision would lose the answer being given.
        foreach (var mode in (UiMode[])[UiMode.Help, UiMode.GateDecision, UiMode.FlightActions])
        {
            await Assert.That(Keymap.Resolve(KeyStroke.Char('b'), new KeymapContext(mode)))
                .IsNotEqualTo(Command.ToggleBrowse);
        }
    }

    [Test]
    public async Task The_toggle_flips_the_pane_and_nothing_else()
    {
        var shown = Reducer.BrowseToggled(new AppState { SelectedRow = 2 });

        await Assert.That(shown.BrowseVisible).IsTrue();
        await Assert.That(shown.SelectedRow).IsEqualTo(2)
            .Because("opening a browser must not lose the row a person was looking at.");

        var hidden = Reducer.BrowseToggled(shown);

        await Assert.That(hidden.BrowseVisible).IsFalse();
    }

    [Test]
    public async Task Hiding_the_browser_keeps_what_it_found()
    {
        // A person who hides the pane and opens it again should not pay for a
        // second read of the tracker to see what they just saw.
        var listed = Reducer.Browsed(
            new AppState { BrowseVisible = true, ActiveTab = TabId.Browse },
            "a-tracker",
            new BrowseOutcome.Listed(new WorkItemPage(
                [new WorkItemSummary("18398", "A draft job fails", "New", "", null)], null)));

        var hidden = Reducer.BrowseToggled(listed);

        await Assert.That(hidden.BrowseVisible).IsFalse();
        await Assert.That(hidden.Browse).IsNotNull();
        await Assert.That(hidden.Browse!.Items).Count().IsEqualTo(1);
    }

    [Test]
    public async Task The_browse_pane_and_the_other_two_do_not_all_draw_at_once()
    {
        // WAS "do not all SHOW at once", and it asserted that opening browse
        // turned the other two off - which is how one region was kept from
        // being drawn over twice. A view takes the whole screen now, so three
        // OPEN at once is the point and only one of them draws. The claim this
        // test exists for is the same claim; the field it reads moved.
        var state = Reducer.BrowseToggled(
            new AppState { EvidenceVisible = true, LiveVisible = true });

        await Assert.That(state.BrowseVisible).IsTrue();
        await Assert.That(state.EvidenceVisible).IsTrue()
            .Because("what somebody was reading is still open behind the tab they opened.");

        var drawn = Enum.GetValues<TabId>().Where(tab => Tabs.Showing(state, tab)).ToList();

        await Assert.That(drawn).IsEquivalentTo((TabId[])[TabId.Browse])
            .Because("one screen, one view. Found: " + string.Join(", ", drawn));
    }
}
