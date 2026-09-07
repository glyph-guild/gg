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

    [DllImport("libc", SetLastError = true)]
    private static extern int grantpt(int fd);

    [DllImport("libc", SetLastError = true)]
    private static extern int unlockpt(int fd);

    [DllImport("libc", SetLastError = true)]
    private static extern IntPtr ptsname(int fd);

    [DllImport("libc", SetLastError = true, EntryPoint = "open")]
    private static extern int open_(string path, int flags);

    private const int ORdWr = 2;
    private const int ONoctty = 0x20000;

    /// <summary>
    /// The SLAVE side of a fresh pseudo-terminal.
    /// </summary>
    /// <remarks>
    /// <b>The slave, not the master, and the distinction is the whole point.</b>
    /// <c>posix_openpt</c> hands back the master - the end a terminal EMULATOR
    /// holds - and termios settings belong to the slave, the end a program
    /// believes is its terminal. Asking the master about its line discipline
    /// answers about nothing, which is what a first version of this test did.
    /// </remarks>
    private static (int Master, int Slave) OpenTerminal()
    {
        var master = posix_openpt(ORdWr | ONoctty);
        if (master < 0 || grantpt(master) != 0 || unlockpt(master) != 0)
        {
            return (-1, -1);
        }

        var name = Marshal.PtrToStringAnsi(ptsname(master));
        if (name is null)
        {
            close(master);
            return (-1, -1);
        }

        var slave = open_(name, ORdWr | ONoctty);
        if (slave < 0)
        {
            close(master);
            return (-1, -1);
        }

        return (master, slave);
    }

    [Test]
    public async Task Raw_mode_turns_off_the_line_discipline_and_the_echo()
    {
        // The two properties the host actually needs. Canonical mode holds
        // input until Enter, which makes a keystroke-driven child unusable;
        // echo draws what is typed on top of what gg paints.
        var (master, fd) = OpenTerminal();
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
            close(master);
        }
    }

    [Test]
    public async Task What_it_reports_is_read_back_rather_than_remembered()
    {
        // THE DEFECT THIS TYPE EXISTS BECAUSE OF. A previous version reported
        // success on the strength of having been asked, and was believed while
        // reads blocked forever. Describe must answer from the terminal, so
        // changing the terminal behind its back changes what it says.
        var (master, fd) = OpenTerminal();
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
            close(master);
        }
    }

    [Test]
    public async Task Restoring_puts_back_exactly_what_was_there()
    {
        // Not "puts back something reasonable". The terminal belongs to whoever
        // ran gg, and a session that ends with different settings from the ones
        // it found is one that changed a person's shell.
        var (master, fd) = OpenTerminal();
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
            close(master);
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

        // Reaching here is the assertion; this states it against something the
        // analyzer will accept, and something a reader can see is a
        // consequence of the two calls above rather than of nothing.
        await Assert.That(RawMode.Describe(-1).IsTerminal).IsFalse()
            .Because("restoring nothing changed nothing, including whether -1 is a tty.");
    }

    [Test]
    public async Task Nothing_here_shells_out()
    {
        // THE RATCHET AGAINST THE SPIKE'S VERSION. It called /bin/stty three
        // times a session and parsed `stty -a` to find out what had happened -
        // which costs a process spawn per call, depends on the binary being
        // there, and has no answer at all on Windows.
        var source = ConsoleSource.Text("Gg.Console", "RawMode.cs");

        // THE MECHANISM, NOT THE WORD. A first version of this forbade the
        // string "stty" anywhere in the file and fired on the comment
        // explaining why that program is no longer used - which is prose being
        // punished for describing the decision the guard exists to enforce.
        // What must not come back is starting a process.
        foreach (var spawning in (string[])
            ["Process.Start", "ProcessStartInfo", "System.Diagnostics"])
        {
            await Assert.That(source).DoesNotContain(spawning, StringComparison.Ordinal)
                .Because($"'{spawning}' is how the spike did this: a process spawn per call, "
                       + "a dependency on a binary being present, and its output parsed to "
                       + "discover what had happened.");
        }

        // And the reason it is worth a test at all: the calls that replaced it.
        foreach (var call in (string[])["tcgetattr", "tcsetattr"])
        {
            await Assert.That(source).Contains(call, StringComparison.Ordinal)
                .Because("a guard that only forbids leaves the field open to a third way of "
                       + "doing it; this says which way is meant.");
        }
    }
}
