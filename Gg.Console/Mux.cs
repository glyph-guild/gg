using System.Collections.Concurrent;
using System.Text;
using Porta.Pty;
using XTerm.Options;

// `Terminal` is XTerm.NET's emulator type and also the root namespace of
// Terminal.Gui, which this assembly references.
using XTermTerminal = XTerm.Terminal;

namespace Gg.Console;

/// <summary>
/// The agents gg holds beside itself (slice sixty-nine): each in a pty of its own, each emulator
/// fed whether or not it is on screen, each ending only when its process does.
/// </summary>
/// <remarks>
/// <para>
/// <b>The shell's, never a UI session's.</b> gg's rule is that a UI session starts nothing and
/// blocks on nothing. The readers here keep feeding emulators while Terminal.Gui is up, so they
/// belong where <see cref="PtyHost"/> already does: outside every UI lifetime. The console only
/// reads <see cref="Rows"/>, and is told about an ending on its tick.
/// </para>
/// <para>
/// <b>Not <see cref="PtyHost"/>, which holds nothing between calls and a test asserts it.</b> An
/// agent that outlives the screen showing it is state between calls by definition, so it lives
/// here, and the host stays the one-child, one-call path the editor still takes.
/// </para>
/// <para>
/// <b>Nothing a child says is written to disk.</b> The ledger of sessions holds ids, labels,
/// directories and times; a session's transcript stays Claude Code's.
/// </para>
/// </remarks>
public sealed partial class Mux
{
    [ThreadStatic]
    private static string? _launching;

    private readonly Func<IHostTerminal?> _terminal;
    private readonly MuxLedger? _ledger;
    private readonly Func<DateTimeOffset> _clock;
    private readonly string _agentCommand;
    private readonly List<MuxAgent> _agents = [];
    private readonly ConcurrentQueue<Func<AppState, AppState>> _endings = new();
    private readonly Lock _lock = new();
    private IHostTerminal? _opened;
    private MuxTab? _wanted;
    private int _ids;

    public Mux(
        Func<IHostTerminal?>? terminal = null,
        MuxLedger? ledger = null,
        Func<DateTimeOffset>? clock = null,
        string? agentCommand = null)
    {
        _terminal = terminal ?? OwnedTerminal.Open;
        _ledger = ledger;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
        _agentCommand = agentCommand
            ?? Environment.GetEnvironmentVariable("GG_TAKE_COMMAND")
            ?? "claude";
    }

    /// <summary>Raised, on a reader's thread, when an agent's screen changes or it ends.</summary>
    internal event Action? Moved;

    /// <summary>Whether any agent is alive.</summary>
    public bool Any
    {
        get
        {
            lock (_lock)
            {
                return _agents.Count > 0;
            }
        }
    }

    /// <summary>Whether an agent has ended and left something for the shell to fold.</summary>
    public bool Ending => !_endings.IsEmpty;

    /// <summary>The ledger history reads sessions from, when there is one.</summary>
    public MuxLedger? Ledger => _ledger;

    /// <summary>The live agents, as the column lists them.</summary>
    public IReadOnlyList<MuxRow> Rows()
    {
        var now = _clock();
        lock (_lock)
        {
            return [.. _agents.Select((agent, at) =>
                new MuxRow(at + 1, agent.Label, now - agent.Started, agent.Changed))];
        }
    }

    /// <summary>The live agents' labels, in row order.</summary>
    public IReadOnlyList<string> Labels() => [.. Rows().Select(row => row.Label)];

    /// <summary>The agent on row <paramref name="number"/>, from 1.</summary>
    internal MuxAgent? Agent(int number)
    {
        lock (_lock)
        {
            return number >= 1 && number <= _agents.Count ? _agents[number - 1] : null;
        }
    }

    /// <summary>What the agent on row <paramref name="number"/> has on its screen now, as text.</summary>
    /// <remarks>
    /// Read off its emulator, which its reader feeds whether or not it is shown - so this is what
    /// switching to it would paint.
    /// </remarks>
    public string Screen(int number) => Agent(number)?.Text() ?? "";

