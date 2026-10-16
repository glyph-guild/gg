using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Gg.Console;

/// <summary>
/// The Windows console this process is attached to, opened directly: the host terminal
/// <see cref="OwnedTerminal"/> is on macOS and Linux.
/// </summary>
/// <remarks>
/// <para>
/// <b><c>CONIN$</c> and <c>CONOUT$</c> rather than standard input and output</b>, for the
/// reason <c>/dev/tty</c> is opened elsewhere: standard handles can be redirected, and
/// these always name the console itself.
/// </para>
/// <para>
/// <b>Escape sequences both ways.</b> <see cref="RawMode"/> puts the input in virtual
/// terminal mode, so a person's keys and clicks arrive as the bytes a terminal sends, and
/// the output interprets the escape sequences gg paints. Everything above this type stays
/// the same on every platform.
/// </para>
/// <para>
/// <b>The resize is an input record.</b> Windows has no SIGWINCH; it queues a window-size
/// event, which the keystroke stream takes off the queue and reports as
/// <see cref="Resized"/>. Every caller reads in a loop, so the report is as prompt as a
/// signal and needs no timer.
/// </para>
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class WindowsTerminal : IHostTerminal, IDisposable
{
    private readonly nint _input;
    private readonly nint _output;
    private readonly ConsoleKeystrokes _keystrokes;

    private WindowsTerminal(nint input, nint output)
    {
        _input = input;
        _output = output;
        _keystrokes = new ConsoleKeystrokes(input, () => Resized?.Invoke());
    }

    public event Action? Resized;

    /// <summary>The console, or null if this process has none.</summary>
    public static WindowsTerminal? Open()
    {
        var input = WindowsConsoleNative.Open("CONIN$");
        if (input == WindowsConsoleNative.InvalidHandle)
        {
            return null;
        }

        var output = WindowsConsoleNative.Open("CONOUT$");
        if (output == WindowsConsoleNative.InvalidHandle)
        {
            WindowsConsoleNative.CloseHandle(input);
            return null;
        }

        // ASKED, NOT ASSUMED, as on Unix: a handle can open and still not be a console.
        if (!WindowsConsoleNative.GetConsoleMode(input, out _)
            || !WindowsConsoleNative.GetConsoleMode(output, out _))
        {
            WindowsConsoleNative.CloseHandle(input);
            WindowsConsoleNative.CloseHandle(output);
            return null;
        }

        return new WindowsTerminal(input, output);
    }

    public int Columns => Math.Max(Window()?.Columns ?? 80, 20);

    public int Rows => Math.Max(Window()?.Rows ?? 24, 6);

    /// <summary>The input handle, which <see cref="RawMode"/> takes on Windows as the descriptor.</summary>
    public int Descriptor => (int)_input;

    public Stream Keystrokes => _keystrokes;

    public void Paint(string frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        WindowsConsoleNative.WriteConsoleW(_output, frame, (uint)frame.Length, out _, 0);
    }

    private (int Columns, int Rows)? Window()
    {
        if (!WindowsConsoleNative.GetConsoleScreenBufferInfo(_output, out var info))
        {
            return null;
        }

        return (info.WindowRight - info.WindowLeft + 1, info.WindowBottom - info.WindowTop + 1);
    }

    public void Dispose()
    {
        _keystrokes.Dispose();
        WindowsConsoleNative.CloseHandle(_input);
        WindowsConsoleNative.CloseHandle(_output);
    }
}

/// <summary>
/// What a person types at a Windows console, as bytes, without ever waiting.
/// </summary>
/// <remarks>
/// A read returns nought when nothing is waiting, as a termios read with no minimum does.
/// It skips the records that would make a console read wait (<see cref="ConsoleInput"/>),
/// reports a skipped resize, and reads only when text is ready.
/// </remarks>
[SupportedOSPlatform("windows")]
internal sealed class ConsoleKeystrokes(nint input, Action resized) : Stream
{
    private const int Batch = 64;
    private readonly KeystrokeBuffer _buffer = new();
    private readonly WindowsConsoleNative.InputRecord[] _records = new WindowsConsoleNative.InputRecord[Batch];
    private readonly char[] _chars = new char[512];
    private readonly List<ConsoleEvent> _pending = new(Batch);

