namespace Gg.Console;

/// <summary>The Windows console mode flags gg changes to host a child, and what they mean.</summary>
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

    public static uint RawInput(uint saved) => saved;

    public static uint PaintingOutput(uint saved) => saved;

    public static TerminalState Describe(uint input) => default;
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
public static class ConsoleInput
{
    public static ConsolePending Classify(IReadOnlyList<ConsoleEvent> pending) => default;
}

/// <summary>Keystrokes read as UTF-16, handed on as the UTF-8 bytes a terminal sends.</summary>
public sealed class KeystrokeBuffer
{
    public void Append(ReadOnlySpan<char> text)
    {
    }

    public int Read(byte[] buffer, int offset, int count) => 0;
}
