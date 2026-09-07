using System.Runtime.InteropServices;

namespace Gg.Console.Tests;

/// <summary>
/// Putting a terminal in raw mode, and putting it back.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this is a unit rather than something the host does inline.</b> A
/// spike shelled out to <c>stty</c> for this, and it worked — at the cost of
/// three process spawns per session, a dependency on <c>/bin/stty</c> being on
/// the machine, parsing <c>stty -a</c> output to find out what happened, and no
/// Windows story at all. It is the one part of that spike that was never
/// production shaped.
/// </para>
/// <para>
/// <b>It takes a descriptor rather than assuming standard input</b>, and that
/// is testability changing the design for the better: a test can make a
/// pseudo-terminal of its own and drive the real code against it, instead of
/// asserting around whatever the test runner happened to inherit. CI has no
/// controlling terminal, so a version that only worked on stdin could only ever
/// be tested by not testing it.
/// </para>
/// <para>
/// <b>The reporting is the point as much as the switching.</b> The spike's
/// trace said <c>rawMode=applied</c> because <c>stty -g</c> had returned
/// something — which says a terminal was reachable, not that raw mode took. It
/// had not, and believing it cost a round of debugging in the wrong place. What
/// this reports is read back out of the terminal.
/// </para>
/// </remarks>
public class RawModeTests
{
    /// <summary>A pseudo-terminal of this test's own.</summary>
    /// <remarks>
    /// <c>posix_openpt</c> rather than <c>openpty</c>: it is in libc on both
    /// macOS and Linux, where <c>openpty</c> lives in libutil on one of them and
    /// not the other.
    /// </remarks>
    // DllImport rather than LibraryImport: this project is never AOT
    // published, and LibraryImport would need AllowUnsafeBlocks turned on for
    // a test helper. RawMode itself is a different matter - it ships inside the
    // AOT binary and uses the source-generated form.
    [DllImport("libc", SetLastError = true)]
    private static extern int posix_openpt(int flags);

    [DllImport("libc", SetLastError = true)]
    private static extern int close(int fd);

    private const int ORdWr = 2;

    private static int OpenTerminal()
    {
        var fd = posix_openpt(ORdWr);
        return fd;
    }

    [Test]
    public async Task Raw_mode_turns_off_the_line_discipline_and_the_echo()
    {
        // The two properties the host actually needs. Canonical mode holds
        // input until Enter, which makes a keystroke-driven child unusable;
        // echo draws what is typed on top of what gg paints.
        var fd = OpenTerminal();
        await Assert.That(fd).IsGreaterThan(0)
            .Because("this test needs a real terminal to configure, and without one it "
                   + "would be asserting about nothing.");

        try
        {
            var saved = RawMode.Enter(fd);

            await Assert.That(saved).IsNotNull()
                .Because("a terminal that could be configured has settings to put back.");

            var raw = RawMode.Describe(fd);

            await Assert.That(raw.Canonical).IsFalse();
            await Assert.That(raw.Echo).IsFalse();
            await Assert.That(raw.MinimumBytes).IsEqualTo(0)
                .Because("a read must answer immediately with whatever is waiting, or the "
                       + "input loop has to cancel one - and a cancelled read on a tty does "
                       + "not abort the syscall, it stays pending and eats what arrives next.");
        }
        finally
        {
            close(fd);
        }
    }

    [Test]
    public async Task What_it_reports_is_read_back_rather_than_remembered()
    {
        // THE DEFECT THIS TYPE EXISTS BECAUSE OF. A previous version reported
        // success on the strength of having been asked, and was believed while
        // reads blocked forever. Describe must answer from the terminal, so
        // changing the terminal behind its back changes what it says.
        var fd = OpenTerminal();
        await Assert.That(fd).IsGreaterThan(0);

        try
        {
            var saved = RawMode.Enter(fd);
            await Assert.That(RawMode.Describe(fd).Canonical).IsFalse();

            RawMode.Restore(fd, saved);

            await Assert.That(RawMode.Describe(fd).Canonical).IsTrue()
                .Because("restoring put the line discipline back, and a describe that "
                       + "remembered what it had done would still be claiming raw.");
        }
        finally
        {
            close(fd);
        }
    }

    [Test]
    public async Task Restoring_puts_back_exactly_what_was_there()
    {
        // Not "puts back something reasonable". The terminal belongs to whoever
        // ran gg, and a session that ends with different settings from the ones
        // it found is one that changed a person's shell.
        var fd = OpenTerminal();
        await Assert.That(fd).IsGreaterThan(0);

        try
        {
            var before = RawMode.Describe(fd);
            var saved = RawMode.Enter(fd);
            RawMode.Restore(fd, saved);

            await Assert.That(RawMode.Describe(fd)).IsEqualTo(before);
        }
        finally
        {
            close(fd);
        }
    }

    [Test]
    public async Task A_descriptor_that_is_not_a_terminal_is_said_rather_than_thrown()
    {
        // gg runs where there is no terminal - a CI runner, a pipe, a service.
        // The host has to be able to ask and be told no, because throwing here
        // would take the console down for a question it asked itself.
        var saved = RawMode.Enter(-1);

        await Assert.That(saved).IsNull();
        await Assert.That(RawMode.Describe(-1).IsTerminal).IsFalse()
            .Because("what it reports has to distinguish 'not a terminal' from 'a terminal "
                   + "in some state', or a caller cannot tell a missing tty from a raw one.");
    }

    [Test]
    public async Task Restoring_nothing_is_not_an_error()
    {
        // The pair to the line above: a host that could not enter raw mode still
        // runs its finally block, and that must not be where it fails.
        RawMode.Restore(-1, null);
        RawMode.Restore(0, null);

        await Assert.That(true).IsTrue()
            .Because("reaching here without throwing is the assertion.");
    }

    [Test]
    public async Task Nothing_here_shells_out()
    {
        // THE RATCHET AGAINST THE SPIKE'S VERSION. It called /bin/stty three
        // times a session and parsed `stty -a` to find out what had happened -
        // which costs a process spawn per call, depends on the binary being
        // there, and has no answer at all on Windows.
        var source = ConsoleSource.Text("Gg.Console", "RawMode.cs");

        foreach (var shelled in (string[])["stty", "Process.Start", "ProcessStartInfo"])
        {
            await Assert.That(source).DoesNotContain(shelled, StringComparison.Ordinal)
                .Because($"'{shelled}' is how this was done in the spike, and the reasons it "
                       + "changed are in this file's own remarks.");
        }
    }
}
