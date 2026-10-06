namespace Gg.Local;

/// <summary>
/// The planning tool server's key and tools, declared once for the server that serves them and
/// the mux session that grants them. Slice sixty-three, and sixty-six for the mux.
/// </summary>
/// <remarks>
/// <b>One declaration, every spelling</b>, as <see cref="IntentTool"/> is: the server answers
/// these names, the mux grants them as <c>mcp__gg-itinerary__&lt;name&gt;</c>, and a third
/// copy is how one of them stops agreeing.
/// </remarks>
public static class PlanningTool
{
    /// <summary>The server's own key: it holds a person's session, so it is not the runner's <c>gg</c>.</summary>
    public const string Server = "gg-itinerary";

    public const string SetIntent = "set_intent";
    public const string DraftLeg = "draft_leg";
    public const string ReviseLeg = "revise_leg";
    public const string DropLeg = "drop_leg";
    public const string ShowPlan = "show_plan";
    public const string Propose = "propose";

    /// <summary>Every tool, in the order the server lists them.</summary>
    public static IReadOnlyList<string> All { get; } =
        [SetIntent, DraftLeg, ReviseLeg, DropLeg, ShowPlan, Propose];

    /// <summary>The name Claude Code grants a tool of this server by.</summary>
    public static string Qualified(string tool) => $"mcp__{Server}__{tool}";
}
