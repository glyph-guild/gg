using Gg.Contracts;

namespace Gg.Runner;

/// <summary>How a session's agent is asked for.</summary>
/// <param name="Directory">Where it runs: a root this machine allows, or below one.</param>
/// <param name="SessionId">The id the agent is started with, so it can be resumed by it.</param>
/// <param name="Resume">Whether to carry an ended conversation on rather than start one.</param>
/// <param name="Environment">What goes in the child's environment: the agent's own token, never an argument.</param>
public sealed record AgentSessionStart(
    string Directory,
    int Columns,
    int Rows,
    string SessionId,
    bool Resume,
    IReadOnlyDictionary<string, string> Environment);

/// <summary>
/// Starts the machine's agent in a terminal of its own.
/// </summary>
/// <remarks>
/// <b>A port, because this project may not allocate a terminal</b>
/// (<c>NoTerminalTests</c>) - agent login's shape (<c>IRunAnAgentLogin</c>): the
/// runner declares what it needs and the CLI, which may, implements it.
/// </remarks>
public interface IHostAgentSessions
{
    Task<IAgentSessionChild> StartAsync(AgentSessionStart start, CancellationToken cancellationToken);
}

/// <summary>A running agent, as bytes in and bytes out.</summary>
public interface IAgentSessionChild : IDisposable
{
    /// <summary>Blocks for what the agent's terminal wrote; 0 when it will write no more.</summary>
    int Read(byte[] buffer);

    void Write(ReadOnlySpan<byte> bytes);

    void Resize(int columns, int rows);

    void Kill();

    Task<int> Exited { get; }
}

/// <summary>A console attached to a session: told what it writes, that it ended, and when it stops driving.</summary>
public interface IAgentViewer
{
    void Output(ReadOnlySpan<byte> bytes);

    void Exited(int code);

    void ReadOnly();
}

