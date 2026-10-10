using System.Text;
using Gg.Client;
using Gg.Contracts;
using XTerm.Input;

namespace Gg.Console;

/// <summary>What showing the mux ends in, for the shell to act on.</summary>
public enum MuxLeave
{
    /// <summary>Back to gg's console.</summary>
    Gg,

    /// <summary>A new plan agent was asked for: the shell starts it, as `n` `l` would.</summary>
    Plan,

    /// <summary>A new flight was asked for: gg's console opens with `n`'s question.</summary>
    Compose,

    /// <summary>An airspace agent was asked for: the shell starts it beside gg.</summary>
    Airspace,

    /// <summary>
    /// A machine was chosen under Remote Control…: gg's console opens its runner modal on
    /// that machine's sessions (slice seventy-one). <see cref="Mux.TakeRemoteControl"/> says
    /// which.
    /// </summary>
    RemoteControl,
}

/// <summary>What opening a session on another machine came to: its row, or why there is none.</summary>
public sealed record RemoteOpened(int? Agent, string? Refused);

public sealed partial class Mux
{
    private const string Esc = "\u001b";

    private readonly Lock _painting = new();
    private ItineraryDrafts? _drafts;
    private Func<string, BoardPage?>? _plan;
    private Func<IReadOnlyList<RemoteMachine>>? _machines;
    private Func<RemoteMachine, RemoteReach>? _reach;
    private string? _remoteControl;

    /// <summary>The machine chosen under Remote Control…, taken so it is opened once.</summary>
    public string? TakeRemoteControl()
    {
        lock (_lock)
        {
            var chosen = _remoteControl;
            _remoteControl = null;
            return chosen;
        }
    }

    /// <summary>
    /// Starts a new session on <paramref name="machine"/>, attaches to a live one, or resumes an
    /// ended one by its id, and puts it on a row - or says why not (slice seventy-one).
    /// </summary>
    /// <remarks>
    /// <b>The console's runner modal asks for this</b> with what its sessions view knows: the
    /// session's id, whether it runs, and where it worked. Placing the row asks the shell to show
    /// it, so the next turn of the loop is the agent.
    /// </remarks>
    public RemoteOpened OpenRemote(RemoteMachine machine, string? sessionId, bool alive, string? directory = null)
    {
        ArgumentNullException.ThrowIfNull(machine);

        if (Rows().Count >= MuxColumn.Most)
        {
            return new(null, $"gg holds {MuxColumn.Most} agents at most. End one first.");
        }

        var (reached, unreached) = Reach(machine);
        if (reached is not { } link)
        {
            return new(null, unreached);
        }

        var pty = new RemotePty(link);
        var session = sessionId is null
            ? null
            : new AgentSessionStanding
            {
                SessionId = sessionId, StartedAt = DateTimeOffset.MinValue, Alive = alive, Directory = directory,
            };

        var (shown, refused) = Open(machine, link, pty, session, directory);
        if (shown is null)
        {
            pty.Dispose();
        }

        return new(shown, refused);
    }

    /// <summary>
    /// Asks <paramref name="machine"/> to forget an ended session, or every ended one when
    /// <paramref name="sessionId"/> is null; answers the machine's refusal, or null once it has.
    /// </summary>
    public string? ForgetRemote(RemoteMachine machine, string? sessionId)
    {
        ArgumentNullException.ThrowIfNull(machine);

        var (reached, unreached) = Reach(machine);
        if (reached is not { } link)
        {
            return unreached;
        }

        try
        {
            var answer = Ask<AgentFrame>(
                link, new ForgetAgentSession { SessionId = sessionId },
                f => f is AgentSessionList or AgentSessionRefused);

            return answer switch
            {
                AgentSessionList => null,
                AgentSessionRefused refused => refused.Because,
                _ => $"{machine.Name} did not answer. The channel may have dropped; try again.",
            };
        }
        finally
        {
            // NOTHING STAYS OPEN: a forget holds no session, so its channel is not kept.
            link.Dispose();
        }
    }

    /// <summary>A link to the machine, or why there is none.</summary>
    private (Gg.Client.IAgentLink? Link, string? Refused) Reach(RemoteMachine machine)
    {
        if (_reach is null)
        {
            return (null, "This console cannot reach machines.");
        }

        var reached = _reach(machine);
        return reached.Link is { } link
            ? (link, null)
            : (null, reached.Refused ?? $"{machine.Name} could not be reached.");
    }

