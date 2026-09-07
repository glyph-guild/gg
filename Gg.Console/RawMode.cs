using System.Runtime.InteropServices;

namespace Gg.Console;

/// <summary>What a terminal is actually set to, read back from it.</summary>
/// <remarks>
/// <b>Reported rather than remembered.</b> The version this replaces answered
/// "applied" on the strength of having asked, and was believed while reads
/// blocked forever. Every value here comes from the terminal at the moment it
/// is asked.
/// </remarks>
/// <param name="IsTerminal">
/// Whether the descriptor is one at all. Distinguished from a terminal in some
/// state, because a caller that cannot tell those apart will treat a pipe as a
/// cooked tty and wait for a line that never comes.
/// </param>
/// <param name="Canonical">Whether input is held until Enter.</param>
/// <param name="Echo">Whether the terminal draws what is typed.</param>
/// <param name="MinimumBytes">
/// How many bytes a read waits for. Zero means it answers immediately with
/// whatever is there.
/// </param>
public readonly record struct TerminalState(
    bool IsTerminal, bool Canonical, bool Echo, int MinimumBytes);

/// <summary>The settings a terminal had before gg changed them.</summary>
/// <remarks>
/// Opaque on purpose. It is a <c>struct termios</c> whose layout differs by
/// platform, and nothing outside this file has any business reading it — what
/// it is FOR is being handed back to <see cref="RawMode.Restore"/> unchanged.
/// </remarks>
public sealed class TerminalSettings
{
    internal TerminalSettings(byte[] raw) => Raw = raw;

    internal byte[] Raw { get; }
}

/// <summary>
/// Puts a terminal in raw mode while gg hosts a child, and puts it back.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why gg has to do this at all.</b> When gg hands the terminal to
/// <c>$EDITOR</c> the editor owns the tty and sets its own modes. Hosting a
/// child in a pseudo-terminal instead means gg owns the real terminal, and what
/// is in force is whatever the shell left: canonical mode with echo, which
/// holds input until Enter and draws every keystroke on top of what gg paints.
/// </para>
/// <para>
/// <b>Through termios rather than <c>stty</c>.</b> The spike shelled out, which
/// cost a process spawn per call, depended on the binary being present, and
/// needed <c>stty -a</c> output parsed to discover what had happened. This is
/// the same three system calls that program makes.
/// </para>
/// <para>
/// <b>A descriptor, not standard input.</b> .NET reconfigures the console's
/// terminal when the console is first used, so the host opens <c>/dev/tty</c>
/// and works on a descriptor nothing else manages — and a test can hand this a
/// pseudo-terminal of its own rather than asserting around what the runner
/// inherited.
/// </para>
/// </remarks>
public static class RawMode
{
    /// <summary>
    /// A buffer comfortably larger than <c>struct termios</c> on either
    /// platform.
    /// </summary>
    /// <remarks>
    /// macOS needs 72 bytes and Linux 60. The calls write and read only as much
    /// as the structure actually is, so the excess is never touched — and a
    /// number that is too small would be a stack smash rather than a wrong
    /// answer, which is why it is generous rather than exact.
    /// </remarks>
    private const int TermiosBytes = 128;

    /// <summary>Apply the change immediately, discarding nothing.</summary>
    private const int TcsaNow = 0;

    // WHERE THE FIELDS ARE, AND THEY ARE NOT IN THE SAME PLACE. macOS declares
    // the four flag words as `unsigned long` and Linux as `unsigned int`, so
    // every offset after the first differs - and the two disagree about which
    // slot in c_cc holds VMIN and VTIME as well. Getting one of these wrong
    // reads a neighbouring field and is silent.
    private static int LocalFlagsOffset => OperatingSystem.IsMacOS() ? 24 : 12;

    private static int LocalFlagsWidth => OperatingSystem.IsMacOS() ? 8 : 4;

    /// <summary>Where <c>c_cc</c> begins.</summary>
    /// <remarks>
    /// Linux has a <c>c_line</c> byte between the flags and the array; macOS
    /// does not.
    /// </remarks>
    private static int ControlCharsOffset => OperatingSystem.IsMacOS() ? 32 : 17;

