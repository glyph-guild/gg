namespace Gg.Contracts;

/// <summary>What a machine measures against its profile.</summary>
/// <remarks>
/// <b>Closed at three, and the agent's login is not one of them.</b> Whether the
/// agent can start is <see cref="AgentReading"/>'s, which already opens its
/// own gate; this is what only the machine can say about the rest of its
/// profile (slice forty-three, rule 25).
/// </remarks>
[VocabularyOf(VocabularyFingerprints.Contract)]
public static class ReadinessKinds
{
    /// <summary>The profile names an agent, and this machine declares a different one or none.</summary>
    public const string Agent = "agent";

    /// <summary>A credential reference the profile names, which this machine cannot resolve.</summary>
    public const string Credential = "credential";

    /// <summary>A forge the profile names, which this machine cannot reach.</summary>
    public const string Forge = "forge";

    public static IReadOnlyList<string> All { get; } = [Agent, Credential, Forge];
}

/// <summary>One thing a profile asks of a machine, and whether it has it.</summary>
[PinnedId("6f4b2e9a-1d73-4c85-b0a6-8e3c5f7d2a19")]
public sealed record ReadinessItem
{
    /// <summary>One of <see cref="ReadinessKinds"/>.</summary>
    public required string Kind { get; init; }

    /// <summary>
    /// What was checked: the agent's name, a credential's REFERENCE, a forge's
    /// key - never anything a check produced.
    /// </summary>
    public required string Subject { get; init; }

    public required bool Met { get; init; }

    /// <summary>Why not, in a sentence, when it is not met. Never a secret, and never a secret's absence spelled as one.</summary>
    public string? Diagnosis { get; init; }
}

/// <summary>
/// A runner's measurement of whether it meets its profile (slice forty-three,
/// rule 25), for the control plane to open - and close - bring-up gates by.
/// </summary>
/// <remarks>
/// <para>
/// <b>A reading, on <see cref="AgentReading"/>'s argument</b>: its own route
/// and its own <see cref="MeasuredAt"/>, never a field on the beat, because a
/// gate clears when the item VERIFIES on a later reading - not when somebody
/// says it is done.
/// </para>
/// <para>
/// <b>Against one version of the profile</b>, so a reading taken before an
/// apply cannot close a gate the new version opened.
/// </para>
/// </remarks>
[PinnedId("d2a8c6e1-5b39-4f07-9e4c-7a1f3b8d6c52")]
public sealed record ReadinessReading
{
    /// <summary>The profile's name.</summary>
    public required string Profile { get; init; }

    /// <summary>The profile version measured against.</summary>
    public required string Version { get; init; }

    /// <summary>Every item the profile asks for, met or not. Empty is a profile that asks for nothing checkable.</summary>
    public required IReadOnlyList<ReadinessItem> Items
    {
        get => field ?? [];
        init;
    }

    public required DateTimeOffset MeasuredAt { get; init; }
}