    /// <summary>
    /// How the mux finds the machines this person may start a session on, and reaches
    /// one (slice seventy, ADR-0039). The composition root's, because only it may name
    /// the control plane; without it, the new-agent menu does not offer another machine.
    /// </summary>
    public Mux Reaching(Func<IReadOnlyList<RemoteMachine>> machines, Func<RemoteMachine, RemoteReach> reach)
    {
        _machines = machines;
        _reach = reach;
        return this;
    }

    /// <summary>What history reads: the drafts and their records, and one plan from the control plane.</summary>
    public Mux Reading(ItineraryDrafts drafts, Func<string, BoardPage?> plan)
    {
        _drafts = drafts;
        _plan = plan;
        return this;
    }

    /// <summary>
    /// Shows <paramref name="tab"/>, and whatever it is switched to after, until the person goes
    /// back to gg or asks for something gg's console answers.
    /// </summary>
    /// <remarks>
    /// <b>Between UI sessions, as <see cref="PtyHost"/> runs.</b> Terminal.Gui is torn down while
    /// an agent, the new-agent menu or history is on screen; going back to gg builds the console
    /// again from the model, with the column beside it while any agent lives.
    /// </remarks>
    public MuxLeave Show(MuxTab tab)
    {
        if (Terminal() is not { } terminal)
        {
            return MuxLeave.Gg;
        }

        terminal.Paint(MouseInput.Modes(MouseTrackingMode.None, MouseEncoding.Default, focus: false, paste: false));
        terminal.Paint($"{Esc}[?1049h{Esc}[2J");
        var cooked = RawMode.Enter(terminal.Descriptor);

        try
        {
            Drain(terminal);
            while (true)
            {
                var (next, leave) = tab.Place switch
                {
                    MuxPlace.Agent => ShowAgent(terminal, tab.Number),
                    MuxPlace.New => ShowNew(terminal),
                    MuxPlace.History => ShowHistory(terminal),
                    _ => (null, MuxLeave.Gg),
                };

                if (leave is { } left)
                {
                    return left;
                }

                tab = next ?? MuxTab.Gg;
            }
        }
        finally
        {
            // AN AGENT STARTED FROM HERE ASKED TO BE SHOWN, AND WAS: the ask is spent. Left
            // standing, going back to gg showed that agent again at once - found driving the
            // real console, where `+` `c` then ctrl-g `0` came straight back to Claude Code.
            _ = TakeWanted();

            terminal.Paint(MouseInput.Modes(MouseTrackingMode.None, MouseEncoding.Default, focus: false, paste: false));
            terminal.Paint($"{Esc}[0m{Esc}[?1049l");
            RawMode.Restore(terminal.Descriptor, cooked);
        }
    }

