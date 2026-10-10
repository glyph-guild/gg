using System.Collections.Concurrent;
using Gg.Client;
using Gg.Contracts;
using Porta.Pty;

namespace Gg.Console;

/// <summary>
/// What a mux agent's terminal is, wherever it runs: bytes out, bytes in, a size,
/// and an end (slice seventy, ADR-0039).
/// </summary>
/// <remarks>
/// <b>The seven members <see cref="MuxAgent"/> used of Porta.Pty's connection, and no
/// more.</b> A local child's pty answers them, and so does a session on another
/// machine streamed over its `agent` channel - so the column, the switching, the
/// emulator and quitting never learn which they hold.
/// </remarks>
public interface IAgentPty : IDisposable
{
    /// <summary>Blocks for what the terminal wrote; 0 when it will write no more.</summary>
    int Read(byte[] buffer, int offset, int count);

    void Write(ReadOnlySpan<byte> bytes);

    void Resize(int columns, int rows);

    void Kill();

    bool WaitForExit(int milliseconds);

    int ExitCode { get; }

    /// <summary>Whether the agent keeps running when gg quits: it is not gg's child.</summary>
    bool OutlivesTheConsole => false;

    /// <summary>What quitting gg does to it: a child of gg ends, a session elsewhere is let go.</summary>
    void Leave() => Kill();
}

/// <summary>A child in a pty on this machine.</summary>
internal sealed class LocalPty(IPtyConnection connection) : IAgentPty
{
    public int Read(byte[] buffer, int offset, int count) => connection.ReaderStream.Read(buffer, offset, count);

    public void Write(ReadOnlySpan<byte> bytes)
    {
        connection.WriterStream.Write(bytes);
        connection.WriterStream.Flush();
    }

    public void Resize(int columns, int rows) => connection.Resize(columns, rows);

    public void Kill() => connection.Kill();

    public bool WaitForExit(int milliseconds) => connection.WaitForExit(milliseconds);

    public int ExitCode => connection.ExitCode;

    public void Dispose() => connection.Dispose();
}

/// <summary>
/// A session on another machine, as a terminal: its output frames become what is
/// read, and what is written, resized or killed becomes frames.
/// </summary>
/// <remarks>
/// <para>
/// <b>Subscribed before anything is asked of the machine</b>, so the replay that
/// follows <c>Started</c> lands here rather than nowhere.
/// </para>
/// <para>
/// <b>Kept alive across a quiet channel.</b> The runner lets a channel go after ten
/// minutes in which nothing crossed, and a person reading an idle Claude sends
/// nothing for longer than that; a list asked for now and then is enough to say
/// somebody is still there.
/// </para>
/// </remarks>
public sealed class RemotePty : IAgentPty
{
    private readonly IAgentLink _link;
    private readonly BlockingCollection<byte[]> _incoming = [];
    private readonly ManualResetEventSlim _gone = new();
    private readonly Timer? _keepAlive;
    private byte[]? _left;
    private int _exitCode = -1;

    public RemotePty(IAgentLink link, TimeSpan? keepAlive = null)
    {
        ArgumentNullException.ThrowIfNull(link);

        _link = link;
        _link.Heard += Heard;
        _link.Closed += () => End(_exitCode);

        var every = keepAlive ?? TimeSpan.FromMinutes(1);
        _keepAlive = new Timer(_ => Send(new ListAgentSessions()), null, every, every);
    }

    /// <summary>The session the machine said this terminal is attached to.</summary>
    public string? SessionId { get; private set; }

    /// <summary>Whether somebody attached later and drives now, so this one watches.</summary>
    public bool Watching { get; private set; }

    public int ExitCode => _exitCode;

    public int Read(byte[] buffer, int offset, int count)
    {
        try
        {
            var next = _left ?? _incoming.Take();
            var taken = Math.Min(next.Length, count);
            next.AsSpan(0, taken).CopyTo(buffer.AsSpan(offset));
            _left = taken < next.Length ? next[taken..] : null;
            return taken;
        }
        catch (InvalidOperationException)
        {
            return 0;
        }
    }

    public void Write(ReadOnlySpan<byte> bytes)
    {
        foreach (var chunk in AgentFrameCodec.Input(bytes.ToArray()))
        {
            Send(chunk);
        }
    }

    public void Resize(int columns, int rows) => Send(new AgentResize { Columns = columns, Rows = rows });

    public void Kill() => Send(new KillAgentSession());

    public bool OutlivesTheConsole => true;

    /// <summary>
    /// Lets go of the session and leaves it running on its machine, as a tmux detach
    /// does: the channel closes and this terminal reads its end (ADR-0039 Decision 5).
    /// </summary>
    public void Leave()
    {
        _keepAlive?.Dispose();
        End(_exitCode);
        _link.Dispose();
    }

    public bool WaitForExit(int milliseconds) => _gone.Wait(milliseconds);

    public void Dispose()
    {
        _keepAlive?.Dispose();
        _link.Dispose();
    }

    private void Heard(AgentFrame frame)
    {
        switch (frame)
        {
            case AgentOutput output:
                try
                {
                    _incoming.Add(output.Bytes);
                }
                catch (InvalidOperationException)
                {
                }

                break;
            case AgentSessionStarted started:
                SessionId = started.SessionId;
                break;
            case AgentSessionExited exited:
                End(exited.Code);
                break;
            case AgentSessionReadOnly:
                Watching = true;
                break;
        }
    }

