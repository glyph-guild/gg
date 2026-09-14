using Gg.Console;

namespace Gg.Console.Tests;

/// <summary>
/// The reader is started beside the console too, and the exception that allows
/// it is written down.
/// </summary>
/// <remarks>
/// <para>
/// <b>REPORTED FROM USE: "is there any way of avoiding that initial
/// flash/blink?"</b> One press per console lifetime still tore the terminal
/// down — the first browse, because it had to start the reader and a session
/// may not start anything. Everything after it folded in. So the blink stopped
/// being a rule people could see and became a hiccup on the first keypress and
/// never again, which is worse than either: a person learns it as a fault
/// rather than as a boundary.
/// </para>
/// <para>
/// <b>The rule's stated reason is not what the code does.</b> All four guards
/// and CLAUDE.md gave one sentence: <i>"a reader is an executable launched with
/// a credential in its environment, and nothing in a session may start one."</i>
/// Measured against <c>SpawnedReader</c>, the first half is false — it reads
/// neither <c>IntentReader.EnvironmentVariable</c> nor <c>.Locator</c> and
/// places no secret at all. The shape this machine uses says so where the
/// reader is built: <i>"NOTHING FOR AN ENVIRONMENT BLOCK, which is the whole
/// point: the launch has no secret to place, so it writes none."</i> The child
/// is handed a LOCATOR as an argument and resolves the credential on its own
/// side, which is the same arrangement the runner already has.
/// </para>
/// <para>
/// <b>And it cannot reach the terminal either.</b> All three standard streams
/// are redirected with <c>UseShellExecute = false</c>, so the child talks over
/// pipes and has nothing to write on the screen Terminal.Gui is holding. On the
/// background read task it blocks nothing.
/// </para>
/// <para>
/// <b>Which leaves the word "start" and no harm under it</b> — so the exception
/// is granted, scoped, and recorded in <c>LiveStreamingTests</c> beside the
/// clipboard's and the file dialog's. It is recorded THERE because that scan
/// cannot see this spawn: it is a regex over five named files and
/// <c>SpawnedReader.cs</c> is not one of them, so the guard would pass either
/// way. That is the trap the clipboard paragraph already names — <i>"an
/// exception nobody told the guard about is worse than no guard, because it
/// looks like one."</i>
/// </para>
/// <para>
/// <b>What is NOT granted: starting one at launch.</b> A reader nobody asked
/// for is still a child process nobody asked for, and the console must come up
/// without waiting on one. The spawn moves from the first keypress's SHELL to
/// the first keypress's READ — it does not move earlier.
/// </para>
/// </remarks>
public class TheSpawnFoldsInBesideTheConsoleTests
{
    [Test]
    public async Task No_press_falls_to_the_shell_to_start_a_reader()
    {
        // THE CONCEPT GOES, RATHER THAN BEING EMPTIED. `NeedsAReader` answered
        // "which press is the shell's", and the answer is now "none of them" -
        // a set that exists and is empty is a question still being asked.
        await Assert.That(typeof(ShellCommands).GetField("NeedsAReader")).IsNull()
            .Because("a command that needed the shell to spawn for it is a category with "
                   + "nothing in it now, and a category with nothing in it reads as one "
                   + "somebody forgot to fill.");
    }

    [Test]
    public async Task Browsing_is_a_read_and_only_a_read()
    {
        foreach (var command in (Command[])
                 [Command.ToggleBrowse, Command.ShowWorkItem, Command.FilterBrowse,
                  Command.BrowseFiltered])
        {
            await Assert.That(ShellCommands.Reads).Contains(command);
            await Assert.That(ShellCommands.Handled).DoesNotContain(command)
                .Because($"{command} costs no screen now, first press included.");
        }
    }

    [Test]
    public async Task Opening_one_in_a_browser_is_still_the_shells()
    {
        // THE CONTROL, AND IT DOES NOT MOVE. Opening an item starts a BROWSER -
        // a new process every time rather than a pipe to one already running,
        // and one that takes over the display. The exception granted here is
        // for a pipe-and-forget child, and nothing about it reaches this.
        await Assert.That(ShellCommands.Handled).Contains(Command.OpenWorkItem);
        await Assert.That(ShellCommands.Reads).DoesNotContain(Command.OpenWorkItem);
    }

    [Test]
    public async Task The_screen_routes_on_one_question_again()
    {
        // ASSERTED AGAINST THE SOURCE, because ConsoleScreen cannot be built
        // without a terminal. Two routing sites, and the whole defect the
        // second condition existed for is gone - a second copy of a rule is
        // the one place a rule gets half-removed.
        var screen = Sources.Read("Gg.Console", "Views", "ConsoleScreen.cs");

        await Assert.That(screen).DoesNotContain("NeedsAReader", StringComparison.Ordinal)
            .Because("the screen asked whether a reader was running before deciding whether "
                   + "the keypress was the shell's; it no longer has to ask.");

        await Assert.That(screen).DoesNotContain(".Ready(", StringComparison.Ordinal);
    }

    [Test]
    public async Task The_reads_port_no_longer_asks_whether_it_is_ready()
    {
        // THE PREDICATE WAS THE WHOLE MECHANISM, so its absence is the change.
        // A ctor that still took one would leave a caller able to reinstate the
        // blink from the composition root without touching any of this.
        await Assert.That(typeof(BackgroundReads).GetMethod("Ready")).IsNull();

        await Assert.That(
                typeof(BackgroundReads).GetConstructors()
                    .Any(c => c.GetParameters().Any(p => p.Name == "ready")))
            .IsFalse()
            .Because("the argument is the blink, expressed from outside.");
    }

    [Test]
    public async Task Nothing_is_started_at_launch()
    {
        // THE CONSTRAINT THAT DID NOT MOVE, and the one most at risk from this
        // change: with the spawn allowed beside the console, the cheap way to
        // remove the wait on the first press is to do it during the boot. That
        // is a child process nobody asked for, and the owner's constraint is
        // that the TUI comes up with no delay at all.
        var root = Sources.Read("Gg.Cli", "Program.cs");

        await Assert.That(root).DoesNotContain("readers.For(", StringComparison.Ordinal)
            .Because("starting a reader from the composition root would start it for every "
                   + "console, including the ones that never browse.");
    }

    [Test]
    public async Task The_exception_is_written_where_the_scan_that_cannot_see_it_lives()
    {
        // THE CLIPBOARD'S OWN ARGUMENT, APPLIED TO THIS. That paragraph exists
        // because the spawn hides one layer down and the scan passes anyway,
        // which makes a green guard read as compliance. This spawn hides the
        // same way - through a Func composed in the root, into a file the scan
        // does not name - so it has to be recorded in the same place or the
        // next reader will believe the guard covers it.
        var guard = Sources.Read("Gg.Console.Tests", "LiveStreamingTests.cs");

        await Assert.That(guard).Contains("SpawnedReader", StringComparison.Ordinal)
            .Because("the file the scan cannot see has to be named by the exception that "
                   + "allows it, or nothing connects the two.");

        await Assert.That(guard).Contains("THE THIRD", StringComparison.Ordinal)
            .Because("two exceptions were written down as a numbered pair, and an unnumbered "
                   + "third added below them is one a reader skims past.");
    }
}