    /// <summary>The row an agent is on, from 1, or 0 once it has gone.</summary>
    internal int NumberOf(MuxAgent agent)
    {
        lock (_lock)
        {
            return _agents.IndexOf(agent) + 1;
        }
    }

    /// <summary>Asks the shell to show <paramref name="tab"/> next.</summary>
    public void Want(MuxTab tab)
    {
        lock (_lock)
        {
            _wanted = tab;
        }
    }

    /// <summary>What the shell was asked to show, taken so it is shown once.</summary>
    public MuxTab? TakeWanted()
    {
        lock (_lock)
        {
            var wanted = _wanted;
            _wanted = null;
            return wanted;
        }
    }

    /// <summary>Folds what ended agents left, oldest first, and says whether anything was.</summary>
    public AppState Fold(AppState state, out bool folded)
    {
        folded = false;
        while (_endings.TryDequeue(out var ending))
        {
            folded = true;
            try
            {
                state = ending(state);
            }
            catch (Exception failure)
            {
                state = state with { Diagnosis = "An agent ended and gg could not take what it left: " + failure.Message };
            }
        }

        return Rows() is var rows ? state with { Agents = rows } : state;
    }

    /// <summary>
    /// Runs a hosted session on a thread of its own, its child as a mux agent, and returns once
    /// the agent is on its row (or the session ended before it had one).
    /// </summary>
    /// <param name="label">What the column calls the agent.</param>
    /// <param name="work">
    /// The session: it hosts its child through <see cref="Host"/>, blocks until the child ends,
    /// and answers with what to fold into the console once it has.
    /// </param>
    /// <remarks>
    /// <b>The session's own code, unchanged.</b> Compose and plan already host a child, wait for
    /// it, and say what it left; run on a thread of its own, the wait is the agent's life and what
    /// it says is folded when it ends. A second, asynchronous copy of each session would be two
    /// places for its bar and its ending to disagree.
    /// </remarks>
    public void Launch(string label, Func<Func<AppState, AppState>> work)
    {
        ArgumentNullException.ThrowIfNull(work);

        if (Rows().Count >= MuxColumn.Most)
        {
            _endings.Enqueue(state => state with
            {
                Diagnosis = $"gg holds {MuxColumn.Most} agents at most; end one to start {label}.",
            });
            return;
        }

        var placed = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            _launching = label;
            Placed = placed;
            try
            {
                _endings.Enqueue(work());
            }
            catch (Exception failure)
            {
                _endings.Enqueue(state => state with { Diagnosis = $"{label} could not run: {failure.Message}" });
            }
            finally
            {
                placed.Set();
                Moved?.Invoke();
            }
        })
        {
            IsBackground = true,
            Name = "gg mux: " + label,
        };

        thread.Start();
        placed.Wait(TimeSpan.FromSeconds(30));
    }

    [ThreadStatic]
    private static ManualResetEventSlim? Placed;

    /// <summary>
    /// The host a session is given: inside <see cref="Launch"/> it starts the child as an agent
    /// and completes when the child ends; anywhere else it is <see cref="PtyHost.RunAsync"/>.
    /// </summary>
    public HostRun Host => (terminal, command, arguments, workingDirectory, panel, took, cancellationToken) =>
        _launching is { } label
            ? Started(label, command, arguments, workingDirectory, panel, took)
            : PtyHost.RunAsync(terminal, command, arguments, workingDirectory, panel, took, cancellationToken);

    /// <summary>
    /// A plain Claude Code session in <paramref name="workingDirectory"/>, or one resumed by id.
    /// </summary>
    public MuxAgent? StartClaudeCode(string workingDirectory, string? resume = null)
    {
        var parts = _agentCommand.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var label = resume is null ? "Claude Code" : "Claude Code · resumed";
        string? bar = null;

        // TAKES CTRL-G AND NOTHING ELSE: there is no panel, so the switch keys are all it opens.
        HostedRows Panel(int most, int columns) => new([bar ??= $"gg · {label} · ctrl-g then 0 for gg"], false);

        var arguments = resume is null
            ? (IReadOnlyList<string>)[.. parts.Skip(1)]
            : [.. parts.Skip(1), "--resume", resume];

        var exited = Start(label, parts[0], arguments, workingDirectory, Panel,
            (gesture, typed) => gesture == HostedGesture.Typed && typed.Length == 1 && typed.Span[0] == HostedBar.Prefix,
            ending: state => state,
            sessionId: resume);
        return exited;
    }

    private Task<int> Started(
        string label,
        string command,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        HostPanel panel,
        HostTook took)
    {
        var agent = Start(label, command, arguments, workingDirectory, panel, took, ending: null);
        Placed?.Set();
        return agent?.Exited ?? Task.FromResult(-1);
    }

    /// <summary>Starts a child as an agent, on the next row, and asks the shell to show it.</summary>
    private MuxAgent? Start(
        string label,
        string command,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        HostPanel panel,
        HostTook took,
        Func<AppState, AppState>? ending,
        string? sessionId = null)
    {
        var terminal = Terminal();
        var columns = Math.Max((terminal?.Columns ?? 120) - MuxColumn.Width, 20);
        var total = terminal?.Rows ?? 40;
        var rows = Math.Max(total - Math.Max(panel(Math.Max(total / 2, 2), columns).Top.Count, 1), 5);

        // AN ID CLAUDE CODE WILL ANSWER TO, so history can resume it. Given, not read back: the mux
        // starts the session with --session-id and therefore knows it. First among the arguments,
        // because --allowedTools takes every word after it.
        var id = sessionId;
        var given = arguments;
        if (id is null && Path.GetFileName(command) == "claude")
        {
            id = Guid.NewGuid().ToString();
            given = ["--session-id", id, .. arguments];
        }

        var options = new PtyOptions
        {
            Name = "gg",
            Cols = columns,
            Rows = rows,
            Cwd = workingDirectory,
            App = command,
            CommandLine = [.. given],
            Environment = new Dictionary<string, string> { ["TERM"] = "xterm-256color" },
        };

        var pty = PtyProvider.SpawnAsync(options, CancellationToken.None).GetAwaiter().GetResult();
        var agent = new MuxAgent(
            Interlocked.Increment(ref _ids), label, id, _clock(), pty,
            new XTermTerminal(new TerminalOptions { Cols = columns, Rows = rows }),
            panel, took, columns, rows);

        lock (_lock)
        {
            _agents.Add(agent);
            _wanted = MuxTab.Agent(_agents.Count);
        }

        if (id is not null)
        {
            _ledger?.Keep(new MuxSession(id, label, workingDirectory, agent.Started));
        }

        agent.Read(() => Moved?.Invoke(), () =>
        {
            lock (_lock)
            {
                _agents.Remove(agent);
            }

            if (ending is not null)
            {
                _endings.Enqueue(ending);
            }

            Moved?.Invoke();
        });

        return agent;
    }

    /// <summary>Ends every agent: quitting gg, asked first, ends them.</summary>
    public void EndAll()
    {
        MuxAgent[] all;
        lock (_lock)
        {
            all = [.. _agents];
        }

        foreach (var agent in all)
        {
            agent.Kill();
        }

        foreach (var agent in all)
        {
            agent.Exited.Wait(TimeSpan.FromSeconds(5));
        }
    }

    /// <summary>The terminal the mux paints on, opened once.</summary>
    internal IHostTerminal? Terminal()
    {
        lock (_lock)
        {
            return _opened ??= _terminal();
        }
    }
}