    public override int Read(byte[] buffer, int offset, int count)
    {
        while (!_buffer.Any)
        {
            if (!WindowsConsoleNative.GetNumberOfConsoleInputEvents(input, out var waiting) || waiting == 0)
            {
                return 0;
            }

            if (!WindowsConsoleNative.PeekConsoleInputW(input, _records, Batch, out var peeked) || peeked == 0)
            {
                return 0;
            }

            _pending.Clear();
            for (var i = 0; i < peeked; i++)
            {
                _pending.Add(Event(_records[i]));
            }

            var next = ConsoleInput.Classify(_pending);
            if (next.Skip > 0)
            {
                WindowsConsoleNative.ReadConsoleInputW(input, _records, (uint)next.Skip, out _);
            }

            if (next.Resized)
            {
                resized();
            }

            if (!next.Ready)
            {
                // EVERYTHING PEEKED WAS SKIPPED. More may be queued behind a full batch, so
                // look again; an empty queue answers nought above.
                if (next.Skip == peeked && peeked == Batch)
                {
                    continue;
                }

                return 0;
            }

            if (!WindowsConsoleNative.ReadConsoleW(input, _chars, (uint)_chars.Length, out var read, 0) || read == 0)
            {
                return 0;
            }

            _buffer.Append(_chars.AsSpan(0, (int)read));
        }

        return _buffer.Read(buffer, offset, count);
    }

    private static ConsoleEvent Event(WindowsConsoleNative.InputRecord record) => record.EventType switch
    {
        WindowsConsoleNative.KeyEvent => new ConsoleEvent(
            ConsoleEventKind.Key, record.KeyDown != 0, record.VirtualKeyCode, (char)record.UnicodeChar),
        WindowsConsoleNative.MouseEvent => new ConsoleEvent(ConsoleEventKind.Mouse, false, 0, '\0'),
        WindowsConsoleNative.WindowBufferSizeEvent => new ConsoleEvent(ConsoleEventKind.WindowSize, false, 0, '\0'),
        WindowsConsoleNative.MenuEvent => new ConsoleEvent(ConsoleEventKind.Menu, false, 0, '\0'),
        _ => new ConsoleEvent(ConsoleEventKind.Focus, false, 0, '\0'),
    };

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

/// <summary>The Windows console calls the host terminal and raw mode make.</summary>
[SupportedOSPlatform("windows")]
internal static class WindowsConsoleNative
{
    internal const ushort KeyEvent = 0x0001;
    internal const ushort MouseEvent = 0x0002;
    internal const ushort WindowBufferSizeEvent = 0x0004;
    internal const ushort MenuEvent = 0x0008;

    internal static readonly nint InvalidHandle = -1;

    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint OpenExisting = 3;

    /// <summary>An INPUT_RECORD, as much of it as a key or the record's kind needs.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 20)]
    internal struct InputRecord
    {
        [FieldOffset(0)] public ushort EventType;
        [FieldOffset(4)] public int KeyDown;
        [FieldOffset(10)] public ushort VirtualKeyCode;
        [FieldOffset(14)] public ushort UnicodeChar;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ScreenBufferInfo
    {
        public short SizeX;
        public short SizeY;
        public short CursorX;
        public short CursorY;
        public ushort Attributes;
        public short WindowLeft;
        public short WindowTop;
        public short WindowRight;
        public short WindowBottom;
        public short MaximumX;
        public short MaximumY;
    }

    internal static nint Open(string name) =>
        CreateFileW(name, GenericRead | GenericWrite, FileShareRead | FileShareWrite, 0, OpenExisting, 0, 0);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern nint CreateFileW(
        string name, uint access, uint share, nint security, uint disposition, uint flags, nint template);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool CloseHandle(nint handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool GetConsoleMode(nint handle, out uint mode);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool SetConsoleMode(nint handle, uint mode);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool GetConsoleScreenBufferInfo(nint handle, out ScreenBufferInfo info);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool GetNumberOfConsoleInputEvents(nint handle, out uint count);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool PeekConsoleInputW(nint handle, [Out] InputRecord[] records, uint length, out uint read);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool ReadConsoleInputW(nint handle, [Out] InputRecord[] records, uint length, out uint read);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern bool ReadConsoleW(nint handle, [Out] char[] buffer, uint length, out uint read, nint control);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern bool WriteConsoleW(nint handle, string text, uint length, out uint written, nint reserved);
}
