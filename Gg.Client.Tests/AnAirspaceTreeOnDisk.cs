namespace Gg.Client.Tests;

/// <summary>
/// Working copies on disk, holding documents that actually parse.
/// </summary>
/// <remarks>
/// <para>
/// <b>THREE TESTS WROTE THEIR OWN AND TWO OF THEM DID NOT PARSE.</b> Each
/// trimmed a work kind down to the keys it thought were relevant, and
/// <c>AirspaceTree.Read</c> correctly classified the result as a file that
/// sits where a document goes and does not read as one — so the apply refused
/// it as unreadable before reaching the topology, the door, or anything the
/// test was about. The failure looked like the feature under test and was not.
/// </para>
/// <para>
/// <b>So the documents live here, whole, taken from a real tenant's tree.</b>
/// A fixture that has silently measured the wrong thing twice is worth one
/// file — and this is the shape of the hazard rather than laziness: the parser
/// is strict on purpose, and a test author's idea of a minimal document is a
/// guess the parser does not share.
/// </para>
/// </remarks>
public static class AnAirspaceTreeOnDisk
{
    /// <summary>A temporary working copy holding one work kind.</summary>
    public static DirectoryInfo WithAWorkKind(string name = "score-hal")
    {
        var root = Directory.CreateTempSubdirectory("gg-tree-");

        Directory.CreateDirectory(Path.Combine(root.FullName, "airspace", "work-kinds"));

        File.WriteAllText(
            Path.Combine(root.FullName, "airspace", "work-kinds", $"{name}.yaml"),
            WorkKind);

        return root;
    }

    /// <summary>A temporary working copy holding one strategy.</summary>
    public static DirectoryInfo WithAStrategy(string name = "dev")
    {
        var root = Directory.CreateTempSubdirectory("gg-tree-");

        Directory.CreateDirectory(Path.Combine(root.FullName, "airspace", "strategies"));

        File.WriteAllText(
            Path.Combine(root.FullName, "airspace", "strategies", $"{name}.yaml"),
            Strategy);

        return root;
    }

    /// <summary>
    /// A work kind, whole, from a real tree the diff read successfully.
    /// </summary>
    public const string WorkKind =
        """
        context:
          scope: "**"
          constitution: "1.0.0"
        environments: dev
        repositories:
          - "JDX/JDNext"
        accepts:
          - tracker
          - repository
        produces:
          - loop.outcome
          - loop.digest
          - loop.question
          - destination.landed
        obligations:
          hal-in-scope:
            check: machine
            rule: no-file-outside-scope
        loops:
          score:
            executor: frontier
            discharges:
              - hal-in-scope
            moves:
              - read
              - search
            budget:
              wall-clock: "20m"
            on-exhaustion: handoff-to-human
        destinations:
          agentic-backlog:
            kind: work-item-tracker
            may-perform:
              - field
            may-write:
              - "Custom.HAL"
            requires:
              - hal-in-scope
        """;

    /// <summary>A strategy, whole, from the same tree.</summary>
    public const string Strategy =
        """
        kind: docker-host
        environment: dev
        inventory:
          pool: gg-pool-dev
          size: 2
          warm: 1
        pull-point: resident-runner
        image: "127.0.0.1:5000/gg-member@sha256:fa54cda495558afe3a281744a5a05165e6461dfb18b8300a4299798fd2db7254"
        bounds:
          pool-max: 2
        """;
}
