using Gg.Console;

namespace Gg.Console.Tests;

/// <summary>
/// Browsing stops taking the screen away, and now it stops on the first press
/// as well as every one after.
/// </summary>
/// <remarks>
/// <para>
/// <b>This class landed in two steps and the second one is the interesting
/// one.</b> The first moved browsing out of the shell for every press but the
/// first: four guards said browsing was the shell's, each giving the same
/// reason — an <c>IntentReader</c> is an executable launched with a credential
/// in its environment — and <c>ReaderSessions</c> caches what it starts, so the
/// spawn is ONE act and the asking is another. The first press did the spawn in
/// the shell; the rest folded in.
/// </para>
/// <para>
/// <b>Then the reason was measured and it was not true of this code.</b>
/// <c>SpawnedReader</c> reads neither <c>EnvironmentVariable</c> nor
/// <c>Locator</c>: it places no secret, redirects all three streams so the child
/// cannot touch the terminal, and on the read task blocks nothing. The three
/// things the rule protects — a session may not START anything, resolve a
/// credential, or block — had two already satisfied, and the third was a word
/// with no harm under it. So the spawn folds in too, under an exception written
/// down in <see cref="LiveStreamingTests"/> beside the clipboard's.
/// </para>
/// <para>
/// <b>What that leaves: the console takes the screen away for an editor, a
/// take, a runner and a browser, and for nothing about reading a tracker.</b>
/// Nothing is started at launch, which is the constraint that did not move —
/// the spawn went from the first keypress's shell to the first keypress's read,
/// not earlier. See <see cref="TheSpawnFoldsInBesideTheConsoleTests"/>.
/// </para>
/// </remarks>
public class BrowsingFoldsInBesideTheConsoleTests
{
    [Test]
    public async Task Browsing_is_a_read_now_and_is_no_longer_the_shells()
    {
        // THE SETS ARE DISJOINT, which is the guard the last attempt tripped
        // over: it added browse to `Reads` and left it in `Handled`, so the
        // guard asserting browsing is the shell's went on passing and nothing
        // changed. Moving it has to be a removal as well as an addition.
        foreach (var command in (Command[])
                 [Command.ToggleBrowse, Command.ShowWorkItem, Command.FilterBrowse,
                  Command.BrowseFiltered])
        {
            await Assert.That(ShellCommands.Reads).Contains(command);
            await Assert.That(ShellCommands.Handled).DoesNotContain(command)
                .Because($"{command} cannot be both, and being left in Handled is exactly how "
                       + "the previous attempt at this changed nothing while passing.");
        }
    }

    [Test]
    public async Task Opening_one_in_a_browser_is_still_the_shells()
    {
        // THE CONTROL, and the one that must not move. Opening an item starts a
        // BROWSER - a new process every time, not a pipe to one already
        // running, and one that takes the display - so it is a spawn in a sense
        // the granted exception says nothing about.
        await Assert.That(ShellCommands.Handled).Contains(Command.OpenWorkItem);
        await Assert.That(ShellCommands.Reads).DoesNotContain(Command.OpenWorkItem);
    }

    [Test]
    public async Task A_console_with_no_reads_port_still_answers_the_key()
    {
        // NO PORT IS NOT A REFUSAL. A console composed without one - every test
        // that builds a screen, and any caller that supplies no reader - has to
        // go on behaving as it did rather than routing the key to a shell that
        // has nothing to serve it with either. The pane opens and says what it
        // is waiting for; it does not cost a terminal.
        var opened = Reducer.Reduce(new AppState(), Command.ToggleBrowse);

        await Assert.That(opened.BrowseVisible).IsTrue();
        await Assert.That(opened.ActiveTab).IsEqualTo(TabId.Browse);
    }

    [Test]
    public async Task The_browse_pane_says_when_a_read_is_in_the_air()
    {
        // THE BLINK WAS THE PROGRESS INDICATOR. A screen taken away and given
        // back said something was happening; folding the answer in says nothing
        // until it arrives, and a tracker that takes two seconds would look
        // like a pane that had simply not loaded.
        //
        // AND IT NOW COVERS THE SPAWN AS WELL AS THE ASK, which is the slowest
        // moment there is: the first press starts a process and waits for it to
        // answer, and that is precisely the press that used to be a blink.
        var reading = new AppState { ActiveTab = TabId.Browse, ReadInFlight = true };

        await Assert.That(PaneText.Browse(reading)).Contains("Reading")
            .Because("a pane that is waiting has to say so, or the person reading it cannot "
                   + "tell waiting from empty.");
    }
}
