using System.Reflection;

namespace Gg.Console.Tests;

/// <summary>
/// Hosting a child in a pseudo-terminal gg owns: what the child is told, what
/// reaches the screen, what is put back afterwards, and what is kept.
/// </summary>
/// <remarks>
/// <para>
/// <b>These run a real child on a real pseudo-terminal.</b> Not a mock: the
/// whole class of defect being guarded against lives in the seam between a
/// process, a tty and a line discipline, and every one found so far was found
/// by running something. What makes them CI-safe is that the terminal is one the
/// test made — nothing here touches the terminal the runner inherited, and there
/// need not be one.
/// </para>
/// <para>
/// <b>No sleeps.</b> Each child either exits on its own or is killed, and the
/// host is awaited. A test that waited a fixed time for a process would be a
/// test that fails on a loaded machine and passes on a quiet one.
/// </para>
/// </remarks>
public class PtyHostTests
{
    /// <summary>
    /// A string a diagnostic has no legitimate reason to carry, shaped like the
    /// thing that would actually leak.
    /// </summary>
    private const string Needle = "ghp_hostedChildNeedle7fQ2xVn";

    /// <summary>Waits for something to become true, and says so when it does not.</summary>
    /// <remarks>
    /// <b>Not a sleep, and the difference matters.</b> A fixed wait passes on a
    /// quiet machine and fails on a loaded one, which is how a suite acquires a
    /// test nobody trusts. This returns the moment the condition holds and fails
    /// loudly if it never does, so the only thing the cap decides is how long a
    /// genuine failure takes to report.
    /// </remarks>
    private static bool Until(Func<bool> held)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);

        while (DateTime.UtcNow < deadline)
        {
            if (held())
            {
                return true;
            }

            Thread.Sleep(5);
        }

        return false;
    }

    private static Task<int> Host(HostedTerminal terminal, string script, string bar = "gg") =>
        PtyHost.RunAsync(
            terminal,
            command: "/bin/sh",
            arguments: ["-c", script],
            workingDirectory: Path.GetTempPath(),
            // ONE ROW, WRAPPED, because the host asks for the panel each frame -
            // both what it says and how many rows it is change while a composing
            // session runs, and these tests are about the frame rather than
            // about what gg chose to put on it.
            panel: () => [bar],
            CancellationToken.None);

    [Test]
    public async Task The_child_is_told_the_screen_is_one_row_shorter_than_it_is()
    {
        // THE MECHANISM THAT KEEPS THE BAR, measured rather than asserted about
        // a variable. A full-screen program cannot paint a row it does not
        // believe exists - which is why this is a lie about the size and not a
        // scroll region, because a program like that resets the region and
        // repaints everything inside it.
        using var terminal = new HostedTerminal { Columns = 40, Rows = 10 };

        await Host(terminal, "stty size");

        await Assert.That(terminal.Painted).Contains("9 40", StringComparison.Ordinal)
            .Because("ten rows on gg's terminal is nine for the child and one for the bar, "
                   + "and the child is the one being asked.");
    }

    [Test]
    public async Task The_bar_is_on_the_screen_while_the_child_runs()
    {
        using var terminal = new HostedTerminal();

        // A CHILD THAT WRITES NOTHING TO THE SCREEN. `printf hello` let this
        // pass for the weaker reason - the bar rode along with the child's first
        // output - and an editor opening an empty file says nothing at all.
        await Host(terminal, "exit 0", bar: "gg | flight 41");

        await Assert.That(terminal.Painted).Contains("gg | flight 41", StringComparison.Ordinal)
            .Because("the top row is the only surface gg still owns while a child has the "
                   + "screen, and a person handed an agent cannot ask it what gg wants.");
    }

    [Test]
    public async Task The_exit_code_is_the_child_s_own()
    {
        using var terminal = new HostedTerminal();

        await Assert.That(await Host(terminal, "exit 0")).IsEqualTo(0);

        using var second = new HostedTerminal();
        await Assert.That(await Host(second, "exit 3")).IsEqualTo(3)
            .Because("a caller deciding whether an edit was abandoned reads this, and a host "
                   + "that always answered zero would report every abandonment as a save.");
    }

    [Test]
    public async Task The_terminal_is_put_back_however_the_child_ended()
    {
        // EVERY EXIT PATH, because the one that is forgotten is the one that
        // leaves a person with a shell that does not echo. Raw mode is not a
        // thing gg can leave behind: the terminal belongs to whoever ran it.
        foreach (var (ending, script) in ((string, string)[])
                 [("cleanly", "exit 0"),
                  ("badly", "exit 3"),
                  ("killed outright", "kill -9 $$")])
        {
            using var terminal = new HostedTerminal();

            var before = RawMode.Describe(terminal.Descriptor);
            await Assert.That(before.Canonical).IsTrue()
                .Because("a terminal that was already raw would make the restore invisible.");

            await Host(terminal, script);

            await Assert.That(RawMode.Describe(terminal.Descriptor)).IsEqualTo(before)
                .Because($"a child that ended {ending} still ends gg's session, and the "
                       + "settings that come back are the ones that were found - not ones "
                       + "gg considered reasonable.");
        }
    }

    [Test]
    public async Task The_alternate_screen_is_entered_and_left()
    {
        // What the child painted belongs to the child. Leaving on the alternate
        // screen puts back the scrollback a person had before gg started, which
        // is the difference between an editor and a program that scribbled on
        // their terminal.
        using var terminal = new HostedTerminal();

        await Host(terminal, "printf hello");

        var painted = terminal.Painted;
        var entered = painted.IndexOf("\u001b[?1049h", StringComparison.Ordinal);
        var left = painted.LastIndexOf("\u001b[?1049l", StringComparison.Ordinal);

        await Assert.That(entered).IsGreaterThanOrEqualTo(0);
        await Assert.That(left).IsGreaterThan(entered)
            .Because("entered before anything is painted and left after everything is.");
    }

    [Test]
    public async Task What_the_child_put_on_the_screen_reaches_the_screen_and_nothing_else()
    {
        // THE PLANT GOES IN THROUGH THE REAL PATH: a real child prints it, a
        // real pty carries it, the real emulator interprets it and the real
        // renderer paints it. Planted into an artifact directly it would prove
        // only that a string I put in one place is absent from another.
        //
        // WIDE ENOUGH FOR THE PLANT TO FIT ON ONE ROW. At the 40 columns the
        // other tests use, the 44-character line wraps and the needle arrives
        // on screen with a cursor address through the middle of it - so the
        // liveness assertion failed while the emulator was behaving perfectly.
        // A test whose plant does not survive its own screen would have been
        // "fixed" by weakening the assertion that caught it.
        using var terminal = new HostedTerminal { Columns = 80, Rows = 10 };

        await Host(terminal, $"printf 'export GH_TOKEN={Needle}'");

        await Assert.That(terminal.Painted).Contains(Needle, StringComparison.Ordinal)
            .Because("the plant has to have worked, or every absence below is vacuous.");

        // AND THE HOST KEPT NONE OF IT. A child's screen is not gg's to hold: it
        // is whatever a person's editor had open or whatever an agent printed,
        // and unlike the live channel - which survives terminal release on
        // purpose - none of this has any reason to outlive the call.
        var kept = typeof(PtyHost)
            .GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
            .Where(f => !f.IsLiteral)
            .Select(f => f.Name)
            .ToArray();

        await Assert.That(kept).IsEmpty()
            .Because("a field on the host outlives the session it was filled in, and the "
                   + "next caller inherits the last child's screen.");
    }

    [Test]
    public async Task A_resize_reaches_the_child_and_the_screen()
    {
        // A PERSON DRAGGING THE CORNER OF THEIR WINDOW, which is not an exotic
        // case - it is what anybody does when the thing they are editing is
        // wider than the pane they gave it. Without this the child keeps the
        // size it was told at spawn: an editor draws to a right margin that is
        // no longer there, and gg keeps painting rows the terminal no longer
        // has.
        using var terminal = new HostedTerminal { Columns = 80, Rows = 24 };

        // A child that waits, so there is a session to resize. It reports its
        // size AFTER the wait, which is what proves the pty was resized rather
        // than only the emulator gg paints from.
        var host = Host(terminal, "read x; stty size");

        await Assert.That(Until(() => terminal.Painted.Length > 0)).IsTrue()
            .Because("the bar goes up before the child says anything, so a painted frame is "
                   + "the earliest point at which there is a session to resize.");

        terminal.Resize(columns: 100, rows: 40);

        await Assert.That(Until(() => terminal.Painted.Contains("\u001b[40;1H", StringComparison.Ordinal)))
            .IsTrue()
            .Because("thirty-nine child rows under the bar makes the last one terminal row "
                   + "forty, and a frame that never addresses it is one still painting the "
                   + "old screen.");

        terminal.Type("\n");

        await host;

        await Assert.That(terminal.Painted).Contains("39 100", StringComparison.Ordinal)
            .Because("the CHILD was asked, and it answers with what the pty says it has - "
                   + "resizing the emulator gg paints from without resizing the pty would "
                   + "look right on screen and be wrong to every program in it.");
    }

    [Test]
    public async Task Nothing_here_writes_the_child_to_a_file()
    {
        // THE RATCHET AGAINST THE SPIKE'S OWN TRACE, which is the real leak this
        // slice has to close. It opened a file in the temp directory and wrote
        // every byte in and out of the child to it, to debug the four terminal
        // defects - so a person's editor buffer, their agent transcript and
        // anything either one echoed went to disk beside it, unredacted and
        // surviving the session. It was the right tool for a spike and it must
        // not come back with the code it was used on.
        foreach (var file in (string[])["PtyHost.cs", "PtyScreen.cs"])
        {
            var source = ConsoleSource.Text("Gg.Console", file);

            foreach (var writing in (string[])
                     ["File.", "StreamWriter", "AppendText", "WriteAllText", "Trace"])
            {
                await Assert.That(source).DoesNotContain(writing, StringComparison.Ordinal)
                    .Because($"'{writing}' in {file} is how the child's bytes reach a disk, "
                           + "and there is no diagnostic worth that.");
            }
        }
    }
}
