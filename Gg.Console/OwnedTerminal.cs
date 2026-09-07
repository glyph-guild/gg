using System.Runtime.InteropServices;

namespace Gg.Console;

/// <summary>
/// The terminal this process is attached to, opened directly.
/// </summary>
/// <remarks>
/// <para>
/// <b><c>/dev/tty</c> rather than standard input.</b> .NET owns the console's
/// descriptor and reconfigures its termios the first time the console is used,
/// so raw mode applied there is silently wiped and every read afterwards blocks
/// until Enter. This is a descriptor nothing else manages.
/// </para>
/// <para>
/// <b>Null when there is not one, and that is an answer.</b> gg runs in CI,
/// behind a pipe, as a service, and on Windows — where there is no
/// <c>/dev/tty</c> to open at all. The Windows story is ConPTY and
/// <c>SetConsoleMode</c>, which slot into the same shape and are not written
/// yet; until they are, gg on Windows takes the unhosted path and keeps the
/// editor it has always had. Saying so out loud is better than a host that
/// throws on a platform nobody tested it on.
/// </para>
/// </remarks>
public sealed class OwnedTerminal : IHostTerminal, IDisposable
{
    private readonly FileStream _tty;
    private readonly PosixSignalRegistration? _winch;

    private OwnedTerminal(FileStream tty)
    {
        _tty = tty;

        // SIGWINCH IS HOW A TERMINAL SAYS IT CHANGED SIZE, and there is no other
        // way to hear it - the size is not polled anywhere, and a host that
        // polled it would need a tick in a type whose whole discipline is
        // holding no state between calls.
        //
        // The handler does nothing but raise the event. It runs on a thread the
        // runtime chose, at a moment nothing here controls, and anything
        // substantial done in one is done in the wrong place.
        //
        // GUARDED FOR WINDOWS, which has no such signal. Open() already answers
        // null there so this constructor cannot run - but the analyzer is right
        // to want it said here rather than inferred from a check three methods
        // away, because that check is exactly the kind that gets relaxed later.
        _winch = OperatingSystem.IsWindows()
            ? null
            : PosixSignalRegistration.Create(PosixSignal.SIGWINCH, _ => Resized?.Invoke());
    }

    public event Action? Resized;

    /// <summary>The terminal, or null if this process has none.</summary>
    public static OwnedTerminal? Open()
    {
        if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux())
        {
            return null;
        }

        try
        {
            // ReadWrite share: the terminal is genuinely shared - a shell, a
            // pager and gg can all hold it - and asking for exclusive use fails
            // for a reason that has nothing to do with whether it is usable.
            var tty = new FileStream(
                "/dev/tty", FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);

            var terminal = new OwnedTerminal(tty);

            // ASKED, NOT ASSUMED. /dev/tty can open and still not be a terminal
            // in the state a host needs, and the whole point of RawMode
            // reporting from the terminal is that this can be checked rather
            // than believed.
            if (!RawMode.Describe(terminal.Descriptor).IsTerminal)
            {
                terminal.Dispose();
                return null;
            }

            return terminal;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    public int Columns => Math.Max(SafeSize(() => System.Console.WindowWidth, 80), 20);

    public int Rows => Math.Max(SafeSize(() => System.Console.WindowHeight, 24), 6);

    public int Descriptor => (int)_tty.SafeFileHandle.DangerousGetHandle();

    public Stream Keystrokes => _tty;

    public void Paint(string frame)
    {
        System.Console.Write(frame);
        System.Console.Out.Flush();
    }

    /// <summary>
    /// The console's idea of its own size, or a sane one.
    /// </summary>
    /// <remarks>
    /// <c>WindowWidth</c> throws when output is redirected, which is a state gg
    /// can be started in — and a host that fell over asking how wide the screen
    /// is would fail before it could decide it had no screen.
    /// </remarks>
    private static int SafeSize(Func<int> ask, int otherwise)
    {
        try
        {
            return ask();
        }
        catch (IOException)
        {
            return otherwise;
        }
        catch (PlatformNotSupportedException)
        {
            return otherwise;
        }
    }

    public void Dispose()
    {
        // THE REGISTRATION FIRST. A handler that outlived the terminal it
        // reports on would fire into a disposed stream at the next resize, and
        // the next resize can be at any time at all.
        _winch?.Dispose();

        // The handle is not owned by anything else - this type opened it - so
        // disposing the stream closes the descriptor, which is what is wanted.
        _tty.Dispose();
    }
}