/// <summary>
/// The ad hoc agent sessions this machine holds (slice seventy, ADR-0039).
/// </summary>
/// <remarks>
/// <para>
/// <b>Bookkeeping over bytes, and nothing else.</b> The terminal is the CLI's, the
/// channel is the server's; this decides whether a session may start, where, and
/// what each one has written lately.
/// </para>
/// <para>
/// <b>The refusals are this machine's to say</b>, in sentences, because they are
/// facts about this machine: it is flying, the directory is not one its owner
/// allowed, it already holds as many as it will.
/// </para>
/// </remarks>
public sealed class AgentSessions(
    IHostAgentSessions host,
    IReadOnlyList<string> roots,
    Func<bool> flying,
    Func<DateTimeOffset> now,
    Func<IReadOnlyDictionary<string, string>>? environment = null) : IDisposable
{
    /// <summary>How many live sessions one machine holds at once.</summary>
    public const int MostSessions = 3;

    /// <summary>How much recent output a session keeps to show the next console.</summary>
    public const int RingBytes = 256 * 1024;

    /// <summary>How long a session may go with no byte either way before it is ended.</summary>
    public static readonly TimeSpan IdleLimit = TimeSpan.FromHours(4);

    /// <summary>How long an ended session is still listed, so it can be resumed by its id.</summary>
    public static readonly TimeSpan EndedKept = TimeSpan.FromHours(24);

    private readonly List<AgentSession> _sessions = [];
    private readonly Lock _gate = new();

    private readonly IReadOnlyList<string> _roots =
        [.. roots.Select(r => Path.TrimEndingDirectorySeparator(Path.GetFullPath(r)))];

    /// <summary>A session, or why there is not one.</summary>
    public sealed record Opened(AgentSession? Session, string? Refused);

    /// <summary>Start the agent, or resume an ended session asked for by its id.</summary>
    public async Task<Opened> StartAsync(StartAgentSession asked, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(asked);

        if (flying())
        {
            return Refused("This machine is running a flight, and takes no new session while it "
                         + "does: one agent subscription and one working tree. Try again when it lands.");
        }

        if (Where(asked.Directory) is not { } directory)
        {
            return Refused($"'{asked.Directory}' is not under a root this machine's owner allowed "
                         + $"(agent-session-roots: {string.Join(", ", _roots)}).");
        }

        AgentSession? ended = null;

        lock (_gate)
        {
            if (asked.SessionId is { Length: > 0 } id
                && _sessions.FirstOrDefault(s => s.Id == id) is { } existing)
            {
                if (existing.Alive)
                {
                    return Refused($"Session {id} is already running here; attach to it instead.");
                }

                ended = existing;
            }

            if (_sessions.Count(s => s.Alive) >= MostSessions)
            {
                return Refused($"This machine already holds {MostSessions} sessions, which is as "
                             + "many as it will. End one first.");
            }
        }

        var sessionId = asked.SessionId is { Length: > 0 } given ? given : Guid.NewGuid().ToString();

        var child = await host.StartAsync(
            new AgentSessionStart(
                directory,
                Math.Max(asked.Columns, 20),
                Math.Max(asked.Rows, 5),
                sessionId,
                Resume: ended is not null,
                environment?.Invoke() ?? new Dictionary<string, string>()),
            cancellationToken);

        var session = new AgentSession(sessionId, directory, now(), child, now);

        lock (_gate)
        {
            if (ended is not null)
            {
                _sessions.Remove(ended);
            }

            _sessions.Add(session);
        }

        return new Opened(session, null);
    }

    /// <summary>The live session with this id, or why it cannot be attached to.</summary>
    public Opened Find(string sessionId)
    {
        lock (_gate)
        {
            return _sessions.FirstOrDefault(s => s.Id == sessionId) switch
            {
                { Alive: true } live => new Opened(live, null),
                { } => Refused($"Session {sessionId} has ended. Start it again by its id to resume it."),
                null => Refused($"This machine holds no session {sessionId}."),
            };
        }
    }

    /// <summary>What this machine holds, for a list frame and the heartbeat.</summary>
    public IReadOnlyList<AgentSessionStanding> Standings()
    {
        lock (_gate)
        {
            return [.. _sessions.Select(s => s.Standing())];
        }
    }

    /// <summary>Ends what has been idle past the limit, and forgets what ended long ago. Called on the beat.</summary>
    public void Sweep()
    {
        List<AgentSession> idle;

        lock (_gate)
        {
            var at = now();
            idle = [.. _sessions.Where(s => s.Alive && at - s.LastActivity > IdleLimit)];
            _sessions.RemoveAll(s => !s.Alive && at - s.LastActivity > EndedKept);
        }

        foreach (var session in idle)
        {
            session.Kill();
        }
    }

    public void Dispose()
    {
        List<AgentSession> all;

        lock (_gate)
        {
            all = [.. _sessions];
        }

        foreach (var session in all)
        {
            session.Kill();
            session.Dispose();
        }
    }

    private static Opened Refused(string because) => new(null, because);

    /// <summary>The directory to run in, when it is a root or under one; null otherwise.</summary>
    private string? Where(string? asked)
    {
        if (_roots.Count == 0)
        {
            return null;
        }

        if (asked is not { Length: > 0 })
        {
            return _roots[0];
        }

        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(asked));

        var under = _roots.Any(root =>
            string.Equals(full, root, StringComparison.Ordinal)
            || full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal));

        return under && Directory.Exists(full) ? full : null;
    }
}

/// <summary>One agent this machine is running, and the consoles attached to it.</summary>
public sealed class AgentSession : IDisposable
{
    private readonly IAgentSessionChild _child;
    private readonly Func<DateTimeOffset> _now;
    private readonly Lock _gate = new();
    private readonly byte[] _ring = new byte[AgentSessions.RingBytes];
    private readonly List<IAgentViewer> _viewers = [];

    private long _written;
    private int _ringStart;
    private int _ringLength;
    private IAgentViewer? _driver;
    private int _columns;
    private int _rows;

    internal AgentSession(
        string id, string directory, DateTimeOffset started, IAgentSessionChild child, Func<DateTimeOffset> now)
    {
        Id = id;
        Directory = directory;
        StartedAt = started;
        LastActivity = started;
        _child = child;
        _now = now;

        var reader = new Thread(Pump) { IsBackground = true, Name = $"gg agent session: {id}" };
        reader.Start();
    }

    public string Id { get; }

    public string Directory { get; }

    public DateTimeOffset StartedAt { get; }

    public DateTimeOffset LastActivity { get; private set; }

    public bool Alive { get; private set; } = true;

    /// <summary>Every byte the agent has written since it started.</summary>
    public long Written
    {
        get
        {
            lock (_gate)
            {
                return _written;
            }
        }
    }

    /// <summary>How many bytes of recent output the ring holds.</summary>
    public int Buffered
    {
        get
        {
            lock (_gate)
            {
                return _ringLength;
            }
        }
    }

