using Gg.Console;

namespace Gg.Console.Tests;

/// <summary>
/// Enter on a browse row shows what the item actually says.
/// </summary>
/// <remarks>
/// <para>
/// <b>The browse pane shows an id, a state and a title, and that is all there
/// was.</b> Choosing work by those three is choosing by headline; the body — why
/// it exists, what would count as done — was reachable only by leaving the
/// console for a browser. The reader has been able to answer it the whole time.
/// </para>
/// <para>
/// <b>Enter, because that is what enter means here.</b> Two tabs open the flight
/// under the cursor and one opens the machine under it; browse was in the
/// <c>_ => []</c> arm with nothing to open. <c>EnterBelongsToATabTests</c>
/// asserts that arm, and it is rewritten rather than worked around — a tab that
/// grows something to open stops belonging in a list of tabs that have nothing.
/// </para>
/// <para>
/// <b>A read, and it costs no screen at all.</b> The sentence this used to give
/// — <i>"starting a reader is a child process holding a credential and a UI
/// session may not do it"</i> — was measured and is not what
/// <c>SpawnedReader</c> does: it places no secret and the child talks over
/// pipes. So the spawn folds in beside the console like the asking, under the
/// exception scoped in <see cref="LiveStreamingTests"/>.
/// </para>
/// <para>
/// <b>And the reading moved with it.</b> <c>ConsoleLoop.ShowedWorkItem</c> was
/// the shell's half and is gone; <c>ConsoleBrowsing.ItemPatch</c> is what
/// answers this keypress now. These assertions moved onto it rather than being
/// dropped — a behaviour that still happens, asserted against the thing that
/// still performs it.
/// </para>
/// </remarks>
public class EnterShowsTheWorkItemTests
{
    private static AppState Browsing() =>
        Reducer.Browsed(
            new AppState { BrowseVisible = true, ActiveTab = TabId.Browse },
            "a-tracker",
            new BrowseOutcome.Listed(new Gg.Local.WorkItemPage(
                [
                    new Gg.Local.WorkItemSummary(
                        "18515", "Oz asks guided questions", "Active",
                        "https://tracker.example/acme/_workitems/edit/18515", null),
                ],
                null)));

    /// <summary>A browser that answers without a process.</summary>
    /// <remarks>
    /// <b>Was two delegates handed to <c>ConsoleLoop.ShowedWorkItem</c>.</b>
    /// The read takes a browser now, because the same port answers the listing,
    /// the item and its history — a person reading an item is talking to the
    /// tracker they listed it from.
    /// </remarks>
    private sealed class Answers(ItemOutcome said, HistoryOutcome happened) : IWorkBrowser
    {
        internal int Asked { get; private set; }

        public string? Key => "a-tracker";

        public Task<BrowseOutcome> BrowseAsync(
            string? cursor, int limit, Gg.Local.WorkItemFilter? filter, CancellationToken token) =>
            Task.FromResult<BrowseOutcome>(new BrowseOutcome.Silent("not asked here"));

        public Task<FacetOutcome> FacetsAsync(CancellationToken token) =>
            Task.FromResult<FacetOutcome>(
                new FacetOutcome.Offered(Gg.Local.WorkItemFacets.Nothing));

        public Task<ItemOutcome> ReadAsync(string id, CancellationToken token)
        {
            Asked++;
            return Task.FromResult(said);
        }

        public Task<HistoryOutcome> HistoryAsync(string id, CancellationToken token) =>
            Task.FromResult(happened);
    }

    /// <summary>What the modal holds once the read has landed.</summary>
    private static AppState Shown(AppState state, ItemOutcome said, HistoryOutcome happened) =>
        ConsoleBrowsing.ItemPatch(new Answers(said, happened), state)(state);

    [Test]
    public async Task Enter_on_a_browse_row_asks_about_that_row()
    {
        var command = Keymap.Resolve(KeyStroke.EnterKey, KeymapContext.For(Browsing()));

        await Assert.That(command).IsEqualTo(Command.ShowWorkItem)
            .Because("enter means the row under the cursor, and on this tab the row is a "
                   + "work item.");
    }

    [Test]
    public async Task It_is_a_read_and_costs_no_screen()
    {
        // THE SENTENCE THE DECLARATION USED TO DRAW THIS FROM WAS WRONG ABOUT
        // THE CODE. Browsing "launches an executable with a credential in its
        // environment" - and SpawnedReader references neither the environment
        // variable nor the locator on the reader it launches. It places no
        // secret; the child resolves its own. Same door, same reason, both
        // halves, and now no press of either ends the session.
        await Assert.That(ShellCommands.Handled.Contains(Command.ShowWorkItem)).IsFalse()
            .Because("the spawn under this key holds no credential and no stream of the "
                   + "terminal, so there is nothing for a teardown to protect.");

        await Assert.That(ShellCommands.Reads.Contains(Command.ShowWorkItem)).IsTrue()
            .Because("and once one is running, asking it about an item is a read like any "
                   + "other: the answer folds in beside the console rather than costing the "
                   + "screen.");

        await Assert.That(ShellCommands.Handled.Contains(Command.ShowWorkItem)).IsFalse()
            .Because("a command is the shell's or a read and never both - the guard the last "
                   + "attempt at this tripped over by adding to one set and not removing from "
                   + "the other.");
    }