    private void End(int code)
    {
        _exitCode = code;
        try
        {
            _incoming.CompleteAdding();
        }
        catch (ObjectDisposedException)
        {
        }

        _gone.Set();
    }

    private void Send(AgentFrame frame)
    {
        if (_gone.IsSet)
        {
            return;
        }

        try
        {
            _link.Send(frame);
        }
        catch (Exception gone) when (gone is InvalidOperationException or ObjectDisposedException or IOException)
        {
        }
    }
}

/// <summary>
/// A session on another machine while it is still being reached: a line saying so, then the
/// session's own terminal once the machine answers - or why it did not, until a key closes it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The row comes first, the machine second</b> (owner, 2026-10-10). Reaching one is an
/// introduction the machine answers on its next heartbeat, a channel, a credential and a start:
/// seconds in which a key that placed nothing reads as a key that was not heard.
/// </para>
/// <para>
/// <b>What is typed before the machine answers is dropped</b>, not queued: it was typed at a
/// line saying "reaching", not at the agent. A size is kept and told once reached.
/// </para>
/// </remarks>
public sealed class ReachingPty : IAgentPty
{
    private readonly BlockingCollection<byte[]> _said = [];
    private readonly ManualResetEventSlim _gone = new();
    private readonly Lock _lock = new();
    private volatile IAgentPty? _reached;
    private byte[]? _left;
    private bool _refused;
    private bool _letGo;
    private (int Columns, int Rows)? _size;

    /// <param name="machine">What the line calls the machine.</param>
    /// <param name="reach">Reaches it and starts or attaches, on a thread of its own: the session's terminal, or why not.</param>
    public ReachingPty(string machine, Func<(IAgentPty? Reached, string? Refused)> reach)
    {
        ArgumentNullException.ThrowIfNull(reach);

        Say($"Reaching {machine}\u2026 it answers on its next heartbeat.\r\n");
        _ = Task.Factory.StartNew(
            () => Reach(machine, reach), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }

    public int ExitCode => _reached?.ExitCode ?? -1;

    public bool OutlivesTheConsole => true;

    public int Read(byte[] buffer, int offset, int count)
    {
        if (_left is null && _said.IsCompleted)
        {
            // SAID EVERYTHING: the session's own terminal from here, or the end.
            return _reached?.Read(buffer, offset, count) ?? 0;
        }

        try
        {
            var next = _left ?? _said.Take();
            var taken = Math.Min(next.Length, count);
            next.AsSpan(0, taken).CopyTo(buffer.AsSpan(offset));
            _left = taken < next.Length ? next[taken..] : null;
            return taken;
        }
        catch (InvalidOperationException)
        {
            return _reached?.Read(buffer, offset, count) ?? 0;
        }
    }

    public void Write(ReadOnlySpan<byte> bytes)
    {
        if (_reached is { } reached)
        {
            reached.Write(bytes);
            return;
        }

        bool close;
        lock (_lock)
        {
            close = _refused;
        }

        if (close)
        {
            End();
        }
    }

    public void Resize(int columns, int rows)
    {
        lock (_lock)
        {
            _size = (columns, rows);
        }

        _reached?.Resize(columns, rows);
    }

    public void Kill()
    {
        if (_reached is { } reached)
        {
            reached.Kill();
            return;
        }

        LetGo();
    }

    public void Leave()
    {
        if (_reached is { } reached)
        {
            reached.Leave();
            return;
        }

        LetGo();
    }

    public bool WaitForExit(int milliseconds) =>
        _reached is { } reached ? reached.WaitForExit(milliseconds) : _gone.Wait(milliseconds);

    public void Dispose()
    {
        _reached?.Dispose();
        End();
    }

    private void Reach(string machine, Func<(IAgentPty? Reached, string? Refused)> reach)
    {
        IAgentPty? reached;
        string? refused;
        try
        {
            (reached, refused) = reach();
        }
        catch (Exception failed)
        {
            (reached, refused) = (null, failed.Message);
        }

        lock (_lock)
        {
            if (_letGo)
            {
                // LET GO WHILE REACHING: whatever answered is left running there, as a leave does.
                reached?.Leave();
                return;
            }

            if (reached is null)
            {
                _refused = true;
            }
            else
            {
                if (_size is { } size)
                {
                    reached.Resize(size.Columns, size.Rows);
                }

                _reached = reached;
            }
        }

        if (reached is null)
        {
            Say($"\r\n{refused ?? machine + " could not be reached."}\r\n\r\nAny key closes this row.\r\n");
            return;
        }

        // CLEARED for the session, whose own replay draws what it shows.
        Say("\u001b[2J\u001b[H");
        Complete();
    }

    private void LetGo()
    {
        lock (_lock)
        {
            _letGo = true;
        }

        End();
    }

    private void Say(string text)
    {
        try
        {
            _said.Add(System.Text.Encoding.UTF8.GetBytes(text));
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void Complete()
    {
        try
        {
            _said.CompleteAdding();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void End()
    {
        Complete();
        _gone.Set();
    }
}

/// <summary>A machine the mux may offer to start a session on.</summary>
public sealed record RemoteMachine(string Id, string Name);

/// <summary>A link to a machine's sessions, or why there is not one.</summary>
/// <param name="Delegation">
/// Mints the credential that lets the session started next act as this person, given its id,
/// or answers null when the control plane will not (ADR-0039 Amendment 2). The composition
/// root's, because only it may name the control plane; null starts every session without one.
/// </param>
public sealed record RemoteReach(
    IAgentLink? Link, string? Refused, Func<string, Gg.Contracts.DelegateAgentSession?>? Delegation = null);
