namespace Gg.Contracts;

/// <summary>What an enrolled machine may be for: taking flights, and maintaining a pool.</summary>
/// <remarks>
/// <b>Closed at two, and <see cref="Maintain"/> is the one that matters.</b> A
/// runner whose profile says maintain is a resident - the only kind that may
/// mint pool members (slice forty-three, rule 20). Any runner may run.
/// </remarks>
[VocabularyOf(VocabularyFingerprints.Contract)]
public static class ProfileRoles
{
    /// <summary>It claims flights.</summary>
    public const string Run = "run";

    /// <summary>It keeps a pool warm, and so may mint members.</summary>
    public const string Maintain = "maintain";

    public static IReadOnlyList<string> All { get; } = [Run, Maintain];
}

/// <summary>
/// What an enrolled machine is: an airspace document, <c>airspace/fleet/&lt;name&gt;.yaml</c>,
/// applied and governed like a strategy (ADR-0025 section 2).
/// </summary>
/// <remarks>
/// <para>
/// <b>It names, and the machine resolves</b> (slice forty-three, rule 14). An
/// <see cref="Agent"/> is a name, never a path to a binary; a
/// <see cref="Credentials"/> entry is a reference, never a secret. Which binary
/// and which secret stay the machine's to find, and <c>executor-binary</c>,
/// <c>intent-readers</c>, <c>pool-endpoint</c> and the <c>accept-*</c> flags
/// stay never-offerable.
/// </para>
/// <para>
/// <b>Its environment is what the lease matches</b> (rule 16): a runner enrolled
/// under a profile advertises at most <c>environment=&lt;Environment&gt;</c>,
/// whatever it says on its heartbeat. Verification can only subtract.
/// </para>
/// </remarks>
[PinnedId("5d1e8a37-4c26-4b9f-8e03-a7f2c9b6d451")]
public sealed record FleetProfile
{
    /// <summary>One or more of <see cref="ProfileRoles"/>.</summary>
    public required IReadOnlyList<string> Roles { get; init; }

    /// <summary>The charted environment a machine under this profile furnishes.</summary>
    public required string Environment { get; init; }

    /// <summary>The agent it runs, by name - never a path. Null for a machine that runs none.</summary>
    public string? Agent { get; init; }

    /// <summary>The forges it serves, in <c>vcs-hosts</c>' spelling: <c>key=host</c>.</summary>
    public IReadOnlyList<string> Forges { get; init; } = [];

    /// <summary>Where it may land work, in <c>destination-apis</c>' spelling: <c>key=api</c>.</summary>
    public IReadOnlyList<string> Destinations { get; init; } = [];

    /// <summary>Whether it sweeps the tenant's watches when idle.</summary>
    public bool Sweeps { get; init; }

    /// <summary>The credentials it needs, as references its own sources resolve - never a value.</summary>
    public IReadOnlyList<string> Credentials { get; init; } = [];

    /// <summary>The label the lease matches a runner under this profile by. Nothing yet.</summary>
    public static string LabelFor(FleetProfile profile) => "";

    /// <summary>The schema's own rule. Refuses nothing yet.</summary>
    public static string? Validate(FleetProfile profile) => null;

    /// <summary>Whether a change widens. Says nothing yet.</summary>
    public static (string Field, string Because)? Widening(FleetProfile prior, FleetProfile proposed) => null;
}

/// <summary>One applied fleet profile, as the read side serves it.</summary>
[PinnedId("e8b3c1f2-6a4d-4e9b-b7c5-3f1a2d8e9c60")]
public sealed record FleetProfileState
{
    /// <summary>The topology name the profile was applied to.</summary>
    public required string Name { get; init; }

    /// <summary>The per-name version in force, e.g. v2.</summary>
    public required string Version { get; init; }

    public required DateTimeOffset AppliedAt { get; init; }

    public required FleetProfile Profile { get; init; }
}

/// <summary>Every fleet profile in force for the tenant.</summary>
[PinnedId("2a9f6d4b-8c1e-4f73-a5b2-9d0e7c3f1b84")]
public sealed record FleetProfileList
{
    public required IReadOnlyList<FleetProfileState> Profiles { get; init; }
}