/// <summary>One live agent: its pty, its emulator, and the bar its session keeps.</summary>
public sealed class MuxAgent
{
    private readonly IPtyConnection _pty;
    private readonly TaskCompletionSource<int> _exited = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private volatile bool _changed;
    private volatile bool _shown;

    internal MuxAgent(
        int id,
        string label,
        string? sessionId,
        DateTimeOffset started,
        IPtyConnection pty,
        XTermTerminal emulator,
        HostPanel panel,
        HostTook took,
        int columns,
        int rows)
    {
        Id = id;
        Label = label;
        SessionId = sessionId;
        Started = started;
        _pty = pty;
        Emulator = emulator;
        Panel = panel;
        Took = took;
        Columns = columns;
        RowsHigh = rows;
    }

    public int Id { get; }

    public string Label { get; }

    /// <summary>The Claude Code session id the mux gave it, when its child is Claude Code.</summary>
    public string? SessionId { get; }

    public DateTimeOffset Started { get; }

    /// <summary>Completes with the child's exit code when it ends.</summary>
    public Task<int> Exited => _exited.Task;

    /// <summary>Whether its screen changed while it was not shown.</summary>
    public bool Changed => _changed;

    /// <summary>Held while the emulator is written, resized or painted.</summary>
    internal Lock Screen { get; } = new();

