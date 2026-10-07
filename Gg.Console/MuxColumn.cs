using System.Text;

namespace Gg.Console;

/// <summary>Where the mux can be showing: gg, one agent, the new-agent menu, or history.</summary>
public enum MuxPlace
{
    Gg,
    Agent,
    New,
    History,
}

/// <summary>One of the mux's tabs. <see cref="Number"/> is the agent's row, from 1, for an agent.</summary>
public readonly record struct MuxTab(MuxPlace Place, int Number = 0)
{
    public static MuxTab Gg => new(MuxPlace.Gg);

    public static MuxTab New => new(MuxPlace.New);

    public static MuxTab History => new(MuxPlace.History);

    public static MuxTab Agent(int number) => new(MuxPlace.Agent, number);
}

/// <summary>One live agent, as the column lists it.</summary>
/// <param name="Number">Its row, from 1: the key that switches to it after ctrl-g.</param>
/// <param name="Label">What it is: "plan · console", "compose · implement", "Claude Code".</param>
/// <param name="Running">How long its process has been running.</param>
/// <param name="Changed">Whether its screen changed while it was not shown.</param>
public sealed record MuxRow(int Number, string Label, TimeSpan Running, bool Changed);

/// <summary>One row of the column: its text, the tab it switches to, and whether it is the one shown.</summary>
public readonly record struct MuxLine(string Text, MuxTab? Tab, bool Active);

/// <summary>
/// The mux's navigation column (slice sixty-nine): thirty columns on the left, in the logo's blue,
/// listing gg, each live agent, "+ new agent", and history at the bottom.
/// </summary>
/// <remarks>
/// <para>
/// <b>One model, two painters.</b> While an agent is shown, the mux paints this column itself
/// with escape sequences (<see cref="Paint"/>). While gg is shown, Terminal.Gui draws the same
/// <see cref="Lines"/> in a view. Two layouts would put "gg" on a different row on either side of
/// a switch, and a click would land on the wrong tab.
/// </para>
/// <para>
/// <b>The logo's blue, never white</b> (the owner, 2026-10-07): <c>#7CB0CC</c>, the colour
/// <see cref="Views.ConsoleTheme.Stamp"/> gives the version badge. The hosted bar is inverse
/// video, which most terminals show white, and the column must not read as more bar.
/// </para>
/// </remarks>
public static class MuxColumn
{
    /// <summary>How wide the column is, in columns.</summary>
    public const int Width = 30;

    /// <summary>The most agents the mux holds: <c>1</c>-<c>9</c>, one key each.</summary>
    public const int Most = 9;

    /// <summary>The logo's blue, as an SGR colour triple.</summary>
    public const string Blue = "124;176;204";

    /// <summary>The ink on the blue: near-black, so the blue stays the colour.</summary>
    public const string Ink = "16;24;32";

    private const string Esc = "\u001b";

    /// <summary>The column's rows, top to bottom, exactly <paramref name="height"/> of them.</summary>
    /// <param name="agents">The live agents, in row order.</param>
    /// <param name="shown">What the mux is showing, marked as the active row.</param>
    /// <param name="height">How many rows the column has: the terminal's height.</param>
    /// <param name="armed">
    /// Whether ctrl-g was just pressed, so the hint spells out the keys that follow it.
    /// </param>
    /// <param name="now">Unused; the ages come in on the rows.</param>
    public static IReadOnlyList<MuxLine> Lines(
        IReadOnlyList<MuxRow> agents, MuxTab shown, int height, bool armed = false)
    {
        ArgumentNullException.ThrowIfNull(agents);

        var lines = new List<MuxLine>
        {
            Line(" gg", MuxTab.Gg, shown),
            new("", null, false),
        };

        foreach (var agent in agents.Take(Most))
        {
            var mark = agent.Changed ? " •" : "  ";
            var age = Age(agent.Running);
            var room = Width - 4 - age.Length - mark.Length - 1;
            var label = agent.Label.Length > room ? agent.Label[..Math.Max(room - 1, 0)] + "…" : agent.Label;
            lines.Add(Line($" {agent.Number} {label.PadRight(room)} {age}{mark}", MuxTab.Agent(agent.Number), shown));
        }

        lines.Add(Line(
            agents.Count >= Most ? " + new agent · nine is the most" : " + new agent",
            MuxTab.New,
            shown));

        // HISTORY PINNED TO THE BOTTOM, with the keys above it. On a terminal too short for
        // everything, the agents give way first: the column always says how to reach gg and history.
        var hint = armed
            ? new MuxLine(" 0 gg  1-9  + new  H history", null, false)
            : new MuxLine(" ctrl-g then 0-9, + or H", null, false);
        var bottom = new[] { hint, Line(" history", MuxTab.History, shown) };

        var body = Math.Max(height - bottom.Length, 1);
        var kept = lines.Take(body).ToList();
        while (kept.Count < body)
        {
            kept.Add(new MuxLine("", null, false));
        }

        kept.AddRange(bottom);
        return [.. kept.Take(Math.Max(height, bottom.Length)).Select(Fit)];
    }