    /// <summary>One agent on screen, beside the column, until it is switched away from or ends.</summary>
    private (MuxTab? Next, MuxLeave? Leave) ShowAgent(IHostTerminal terminal, int number)
    {
        if (Agent(number) is not { } agent)
        {
            return (null, MuxLeave.Gg);
        }

        var armed = false;
        var held = false;
        var barRows = 1;
        var mirrored = "";
        var last = number;

        void Repaint()
        {
            var width = Math.Max(terminal.Columns - MuxColumn.Width, 20);
            var kept = agent.Panel(Math.Max(terminal.Rows / 2, 2), width);
            var height = Math.Max(terminal.Rows - Math.Max(kept.Top.Count, 1), 5);
            barRows = kept.Top.Count;

            string frame;
            lock (agent.Screen)
            {
                agent.Resize(width, height);

                // THE COLUMN NEEDS THE MOUSE, so reporting is on while it shows: the child's own
                // tracking when it asked for some, press-and-release otherwise. Always SGR, so a
                // click's column can be read past 223 and moved past the column.
                var tracking = agent.Emulator.MouseTrackingMode == MouseTrackingMode.None
                    ? MouseTrackingMode.VT200
                    : agent.Emulator.MouseTrackingMode;
                var modes = MouseInput.Modes(tracking, MouseEncoding.SGR,
                    agent.Emulator.SendFocusEvents, agent.Emulator.BracketedPasteMode);

                frame = (string.Equals(modes, mirrored, StringComparison.Ordinal) ? "" : modes)
                      + MuxColumn.Paint(MuxColumn.Lines(Rows(), MuxTab.Agent(NumberOf(agent)), terminal.Rows, armed))
                      + PtyScreen.Paint(agent.Emulator, height, width, kept.Top, footer: null,
                            dim: kept.Open, left: MuxColumn.Width)
                      + agent.TakeCopied();
                mirrored = modes;
            }

            Paint(terminal, frame);
        }

        agent.Shown = true;
        agent.Painted = Repaint;
        terminal.Resized += Repaint;
        terminal.Paint($"{Esc}[2J");

        try
        {
            Repaint();
            var ticked = DateTime.UtcNow;
            var typed = new byte[1024];
            while (true)
            {
                if (agent.Exited.IsCompleted)
                {
                    // THE AGENT ABOVE IT, OR GG: its row is gone, and the one it sat under is
                    // where the eye already is.
                    return (last > 1 && Agent(last - 1) is not null ? MuxTab.Agent(last - 1) : MuxTab.Gg, null);
                }

                // ONLY WHILE IT HAS A ROW: its row goes a moment before its exit is seen, and
                // reading nought in that gap sent an agent on row two to gg, not to row one.
                if (NumberOf(agent) is > 0 and var row)
                {
                    last = row;
                }

                var read = terminal.Keystrokes.Read(typed, 0, typed.Length);
                if (read <= 0)
                {
                    if (DateTime.UtcNow - ticked > TimeSpan.FromMilliseconds(250))
                    {
                        ticked = DateTime.UtcNow;
                        Repaint();
                    }

                    Thread.Sleep(5);
                    continue;
                }

                foreach (var bytes in Keys(new ReadOnlySpan<byte>(typed, 0, read), armed))
                {
                if (Aside(bytes) is { } click)
                {
                    if (click.Column <= MuxColumn.Width)
                    {
                        if (click.Pressed
                            && MuxColumn.At(Rows(), click.Row - 1, terminal.Rows) is { } chosen
                            && chosen != MuxTab.Agent(NumberOf(agent)))
                        {
                            return (chosen, null);
                        }

                        continue;
                    }

                    // THE WHEEL, AS THE TERMINAL WOULD HAVE SENT IT. Reporting is on for the
                    // column's sake; without it a terminal on the alternate screen turns the wheel
                    // into arrows, and that is how the panel and an agent that never asked for the
                    // mouse scrolled. So a wheel report becomes an arrow, and goes the way a typed
                    // one does: to gg's panel first, then the child.
                    if (agent.Emulator.MouseTrackingMode == MouseTrackingMode.None
                        && click.Final == 'M' && (click.Button & 64) != 0)
                    {
                        byte[] arrow = (click.Button & 1) == 0 ? [0x1b, (byte)'[', (byte)'A'] : [0x1b, (byte)'[', (byte)'B'];
                        if (agent.Took(HostedGesture.Typed, arrow))
                        {
                            Repaint();
                        }
                        else
                        {
                            agent.Write(arrow);
                        }

                        continue;
                    }

                    var moved = MouseInput.Read(Shifted(click), barRows);
                    if (moved.Kind == MouseReading.Nothing)
                    {
                        continue;
                    }

                    var gesture = moved.Kind switch
                    {
                        MouseReading.Pressed => HostedGesture.Pressed,
                        MouseReading.ScrolledUp => HostedGesture.ScrolledUp,
                        MouseReading.ScrolledDown => HostedGesture.ScrolledDown,
                        _ => HostedGesture.Typed,
                    };

                    if (agent.Took(gesture, moved.Bytes))
                    {
                        Repaint();
                        continue;
                    }

                    // ONLY TO A CHILD THAT ASKED FOR THE MOUSE, and in the encoding it asked for:
                    // reporting is on for the column's sake, not the child's.
                    if (agent.Emulator.MouseTrackingMode != MouseTrackingMode.None)
                    {
                        agent.Write(agent.Emulator.MouseEncoding == MouseEncoding.SGR
                            ? moved.Bytes.Span
                            : X10(moved.Bytes.Span));
                    }

                    continue;
                }

                if (armed)
                {
                    armed = false;
                    var wasHeld = held;
                    held = false;
                    if (bytes.Length == 1 && MuxColumn.Key(bytes[0], Rows().Count) is { } switched)
                    {
                        // THE PANEL CTRL-G OPENED CLOSES AGAIN: the agent is left as it was found.
                        agent.Took(HostedGesture.Typed, new[] { (byte)0x1b });
                        if (switched == MuxTab.Agent(NumberOf(agent)))
                        {
                            Repaint();
                            continue;
                        }

                        return (switched, null);
                    }

                    // NOT A SWITCH, SO THE CTRL-G HELD BACK WAS THE CHILD'S AFTER ALL.
                    if (wasHeld)
                    {
                        agent.Write([HostedBar.Prefix]);
                    }
                }
                else if (bytes.Length == 1 && bytes[0] == HostedBar.Prefix)
                {
                    armed = true;

                    // HELD BACK FROM A CHILD THE PANEL DID NOT TAKE IT FOR: a switch's prefix is
                    // never the child's to read. Found as a CI race - a shell echoing ^G a moment
                    // after it was hidden, marked as having changed while nobody looked.
                    if (!agent.Took(HostedGesture.Typed, bytes))
                    {
                        held = true;
                    }
                    else
                    {
                        Repaint();
                    }

                    continue;
                }

                if (agent.Took(HostedGesture.Typed, bytes))
                {
                    Repaint();
                    continue;
                }

                agent.Write(bytes);
                }
            }
        }
        finally
        {
            terminal.Resized -= Repaint;
            agent.Painted = null;
            agent.Shown = false;
        }
    }

