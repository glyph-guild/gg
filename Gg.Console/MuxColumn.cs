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
    public const int Width = 30;

    public const int Most = 9;

    public const string Blue = "124;176;204";

    public static IReadOnlyList<MuxLine> Lines(
        IReadOnlyList<MuxRow> agents, MuxTab shown, int height, bool armed = false) => [];

    public static MuxTab? At(IReadOnlyList<MuxRow> agents, int row, int height) => null;

    public static MuxTab? Key(byte typed, int agents) => null;

    public static string Paint(IReadOnlyList<MuxLine> lines) => "";

    public static string QuitQuestion(IReadOnlyList<string> labels) => "";
}

/// <summary>The commands that switch the mux, and the tabs they name.</summary>
public static class MuxCommands
{
    public static Command Agent(int number) => Command.ShowGg;

    public static MuxTab? Tab(Command command) => null;

    public static Command For(MuxTab tab) => Command.ShowGg;
}