    /// <summary>The tab a click on <paramref name="row"/> (from 0) chooses, if any.</summary>
    public static MuxTab? At(IReadOnlyList<MuxRow> agents, int row, int height)
    {
        var lines = Lines(agents, MuxTab.Gg, height);
        return row >= 0 && row < lines.Count ? lines[row].Tab : null;
    }

    /// <summary>
    /// The tab a key typed after ctrl-g names: <c>0</c> gg, <c>1</c>-<c>9</c> a live agent,
    /// <c>+</c> a new agent, <c>H</c> history. Null for anything else, and for an agent number
    /// nothing is on.
    /// </summary>
    public static MuxTab? Key(byte typed, int agents) => typed switch
    {
        (byte)'0' => MuxTab.Gg,
        >= (byte)'1' and <= (byte)'9' when typed - '0' <= agents => MuxTab.Agent(typed - '0'),
        (byte)'+' => MuxTab.New,
        (byte)'H' or (byte)'h' => MuxTab.History,
        _ => null,
    };

    /// <summary>
    /// The column as escape sequences, painted at the left edge of every row: ink on the logo's
    /// blue, and the active row turned over.
    /// </summary>
    public static string Paint(IReadOnlyList<MuxLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var painted = new StringBuilder();
        for (var row = 0; row < lines.Count; row++)
        {
            var line = lines[row];
            var dress = line.Active
                ? $"{Esc}[0;1;38;2;{Blue};48;2;{Ink}m"
                : $"{Esc}[0;38;2;{Ink};48;2;{Blue}m";

            painted.Append($"{Esc}[{row + 1};1H").Append(dress).Append(line.Text).Append($"{Esc}[0m");
        }

        return painted.ToString();
    }

    /// <summary>"Quit ends …": the question quitting gg asks while agents live.</summary>
    public static string QuitQuestion(IReadOnlyList<string> labels) =>
        $"{Running(labels.Count)} still running: {string.Join(", ", labels)}. Quitting gg ends "
        + (labels.Count == 1 ? "it" : "them")
        + ". Press q again to quit, or anything else to keep "
        + (labels.Count == 1 ? "it." : "them.");

    private static string Running(int count) => count == 1 ? "An agent is" : $"{count} agents are";

    private static MuxLine Line(string text, MuxTab tab, MuxTab shown) => new(text, tab, tab == shown);

    private static MuxLine Fit(MuxLine line) => line with
    {
        Text = line.Text.Length > Width ? line.Text[..Width] : line.Text.PadRight(Width),
    };

    private static string Age(TimeSpan running) => running.TotalMinutes < 1
        ? $"{Math.Max((int)running.TotalSeconds, 0)}s"
        : running.TotalHours < 1
            ? $"{(int)running.TotalMinutes}m"
            : $"{(int)running.TotalHours}h";
}

/// <summary>The commands that switch the mux, and the tabs they name.</summary>
public static class MuxCommands
{
    private static readonly Command[] Agents =
    [
        Command.ShowAgent1, Command.ShowAgent2, Command.ShowAgent3,
        Command.ShowAgent4, Command.ShowAgent5, Command.ShowAgent6,
        Command.ShowAgent7, Command.ShowAgent8, Command.ShowAgent9,
    ];

    /// <summary>The command that shows the agent on row <paramref name="number"/>, from 1.</summary>
    public static Command Agent(int number) => Agents[Math.Clamp(number, 1, MuxColumn.Most) - 1];

    /// <summary>The tab a command switches to, or null for a command that switches nothing.</summary>
    public static MuxTab? Tab(Command command) => command switch
    {
        Command.ShowGg => MuxTab.Gg,
        Command.ShowNewAgent => MuxTab.New,
        Command.ShowHistory => MuxTab.History,
        _ when Array.IndexOf(Agents, command) is >= 0 and var at => MuxTab.Agent(at + 1),
        _ => null,
    };

    /// <summary>The command a tab is switched to by: a click on the column dispatches it.</summary>
    public static Command For(MuxTab tab) => tab.Place switch
    {
        MuxPlace.Agent => Agent(tab.Number),
        MuxPlace.New => Command.ShowNewAgent,
        MuxPlace.History => Command.ShowHistory,
        _ => Command.ShowGg,
    };
}