    /// <summary>"+ new agent": what `n` offers, and a plain Claude Code session here.</summary>
    /// <remarks>
    /// <b>A list, not letters</b> (owner, 2026-10-10): the arrows or j/k move, enter picks.
    /// Claude Code here is first and the cursor opens on it; Remote Control… is last, because
    /// it opens a choice of its own rather than an agent.
    /// </remarks>
    private (MuxTab? Next, MuxLeave? Leave) ShowNew(IHostTerminal terminal)
    {
        var here = Directory.GetCurrentDirectory();
        var full = Rows().Count >= MuxColumn.Most;
        var cursor = 0;

        List<(string Text, Func<(MuxTab? Next, MuxLeave? Leave)> Pick)> items =
        [
            ($"Claude Code here: {here}", () => StartClaudeCode(here) is { } started && NumberOf(started) is > 0 and var at
                ? (MuxTab.Agent(at), null)
                : (MuxTab.Gg, null)),
            ("manage gg with an agent: gates, the board, flights, runners", () =>
                StartManaging(here) is { } managing && NumberOf(managing) is > 0 and var shown
                    ? (MuxTab.Agent(shown), null)
                    : (MuxTab.Gg, null)),
            ("plan several flights with an agent", () => (null, MuxLeave.Plan)),
            ("manage the airspace with an agent", () => (null, MuxLeave.Airspace)),
            ("a new flight (gg asks what kind, as `n` does)", () => (null, MuxLeave.Compose)),
        ];

        if (_machines is not null)
        {
            items.Add(("Remote Control…", () => ShowMachines(terminal)));
        }

        IReadOnlyList<string> Lines() => full
            ?
            [
                "New agent",
                "",
                $"gg holds {MuxColumn.Most} agents at most. End one, and this offers a new one.",
                "",
                "esc  back",
            ]
            :
            [
                "New agent",
                "",
                .. items.Select((item, at) => (at == cursor ? "▸ " : "  ") + item.Text),
                "",
                "↑/↓ or j/k move · enter picks · esc back",
            ];

        return Menu(terminal, MuxTab.New, Lines, (typed, sequence) =>
        {
            if (full)
            {
                return null;
            }

            var down = typed == (byte)'j' || IsArrow(sequence, (byte)'B');
            var up = typed == (byte)'k' || IsArrow(sequence, (byte)'A');
            if (down || up)
            {
                cursor = Math.Clamp(cursor + (down ? 1 : -1), 0, items.Count - 1);
                return (Stay, null);
            }

            if (typed is (byte)'\r' or (byte)'\n')
            {
                var went = items[cursor].Pick();

                // BACK FROM REMOTE CONTROL IS BACK TO THIS MENU, as back from a machine's
                // sessions is back to the machines.
                return went.Next == MuxTab.New && went.Leave is null ? (Stay, null) : went;
            }

            return null;
        }, keepOn: Stay);
    }

