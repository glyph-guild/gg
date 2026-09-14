using Gg.Console;

namespace Gg.Console.Tests;

/// <summary>
/// Browsing stops taking the screen away, and the one thing a session still
/// may not do is start the reader.
/// </summary>
/// <remarks>
/// <para>
/// <b>The guards were right, and they were right about a SPAWN.</b> Four of
/// them said browsing is the shell's, and every one gives the same reason: an
/// <c>IntentReader</c> is an executable launched with a credential in its
/// environment, and <i>"a toggle handled inside the session would have to spawn
/// the reader from inside the session"</i>. That premise held while
/// <c>ReaderSessions</c> started one lazily, on the keypress. It is the premise
/// that changes here, not the rule.
/// </para>
/// <para>
/// <b>So the first browse still ends the session, and nothing else does.</b>
/// The reader is started once per console lifetime, by the shell, where every
/// spawn already happens — and every browse, item and filter after it folds in
/// beside the console the way a flight's detail already does. Nothing starts at
/// launch: a reader that nobody asked for is a child process nobody asked for,
/// and the console must come up without waiting on one.
/// </para>
/// <para>
/// <b>What the rule now says is what it always protected:</b> a session may not
/// START a process, resolve a credential or block. Talking to a reader that was
/// running before the session existed is the shape <c>LiveTails</c> already has
/// — owned outside every UI lifetime, handed in, asked.
/// </para>
/// <para>
/// <b>And the blink was the progress indicator.</b> Taking the screen away said
/// something was happening. Folding the read in beside the console means a slow
/// tracker looks like an idle pane unless the pane says otherwise, so
/// <c>ReadInFlight</c> has to reach the browse pane rather than only the ones
/// that already had it.
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
        // running - so it is a spawn in the sense the guards mean and stays
        // where every spawn is.
        await Assert.That(ShellCommands.Handled).Contains(Command.OpenWorkItem);
        await Assert.That(ShellCommands.Reads).DoesNotContain(Command.OpenWorkItem);
    }

    [Test]
    public async Task A_read_whose_reader_is_not_running_yet_is_the_shells_for_that_one_press()
    {
        // THE WHOLE OF THE SPAWN RULE, AS A DECISION THE SCREEN CAN MAKE. The
        // first browse of a console lifetime has nothing to talk to, and
        // starting one is the act a session may not perform - so that press
        // ends the session exactly as it does today, the shell starts the
        // reader, and the answer comes back with the next one built.
        var reads = new BackgroundReads(
            (_, state) => Task.FromResult<Func<AppState, AppState>>(s => s),
            ready: _ => false);

        await Assert.That(reads.Ready(Command.ToggleBrowse)).IsFalse();
    }

    [Test]
    public async Task And_is_served_beside_the_console_once_it_is()
    {
        var reads = new BackgroundReads(
            (_, state) => Task.FromResult<Func<AppState, AppState>>(s => s),
            ready: _ => true);

        await Assert.That(reads.Ready(Command.ToggleBrowse)).IsTrue();
    }

    [Test]
    public async Task A_command_that_reads_nothing_is_ready_by_definition()
    {
        // NO PREDICATE IS NOT A REFUSAL. A console composed without one - every
        // test that builds a screen, and any caller that supplies no reader -
        // must go on behaving as it did rather than routing every read to a
        // shell that has nothing to serve it with.
        var reads = new BackgroundReads(
            (_, state) => Task.FromResult<Func<AppState, AppState>>(s => s));

        await Assert.That(reads.Ready(Command.ShowFlight)).IsTrue()
            .Because("the predicate is an addition, and its absence is the behaviour that was "
                   + "there before it.");
    }

    [Test]
    public async Task The_browse_pane_says_when_a_read_is_in_the_air()
    {
        // THE BLINK WAS THE PROGRESS INDICATOR. A screen taken away and given
        // back said something was happening; folding the answer in says nothing
        // until it arrives, and a tracker that takes two seconds would look
        // like a pane that had simply not loaded.
        var reading = new AppState { ActiveTab = TabId.Browse, ReadInFlight = true };

        await Assert.That(PaneText.Browse(reading)).Contains("Reading")
            .Because("a pane that is waiting has to say so, or the person reading it cannot "
                   + "tell waiting from empty.");
    }
}