    internal XTermTerminal Emulator { get; }

    internal HostPanel Panel { get; }

    internal HostTook Took { get; }

    internal int Columns { get; private set; }

    internal int RowsHigh { get; private set; }

    /// <summary>What the child last asked of the mouse, as modes.</summary>
    internal string Mirrored { get; set; } = "";

    /// <summary>Called on every change while shown, so the foreground repaints.</summary>
    internal Action? Painted { get; set; }

    /// <summary>On screen or not. Showing it clears the mark.</summary>
    internal bool Shown
    {
        get => _shown;
        set
        {
            _shown = value;
            if (value)
            {
                _changed = false;
            }
        }
    }

    /// <summary>Everything the person typed, to the child.</summary>
    internal void Write(ReadOnlySpan<byte> typed)
    {
        try
        {
            _pty.WriterStream.Write(typed);
            _pty.WriterStream.Flush();
        }
        catch (IOException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    /// <summary>The child told a new size, and the emulator moved with it, under the screen lock.</summary>
    internal void Resize(int columns, int rows)
    {
        if (columns == Columns && rows == RowsHigh)
        {
            return;
        }

        try
        {
            _pty.Resize(columns, rows);
        }
        catch (IOException)
        {
            return;
        }

        Emulator.Resize(columns, rows);
        Columns = columns;
        RowsHigh = rows;
    }

    /// <summary>The emulator's screen, row by row.</summary>
    internal string Text()
    {
        lock (Screen)
        {
            var buffer = Emulator.Buffer;
            var text = new StringBuilder();
            for (var row = 0; row < RowsHigh; row++)
            {
                var line = buffer.Lines[buffer.YDisp + row];
                if (line is null)
                {
                    continue;
                }

                for (var column = 0; column < Columns; column++)
                {
                    var content = line[column].Content;
                    text.Append(string.IsNullOrEmpty(content) ? " " : content);
                }

                text.Append('\n');
            }

            return text.ToString();
        }
    }

    internal void Kill()
    {
        try
        {
            _pty.Kill();
        }
        catch (Exception failure) when (failure is IOException or InvalidOperationException or ObjectDisposedException)
        {
        }
    }

    /// <summary>
    /// Feeds the emulator until the child ends, on a thread of its own: shown or hidden, the
    /// screen is current when somebody switches to it.
    /// </summary>
    internal void Read(Action moved, Action gone)
    {
        var thread = new Thread(() =>
        {
            var buffer = new byte[8192];
            var decoder = Encoding.UTF8.GetDecoder();
            var chars = new char[8192 + 4];
            while (true)
            {
                int read;
                try
                {
                    read = _pty.ReaderStream.Read(buffer, 0, buffer.Length);
                }
                catch (IOException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }

                if (read <= 0)
                {
                    break;
                }

                lock (Screen)
                {
                    // DECODED ACROSS READS: a character split between two reads is not two
                    // replacement characters.
                    var count = decoder.GetChars(buffer, 0, read, chars, 0);
                    Emulator.Write(new string(chars, 0, count));
                }

                if (!_shown)
                {
                    _changed = true;
                }

                Painted?.Invoke();
                moved();
            }

            var code = -1;
            try
            {
                _pty.WaitForExit(5000);
                code = _pty.ExitCode;
            }
            catch (Exception failure) when (failure is InvalidOperationException or IOException)
            {
            }

            _pty.Dispose();
            gone();
            _exited.TrySetResult(code);
            Painted?.Invoke();
        })
        {
            IsBackground = true,
            Name = "gg mux reader: " + Label,
        };

        thread.Start();
    }
}
