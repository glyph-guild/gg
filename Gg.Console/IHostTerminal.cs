namespace Gg.Console;

/// <summary>
/// The real terminal gg owns while it hosts a child in a pseudo-terminal.
/// </summary>
/// <remarks>
/// <para>
/// <b>An interface because a test needs to be able to make one.</b> CI has no
/// controlling terminal, and a host that reached for <c>Console</c> and
/// <c>/dev/tty</c> directly could only be tested by not testing it — which is
/// how four terminal defects reached a person at a keyboard instead of a build.
/// A test opens a pseudo-terminal of its own and hands it here.
/// </para>
/// <para>
/// <b>It is not an abstraction over terminals in general.</b> Five members, all
/// of them things the host cannot get any other way: how big the screen is, a
/// descriptor to put in raw mode, somewhere to read what a person typed,
/// somewhere to write what gg painted, and word that the size just changed.
/// Anything more would be a place for the terminal handling to spread to.
/// </para>
/// </remarks>
public interface IHostTerminal
{
    /// <summary>How wide the real terminal is.</summary>
    int Columns { get; }

    /// <summary>
    /// How tall the real terminal is, gg's bar included.
    /// </summary>
    /// <remarks>
    /// The whole thing, not the child's share. The host subtracts the bar in one
    /// place, and an implementation that had already subtracted it would take a
    /// second row away silently.
    /// </remarks>
    int Rows { get; }

    /// <summary>The descriptor whose line discipline the host changes.</summary>
    /// <remarks>
    /// <b>Not standard input.</b> .NET reconfigures the console's terminal when
    /// the console is first used, so raw mode applied to that descriptor is
    /// wiped and every read then blocks until Enter. This is a descriptor
    /// nothing else manages.
    /// </remarks>
    int Descriptor { get; }

    /// <summary>What the person typed, as bytes, exactly as they arrived.</summary>
    /// <remarks>
    /// <b>Bytes rather than keys.</b> <c>Console.ReadKey</c> decomposes an
    /// escape sequence into the characters that spell it, so an arrow key
    /// reaches the child as an escape followed by a bracket followed by a
    /// letter — which an editor reads as Escape, then two keystrokes in normal
    /// mode. Whatever the terminal sent is what the child is owed.
    /// </remarks>
    Stream Keystrokes { get; }

    /// <summary>Put a frame on the screen.</summary>
    void Paint(string frame);

    /// <summary>The screen changed size.</summary>
    /// <remarks>
    /// <para>
    /// <b>An event, because only the terminal can know.</b> On Unix this is
    /// <c>SIGWINCH</c>, which arrives at the process rather than at anything the
    /// host could poll — and polling the size on a timer would be a background
    /// tick in a type whose whole discipline is having no state between calls.
    /// </para>
    /// <para>
    /// <b>And because a test cannot raise a signal safely.</b> Signals are
    /// process-wide, so a test that sent one would be sending it to every other
    /// test running beside it. Behind this event a test can resize a terminal it
    /// made and assert what the child was told.
    /// </para>
    /// </remarks>
    event Action? Resized;
}
