using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>Layers for a root, the investigate base, and children below it.</summary>
internal static class ChainFixtures
{
    public const string Root = """
        context:
          scope: "**"
          constitution: "1.0.0"
        instructions:
          - "Keep your summary short."
        obligations:
          root-finished:
            check: machine
            rule: loop-not-exhausted
          widen-root:
            check: human
            when: "envelope widens"
            approver: platform-owner
        loops:
          implement:
            executor: frontier
            discharges:
              - root-finished
            moves:
              - anything
            budget:
              wall-clock: "20m"
            on-exhaustion: handoff-to-human
        destinations:
          pull-request:
            kind: pull-request
            requires:
              - root-finished
        """;

    public static EnvelopeLayer Floor() => new()
    {
        Role = Roles.Root,
        Name = Roles.Root,
        Document = WorkKindFixtures.Parsed(Root),
        Version = "v20",
    };

    public static EnvelopeLayer Base(string text = WorkKindFixtures.Base) => new()
    {
        Role = Roles.WorkKind,
        Name = "investigate",
        Parent = Roles.Root,
        Document = WorkKindFixtures.Parsed(text),
        Version = "v8",
    };

    public static EnvelopeLayer Child(
        string name = "investigate-instructions",
        string parent = "investigate",
        string text = WorkKindFixtures.Child) => new()
    {
        Role = Roles.WorkKind,
        Name = name,
        Parent = parent,
        Extension = WorkKindFixtures.Extension(text),
        Version = "v1",
    };

    /// <summary>A child that changes only its brief, extending <paramref name="parent"/>.</summary>
    public static EnvelopeLayer Brief(string name, string parent) =>
        Child(name, parent, $"extends: {parent}\nbrief: \"{name}\"\n");
}