    /// <summary>History: what was proposed from this machine, and the sessions the mux started.</summary>
    private (MuxTab? Next, MuxLeave? Leave) ShowHistory(IHostTerminal terminal)
    {
        var rows = _drafts is null || _plan is null
            ? [new HistoryRow("History reads nothing here: this gg was built without a drafts directory.", HistoryKind.Heading)]
            : MuxHistory.Rows(_drafts, _ledger, _plan);
        var choosable = rows.Select((row, at) => (row, at)).Where(r => r.row.Kind != HistoryKind.Heading).Select(r => r.at).ToList();
        var cursor = 0;
        string? opened = null;

        IReadOnlyList<string> Lines()
        {
            if (opened is not null)
            {
                return [.. opened.Split('\n'), "", "esc  back to history"];
            }

            var lines = new List<string> { "History", "" };
            lines.AddRange(rows.Select((row, at) =>
                (choosable.Count > 0 && choosable[cursor] == at ? "▸" : " ") + row.Text));
            lines.Add("");
            lines.Add("↑/↓ or j/k move · enter opens a plan, or resumes a session as a new agent · esc back");
            return lines;
        }

        return Menu(terminal, MuxTab.History, Lines, (typed, sequence) =>
        {
            if (opened is not null)
            {
                if (typed == 0x1b)
                {
                    opened = null;
                    return (MuxTab.History, null);
                }

                return null;
            }

            var down = typed == (byte)'j' || IsArrow(sequence, (byte)'B');
            var up = typed == (byte)'k' || IsArrow(sequence, (byte)'A');
            if (down || up)
            {
                cursor = Math.Clamp(cursor + (down ? 1 : -1), 0, Math.Max(choosable.Count - 1, 0));
                return (MuxTab.History, null);
            }

            if (typed is (byte)'\r' or (byte)'\n' && choosable.Count > 0)
            {
                var row = rows[choosable[cursor]];
                if (row.Kind == HistoryKind.Proposal && _plan is not null)
                {
                    opened = MuxHistory.Describe(row.Reference!, _plan(row.Reference!));
                    return (MuxTab.History, null);
                }

                if (row.Kind == HistoryKind.Session)
                {
                    if (Rows().Count >= MuxColumn.Most)
                    {
                        opened = $"gg holds {MuxColumn.Most} agents at most. End one to resume this session.";
                        return (MuxTab.History, null);
                    }

                    // A SESSION ON ANOTHER MACHINE IS RESUMED THERE: attached to while it
                    // lives, started again by its id - which the machine resumes - once it
                    // has ended (slice seventy).
                    if (row.Machine is { } machine && _machines is not null)
                    {
                        var named = _machines().FirstOrDefault(m => m.Id == machine) ?? new RemoteMachine(machine, machine);
                        var (resumedAt, said) = Resume(named, row.Reference!, row.Directory);
                        if (resumedAt is { } shown)
                        {
                            return (MuxTab.Agent(shown), null);
                        }

                        opened = said;
                        return (MuxTab.History, null);
                    }

                    var directory = Directory.Exists(row.Directory) ? row.Directory! : Directory.GetCurrentDirectory();
                    return StartClaudeCode(directory, resume: row.Reference) is { } resumed && NumberOf(resumed) is > 0 and var at
                        ? (MuxTab.Agent(at), null)
                        : (MuxTab.History, null);
                }
            }

            return null;
        }, keepOn: MuxTab.History);
    }

    /// <summary>
    /// "Stay on this screen and repaint": a tab no row is, so a picker can tell staying
    /// from going back to the tab its column highlights.
    /// </summary>
    private static readonly MuxTab Stay = MuxTab.Agent(0);

