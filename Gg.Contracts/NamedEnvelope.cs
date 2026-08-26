namespace Gg.Contracts;

/// <summary>
/// One named envelope document, as the stream holds it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two document shapes behind one name, because a role decides which.</b> A
/// <c>root</c> or <c>work-kind</c> name carries a full <see cref="Envelope"/>; a
/// <c>narrowing</c> carries an <see cref="EnvelopeNarrowing"/>, which is a partial
/// document by design - ADR-0014 took a type per role rather than one type with
/// optional members, so that the strongest form of the rule is one a document
/// cannot express.
/// </para>
/// <para>
/// <b>Strategies are not here.</b> They have their own door already, and a
/// reader assembling the whole estate reads both. One list that covered every
/// role would need a third nullable member and would still be two reads on the
/// server, so it would buy nothing but a wider type.
/// </para>
/// <para>
/// <b>The version is qualified</b> - <c>payments@v4</c>, never a bare <c>v4</c> -
/// because ADR-0014 made versions per-name and a bare ordinal means root.
/// </para>
/// </remarks>
[PinnedId("f6aec1a4-74cc-4b7c-915b-89ba33d500b0")]
public sealed record NamedEnvelopeState
{
    /// <summary>The name this document is filed under.</summary>
    public required string Name { get; init; }

    /// <summary>One of <see cref="Roles"/>, taken from the topology and never from the document.</summary>
    public required string Role { get; init; }

    /// <summary>Names this exact document, permanently. Qualified: <c>payments@v4</c>.</summary>
    public required string Version { get; init; }

    /// <summary>The document, when the role carries a whole envelope.</summary>
    public Envelope? Envelope { get; init; }

    /// <summary>The document, when the role carries constraints only.</summary>
    public EnvelopeNarrowing? Narrowing { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }

    /// <summary>Who made the change this version records. Article XII.</summary>
    public required string UpdatedBy { get; init; }
}

/// <summary>
/// Every named envelope document a tenant has in force.
/// </summary>
/// <remarks>
/// <b>What <c>pull</c> reads.</b> Rendering the estate as files is a fan-out over
/// the topology by construction (ADR-0014 accepted that cost when it chose a
/// stream per name), so the fan-out happens once, on the server, rather than once
/// per name across the wire.
/// </remarks>
[PinnedId("8aa016b8-2a6b-4ded-9437-a92c2b149ee1")]
public sealed record NamedEnvelopeList
{
    public required IReadOnlyList<NamedEnvelopeState> Documents { get; init; }
}

/// <summary>
/// A document applied to a name: exactly one shape, matching the name's role.
/// </summary>
/// <remarks>
/// <para>
/// <b>A wrapper rather than two doors, because the role already decides.</b> The
/// topology says what a name carries, so a body naming the wrong shape is a
/// mistake the control plane can describe precisely - <i>this name is a
/// narrowing and you sent an envelope</i> - which two endpoints would turn into
/// a 404 on the one the caller did not pick.
/// </para>
/// <para>
/// <b>What is deliberately NOT here is the precondition.</b> <c>based-on:</c> is
/// consumed by the parser and travels as a query parameter, never as a member:
/// the stored form of this document is the idempotence key, its field-by-field
/// comparison decides whether an apply gates, and its bytes are what the
/// composition digest hashes. A member that changes on every pull would mint a
/// version per document per pull and divert every one of them to a gate.
/// </para>
/// </remarks>
[PinnedId("1f348f1e-cdf5-4d9c-9b37-455028b12312")]
public sealed record NamedEnvelopeApply
{
    /// <summary>Set for a <c>root</c> or <c>work-kind</c> name.</summary>
    public Envelope? Envelope { get; init; }

    /// <summary>Set for a <c>narrowing</c> name.</summary>
    public EnvelopeNarrowing? Narrowing { get; init; }

    /// <summary>Why this body cannot be applied at all, or null when it can.</summary>
    /// <remarks>
    /// Shape only - whether it matches the NAME's role is the control plane's to
    /// answer, because only the topology knows the role. Both sides failing
    /// closed on their own format is what makes the pair trustworthy.
    /// </remarks>
    public string? Validate() => (Envelope, Narrowing) switch
    {
        (null, null) =>
            "An apply carries a document, and this one carries neither an envelope nor a "
          + "narrowing. An empty body is not a way to retire a name - retiring is a "
          + "terminal version, gated and attributed like any other change.",
        (not null, not null) =>
            "An apply carries ONE document, and this one carries both an envelope and a "
          + "narrowing. A name has one role, so only one of them could ever have been "
          + "applied, and guessing which would be the control plane choosing a policy.",
        _ => null,
    };
}
