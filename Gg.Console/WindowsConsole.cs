using System.Text;

namespace Gg.Console;

/// <summary>The Windows console mode flags gg changes to host a child, and what they mean.</summary>
/// <remarks>
/// <b>The Windows spelling of termios raw mode.</b> No line editing, no echo, no ctrl+c
/// handled by the console, and input delivered as the escape sequences a terminal sends;
/// on the output side, escape sequences interpreted rather than printed. Pure, so the
/// arithmetic is tested on every build, including the ones with no Windows console.
/// </remarks>
public static class ConsoleModes
{
    public const uint ProcessedInput = 0x0001;
    public const uint LineInput = 0x0002;
    public const uint EchoInput = 0x0004;
    public const uint WindowInput = 0x0008;
    public const uint MouseInput = 0x0010;
    public const uint QuickEdit = 0x0040;
    public const uint ExtendedFlags = 0x0080;
    public const uint VirtualTerminalInput = 0x0200;

    public const uint ProcessedOutput = 0x0001;
    public const uint VirtualTerminalProcessing = 0x0004;
    public const uint DisableNewlineAutoReturn = 0x0008;

    /// <summary>The input mode that hosts a child, from the mode the console had.</summary>
    /// <remarks>
    /// <b>Mouse input off, because the mouse arrives as escape sequences.</b> With virtual
    /// terminal input on, a click the child asked for comes through the same bytes as a key;
    /// console mouse records beside them would only be skipped. Quick edit is off so a click
    /// is not taken for a text selection, and Windows ignores that change unless extended
    /// flags is set with it.
    /// </remarks>
    public static uint RawInput(uint saved) =>
        (saved & ~(ProcessedInput | LineInput | EchoInput | MouseInput | QuickEdit))
        | VirtualTerminalInput | WindowInput | ExtendedFlags;

    /// <summary>The output mode that interprets what gg paints, from the mode the console had.</summary>
    public static uint PaintingOutput(uint saved) =>
        saved | ProcessedOutput | VirtualTerminalProcessing | DisableNewlineAutoReturn;

    /// <summary>An input mode, described as termios raw mode is described.</summary>
    /// <remarks>
    /// <b>The minimum is always nought</b>, because a read never waits: the host skips the
    /// events that would make it wait before it reads (see <see cref="ConsoleInput"/>).
    /// </remarks>
    public static TerminalState Describe(uint input) =>
        new(IsTerminal: true,
            Canonical: (input & LineInput) != 0,
            Echo: (input & EchoInput) != 0,
            MinimumBytes: 0);
}

/// <summary>What kind of record a Windows console input queue holds.</summary>
public enum ConsoleEventKind
{
    Key,
    Mouse,
    WindowSize,
    Menu,
    Focus,
}

/// <summary>One pending console input record, as much of it as a read needs.</summary>
public readonly record struct ConsoleEvent(ConsoleEventKind Kind, bool KeyDown, ushort VirtualKey, char Character);

/// <summary>How many pending records to take off unread, whether one was a resize, and whether text waits.</summary>
public readonly record struct ConsolePending(int Skip, bool Resized, bool Ready);

/// <summary>Which pending console events a read can take without waiting.</summary>
/// <remarks>
/// <b>Termios raw mode is set with no minimum and no wait, and every caller polls.</b> A
/// Windows console read blocks until it has text, and some records never become text: a
/// key released, a modifier pressed alone, focus, a menu, a resize. Those at the front of
/// the queue are skipped, so a read is made only when one will return at once. Order is
/// kept: a record after the first key that will become text is left for the next read.
/// </remarks>
public static class ConsoleInput
{
    public static ConsolePending Classify(IReadOnlyList<ConsoleEvent> pending)
    {
        ArgumentNullException.ThrowIfNull(pending);

        var skip = 0;
        var resized = false;

        foreach (var record in pending)
        {
            if (record.Kind == ConsoleEventKind.Key && record.KeyDown
                && (record.Character != '\0' || !IsModifier(record.VirtualKey)))
            {
                return new ConsolePending(skip, resized, Ready: true);
            }

            resized |= record.Kind == ConsoleEventKind.WindowSize;
            skip++;
        }

        return new ConsolePending(skip, resized, Ready: false);
    }

    /// <summary>A key that changes what another key means and types nothing of its own.</summary>
    private static bool IsModifier(ushort virtualKey) => virtualKey is
        0x10 or 0x11 or 0x12 // shift, ctrl, alt
        or 0x14              // caps lock
        or 0x5B or 0x5C      // the Windows keys
        or 0x90 or 0x91      // num lock, scroll lock
        or >= 0xA0 and <= 0xA5; // left and right shift, ctrl, alt
}

/// <summary>Keystrokes read as UTF-16, handed on as the UTF-8 bytes a terminal sends.</summary>
/// <remarks>
/// <b>A character can arrive in two reads</b> when it is outside the basic plane, as two
/// UTF-16 halves. The first half is held until the second comes; encoded alone it would
/// become a replacement character, and the child would be sent something nobody typed.
/// </remarks>
public sealed class KeystrokeBuffer
{
    private readonly Queue<byte> _bytes = new();
    private char? _highSurrogate;

    /// <summary>Whether bytes are waiting.</summary>
    public bool Any => _bytes.Count > 0;

    public void Append(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty)
        {
            return;
        }

        var joined = new StringBuilder(text.Length + 1);
        if (_highSurrogate is { } held)
        {
            joined.Append(held);
            _highSurrogate = null;
        }

        joined.Append(text);

        if (char.IsHighSurrogate(joined[^1]))
        {
            _highSurrogate = joined[^1];
            joined.Length--;
        }

        foreach (var b in Encoding.UTF8.GetBytes(joined.ToString()))
        {
            _bytes.Enqueue(b);
        }
    }

    public int Read(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        var read = 0;
        while (read < count && _bytes.TryDequeue(out var b))
        {
            buffer[offset + read] = b;
            read++;
        }

        return read;
    }
}
