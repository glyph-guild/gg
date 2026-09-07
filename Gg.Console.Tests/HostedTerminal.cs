using System.Text;

namespace Gg.Console.Tests;

/// <summary>
/// A terminal a test owns: a real pseudo-terminal underneath, and everything gg
/// painted on it kept for inspection.
/// </summary>
/// <remarks>
/// <para>
/// <b>Real underneath, because the parts that break are the real parts.</b> The
/// descriptor is a genuine pty slave, so <see cref="RawMode"/> changes a real
/// line discipline and a real child inherits it. What is a stand-in is only the
/// screen: <see cref="Paint"/> keeps the frames instead of drawing them, which
/// is what lets a test read what gg drew.
/// </para>
/// <para>
/// <b>Shared, after being written twice.</b> The host's tests and the editor's
/// both needed one, and the second copy came to light when the interface gained
/// <see cref="Resized"/> and only one of them compiled. A test double that
/// exists twice is one that drifts.
/// </para>
/// </remarks>
internal sealed class HostedTerminal : IHostTerminal, IDisposable
{
    private readonly PseudoTerminal _pty = PseudoTerminal.Open();
    private readonly StringBuilder _painted = new();
    private readonly Lock _lock = new();
    private FileStream? _keystrokes;

    public int Columns { get; set; } = 40;

    public int Rows { get; set; } = 10;

    public int Descriptor => _pty.Slave;

    public Stream Keystrokes => _keystrokes ??= _pty.ReadSlave();

    public event Action? Resized;

    public void Paint(string frame)
    {
        lock (_lock)
        {
            _painted.Append(frame);
        }
    }

    /// <summary>Everything gg wrote to the screen, in order.</summary>
    internal string Painted
    {
        get
        {
            lock (_lock)
            {
                return _painted.ToString();
            }
        }
    }

    /// <summary>What a person dragging the corner of their window does.</summary>
    internal void Resize(int columns, int rows)
    {
        Columns = columns;
        Rows = rows;
        Resized?.Invoke();
    }

    /// <summary>Typing, from the other end of the pseudo-terminal.</summary>
    internal void Type(string text)
    {
        using var master = _pty.WriteMaster();
        var bytes = Encoding.UTF8.GetBytes(text);
        master.Write(bytes, 0, bytes.Length);
        master.Flush();
    }

    /// <summary>Whether whoever was handed this closed it.</summary>
    /// <remarks>
    /// A real one holds an open <c>/dev/tty</c> and a signal registration, so
    /// "was it disposed" is a question with a consequence rather than a
    /// formality — and it is not observable any other way.
    /// </remarks>
    internal bool Disposed { get; private set; }

    public void Dispose()
    {
        Disposed = true;
        _keystrokes?.Dispose();
        _pty.Dispose();
    }
}
