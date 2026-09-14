using Gg.Console;

namespace Gg.Console.Tests;

/// <summary>
/// `b` opens the pane and `b` closes it, however the press was served.
/// </summary>
/// <remarks>
/// <para>
/// <b>REPORTED FROM USE: "it only responds on the first press and doesn't
/// switch subsequently."</b> Exactly right, and the shape of it says which
/// press is which. The FIRST browse of a console lifetime finds no reader
/// running, so it falls to the shell — and the shell's arm calls
/// <see cref="Reducer.BrowseToggled"/> directly. Every press after it is a
/// read served beside the console, which goes through <see cref="Reducer.Reduce"/>
/// — and <c>Reduce</c> has no arm for <c>ToggleBrowse</c> at all.
/// </para>
/// <para>
/// <b>So the pane toggled once and then stopped, while still asking the tracker
/// every time.</b> <c>ConsoleBrowsing.Patch</c> reads whenever
/// <c>BrowseVisible</c> is true, and nothing had turned it off — so the second
/// press was a silent round trip that redrew the same pane, which is worse than
/// a dead key because it costs a request to do nothing.
/// </para>
/// <para>
/// <b>The arm was deliberately absent, and the reason expired.</b>
/// <c>BrowseToggled</c>'s own remark says it: <i>"NOT REACHABLE THROUGH Reduce,
/// and a ratchet says so. ToggleBrowse is a shell command because showing this
/// pane starts a reader, and a shell command that ALSO has a reducer arm has
/// two effects — the local one happening whether or not the remote one did."</i>
/// That was true while the command was the shell's. It is a read now, and a
/// read whose local half never happens is a keypress that does nothing.
/// </para>
/// <para>
/// <b>Both effects still happen exactly once, because the paths do not
/// overlap.</b> <c>ConsoleScreen</c> exits BEFORE reducing when a command is
/// the shell's, and <c>ConsoleLoop</c> never calls <c>Reduce</c> — it switches
/// on the exit command and calls the named reducer. So the shell path toggles
/// through <c>BrowseToggled</c> and the read path toggles through <c>Reduce</c>,
/// and neither sees the other's.
/// </para>
/// </remarks>
public class TheBrowseKeyTogglesEveryPressTests
{
    [Test]
    public async Task The_press_that_is_served_as_a_read_still_closes_the_pane()
    {
        // THE SECOND PRESS, which is the first one that goes through Reduce.
        // The first opened the pane in the shell, because that is the press
        // that had to start the reader.
        var open = Reducer.BrowseToggled(new AppState());

        await Assert.That(open.BrowseVisible).IsTrue()
            .Because("the shell's arm is what the first press takes, and it worked.");

        var closed = Reducer.Reduce(open, Command.ToggleBrowse);

        await Assert.That(closed.BrowseVisible).IsFalse()
            .Because("a toggle that only ever opens is not a toggle, and this is the press "
                   + "the report is about: the reader is running by now, so the command is "
                   + "reduced beside the console instead of ending it.");
    }

    [Test]
    public async Task And_the_one_after_it_opens_it_again()
    {
        // THE WHOLE CYCLE THROUGH THE PATH THAT WAS SILENT, because a fix that
        // only closed would be the same defect facing the other way.
        var state = Reducer.Reduce(
            Reducer.Reduce(Reducer.BrowseToggled(new AppState()), Command.ToggleBrowse),
            Command.ToggleBrowse);

        await Assert.That(state.BrowseVisible).IsTrue();
        await Assert.That(state.ActiveTab).IsEqualTo(TabId.Browse);
    }

    [Test]
    public async Task The_two_paths_are_the_same_toggle()
    {
        // ONE ACT, TWO WAYS IN. The shell's arm and the reducer's must not
        // drift into meaning different things, or which press you are on
        // changes what the key does - which is the report, one level down.
        foreach (var start in (AppState[])
                 [new AppState(), Reducer.BrowseToggled(new AppState())])
        {
            await Assert.That(Reducer.Reduce(start, Command.ToggleBrowse))
                .IsEqualTo(Reducer.BrowseToggled(start))
                .Because("the loop calls one and the screen calls the other, for the same "
                       + "keypress.");
        }
    }

    [Test]
    public async Task Closing_the_pane_asks_the_tracker_nothing()
    {
        // THE OTHER HALF OF THE COST, and the reason the silent press was
        // worse than a dead one. The read runs AFTER the reduce - Asked() is
        // handed the reduced state - so a toggle that shuts the pane has to
        // leave Patch with nothing to do.
        var open = Reducer.BrowseToggled(new AppState());
        var closed = Reducer.Reduce(open, Command.ToggleBrowse);

        var patch = ConsoleBrowsing.Patch(new ConsoleDoubles.NeverAsked(), closed);

        await Assert.That(patch(closed)).IsEqualTo(closed)
            .Because("a toggle that shut the pane and then fetched what to put in it is a "
                   + "request nobody asked for - ConsoleBrowsing.Patch's own sentence.");
    }
}
