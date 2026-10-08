using Gg.Local;

namespace Gg.Console;

/// <summary>
/// What an agent gg launches for a flight or a plan is told first: what the session is for, and
/// which of gg's tools carry its answer (owner's call, 2026-10-07).
/// </summary>
/// <remarks>
/// <para>
/// <b>Sent as the session's first message, not a system prompt</b>, so the person sees it in the
/// transcript and the agent answers it - the conversation starts where gg means it to.
/// </para>
/// <para>
/// <b>Passed before every flag.</b> <c>--allowedTools</c> and <c>--mcp-config</c> each take a list,
/// so a prompt after them is read as one more tool name.
/// </para>
/// <para>
/// <b>One line, and the tools by their qualified names</b>, from the declarations that own them:
/// the name an agent calls is the name it was granted, spelled once.
/// </para>
/// </remarks>
public static class AgentOpening
{
    /// <summary>The opening for an agent composing one flight's intent.</summary>
    public static string Compose(ComposeBrief brief)
    {
        ArgumentNullException.ThrowIfNull(brief);

        var kind = brief.WorkKind is { Length: > 0 } named ? $" of the '{named}' work kind" : "";
        var against = brief.Against.Count > 0
            ? $", against {string.Join(", ", brief.Against)}"
            : ", naming no repository";

        return $"You are helping me write the intent for one new gg flight{kind}{against}. An intent "
             + "says what the flight should achieve, in a sentence or a short paragraph a fleet agent "
             + "can act on without asking anything. Ask me what the flight is for and help me make it "
             + "specific; do not do the work yourself, because another agent flies it. When I say it "
             + $"is ready, submit it with the {IntentTool.Qualified} tool - that is the only way it "
             + "reaches gg, so do not write it to a file or leave it in your reply. Submitting again "
             + "replaces it. After submitting, tell me to close this session, which opens the flight.";
    }

    /// <summary>The opening for an agent drafting a plan in <paramref name="draft"/>.</summary>
    public static string Plan(string draft)
    {
        static string Q(string tool) => PlanningTool.Qualified(tool);

        return $"You are helping me draft a gg plan, the draft '{draft}': one intent and a list of "
             + "legs, each leg a piece of work one flight will do. Draft it only with the "
             + $"{PlanningTool.Server} tools. Start with {Q(PlanningTool.ShowPlan)}: the draft may "
             + "already have legs, so carry on from what is there. Use "
             + $"{Q(PlanningTool.SetIntent)} to say what the plan is about, {Q(PlanningTool.DraftLeg)} "
             + "to add each leg (a subject saying which piece, a work_kind from its menu, a reason, "
             + "and after when it must follow another leg), and "
             + $"{Q(PlanningTool.ReviseLeg)} or {Q(PlanningTool.DropLeg)} to change one. I can edit "
             + "the same draft from gg's panel (ctrl-g), so show the plan again before changing a "
             + "leg you have not just seen. Do not write the plan file yourself and do not start any "
             + $"of the work. Only call {Q(PlanningTool.Propose)} when I say so: it proposes the plan "
             + "as me, and it waits for its gate. Begin by showing the plan and asking me what it "
             + "is for.";
    }

    /// <summary>The opening for an agent managing the airspace, in the working copy it runs in.</summary>
    public static string Airspace() =>
        "You are helping me manage this tenant's gg airspace: the envelope documents in this "
      + "working copy, which govern what every flight may do. Use gg's tools rather than guessing "
      + $"at commands. Start with {AirspaceContextTool.Qualified}, which says how documents are "
      + "read and what this copy holds; if it says nothing has been pulled or a document is "
      + $"missing, call {AirspacePullTool.Qualified}. Hand back each document you write or change "
      + $"with {DocumentTool.Qualified} rather than editing the file yourself. Before anything is "
      + $"applied, call {AirspaceDiffTool.Qualified} and show me what would change. Only call "
      + $"{AirspaceApplyTool.Qualified} when I say to apply - it acts as me, gg asks me before "
      + "each call, and a loosening waits for a person to approve it. Begin by describing the "
      + "airspace and asking me what I want to change.";

    /// <summary>The opening for an agent managing gg through the <c>gg-manage</c> tools.</summary>
    public static string Manage()
    {
        static string Q(string tool) => ManageTool.Qualified(tool);

        return "You are helping me manage gg: the flights my tenant's fleet runs, the gates waiting on "
             + "me, and the work proposed on the board. Use only the gg-manage tools; never guess at gg "
             + $"commands. To look: {Q(ManageTool.WhoAmI)}, {Q(ManageTool.ListGates)}, "
             + $"{Q(ManageTool.ListBoard)}, {Q(ManageTool.ListFlights)}, {Q(ManageTool.ShowFlight)}, "
             + $"{Q(ManageTool.FlightLog)}, {Q(ManageTool.WhyFlight)}, {Q(ManageTool.ListRunners)} and "
             + $"{Q(ManageTool.ListWatches)}. To act: {Q(ManageTool.DecideGate)}, "
             + $"{Q(ManageTool.AnswerNomination)}, {Q(ManageTool.Fly)}, {Q(ManageTool.Ground)}, "
             + $"{Q(ManageTool.DeclareName)} and {Q(ManageTool.RetireName)}. Every act is taken as me "
             + "and gg asks me before each one, so call an act only when I say to, and tell me what it "
             + "will do first. Begin by showing me what is waiting on me - the gates and the board - "
             + "and asking what I want to do.";
    }
}
