namespace Gg.Contracts;

/// <summary>
/// A label's disposition: how much its word is worth.
/// </summary>
/// <remarks>
/// <para>
/// <b>Derived where a meaning exists, and visibly <c>stated</c> where none
/// does.</b> A label whose name has a registered meaning is evaluated by the
/// control plane from produced facts - that is <c>measured</c>. A label with
/// no registered meaning is an advertised claim, admitted rather than refused,
/// and the disposition is what keeps the admission honest: the lie hazard was
/// never the claim, it is a claim wearing measurement's clothes.
/// </para>
/// <para>
/// <b>Deliberately not <see cref="EvidenceVoices"/>,</b> though the words
/// coincide today. One says what a gate's evidence is; this says what a
/// capability claim is. Coupling two closed enumerations makes a value added
/// for either concept a halt for readers of the other.
/// </para>
/// </remarks>
[VocabularyOf(VocabularyFingerprints.Contract)]
public static class LabelDispositions
{
    /// <summary>A registered predicate, evaluated from produced facts.</summary>
    public const string Measured = "measured";

    /// <summary>An advertised claim with no registered meaning.</summary>
    public const string Stated = "stated";

    public static IReadOnlyList<string> All { get; } = [Measured, Stated];
}

/// <summary>
/// Who can make a checklist item true, in the slice where strategies do not
/// exist yet.
/// </summary>
/// <remarks>
/// <b>Closed at two, and the closure is the design.</b> In this slice a
/// requirement is either already true via matching or nobody in the fleet can
/// satisfy it. Strategy actions and human assists arrive with the phases that
/// own them; a rendered placeholder for machinery that does not exist would be
/// the checklist containing a promise, which is the same defect as containing
/// a procedure. A third value here means a strategy exists - a design event
/// that arrives as a deliberate contract change.
/// </remarks>
[VocabularyOf(VocabularyFingerprints.Contract)]
public static class ChecklistSatisfiers
{
    /// <summary>A live runner's advertised labels already contain it.</summary>
    public const string MatchingRunner = "already-true-via-matching";

    /// <summary>No runner in the fleet advertises it: a declared capability gap.</summary>
    public const string Nobody = "nobody-declared-capability-gap";

    public static IReadOnlyList<string> All { get; } = [MatchingRunner, Nobody];
}

/// <summary>Ask the control plane to add an environment name to the chart.</summary>
/// <remarks>
/// <para>
/// <b>The chart ships in the same release as the <c>environment:</c> field,
/// never later.</b> An envelope naming an uncharted environment is refused at
/// apply with the fix in the refusal - chart it - which only works if charting
/// exists the moment the field does.
/// </para>
/// <para>
/// The meaning is optional, and nothing in this slice enforces one: an entry
/// with no meaning admits the label as <see cref="LabelDispositions.Stated"/>.
/// Registering a meaning is what earns <see cref="LabelDispositions.Measured"/>.
/// </para>
/// </remarks>
[PinnedId("0188cc05-9c29-4fc1-bf06-5e96d2f39245")]
public sealed record ChartEnvironmentRequest
{
    /// <summary>The name an envelope may then select, e.g. aspire-payments.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// What the name means, as a fact predicate - or null, which is what
    /// <c>stated</c> means.
    /// </summary>
    public string? Meaning { get; init; }
}

/// <summary>One charted environment name, and who put it there.</summary>
/// <remarks>
/// Registration in v0 is unrestricted AND logged, and the second half is
/// load-bearing: a chart entry that could not say who made it would be an
/// unaudited way to widen what every envelope may select. Who MAY chart stays
/// an open question elsewhere; attribution is not.
/// </remarks>
[PinnedId("6b7ab4f9-8b79-477e-8ba2-98e64faa8eea")]
public sealed record EnvironmentCharted
{
    public required string Name { get; init; }

    /// <summary>The registered meaning, or null when the name is a claim.</summary>
    public string? Meaning { get; init; }

