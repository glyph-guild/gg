using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// An investigate-shaped base and the child that writes its analysis to the
/// tracker: slice seventy-seven's walk, as text an author would write.
/// </summary>
internal static class WorkKindFixtures
{
    public const string Landing = "Your whole result is the landing you propose.";

    public const string Base = """
        description: "Analyse a work item."
        brief: "Investigate the work item."
        context:
          scope: "**"
          constitution: "1.0.0"
        accepts:
          - repository
        produces:
          - landing.proposal
        instructions:
          - "Read the work item."
          - "Your whole result is the landing you propose."
        obligations:
          investigate-finished:
            check: machine
            rule: loop-not-exhausted
        loops:
          investigate:
            executor: frontier
            model: claude-opus-5-5
            discharges:
              - investigate-finished
            moves:
              - propose-landing
              - read
            budget:
              wall-clock: "20m"
            on-exhaustion: handoff-to-human
        destinations:
          none:
            kind: none
            requires:
              - investigate-finished
        """;

    public const string Child = """
        extends: investigate
        brief: "Investigate the work item and write the plan to its tracker."
        produces:
          - destination.landed
        instructions:
          - "Write the analysis to Custom.ClaudeCodePlan as html."
        remove:
          instructions:
            - "Your whole result is the landing you propose."
          destinations:
            - none
        loops:
          investigate:
            executor: frontier
            model: claude-opus-5-5
            discharges:
              - investigate-finished
            moves:
              - propose-work-item
              - read
            budget:
              wall-clock: "20m"
            on-exhaustion: handoff-to-human
        destinations:
          ado:
            kind: work-item-tracker
            requires:
              - investigate-finished
            may-perform:
              - field
            may-write:
              - Custom.ClaudeCodePlan
        """;

    public static Envelope Parsed(string text) =>
        EnvelopeYaml.Parse(text).Envelope
        ?? throw new InvalidOperationException(EnvelopeYaml.Parse(text).Diagnosis);

    public static EnvelopeExtension Extension(string text) =>
        EnvelopeYaml.ParseExtension(text).Extension
        ?? throw new InvalidOperationException(EnvelopeYaml.ParseExtension(text).Diagnosis);

    public static WorkKindResolution Resolve(string child) =>
        WorkKindChain.Resolve("investigate", Parsed(Base), [("investigate-instructions", Extension(child))]);
}