    /// <summary>
    /// The machines this person may start a session on; enter hands the one under the cursor to
    /// gg's console, which opens its runner modal on that machine's sessions (slice seventy-one).
    /// </summary>
    /// <remarks>
    /// <b>One list of a machine's sessions, and it is the console's.</b> This screen listed them
    /// itself until the runner modal could, and two lists of the same sessions with their own
    /// keys would drift. So the machine is only chosen here; nothing is reached.
    /// </remarks>
    private (MuxTab? Next, MuxLeave? Leave) ShowMachines(IHostTerminal terminal)
    {
        var machines = _machines!();
        var cursor = 0;

        IReadOnlyList<string> Lines()
        {
            var lines = new List<string> { "Remote Control", "" };

            if (machines.Count == 0)
            {
                lines.Add("No machine you can reach is beating. `gg runners` lists the fleet.");
            }

            lines.AddRange(machines.Select((m, at) => (at == cursor ? "▸ " : "  ") + m.Name));
            lines.Add("");
            lines.Add("↑/↓ or j/k move · enter opens its sessions in gg · esc back");
            return lines;
        }

        return Menu(terminal, MuxTab.New, Lines, (typed, sequence) =>
        {
            if (sequence is [0x1b])
            {
                return (MuxTab.New, null);
            }

            var down = typed == (byte)'j' || IsArrow(sequence, (byte)'B');
            var up = typed == (byte)'k' || IsArrow(sequence, (byte)'A');
            if (down || up)
            {
                cursor = Math.Clamp(cursor + (down ? 1 : -1), 0, Math.Max(machines.Count - 1, 0));
                return (Stay, null);
            }

            if (typed is (byte)'\r' or (byte)'\n' && machines.Count > 0)
            {
                lock (_lock)
                {
                    _remoteControl = machines[cursor].Id;
                }

                return (null, MuxLeave.RemoteControl);
            }

            return null;
        }, keepOn: Stay);
    }

    /// <summary>History's way back to a remote session: attach while it lives, resume once it ended.</summary>
    private (int? Shown, string Said) Resume(RemoteMachine machine, string sessionId, string? directory)
    {
        if (Rows().Count >= MuxColumn.Most)
        {
            return (null, $"gg holds {MuxColumn.Most} agents at most. End one to resume this session.");
        }

        var reached = _reach!(machine);
        if (reached.Link is not { } link)
        {
            return (null, reached.Refused ?? $"{machine.Name} could not be reached.");
        }

        var pty = new RemotePty(link);
        var held = Ask<AgentSessionList>(link, new ListAgentSessions())?.Sessions
            .FirstOrDefault(s => s.SessionId == sessionId)
            ?? new AgentSessionStanding { SessionId = sessionId, StartedAt = DateTimeOffset.MinValue, Alive = false };

        var (shown, refused) = Open(machine, link, pty, held, directory);
        if (shown is null)
        {
            pty.Dispose();
        }

        return (shown, refused ?? "");
    }

    /// <summary>
    /// Starts a new session, attaches to a live one, or resumes an ended one by its id,
    /// and puts it on a row - or answers the machine's refusal.
    /// </summary>
    private (int? Shown, string? Refused) Open(
        RemoteMachine machine, Gg.Client.IAgentLink link, RemotePty pty, AgentSessionStanding? session, string? directory)
    {
        var (columns, rows) = PaneSize();

        AgentFrame ask = session is { Alive: true } live
            ? new AttachAgentSession { SessionId = live.SessionId, Columns = columns, Rows = rows }
            : new StartAgentSession
            {
                Columns = columns,
                Rows = rows,
                SessionId = session?.SessionId ?? Guid.NewGuid().ToString(),
                Directory = directory ?? session?.Directory,
            };

        var answer = Ask<AgentFrame>(link, ask, f => f is AgentSessionStarted or AgentSessionRefused);

        if (answer is not AgentSessionStarted started)
        {
            return (null, (answer as AgentSessionRefused)?.Because
                ?? $"{machine.Name} did not answer. The channel may have dropped; try again.");
        }

        var agent = StartRemote(
            $"claude @ {machine.Name}", pty, started.SessionId, machine.Id,
            directory ?? session?.Directory ?? "");

        return (NumberOf(agent), null);
    }

    /// <summary>Sends a frame and waits, boundedly, for the answer it asked for.</summary>
    private static T? Ask<T>(Gg.Client.IAgentLink link, AgentFrame frame, Func<AgentFrame, bool>? wanted = null)
        where T : AgentFrame
    {
        using var heard = new ManualResetEventSlim();
        AgentFrame? answer = null;

        void Heard(AgentFrame arrived)
        {
            if (arrived is T && (wanted?.Invoke(arrived) ?? true) || wanted?.Invoke(arrived) is true)
            {
                answer ??= arrived;
                heard.Set();
            }
        }

        link.Heard += Heard;
        try
        {
            link.Send(frame);
            heard.Wait(TimeSpan.FromSeconds(15));
            return answer as T;
        }
        finally
        {
            link.Heard -= Heard;
        }
    }

