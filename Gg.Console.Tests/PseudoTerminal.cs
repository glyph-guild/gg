using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Gg.Console.Tests;

/// <summary>A pseudo-terminal of a test's own, master and slave.</summary>
/// <remarks>
/// <para>
/// <b>The slave, not the master, and the distinction is the whole point.</b>
/// <c>posix_openpt</c> hands back the MASTER — the end a terminal emulator holds
/// — and termios settings belong to the SLAVE, the end a program believes is its
/// terminal. Asking the master about its line discipline answers about nothing,
/// which is what a first version of these tests did.
/// </para>
/// <para>
/// <b>Why a test makes one at all.</b> CI has no controlling terminal, so
/// anything that only worked on standard input could only ever be tested by not
/// testing it. Everything gg does to a terminal takes a descriptor, and a test
/// hands it one it owns.
/// </para>
/// </remarks>
internal sealed class PseudoTerminal : IDisposable
{
    // DllImport rather than LibraryImport: this project is never AOT published,
    // and LibraryImport would need AllowUnsafeBlocks turned on for a test
    // helper. Production code that ships inside the binary is a different
    // matter and uses the source-generated form.
    [DllImport("libc", SetLastError = true)]
    private static extern int posix_openpt(int flags);

    [DllImport("libc", SetLastError = true)]
    private static extern int grantpt(int fd);

    [DllImport("libc", SetLastError = true)]
    private static extern int unlockpt(int fd);

    [DllImport("libc", SetLastError = true)]
    private static extern IntPtr ptsname(int fd);

    [DllImport("libc", SetLastError = true, EntryPoint = "open")]
    private static extern int open_(string path, int flags);

    [DllImport("libc", SetLastError = true)]
    private static extern int close(int fd);

    private const int ORdWr = 2;

    /// <summary>
    /// <c>O_NOCTTY</c>, WHICH IS A DIFFERENT NUMBER ON EACH PLATFORM.
    /// </summary>
    /// <remarks>
    /// 0x20000 on macOS and 0x100 on Linux — and 0x20000 on Linux is
    /// <c>O_NOFOLLOW</c>, so the wrong constant does not fail, it asks for
    /// something else and is granted it. The same hazard as the termios offsets
    /// in <c>RawMode</c>, found the same way: by asking what the other platform
    /// calls it rather than assuming a header is a header.
    /// </remarks>
    private static int ONoctty => OperatingSystem.IsMacOS() ? 0x20000 : 0x100;

    private PseudoTerminal(int master, int slave)
    {
        Master = master;
        Slave = slave;
    }

    internal int Master { get; }

    internal int Slave { get; }

    /// <summary>
    /// Says which call failed and why, rather than answering "no".
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>THROWN, BECAUSE NOTHING HANDLES THE ALTERNATIVE.</b> This used to
    /// answer with an <c>Opened</c> flag, and all twenty-five callers did the
    /// same thing with it: assert it was true and carry on. Not one had a
    /// fallback, so the flag was twenty-five ways of writing "fail here" - and
    /// it failed saying only "this test needs a real terminal", which is the
    /// exact shape of diagnostic this subsystem has already been bitten by: one
    /// reporting the intent rather than the state.
    /// </para>
    /// <para>
    /// <b>And it can fail transiently.</b> A Linux container run flaked one
    /// RawMode test out of six and never repeated it across twenty-four further
    /// runs, with pty slots nowhere near exhausted, so what happened is unknown
    /// - unknowable, from what the failure said. The next occurrence names the
    /// call and its errno.
    /// </para>
    /// </remarks>
    private static PseudoTerminal Failed(string call, int master = -1)
    {
        var errno = Marshal.GetLastWin32Error();

        if (master > 0)
        {
            close(master);
        }

        throw new InvalidOperationException(
            $"no pseudo-terminal: {call} failed with errno {errno}. This test drives real "
          + "terminal handling against a real pty, so without one it would assert nothing.");
    }

    /// <summary>Only one thread may be part-way through opening a pty.</summary>
    /// <remarks>
    /// <para>
    /// <b><c>ptsname</c> returns a pointer to STATIC storage.</b> Its own manual
    /// says so — "this buffer is overwritten on the next call" — and POSIX does
    /// not require it to be thread-safe. These tests run in parallel and several
    /// of them open a pty, so two can interleave between the call and reading
    /// what it pointed at: the second overwrites the buffer, and the first then
    /// opens the SECOND one's slave. Nothing fails at that moment. What fails is
    /// an assertion later, in whichever test was handed a terminal another one
    /// was also using.
    /// </para>
    /// <para>
    /// <b>A lock rather than <c>ptsname_r</c>, which is glibc's and not
    /// macOS's.</b> Opening a pty is not hot enough for the difference to
    /// matter, and one lock is a smaller thing to be right about than two
    /// platform-specific declarations.
    /// </para>
    /// <para>
    /// This is a real defect in this helper whether or not it is the Linux flake
    /// that prompted the look — the call was unsynchronised and the manual says
    /// it may not be.
    /// </para>
    /// </remarks>
    private static readonly Lock Opening = new();

    internal static PseudoTerminal Open()
    {
        lock (Opening)
        {
            return OpenOne();
        }
    }

    private static PseudoTerminal OpenOne()
    {
        var master = posix_openpt(ORdWr | ONoctty);
        if (master < 0)
        {
            return Failed("posix_openpt");
        }

        if (grantpt(master) != 0)
        {
            return Failed("grantpt", master);
        }

        if (unlockpt(master) != 0)
        {
            return Failed("unlockpt", master);
        }

        var name = Marshal.PtrToStringAnsi(ptsname(master));
        if (name is null)
        {
            return Failed("ptsname", master);
        }

        var slave = open_(name, ORdWr | ONoctty);
        if (slave < 0)
        {
            return Failed($"open({name})", master);
        }

        return new PseudoTerminal(master, slave);
    }

    /// <summary>Reading the slave the way a host reads a person's keystrokes.</summary>
    /// <remarks>
    /// <c>ownsHandle: false</c>, because this type closes the descriptors and a
    /// stream that also closed them would close one twice — and by then the
    /// number may belong to something else entirely.
    /// </remarks>
    internal FileStream ReadSlave() =>
        new(new SafeFileHandle((IntPtr)Slave, ownsHandle: false), FileAccess.Read);

    /// <summary>Writing the master the way a person types.</summary>
    internal FileStream WriteMaster() =>
        new(new SafeFileHandle((IntPtr)Master, ownsHandle: false), FileAccess.Write);

    private bool _closed;

    /// <summary>Closes both ends, once.</summary>
    /// <remarks>
    /// <b>Idempotent, and not as a formality.</b> A host disposes the terminal
    /// it was handed and a test disposes the one it made, so this is genuinely
    /// called twice - and closing a descriptor twice is not harmless: between
    /// the two closes the number can be handed out again, and the second close
    /// then shuts something else's file.
    /// </remarks>
    public void Dispose()
    {
        if (_closed)
        {
            return;
        }

        _closed = true;

        if (Slave > 0)
        {
            close(Slave);
        }

        if (Master > 0)
        {
            close(Master);
        }
    }
}
