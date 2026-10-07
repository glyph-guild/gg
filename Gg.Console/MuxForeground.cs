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
}

public sealed partial class Mux
{
    private const string Esc = "\u001b";

    private readonly Lock _painting = new();
    private ItineraryDrafts? _drafts;
    private Func<string, BoardPage?>? _plan;

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
                            dim: kept.Open, left: MuxColumn.Width);
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

                last = Math.Max(NumberOf(agent), 1);

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
                }
                else if (bytes.Length == 1 && bytes[0] == HostedBar.Prefix)
                {
                    armed = true;
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
    private (MuxTab? Next, MuxLeave? Leave) ShowNew(IHostTerminal terminal)
    {
        var here = Directory.GetCurrentDirectory();
        var full = Rows().Count >= MuxColumn.Most;
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
                "l    plan several flights with an agent",
                $"c    Claude Code, here: {here}",
                "n    a new flight (gg asks what kind, as `n` does)",
                "",
                "esc  back",
            ];

        return Menu(terminal, MuxTab.New, Lines, (typed, _) => full
            ? null
            : typed switch
            {
                (byte)'l' => (null, MuxLeave.Plan),
                (byte)'n' => (null, MuxLeave.Compose),
                (byte)'c' => StartClaudeCode(here) is { } started && NumberOf(started) is > 0 and var at
                    ? (MuxTab.Agent(at), null)
                    : (MuxTab.Gg, null),
                _ => null,
            });
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
            lines.Add("j/k move · enter opens a plan, or resumes a session as a new agent · esc back");
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

            var down = typed == (byte)'j' || sequence is [0x1b, (byte)'[', (byte)'B'];
            var up = typed == (byte)'k' || sequence is [0x1b, (byte)'[', (byte)'A'];
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
                if (answered is { } went && went.Next != keepOn)
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
    /// One read, as the keys in it: ctrl-g, and the key after it, are each their own.
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
        while (rest.Length > 1 && (rest[0] == HostedBar.Prefix || (armed && rest[0] != 0x1b)))
        {
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
