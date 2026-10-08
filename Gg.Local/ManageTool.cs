namespace Gg.Local;

/// <summary>
/// The <c>gg-manage</c> tool server's tools: gg's management layer, for an agent a person runs
/// beside gg (owner's call, 2026-10-08). Each tool runs the <c>gg</c> verb a person would type.
/// </summary>
/// <remarks>
/// <para>
/// <b>Reads are granted at launch; acts never are.</b> An act changes what happens to somebody's
/// work - a gate answered, a proposal opened, a flight flown or grounded, a name declared or
/// retired - so Claude Code asks the person before each call, and that prompt is the confirmation.
/// </para>
/// <para>
/// <b>No credentials and no allowances</b>: an agent has no business near a secret, and the
/// fleet's allowances are an administrator's.
/// </para>
/// </remarks>
public static class ManageTool
{
    /// <summary>The server key, and therefore the tool-name prefix.</summary>
    public const string Server = "gg-manage";

    public const string WhoAmI = "whoami";
    public const string ListGates = "list_gates";
    public const string ListBoard = "list_board";
    public const string ListFlights = "list_flights";
    public const string ShowFlight = "show_flight";
    public const string FlightLog = "flight_log";
    public const string WhyFlight = "why_flight";
    public const string ListRunners = "list_runners";
    public const string ListWatches = "list_watches";

    public const string DecideGate = "decide_gate";
    public const string AnswerNomination = "answer_nomination";
    public const string Fly = "fly";
    public const string Ground = "ground";
    public const string DeclareName = "declare_name";
    public const string RetireName = "retire_name";

    /// <summary>The tools that only read: granted at launch.</summary>
    public static IReadOnlyList<string> Reads { get; } =
        [WhoAmI, ListGates, ListBoard, ListFlights, ShowFlight, FlightLog, WhyFlight, ListRunners, ListWatches];

    /// <summary>The tools that act: declared, never granted, so the person is asked each time.</summary>
    public static IReadOnlyList<string> Acts { get; } =
        [DecideGate, AnswerNomination, Fly, Ground, DeclareName, RetireName];

    /// <summary>The tool as the agent sees it, and as a launch grants it.</summary>
    public static string Qualified(string tool) => $"mcp__{Server}__{tool}";
}