    /// <summary>
    /// A screen of text right of the column, with the column's clicks and ctrl-g's keys, until a
    /// key answers with somewhere to go.
    /// </summary>
    /// <param name="keepOn">The tab that means "stay and repaint", for a screen with a cursor.</param>
    private (MuxTab? Next, MuxLeave? Leave) Menu(
        IHostTerminal terminal,
        MuxTab shown,
        Func<IReadOnlyList<string>> lines,
        Func<byte, byte[], (MuxTab? Next, MuxLeave? Leave)?> answer,
        MuxTab? keepOn = null)
    {
        var armed = false;

        void Repaint()
        {
            var width = Math.Max(terminal.Columns - MuxColumn.Width - 2, 10);
            var text = new StringBuilder();
            text.Append(MouseInput.Modes(MouseTrackingMode.VT200, MouseEncoding.SGR, focus: false, paste: false));
            text.Append(MuxColumn.Paint(MuxColumn.Lines(Rows(), shown, terminal.Rows, armed)));

            var said = lines();
            for (var row = 0; row < terminal.Rows; row++)
            {
                var line = row < said.Count ? said[row] : "";
                if (line.Length > width)
                {
                    line = line[..Math.Max(width - 1, 0)] + "…";
                }

                text.Append($"{Esc}[{row + 1};{MuxColumn.Width + 1}H{Esc}[0m{Esc}[K  ").Append(line);
            }

            text.Append($"{Esc}[?25l");
            Paint(terminal, text.ToString());
        }

        terminal.Resized += Repaint;
        terminal.Paint($"{Esc}[2J");

        try
        {
            Repaint();
            var ticked = DateTime.UtcNow;
            var typed = new byte[1024];
            while (true)
            {
                var read = terminal.Keystrokes.Read(typed, 0, typed.Length);
                if (read <= 0)
                {
                    if (DateTime.UtcNow - ticked > TimeSpan.FromSeconds(1))
                    {
                        ticked = DateTime.UtcNow;
                        Repaint();
                    }

                    Thread.Sleep(5);
                    continue;
                }

                foreach (var bytes in Keys(new ReadOnlySpan<byte>(typed, 0, read), armed))
                {
                if (Aside(bytes) is { } click)
                {
                    if (click.Pressed && click.Column <= MuxColumn.Width
                        && MuxColumn.At(Rows(), click.Row - 1, terminal.Rows) is { } chosen
                        && chosen != shown)
                    {
                        return (chosen, null);
                    }

                    continue;
                }

                if (armed)
                {
                    armed = false;
                    if (bytes.Length == 1 && MuxColumn.Key(bytes[0], Rows().Count) is { } switched)
                    {
                        if (switched != shown)
                        {
                            return (switched, null);
                        }

                        Repaint();
                        continue;
                    }
                }

                if (bytes.Length == 1 && bytes[0] == HostedBar.Prefix)
                {
                    armed = true;
                    Repaint();
                    continue;
                }

                var answered = answer(bytes.Length == 1 ? bytes[0] : (byte)0, bytes);
                // LEAVING FOR THE SHELL HAS NO NEXT TAB, and staying has none either on a screen
                // with no cursor: so a leave is asked for first. `l` read as "stay" until it was.
                if (answered is { } went
                    && (went.Leave is not null || (went.Next is { } next && next != keepOn)))
                {
                    return went;
                }

                if (answered is null && bytes is [0x1b])
                {
                    return (MuxTab.Gg, null);
                }

                Repaint();
                }
            }
        }
        finally
        {
            terminal.Resized -= Repaint;
            terminal.Paint($"{Esc}[?25h");
        }
    }

    /// <summary>
    /// Whether a key is the arrow ending in <paramref name="final"/> - <c>A</c> up, <c>B</c> down -
    /// in either form a terminal sends it.
    /// </summary>
    /// <remarks>
    /// <b>Both, because gg's console leaves application cursor keys on.</b> Terminal.Gui writes
    /// <c>ESC [ ? 1 h</c> and nothing writes the <c>l</c>, so by the time a list here is on screen
    /// the terminal sends <c>ESC O B</c>, not <c>ESC [ B</c>. Matching only the one meant only j
    /// and k moved - found by a person reaching for the arrows.
    /// </remarks>
    private static bool IsArrow(byte[] sequence, byte final) =>
        sequence is [0x1b, (byte)'[' or (byte)'O', var last] && last == final;

