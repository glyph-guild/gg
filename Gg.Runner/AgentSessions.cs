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
/// facts about this machine: it is flying, it already holds as many as it will, the
/// session asked for is running or unknown.
/// </para>
/// <para>
/// <b>Each session has a directory of its own, and the machine writes its sessions
/// down</b> (slice seventy-one, ADR-0039 Decisions 7 and 8). The directory is
/// <c>home/&lt;id&gt;</c>, made empty for a new session; the ledger is
/// <c>home/sessions.json</c>. A restart forgets nothing, nothing is forgotten on a
/// timer, and a resume runs in the directory Claude filed the conversation under.
/// </para>
/// </remarks>
/// <param name="home">Where session directories and the ledger live; made on first use.</param>
/// <param name="transcripts">Where Claude keeps conversations by directory; the user's <c>~/.claude/projects</c> when null.</param>
public sealed class AgentSessions(
    IHostAgentSessions host,
    string home,
    Func<bool> flying,
    Func<DateTimeOffset> now,
    Func<IReadOnlyDictionary<string, string>>? environment = null,
    string? transcripts = null) : IDisposable
{
    /// <summary>How many live sessions one machine holds at once.</summary>
    public const int MostSessions = 3;

    /// <summary>How much recent output a session keeps to show the next console.</summary>
    public const int RingBytes = 256 * 1024;

    /// <summary>How long a session may go with no byte either way before it is ended.</summary>
    public static readonly TimeSpan IdleLimit = TimeSpan.FromHours(4);

    private readonly List<AgentSession> _sessions = [];
    private readonly Lock _gate = new();
    private readonly string _home = Path.TrimEndingDirectorySeparator(Path.GetFullPath(home));
    private readonly string _transcripts = transcripts
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "projects");

    private Dictionary<string, AgentSessionLedger.Kept>? _kept;

    /// <summary>
    /// Where a machine keeps its sessions unless told otherwise:
    /// <c>$XDG_DATA_HOME/good-grief/agent-sessions</c>, or <c>~/.local/share/...</c>.
    /// </summary>
    /// <remarks>
    /// <b>Not under the flights' working-tree root</b>, which the runner sweeps on every
    /// start: a session has to be resumed in the directory it ran in.
    /// </remarks>
    public static string DefaultHome()
    {
        var data = Environment.GetEnvironmentVariable("XDG_DATA_HOME") is { Length: > 0 } xdg
            ? xdg
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");

        return Path.Combine(data, "good-grief", "agent-sessions");
    }
    private bool _disposed;

    /// <summary>A session, or why there is not one.</summary>
    public sealed record Opened(AgentSession? Session, string? Refused);

    /// <summary>Start a new session, or resume an ended one asked for by its id, in its own directory.</summary>
    public async Task<Opened> StartAsync(StartAgentSession asked, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(asked);

        if (flying())
        {
            return Refused("This machine is running a flight, and takes no new session while it "
                         + "does: one agent subscription and one working tree. Try again when it lands.");
        }

        var sessionId = asked.SessionId is { Length: > 0 } given ? given : Guid.NewGuid().ToString();

        // THE ID NAMES A DIRECTORY, so it may not name a path.
        if (!AgentSessionLedger.IsPlainName(sessionId))
        {
            return Refused($"'{sessionId}' is not a session id this machine can name a directory after.");
        }

        AgentSessionLedger.Kept? ended;

        lock (_gate)
        {
            if (_sessions.Any(s => s.Id == sessionId && s.Alive))
            {
                return Refused($"Session {sessionId} is already running here; attach to it instead.");
            }

            if (_sessions.Count(s => s.Alive) >= MostSessions)
            {
                return Refused($"This machine already holds {MostSessions} sessions, which is as "
                             + "many as it will. End one first.");
            }

            ended = Kept().GetValueOrDefault(sessionId);
        }

        var directory = ended?.Directory ?? Path.Combine(_home, sessionId);
        AgentSessionLedger.Private(_home);
        AgentSessionLedger.Private(directory);

        var child = await host.StartAsync(
            new AgentSessionStart(
                directory,
                Math.Max(asked.Columns, 20),
                Math.Max(asked.Rows, 5),
                sessionId,
                Resume: ended is not null,
                environment?.Invoke() ?? new Dictionary<string, string>()),
            cancellationToken);

        var started = now();
        var session = new AgentSession(sessionId, directory, started, child, now, Ended);

        lock (_gate)
        {
            _sessions.RemoveAll(s => s.Id == sessionId);
            _sessions.Add(session);
            Kept()[sessionId] = new AgentSessionLedger.Kept(sessionId, directory, ended?.StartedAt ?? started, EndedAt: null);
            Save();
        }

        return new Opened(session, null);
    }

    /// <summary>The live session with this id, or why it cannot be attached to.</summary>
    public Opened Find(string sessionId)
    {
        lock (_gate)
        {
            if (_sessions.FirstOrDefault(s => s.Id == sessionId && s.Alive) is { } live)
            {
                return new Opened(live, null);
            }

            return Kept().ContainsKey(sessionId)
                ? Refused($"Session {sessionId} has ended. Start it again by its id to resume it.")
                : Refused($"This machine holds no session {sessionId}.");
        }
    }

    /// <summary>What this machine holds, live and ended, for a list frame and the heartbeat.</summary>
    public IReadOnlyList<AgentSessionStanding> Standings()
    {
        lock (_gate)
        {
            return
            [
                .. Kept().Values.OrderBy(k => k.StartedAt).Select(k =>
                {
                    var alive = _sessions.Any(s => s.Id == k.Id && s.Alive);
                    return new AgentSessionStanding
                    {
                        SessionId = k.Id,
                        Directory = k.Directory,
                        StartedAt = k.StartedAt,
                        Alive = alive,
                        EndedAt = alive ? null : k.EndedAt,
                    };
                }),
            ];
        }
    }

    /// <summary>
    /// Deletes an ended session - its ledger entry, its directory and Claude's transcript of
    /// it - or every ended one when <paramref name="sessionId"/> is null. Answers why not, or
    /// null when it is done.
    /// </summary>
    public string? Forget(string? sessionId)
    {
        List<AgentSessionLedger.Kept> forgotten;

        lock (_gate)
        {
            var kept = Kept();

            if (sessionId is not null)
            {
                if (!kept.TryGetValue(sessionId, out var one))
                {
                    return $"This machine holds no session {sessionId}.";
                }

                if (_sessions.Any(s => s.Id == sessionId && s.Alive))
                {
                    return $"Session {sessionId} is still running. End it, then forget it.";
                }

                forgotten = [one];
            }
            else
            {
                forgotten = [.. kept.Values.Where(k => !_sessions.Any(s => s.Id == k.Id && s.Alive))];
            }

            foreach (var gone in forgotten)
            {
                kept.Remove(gone.Id);
                _sessions.RemoveAll(s => s.Id == gone.Id);
            }

            Save();
        }

        foreach (var gone in forgotten)
        {
            // ONLY UNDER HOME: the ledger is a file on disk, and a directory it names is
            // deleted only if this machine could have made it.
            if (gone.Directory.StartsWith(_home + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                AgentSessionLedger.Delete(gone.Directory);
            }

            AgentSessionLedger.Delete(Path.Combine(_transcripts, AgentSessionLedger.TranscriptName(gone.Directory)));
        }

        return null;
    }

    /// <summary>Ends what has been idle past the limit. Called on the beat; ended sessions stay.</summary>
    public void Sweep()
    {
        List<AgentSession> idle;

        lock (_gate)
        {
            var at = now();
            idle = [.. _sessions.Where(s => s.Alive && at - s.LastActivity > IdleLimit)];
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
            // NOTHING WRITTEN AFTER THIS: a session killed here is read as ended when the
            // ledger is next loaded, and a late write could land over the next runner's.
            _disposed = true;
            all = [.. _sessions];
        }

        foreach (var session in all)
        {
            session.Kill();
            session.Dispose();
        }
    }

    private static Opened Refused(string because) => new(null, because);

    private void Ended(AgentSession session)
    {
        lock (_gate)
        {
            if (_disposed || !Kept().TryGetValue(session.Id, out var kept))
            {
                return;
            }

            Kept()[session.Id] = kept with { EndedAt = now() };
            Save();
        }
    }

    /// <summary>The ledger, read once: a session it says is running died with the last runner.</summary>
    private Dictionary<string, AgentSessionLedger.Kept> Kept()
    {
        if (_kept is null)
        {
            var at = now();
            _kept = AgentSessionLedger.Read(Path.Combine(_home, AgentSessionLedger.FileName))
                .ToDictionary(k => k.Id, k => k.EndedAt is null ? k with { EndedAt = at } : k, StringComparer.Ordinal);
        }

        return _kept;
    }

    private void Save()
    {
        if (_disposed)
        {
            return;
        }

        AgentSessionLedger.Private(_home);
        AgentSessionLedger.Write(Path.Combine(_home, AgentSessionLedger.FileName), [.. Kept().Values]);
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

    private readonly Action<AgentSession>? _ended;

    internal AgentSession(
        string id, string directory, DateTimeOffset started, IAgentSessionChild child, Func<DateTimeOffset> now,
        Action<AgentSession>? ended = null)
    {
        _ended = ended;
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

        _ended?.Invoke(this);

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
