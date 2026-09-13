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
/// <b>The shell's, so it happens between sessions.</b> Asking a reader starts a
/// child process holding a credential, which a UI session may not do — the same
/// sentence that made <c>b</c> a shell command rather than an in-session toggle,
/// and the reason this is not in <c>Reads</c> beside the control-plane ones.
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

    [Test]
    public async Task Enter_on_a_browse_row_asks_about_that_row()
    {
        var command = Keymap.Resolve(KeyStroke.EnterKey, KeymapContext.For(Browsing()));

        await Assert.That(command).IsEqualTo(Command.ShowWorkItem)
            .Because("enter means the row under the cursor, and on this tab the row is a "
                   + "work item.");
    }

    [Test]
    public async Task It_is_the_shells_and_not_a_read()
    {
        // THE DISTINCTION THE DECLARATION ITSELF DRAWS. `Reads` is for
        // control-plane reads, which cost a request; ToggleBrowse is in
        // `Handled` instead because browsing "launches an executable with a
        // credential in its environment", and that is exactly what asking about
        // one item does. Same door, same reason.
        await Assert.That(ShellCommands.Handled.Contains(Command.ShowWorkItem)).IsTrue()
            .Because("an intent reader is a child process holding a credential, and a UI "
                   + "session may start neither.");

        await Assert.That(ShellCommands.Reads.Contains(Command.ShowWorkItem)).IsFalse()
            .Because("a command is the shell's or a read and never both.");
    }

    [Test]
    public async Task What_the_reader_said_is_what_the_modal_draws()
    {
        var shown = ConsoleLoop.ShowedWorkItem(
            Browsing(),
            _ => new ItemOutcome.Read(
                "Type: Product Backlog Item\nTitle: Oz asks guided questions\n"
              + "Description: The wizard should ask rather than assume."),
            _ => new ItemOutcome.Read(""));

        await Assert.That(shown.Mode).IsEqualTo(UiMode.WorkItemDetail);

        var said = PaneText.Modal(shown);

        await Assert.That(said).Contains("The wizard should ask rather than assume.")
            .Because("the body is the reason somebody pressed enter - the title and the "
                   + "state were already on the row they pressed it from.");
    }

    [Test]
    public async Task A_reader_that_could_not_answer_says_so_in_the_modal()
    {
        var shown = ConsoleLoop.ShowedWorkItem(
            Browsing(),
            _ => new ItemOutcome.Nothing("the credential expired on Tuesday"),
            _ => new ItemOutcome.Read(""));

        await Assert.That(shown.Mode).IsEqualTo(UiMode.WorkItemDetail)
            .Because("the modal opens either way: a person pressed a key and something has "
                   + "to answer them, and an empty box would be the console swallowing it.");

        await Assert.That(PaneText.Modal(shown)).Contains("the credential expired on Tuesday");
    }

    [Test]
    public async Task What_has_happened_to_it_is_shown_under_what_it_says()
    {
        // ONE KEYPRESS, TWO QUESTIONS. A body says what somebody wants and a
        // history says what has already been tried; a person choosing work needs
        // both, and making the second one another key would make it the one
        // nobody presses.
        var shown = ConsoleLoop.ShowedWorkItem(
            Browsing(),
            _ => new ItemOutcome.Read("Description: The wizard should ask."),
            _ => new ItemOutcome.Read("2026-09-04  A Colleague  Blocked on the rollout."));

        var said = PaneText.Modal(shown);

        await Assert.That(said).Contains("The wizard should ask.");
        await Assert.That(said).Contains("Blocked on the rollout.");

        await Assert.That(said.IndexOf("The wizard", StringComparison.Ordinal))
            .IsLessThan(said.IndexOf("Blocked on", StringComparison.Ordinal))
            .Because("what it is comes before what has happened to it: the second only "
                   + "means anything once you know the first.");
    }

    [Test]
    public async Task A_reader_with_no_history_still_shows_the_item()
    {
        var shown = ConsoleLoop.ShowedWorkItem(
            Browsing(),
            _ => new ItemOutcome.Read("Description: The wizard should ask."),
            _ => new ItemOutcome.Nothing("this reader does not declare that tool"));

        var said = PaneText.Modal(shown);

        await Assert.That(said).Contains("The wizard should ask.")
            .Because("a reader that answers one of the two questions is more useful than "
                   + "one that is refused for not answering both.");

        await Assert.That(said).Contains("does not declare that tool")
            .Because("and the half that is missing says so, rather than reading as an item "
                   + "nothing has ever happened to.");
    }

    [Test]
    public async Task The_item_can_be_opened_where_it_lives()
    {
        var opened = new List<string>();

        var shown = ConsoleLoop.ShowedWorkItem(
            Browsing(),
            _ => new ItemOutcome.Read("Description: The wizard should ask."),
            _ => new ItemOutcome.Read(""));

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
        var asked = 0;

        var shown = ConsoleLoop.ShowedWorkItem(
            new AppState { ActiveTab = TabId.Browse },
            _ => { asked++; return new ItemOutcome.Read("never reached"); },
            _ => new ItemOutcome.Read(""));

        await Assert.That(asked).IsEqualTo(0)
            .Because("a key that appears to work on an empty pane is worse than one that is "
                   + "not offered.");

        await Assert.That(shown.Mode).IsNotEqualTo(UiMode.WorkItemDetail);
    }
}
