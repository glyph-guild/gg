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
    private const int ONoctty = 0x20000;

    private PseudoTerminal(int master, int slave)
    {
        Master = master;
        Slave = slave;
    }

    internal int Master { get; }

    internal int Slave { get; }

    /// <summary>Whether there is one at all.</summary>
    /// <remarks>
    /// A test that silently skipped here would assert nothing and report a pass,
    /// so callers assert on this rather than branching on it.
    /// </remarks>
    internal bool Opened => Slave > 0;

    internal static PseudoTerminal Open()
    {
        var master = posix_openpt(ORdWr | ONoctty);
        if (master < 0 || grantpt(master) != 0 || unlockpt(master) != 0)
        {
            return new PseudoTerminal(-1, -1);
        }

        var name = Marshal.PtrToStringAnsi(ptsname(master));
        if (name is null)
        {
            close(master);
            return new PseudoTerminal(-1, -1);
        }

        var slave = open_(name, ORdWr | ONoctty);
        if (slave < 0)
        {
            close(master);
            return new PseudoTerminal(-1, -1);
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

    public void Dispose()
    {
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