    [Test]
    public async Task What_the_reader_said_is_what_the_modal_draws()
    {
        var shown = Shown(
            Browsing(),
            new ItemOutcome.Read(
                "Type: Product Backlog Item\nTitle: Oz asks guided questions\n"
              + "Description: The wizard should ask rather than assume."),
            new HistoryOutcome.Read([]));

        await Assert.That(shown.Mode).IsEqualTo(UiMode.WorkItemDetail);

        await Assert.That(WorkItemDetails.Said(shown))
            .Contains("The wizard should ask rather than assume.")
            .Because("the body is the reason somebody pressed enter - the title and the "
                   + "state were already on the row they pressed it from.");
    }

    [Test]
    public async Task A_reader_that_could_not_answer_says_so_in_the_modal()
    {
        var shown = Shown(
            Browsing(),
            new ItemOutcome.Nothing("the credential expired on Tuesday"),
            new HistoryOutcome.Read([]));

        await Assert.That(shown.Mode).IsEqualTo(UiMode.WorkItemDetail)
            .Because("the modal opens either way: a person pressed a key and something has "
                   + "to answer them, and an empty box would be the console swallowing it.");

        await Assert.That(WorkItemDetails.Said(shown))
            .Contains("the credential expired on Tuesday");
    }

    [Test]
    public async Task What_has_happened_to_it_is_shown_under_what_it_says()
    {
        // ONE KEYPRESS, TWO QUESTIONS. A body says what somebody wants and a
        // history says what has already been tried; a person choosing work needs
        // both, and making the second one another key would make it the one
        // nobody presses.
        var shown = Shown(
            Browsing(),
            new ItemOutcome.Read("Description: The wizard should ask."),
            new HistoryOutcome.Read(
                [new WorkItemChangeRow
                {
                    When = "2026-09-04 10:00",
                    Who = "A Colleague",
                    What = "Blocked on the rollout.",
                }]));

        // TWO REGIONS, NOT ONE BLOCK. What it says is prose in a document pane;
        // what has happened to it is rows in a table, which is what it is - so
        // the two are asked for separately rather than concatenated with a rule
        // of dashes between them.
        await Assert.That(WorkItemDetails.Said(shown)).Contains("The wizard should ask.");

        await Assert.That(WorkItemDetails.Changes(shown).Single().What)
            .IsEqualTo("Blocked on the rollout.");
    }

    [Test]
    public async Task A_reader_with_no_history_still_shows_the_item()
    {
        var shown = Shown(
            Browsing(),
            new ItemOutcome.Read("Description: The wizard should ask."),
            new HistoryOutcome.Nothing("this reader does not declare that tool"));

        await Assert.That(WorkItemDetails.Said(shown)).Contains("The wizard should ask.")
            .Because("a reader that answers one of the two questions is more useful than "
                   + "one that is refused for not answering both.");

        await Assert.That(WorkItemDetails.HistoryAbsence(shown))
            .Contains("does not declare that tool")
            .Because("and the half that is missing says so, rather than reading as an item "
                   + "nothing has ever happened to.");
    }

    [Test]
    public async Task The_item_can_be_opened_where_it_lives()
    {
        var opened = new List<string>();

        var shown = Shown(
            Browsing(),
            new ItemOutcome.Read("Description: The wizard should ask."),
            new HistoryOutcome.Read([]));

        var command = Keymap.Resolve(KeyStroke.Char('o'), KeymapContext.For(shown));

        await Assert.That(command).IsEqualTo(Command.OpenWorkItem)
            .Because("a tracker holds more than a reader renders - attachments, links, the "
                   + "people on it - and the console should hand somebody over rather than "
                   + "pretend to replace it.");

        _ = ConsoleLoop.OpenedWorkItem(shown, (state, uri) => { opened.Add(uri); return state; });

        await Assert.That(opened).IsEquivalentTo(
            new List<string> { "https://tracker.example/acme/_workitems/edit/18515" });
    }

    [Test]
    public async Task Nothing_picked_asks_nothing_at_all()
    {
        var reader = new Answers(
            new ItemOutcome.Read("never reached"), new HistoryOutcome.Read([]));

        var empty = new AppState { ActiveTab = TabId.Browse };
        var shown = ConsoleBrowsing.ItemPatch(reader, empty)(empty);

        await Assert.That(reader.Asked).IsEqualTo(0)
            .Because("a key that appears to work on an empty pane is worse than one that is "
                   + "not offered.");

        await Assert.That(shown.Mode).IsNotEqualTo(UiMode.WorkItemDetail);
    }
}
