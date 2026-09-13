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
/// <b>A read, so it happens between sessions.</b> Asking a reader starts a child
/// process holding a credential, which a UI session may not do — the same
/// argument that made <c>b</c> a shell command rather than an in-session toggle.
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
                        "18515", "Oz asks guided questions", "Active", "", null),
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
    public async Task It_is_a_read_because_asking_starts_a_child()
    {
        await Assert.That(ShellCommands.Reads.Contains(Command.ShowWorkItem)).IsTrue()
            .Because("an intent reader is a child process holding a credential, and a UI "
                   + "session may start neither - which is the same argument that made "
                   + "browsing a shell command rather than a toggle.");
    }

    [Test]
    public async Task What_the_reader_said_is_what_the_modal_draws()
    {
        var shown = ConsoleLoop.ShowedWorkItem(
            Browsing(),
            _ => new ItemOutcome.Read(
                "Type: Product Backlog Item\nTitle: Oz asks guided questions\n"
              + "Description: The wizard should ask rather than assume."));

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
            Browsing(), _ => new ItemOutcome.Nothing("the credential expired on Tuesday"));

        await Assert.That(shown.Mode).IsEqualTo(UiMode.WorkItemDetail)
            .Because("the modal opens either way: a person pressed a key and something has "
                   + "to answer them, and an empty box would be the console swallowing it.");

        await Assert.That(PaneText.Modal(shown)).Contains("the credential expired on Tuesday");
    }

    [Test]
    public async Task Nothing_picked_asks_nothing_at_all()
    {
        var asked = 0;

        var shown = ConsoleLoop.ShowedWorkItem(
            new AppState { ActiveTab = TabId.Browse },
            _ => { asked++; return new ItemOutcome.Read("never reached"); });

        await Assert.That(asked).IsEqualTo(0)
            .Because("a key that appears to work on an empty pane is worse than one that is "
                   + "not offered.");

        await Assert.That(shown.Mode).IsNotEqualTo(UiMode.WorkItemDetail);
    }
}