    /// <summary>
    /// One read, as the keys in it: ctrl-g, the key after it, and each mouse report are their own.
    /// </summary>
    /// <remarks>
    /// <b>Typed quickly, ctrl-g and a number arrive in one read</b>, and a prefix only ever looked
    /// for alone was never seen - found by a test typing them back to back, which a person at a
    /// keyboard does too. Anything else stays whole: an escape sequence, a paste, a click.
    /// </remarks>
    internal static IEnumerable<byte[]> Keys(ReadOnlySpan<byte> read, bool armed)
    {
        var keys = new List<byte[]>();
        var rest = read;
        while (rest.Length > 1)
        {
            // ONE MOUSE REPORT AT A TIME: a wheel turned once is several reports in one read.
            if (rest.Length > 3 && rest[0] == 0x1b && rest[1] == '[' && rest[2] == '<')
            {
                var end = rest[3..].IndexOfAny((byte)'M', (byte)'m');
                if (end < 0)
                {
                    break;
                }

                keys.Add(rest[..(end + 4)].ToArray());
                rest = rest[(end + 4)..];
                armed = false;
                continue;
            }

            if (rest[0] != HostedBar.Prefix && !(armed && rest[0] != 0x1b))
            {
                break;
            }

            armed = rest[0] == HostedBar.Prefix;
            keys.Add([rest[0]]);
            rest = rest[1..];
        }

        if (rest.Length > 0)
        {
            keys.Add(rest.ToArray());
        }

        return keys;
    }

    /// <summary>One frame at a time: a reader's repaint and the tick's never splice.</summary>
    private void Paint(IHostTerminal terminal, string frame)
    {
        lock (_painting)
        {
            terminal.Paint(frame);
        }
    }

    /// <summary>Whatever was typed at gg before the mux took the screen is not the agent's.</summary>
    private static void Drain(IHostTerminal terminal)
    {
        var stale = new byte[1024];
        while (terminal.Keystrokes.Read(stale, 0, stale.Length) > 0)
        {
        }
    }

    /// <summary>An SGR mouse report, read: where, and whether it is a press.</summary>
    internal readonly record struct Click(int Button, int Column, int Row, char Final)
    {
        /// <summary>A button going down: not the wheel, not a drag, not a release.</summary>
        public bool Pressed => Final == 'M' && (Button & (32 | 64)) == 0;
    }

    /// <summary>
    /// The click this read is, if it is one. A press is a button going down, not the wheel,
    /// not a drag and not a release.
    /// </summary>
    internal static Click? Aside(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 9 || bytes[0] != 0x1b || bytes[1] != '[' || bytes[2] != '<'
            || bytes[^1] is not ((byte)'M' or (byte)'m'))
        {
            return null;
        }

        var fields = Encoding.ASCII.GetString(bytes[3..^1]).Split(';');
        if (fields.Length != 3
            || !int.TryParse(fields[0], out var button)
            || !int.TryParse(fields[1], out var column)
            || !int.TryParse(fields[2], out var row))
        {
            return null;
        }

        return new Click(button, column, row, (char)bytes[^1]);
    }

    /// <summary>The same report, its column moved past the nav column.</summary>
    internal static ReadOnlyMemory<byte> Shifted(Click click) =>
        Encoding.ASCII.GetBytes(
            $"{Esc}[<{click.Button};{click.Column - MuxColumn.Width};{click.Row}{click.Final}");

    /// <summary>An SGR report re-encoded for a child that asked for the default encoding.</summary>
    private static byte[] X10(ReadOnlySpan<byte> sgr)
    {
        if (Aside(sgr) is not { } click)
        {
            return sgr.ToArray();
        }

        var button = click.Final == 'm' ? 3 : click.Button;
        return [0x1b, (byte)'[', (byte)'M',
            (byte)Math.Min(button + 32, 255),
            (byte)Math.Min(click.Column + 32, 255),
            (byte)Math.Min(click.Row + 32, 255)];
    }
}