    /// <summary>One of <see cref="LabelDispositions"/>. Derived from the meaning, never typed.</summary>
    public required string Disposition { get; init; }

    /// <summary>Who charted it - a display a person can read, not an id.</summary>
    public required string ChartedBy { get; init; }

    public required DateTimeOffset ChartedAt { get; init; }
}

/// <summary>The tenant's chart: every environment name an envelope may select.</summary>
/// <remarks>
/// An envelope rather than a bare array, for the same reason
/// <see cref="FlightList"/> is: a bare array has nowhere to put the paging
/// this will grow.
/// </remarks>
[PinnedId("2a4b741a-d203-4c50-b854-add05332f26b")]
public sealed record EnvironmentChart
{
    public required IReadOnlyList<EnvironmentCharted> Environments { get; init; }
}

/// <summary>A label a runner advertises, with its disposition beside it.</summary>
/// <remarks>
/// The disposition travels WITH the name everywhere the name does - the runner
/// listing, the checklist, the refusal text - so a stated claim can never be
/// read as a measurement by losing its qualifier in transit.
/// </remarks>
[PinnedId("0ab36597-d0e4-4c86-8142-3822d77326d8")]
public sealed record AdvertisedLabel
{
    /// <summary>The label as matched, e.g. environment=aspire-payments.</summary>
    public required string Name { get; init; }

    /// <summary>One of <see cref="LabelDispositions"/>.</summary>
    public required string Disposition { get; init; }
}

/// <summary>One precondition on a flight's clock starting.</summary>
[PinnedId("6fc1044a-781a-4507-9272-e3c8ee4db747")]
public sealed record ChecklistItem
{
    /// <summary>What must hold, as the label the matcher will actually use.</summary>
    public required string Requirement { get; init; }

    /// <summary>How the requirement is checked, as a sentence a person can audit.</summary>
    public required string Verification { get; init; }

    /// <summary>One of <see cref="ChecklistSatisfiers"/>.</summary>
    public required string Satisfier { get; init; }

    /// <summary>
    /// The waiting sentence, when nobody can satisfy this - or null when it is
    /// already true. A sentence that is always present is one nobody reads.
    /// </summary>
    public string? WhenUnmet { get; init; }

    /// <summary>One of <see cref="LabelDispositions"/>.</summary>
    public required string Disposition { get; init; }
}

/// <summary>
/// The flight checklist: what must hold before a flight's clock starts.
/// </summary>
/// <remarks>
/// <para>
/// <b>Derived, never authored</b> - the third flight artifact. The flight plan
/// says what would happen, the flight log says what happened, the checklist
/// says what must hold first. It is computed mechanically from the envelope's
/// selections; nothing in it is a procedure, and the moment an item holds a
/// command line the concept has died one layer down.
/// </para>
/// <para>
/// One shape serves both reads: the tenant-level plan (<c>FlightNumber</c>
/// null - what WOULD a flight under the current envelope need) and the
/// per-flight checklist (pinned to what that flight compiled at creation).
/// </para>
/// </remarks>
[PinnedId("3809f425-8828-45fa-bbe1-090c0a1271c6")]
public sealed record Checklist
{
    /// <summary>The envelope version the requirements derive from.</summary>
    public required string EnvelopeVersion { get; init; }

    /// <summary>Rendered, e.g. GG-1042 - or null for the tenant-level plan.</summary>
    public string? FlightNumber { get; init; }

    /// <summary>The environment selection the requirements came from, if any.</summary>
    public string? Environment { get; init; }

    /// <summary>The repository selection, if any. Validated at flight creation, never a label.</summary>
    public string? Repository { get; init; }

    /// <summary>The compiled labels, exactly as the lease matcher reads them.</summary>
    public required IReadOnlyList<string> RequiredLabels { get; init; }

    public required IReadOnlyList<ChecklistItem> Items { get; init; }
}
