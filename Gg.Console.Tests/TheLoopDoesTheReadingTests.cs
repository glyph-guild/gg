using Gg.Console;
using Gg.Local;

namespace Gg.Console.Tests;

/// <summary>
/// The browse key ends the session and the loop asks the reader.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE ARM MUST EXIST OR THE CONSOLE THROWS.</b> <c>ConsoleLoop</c>'s
/// default case raises <c>InvalidOperationException</c> for any exit it does
/// not handle, which is a good design and a sharp edge: putting a command in
/// <c>ShellCommands.Handled</c> without an arm turns a keystroke into a crash,
/// and no test of the keymap or the reducer can see it.
/// </para>
/// <para>
/// <b>The loop owns the reader, and this is where that is asserted.</b> The
/// session may not spawn one; the loop may. Between them is a command and a
/// state, which is the whole terminal-release shape the console already uses
/// for the editor and the take.
/// </para>
/// </remarks>
public class TheLoopDoesTheReadingTests
{
    /// <summary>A reader that answers without a process.</summary>
    private sealed class Answers(BrowseOutcome outcome) : IWorkBrowser
    {
        internal int Asked { get; private set; }

        public string? Key => "a-tracker";

        public Task<BrowseOutcome> BrowseAsync(string? cursor, int limit, CancellationToken token)
        {
            Asked++;
            return Task.FromResult(outcome);
        }
    }

    private static BrowseOutcome OneItem => new BrowseOutcome.Listed(
        new WorkItemPage(
            [new WorkItemSummary("18398", "A draft job fails to load", "New", "", null)], null));

    [Test]
    public async Task Pressing_browse_asks_the_reader_and_the_answer_reaches_the_state()
    {
        var reader = new Answers(OneItem);
        var ui = new ConsoleDoubles.TypesKeys(Command.ToggleBrowse);

        var final = new ConsoleLoop(ui, new ConsoleDoubles.NoEditor(), browser: reader)
            .Run(new AppState());

        await Assert.That(reader.Asked).IsEqualTo(1);
        await Assert.That(final.BrowseVisible).IsTrue();
        await Assert.That(final.Browse).IsNotNull();
        await Assert.That(final.Browse!.Items[0].Id).IsEqualTo("18398");
    }

    [Test]
    public async Task The_next_session_is_rebuilt_holding_what_the_reader_said()
    {
        // The terminal-release shape's whole claim: the model is the only thing
        // that crosses back, so the redraw must be able to draw the listing.
        var ui = new ConsoleDoubles.TypesKeys(Command.ToggleBrowse);

        _ = new ConsoleLoop(ui, new ConsoleDoubles.NoEditor(), browser: new Answers(OneItem))
            .Run(new AppState());

        await Assert.That(ui.Rendered).Count().IsEqualTo(2)
            .Because("one session showed the queue, the next showed the browser.");
        await Assert.That(ui.Rendered[1].Browse).IsNotNull();
        await Assert.That(PaneText.Browse(ui.Rendered[1])).Contains("A draft job fails to load");
    }

    [Test]
    public async Task Hiding_the_browser_does_not_ask_again()
    {
        // A read costs a whole session rebuild on this path, so spending one to
        // close a pane would be the most expensive no-op in the console.
        var reader = new Answers(OneItem);
        var ui = new ConsoleDoubles.TypesKeys(Command.ToggleBrowse, Command.ToggleBrowse);

        var final = new ConsoleLoop(ui, new ConsoleDoubles.NoEditor(), browser: reader)
            .Run(new AppState());

        await Assert.That(reader.Asked).IsEqualTo(1);
        await Assert.That(final.BrowseVisible).IsFalse();
        await Assert.That(final.Browse).IsNotNull()
            .Because("what it found survives hiding.");
    }

    [Test]
    public async Task A_console_with_no_reader_configured_says_so_rather_than_throwing()
    {
        // ARTICLE XI, and the arm has to exist for this too: a console composed
        // without a browser is the ordinary state of every runner in the fleet.
        var ui = new ConsoleDoubles.TypesKeys(Command.ToggleBrowse);

        var final = new ConsoleLoop(ui, new ConsoleDoubles.NoEditor()).Run(new AppState());

        await Assert.That(final.BrowseVisible).IsTrue();
        await Assert.That(final.Browse).IsNull()
            .Because("null is 'no reader was ever asked', which the pane already words.");
        await Assert.That(PaneText.Browse(final))
            .Contains(IntentConfiguration.ServedVariable);
    }

    [Test]
    public async Task A_reader_that_throws_does_not_take_the_console_with_it()
    {
        // The client turns every failure into an outcome, but the loop is the
        // last line: a bug in a reader must not end a person's session.
        var ui = new ConsoleDoubles.TypesKeys(Command.ToggleBrowse);

        var final = new ConsoleLoop(ui, new ConsoleDoubles.NoEditor(), browser: new Throws())
            .Run(new AppState());

        await Assert.That(final.Browse).IsNotNull();
        await Assert.That(final.Browse!.Absence).IsNotNull();
    }

    private sealed class Throws : IWorkBrowser
    {
        public string? Key => "a-tracker";

        public Task<BrowseOutcome> BrowseAsync(string? cursor, int limit, CancellationToken token) =>
            throw new InvalidOperationException("a bug in a reader");
    }
}
