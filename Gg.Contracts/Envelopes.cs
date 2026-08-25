namespace Gg.Contracts;

/// <summary>
/// A tenant's envelope as the control plane holds it, with the version that
/// names this exact state.
/// </summary>
/// <remarks>
/// <para>
/// <b>The version is the point.</b> Every flight records the
/// <c>envelope-version</c> it ran under, and that only means something if the
/// version resolves to a concrete state afterwards. Held as a file in a
/// customer's repository it would be a hash plus a hope the commit survives;
/// held here it is a lookup rather than an investigation - the same argument
/// that settled how the constitution is distributed.
/// </para>
/// <para>
/// <b>Not the same fact as the contract's version.</b> The package version and
/// the fingerprint ledger cover the SCHEMA: adding a field to
/// <see cref="Obligation"/> moves them. This covers a tenant's INSTANCE:
/// editing an obligation moves it. Two facts, two ledgers, and the distinction
/// has had to be corrected once already - when "the contract's package version
/// and GG-Protocol-Version are the same fact" turned out to be false.
/// </para>
/// </remarks>
[PinnedId("b81d47f9-05a2-4e36-9c8f-3a70d2e5b164")]
public sealed record EnvelopeState
{
    /// <summary>Names this exact envelope, permanently.</summary>
    public required string Version { get; init; }

    public required Envelope Envelope { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }

    /// <summary>
    /// Who made the change this version records.
    /// </summary>
    /// <remarks>
    /// Article XII: every agent action is traceable to an identity. Governing
    /// the thing that governs flights is exactly where an unattributed change
    /// would matter most.
    /// </remarks>
    public required string UpdatedBy { get; init; }
}

/// <summary>What the control plane says after an envelope is applied.</summary>
/// <remarks>
/// Carries the new version rather than the envelope: the caller already has
/// the envelope, and what it does not know is the name the control plane gave
/// this state.
/// </remarks>
[PinnedId("3e6a0c95-8d71-4b24-a0f3-51c8e7d94b06")]
public sealed record EnvelopeApplied
{
    public required string Version { get; init; }

    public required DateTimeOffset AppliedAt { get; init; }

    /// <summary>
    /// Whether anything changed.
    /// </summary>
    /// <remarks>
    /// Applying an identical envelope is a no-op and says so rather than
    /// minting a version that means nothing. A version per apply would make
    /// "which rules governed this change" answer differently for two flights
    /// that ran under the same rules.
    /// </remarks>
    public required bool Changed { get; init; }

    /// <summary>
    /// The field a widening named, when this apply was diverted to a gate
    /// instead of landing. Null on every apply that landed or was refused.
    /// </summary>
    /// <remarks>
    /// Member additions, so an older reader keeps reading applies and a newer
    /// one learns where the gate went. When set, <see cref="Version"/> is the
    /// version still in force - nothing was minted.
    /// </remarks>
    public string? Widens { get; init; }

    /// <summary>The flight the widening rides, when it was diverted.</summary>
    public string? Flight { get; init; }

    /// <summary>Who the gate awaits, when it was diverted.</summary>
    public string? Awaiting { get; init; }
}
