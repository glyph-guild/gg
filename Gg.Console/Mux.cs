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
        string? agentCommand = null,
        Gg.Local.SelfInvocation? self = null)
    {
        _self = self ?? Gg.Local.SelfInvocation.Current;
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
    private readonly Gg.Local.SelfInvocation? _self;

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
                new MuxRow(at + 1, agent.Name, now - agent.Started, agent.Changed, MuxActivity.Read(agent.Activity)))];
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
            sessionId: resume,
            titled: true);
        return exited;
    }

    /// <summary>
    /// An agent that manages gg through the <c>gg-manage</c> tools (owner, 2026-10-08): it opens
    /// on <see cref="AgentOpening.Manage"/>, every read is granted, and no act is - Claude Code asks
    /// the person before each one.
    /// </summary>
    public MuxAgent? StartManaging(string workingDirectory)
    {
        if (_self is null)
        {
            // SAID BY STARTING NOTHING: a server configured with a path that is not this binary is a
            // child that fails at startup, after the agent has been told its tools exist.
            return null;
        }

        var parts = _agentCommand.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string? bar = null;
        HostedRows Panel(int most, int columns) =>
            new([bar ??= "gg · managing gg — reads are free; you approve every act · ctrl-g then 0 for gg"], false);

        return Start("gg", parts[0],
            [.. parts.Skip(1),
             // THE OPENING, BEFORE EVERY FLAG: both flags below take a list, and a prompt after
             // them is one more tool name.
             AgentOpening.Manage(),
             "--mcp-config", ManageConfig(_self),
             // THE READS, NAMED; NO ACT. An ungranted tool still appears and asks, which is the
             // confirmation an act needs.
             "--allowedTools", .. Gg.Local.ManageTool.Reads.Select(Gg.Local.ManageTool.Qualified)],
            workingDirectory, Panel,
            (gesture, typed) => gesture == HostedGesture.Typed && typed.Length == 1 && typed.Span[0] == HostedBar.Prefix,
            ending: state => state);
    }

    /// <summary>The server: <c>gg manage tools</c>, under its own key.</summary>
    private static string ManageConfig(Gg.Local.SelfInvocation self)
    {
        using var buffer = new MemoryStream();
        using (var json = new System.Text.Json.Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();
            json.WriteStartObject("mcpServers");
            json.WriteStartObject(Gg.Local.ManageTool.Server);
            json.WriteString("command", self.Command);
            json.WriteStartArray("args");
            foreach (var argument in self.Under("manage", "tools"))
            {
                json.WriteStringValue(argument);
            }

            json.WriteEndArray();
            json.WriteEndObject();
            json.WriteEndObject();
            json.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
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
        string? sessionId = null,
        bool titled = false)
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

        // WHAT THE AGENT IS DOING, told by its own hooks: given for this launch only, so nothing
        // lands in the person's Claude Code settings. LAST, because a flag ends the list before it.
        var environment = new Dictionary<string, string> { ["TERM"] = "xterm-256color" };
        string? activity = null;
        if (Path.GetFileName(command) == "claude" && _self is not null && _ledger is not null)
        {
            activity = Path.Combine(
                Path.GetDirectoryName(_ledger.Path)!, "activity", Guid.NewGuid().ToString("N"));
            environment[MuxActivity.Variable] = activity;
            given = [.. given, "--settings", MuxActivity.Settings(_self)];
        }

        var options = new PtyOptions
        {
            Name = "gg",
            Cols = columns,
            Rows = rows,
            Cwd = workingDirectory,
            App = command,
            CommandLine = [.. given],
            Environment = environment,
        };

        var pty = PtyProvider.SpawnAsync(options, CancellationToken.None).GetAwaiter().GetResult();
        var agent = new MuxAgent(
            Interlocked.Increment(ref _ids), label, id, _clock(), new LocalPty(pty),
            new XTermTerminal(new TerminalOptions { Cols = columns, Rows = rows }),
            panel, took, columns, rows, activity, titled);

        return Place(agent, id, label, workingDirectory, activity, ending, machine: null);
    }

    /// <summary>
    /// A session on another machine, as an agent: on the next row, its screen filled
    /// from the far terminal, told the pane's size (slice seventy, ADR-0039).
    /// </summary>
    /// <param name="machine">Which machine it runs on, for history to resume it there.</param>
    public MuxAgent StartRemote(
        string label, IAgentPty pty, string? sessionId, string? machine = null, string directory = "")
    {
        ArgumentNullException.ThrowIfNull(pty);

        var (columns, rows) = PaneSize();
        var agent = new MuxAgent(
            Interlocked.Increment(ref _ids), label, sessionId, _clock(), pty,
            new XTermTerminal(new TerminalOptions { Cols = columns, Rows = rows }),
            (_, _) => new HostedRows([], false), (_, _) => false, columns, rows, activity: null, titled: false);

        // TOLD THE PANE NOW, as a local agent is told at its spawn: the far agent was
        // started at the size the picker asked for, and this is the size it is shown at.
        pty.Resize(columns, rows);

        return Place(agent, sessionId, label, directory, activity: null, ending: null, machine);
    }

    /// <summary>The size an agent is shown at: right of the column, and the rows the bar leaves.</summary>
    public (int Columns, int Rows) PaneSize()
    {
        var terminal = Terminal();
        return (Math.Max((terminal?.Columns ?? 120) - MuxColumn.Width, 20), Math.Max((terminal?.Rows ?? 40) - 1, 5));
    }

    /// <summary>Puts a started agent on its row, keeps it in the ledger, and reads it until it ends.</summary>
    private MuxAgent Place(
        MuxAgent agent, string? id, string label, string workingDirectory, string? activity,
        Func<AppState, AppState>? ending, string? machine)
    {
        lock (_lock)
        {
            _agents.Add(agent);
            _wanted = MuxTab.Agent(_agents.Count);
        }

        if (id is not null)
        {
            _ledger?.Keep(new MuxSession(id, label, workingDirectory, agent.Started, machine));
        }

        agent.Read(() => Moved?.Invoke(), () =>
        {
            lock (_lock)
            {
                _agents.Remove(agent);
            }

            if (activity is not null)
            {
                try
                {
                    File.Delete(activity);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                }
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
    private readonly IAgentPty _pty;
    private readonly TaskCompletionSource<int> _exited = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private volatile bool _changed;
    private volatile bool _shown;
    private readonly bool _titled;

    internal MuxAgent(
        int id,
        string label,
        string? sessionId,
        DateTimeOffset started,
        IAgentPty pty,
        XTermTerminal emulator,
        HostPanel panel,
        HostTook took,
        int columns,
        int rows,
        string? activity = null,
        bool titled = false)
    {
        Id = id;
        _titled = titled;
        Activity = activity;
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

    /// <summary>
    /// What its row says: the title a plain Claude Code session set for itself (owner,
    /// 2026-10-09), or the label it was launched under. A plan keeps its label, which names the
    /// draft it works on.
    /// </summary>
    public string Name
    {
        get
        {
            if (!_titled)
            {
                return Label;
            }

            string? title;
            lock (Screen)
            {
                title = Emulator.Title;
            }

            return MuxTitle.Of(title) ?? Label;
        }
    }

    /// <summary>The state file its hooks write what it is doing to, when it has hooks.</summary>
    public string? Activity { get; }

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
    /// <remarks>
    /// <b>Under the screen's lock, as the reader's mark is.</b> Unlocked, the reader could find the
    /// agent hidden, lose the processor while this showed it and cleared the mark, then set the mark
    /// anyway - left on for an agent that had written nothing since it was looked at. A race read
    /// off the code, not one a test has caught.
    /// </remarks>
    internal bool Shown
    {
        get => _shown;
        set
        {
            lock (Screen)
            {
                _shown = value;
                if (value)
                {
                    _changed = false;
                }
            }
        }
    }

    /// <summary>Everything the person typed, to the child.</summary>
    internal void Write(ReadOnlySpan<byte> typed)
    {
        try
        {
            _pty.Write(typed);
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
                    read = _pty.Read(buffer, 0, buffer.Length);
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

                    // MARKED IN THE SAME LOCK THAT SHOWING CLEARS IT IN: the check and the set are
                    // one step, or a show between them leaves a mark nobody earned.
                    if (!_shown)
                    {
                        _changed = true;
                    }
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