    private static int MinIndex => OperatingSystem.IsMacOS() ? 16 : 6;

    private static int TimeIndex => OperatingSystem.IsMacOS() ? 17 : 5;

    // ICANON and ECHO are also numbered differently. ECHO happens to be 8 on
    // both; ICANON is 0x100 on macOS and 0x2 on Linux, and assuming they match
    // because one of them does is the mistake this comment exists to prevent.
    private static ulong Canonical => OperatingSystem.IsMacOS() ? 0x00000100UL : 0x00000002UL;

    private const ulong Echo = 0x00000008UL;

    [DllImport("libc", SetLastError = true)]
    private static extern int tcgetattr(int fd, byte[] termios);

    [DllImport("libc", SetLastError = true)]
    private static extern int tcsetattr(int fd, int actions, byte[] termios);

    [DllImport("libc", SetLastError = true)]
    private static extern void cfmakeraw(byte[] termios);

    [DllImport("libc", SetLastError = true)]
    private static extern int isatty(int fd);

    /// <summary>
    /// Switches to raw, and answers with what was there to put back.
    /// </summary>
    /// <remarks>
    /// <b>Null means it could not, and that is an answer rather than a
    /// failure.</b> gg runs where there is no terminal — a CI runner, a pipe, a
    /// service — and a host that threw here would take the console down over a
    /// question it asked itself.
    /// </remarks>
    public static TerminalSettings? Enter(int fd)
    {
        var saved = new byte[TermiosBytes];

        if (!IsTerminal(fd) || tcgetattr(fd, saved) != 0)
        {
            return null;
        }

        var raw = (byte[])saved.Clone();

        // cfmakeraw is the same function stty's `raw` uses, and it exists on
        // both platforms - which is why none of the flag bits it clears have to
        // be spelled out here.
        cfmakeraw(raw);

        // VMIN 0 / VTIME 0: a read answers immediately with whatever is
        // waiting, and with nothing when there is nothing. That is what lets
        // the input loop poll instead of cancelling a read - and a cancelled
        // read on a tty does not abort the syscall, it stays pending and
        // swallows the bytes that arrive next.
        raw[ControlCharsOffset + MinIndex] = 0;
        raw[ControlCharsOffset + TimeIndex] = 0;

        return tcsetattr(fd, TcsaNow, raw) == 0 ? new TerminalSettings(saved) : null;
    }

    /// <summary>
    /// Puts back exactly what was there.
    /// </summary>
    /// <remarks>
    /// <b>Exactly, rather than something reasonable.</b> The terminal belongs to
    /// whoever ran gg, and a session that ends with settings it chose rather
    /// than the ones it found has changed a person's shell.
    /// <para>
    /// A null does nothing, because the caller that could not enter raw mode
    /// still runs its finally block and that must not be where it fails.
    /// </para>
    /// </remarks>
    public static void Restore(int fd, TerminalSettings? saved)
    {
        if (saved is not null && IsTerminal(fd))
        {
            tcsetattr(fd, TcsaNow, saved.Raw);
        }
    }

    /// <summary>What the terminal is set to now.</summary>
    public static TerminalState Describe(int fd)
    {
        var current = new byte[TermiosBytes];

        if (!IsTerminal(fd) || tcgetattr(fd, current) != 0)
        {
            return new TerminalState(IsTerminal: false, Canonical: false, Echo: false, MinimumBytes: 0);
        }

        var flags = LocalFlags(current);

        return new TerminalState(
            IsTerminal: true,
            Canonical: (flags & Canonical) != 0,
            Echo: (flags & Echo) != 0,
            MinimumBytes: current[ControlCharsOffset + MinIndex]);
    }

    private static bool IsTerminal(int fd) => fd >= 0 && isatty(fd) == 1;

    /// <summary>
    /// <c>c_lflag</c>, whichever width this platform declares it.
    /// </summary>
    private static ulong LocalFlags(byte[] termios) =>
        LocalFlagsWidth == 8
            ? BitConverter.ToUInt64(termios, LocalFlagsOffset)
            : BitConverter.ToUInt32(termios, LocalFlagsOffset);
}