    /// <summary>
    /// Attach a console: show it the recent screen, make it the driver, and nudge
    /// the size so the agent repaints. Dispose the handle to detach.
    /// </summary>
    public IDisposable Attach(IAgentViewer viewer, int columns, int rows)
    {
        ArgumentNullException.ThrowIfNull(viewer);

        IAgentViewer? previous;
        byte[] replay;

        lock (_gate)
        {
            replay = Snapshot();
            previous = _driver;
            _viewers.Add(viewer);
            _driver = viewer;
            _columns = columns;
            _rows = rows;
        }

        if (replay.Length > 0)
        {
            viewer.Output(replay);
        }

        if (previous is not null && !ReferenceEquals(previous, viewer))
        {
            previous.ReadOnly();
        }

        Redraw();

        if (!Alive)
        {
            viewer.Exited(_child.Exited.IsCompletedSuccessfully ? _child.Exited.Result : -1);
        }

        return new Detach(this, viewer);
    }

    /// <summary>Keystrokes, from the driver only.</summary>
    public void Input(IAgentViewer from, ReadOnlySpan<byte> bytes)
    {
        lock (_gate)
        {
            if (!ReferenceEquals(from, _driver))
            {
                return;
            }

            LastActivity = _now();
        }

        _child.Write(bytes);
    }

    /// <summary>A new size, from the driver only.</summary>
    public void Resize(IAgentViewer from, int columns, int rows)
    {
        lock (_gate)
        {
            if (!ReferenceEquals(from, _driver))
            {
                return;
            }

            _columns = columns;
            _rows = rows;
        }

        _child.Resize(columns, rows);
    }

    /// <summary>Moves the size off and back, so the agent sees a change and repaints.</summary>
    public void Redraw()
    {
        int columns;
        int rows;

        lock (_gate)
        {
            columns = _columns;
            rows = _rows;
        }

        if (columns <= 0 || rows <= 0 || !Alive)
        {
            return;
        }

        _child.Resize(columns, rows > 5 ? rows - 1 : rows + 1);
        _child.Resize(columns, rows);
    }

    public void Kill() => _child.Kill();

    public void Dispose() => _child.Dispose();

    internal AgentSessionStanding Standing() => new()
    {
        SessionId = Id,
        Directory = Directory,
        StartedAt = StartedAt,
        Alive = Alive,
    };

    private void Pump()
    {
        var buffer = new byte[16 * 1024];

        while (true)
        {
            int read;

            try
            {
                read = _child.Read(buffer);
            }
            catch (Exception ended) when (ended is IOException or ObjectDisposedException)
            {
                break;
            }

            if (read <= 0)
            {
                break;
            }

            IAgentViewer[] viewers;

            lock (_gate)
            {
                Keep(buffer.AsSpan(0, read));
                _written += read;
                LastActivity = _now();
                viewers = [.. _viewers];
            }

            foreach (var viewer in viewers)
            {
                viewer.Output(buffer.AsSpan(0, read));
            }
        }

        var code = _child.Exited.Wait(TimeSpan.FromSeconds(5)) ? _child.Exited.Result : -1;
        IAgentViewer[] told;

        lock (_gate)
        {
            Alive = false;
            LastActivity = _now();
            told = [.. _viewers];
        }

        foreach (var viewer in told)
        {
            viewer.Exited(code);
        }
    }

    private void Keep(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= _ring.Length)
        {
            bytes[^_ring.Length..].CopyTo(_ring);
            _ringStart = 0;
            _ringLength = _ring.Length;
            return;
        }

        foreach (var b in bytes)
        {
            var at = (_ringStart + _ringLength) % _ring.Length;
            _ring[at] = b;

            if (_ringLength < _ring.Length)
            {
                _ringLength++;
            }
            else
            {
                _ringStart = (_ringStart + 1) % _ring.Length;
            }
        }
    }

    private byte[] Snapshot()
    {
        var copy = new byte[_ringLength];

        for (var i = 0; i < _ringLength; i++)
        {
            copy[i] = _ring[(_ringStart + i) % _ring.Length];
        }

        return copy;
    }

    private sealed class Detach(AgentSession session, IAgentViewer viewer) : IDisposable
    {
        public void Dispose()
        {
            lock (session._gate)
            {
                session._viewers.Remove(viewer);

                if (ReferenceEquals(session._driver, viewer))
                {
                    session._driver = session._viewers.LastOrDefault();
                }
            }
        }
    }
}
