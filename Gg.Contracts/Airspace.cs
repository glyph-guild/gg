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
/// <b>Closed at four, and each new value is the design event the previous
/// closure prophesied.</b> Slice eight closed this at two with the note that a
/// third value here means a strategy exists — a design event that arrives as a
/// deliberate contract change. Slice twelve was that event:
/// <see cref="DeclinedByBound"/> marks a requirement a strategy manages and a
/// declared bound currently declines, which is neither already-true nor a
/// capability gap — the same silence with the opposite remedy, and the whole
/// reason it must not wear either of the other words.
/// <para>
/// <b><see cref="Withheld"/> is the fourth, and the other three would each be a
/// lie about a different thing.</b> A runner reserved to somebody else, parked
/// by a person, or named by a direction and not currently answering is capacity
/// that EXISTS and is being held back. Reporting it as
/// <see cref="MatchingRunner"/> says the requirement is met while no flight can
/// move; as <see cref="Nobody"/> it says the fleet cannot do this, and sends a
/// person to bring up a machine they already own; as
/// <see cref="DeclinedByBound"/> it says a strategy is managing it when no
/// strategy is involved. The remedy for all three withholdings is a PERSON, and
/// none of the other words leads anybody to one.
/// </para>
/// </remarks>
[VocabularyOf(VocabularyFingerprints.Contract)]
public static class ChecklistSatisfiers
{
    /// <summary>A live runner's advertised labels already contain it.</summary>
    public const string MatchingRunner = "already-true-via-matching";

    /// <summary>No runner in the fleet advertises it: a declared capability gap.</summary>
    public const string Nobody = "nobody-declared-capability-gap";

    /// <summary>A strategy manages it and a declared bound currently declines it.</summary>
    public const string DeclinedByBound = "declined-by-bound";

    /// <summary>
    /// A runner could satisfy it and a declaration is holding it back: reserved
    /// elsewhere, parked, or directed at a machine that is not answering.
    /// </summary>
    /// <remarks>
    /// The item's <c>whenUnmet</c> reason says WHICH of the three, because the
    /// remedies differ — release a reservation, clear a parking, or start the
    /// machine somebody named. Withheld is the family; the reason is the member.
    /// </remarks>
    public const string Withheld = "withheld-by-declaration";

    public static IReadOnlyList<string> All { get; } =
        [MatchingRunner, Nobody, DeclinedByBound, Withheld];
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
    /// Why this is unmet, when nobody can satisfy it - or null when it is
    /// already true. A reason that is always present is one nobody reads.
    /// A <see cref="Reason"/> since 0.54.0, the <c>FlightSummary.Waiting</c>
    /// break: same wire name, loud type change, sentence derived.
    /// </summary>
    public Reason? WhenUnmet { get; init; }

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

/// <summary>Ask the control plane to declare an envelope name in the topology.</summary>
/// <remarks>
/// <para>
/// <b>Declaring is what makes a name reachable at all</b> - an envelope
/// applied to an undeclared name is refused pointing here, which only works
/// because the door ships in the same release as the refusal. v0 declaring
/// is unrestricted and logged, the chart's shape; who MAY declare stays an
/// open question elsewhere.
/// </para>
/// <para>
/// <b><c>root</c> is refused, deliberately.</b> The floor exists for every
/// tenant without being declared - never a row, never a pointer - so a
/// request naming it is answered with why, not stored.
/// </para>
/// </remarks>
[PinnedId("4e9b0ebc-856e-48a8-9ab0-56c1400d34d2")]
public sealed record DeclareNameRequest
{
    /// <summary>The name envelopes may then be applied to, e.g. payments.</summary>
    public required string Name { get; init; }

    /// <summary>What the name plays: work-kind or narrowing. Root cannot be claimed.</summary>
    public required string Role { get; init; }

    /// <summary>The name this one sits under. Root for a work kind.</summary>
    public required string Parent { get; init; }

    /// <summary>What the name governs, when it binds to something concrete.</summary>
    public string? SubjectBinding { get; init; }
}

/// <summary>One envelope name in the topology, and who put it there.</summary>
[PinnedId("5545ad96-a502-461f-8192-8d82b0ce70a6")]
public sealed record TopologyName
{
    public required string Name { get; init; }

    public required string Role { get; init; }

    /// <summary>Null only for root, which sits under nothing.</summary>
    public string? Parent { get; init; }

    public string? SubjectBinding { get; init; }

    /// <summary>Who declared it - a display a person can read, not an id.</summary>
    public required string DeclaredBy { get; init; }

    public required DateTimeOffset DeclaredAt { get; init; }
}

/// <summary>The tenant's topology: every envelope name that exists, root first.</summary>
/// <remarks>
/// Root is always present and never declared - it is synthesized by the
/// read, so a topology with no entries still has a floor. An envelope rather
/// than a bare array, the <see cref="EnvironmentChart"/> reason.
/// </remarks>
[PinnedId("1d62c2e1-ba48-4960-88e5-392ef6ab91ef")]
public sealed record EnvelopeTopology
{
    public required IReadOnlyList<TopologyName> Names { get; init; }
}

/// <summary>What a registered repository authenticates with.</summary>
/// <remarks>
/// Two, and the second exists because a walk found the flight it forbids.
/// <c>required</c> is every registration written before this vocabulary
/// existed; <c>none</c> is the registrar asserting there is nothing to
/// authenticate to, so the claim stops demanding what nobody could use.
/// A third disposition is a contract version, not a string.
/// </remarks>
[VocabularyOf(VocabularyFingerprints.Contract)]
public static class RepositoryCredentialModes
{
    /// <summary>A credential reference is demanded before a lease completes. The default, and absence's meaning.</summary>
    public const string Required = "required";

    /// <summary>Nothing to authenticate to. The claim demands no reference for this repository.</summary>
    public const string None = "none";

    public static IReadOnlyList<string> All { get; } = [Required, None];
}

/// <summary>Ask the control plane to register a repository name.</summary>
/// <remarks>
/// <para>
/// <b>Registration is what makes a repository nameable at all</b> - a flight
/// whose intent names an unregistered repository is refused pointing here -
/// so it widens what every envelope layer beneath can reach. v0 is
/// unrestricted and logged, the chart's shape; who MAY register stays with
/// ADR-0016's closure set.
/// </para>
/// <para>
/// <b>No credential, no host.</b> The provider is a KEY the registrar chose
/// and the runner maps to a host of its own; the id is the forge's immutable
/// identifier; the path is a display label that may drift. Which host a
/// customer's credential goes to must never be a policy edit here.
/// </para>
/// </remarks>
[PinnedId("6e2d63b0-8a13-4561-8cd5-673b4831278a")]
public sealed record RegisterRepositoryRequest
{
    /// <summary>The name envelopes and flights refer to, e.g. payments.</summary>
    public required string Name { get; init; }

    /// <summary>The provider key a runner resolves, e.g. a forge host. Never derived from a URI.</summary>
    public required string Provider { get; init; }

    /// <summary>The forge's immutable identifier for the repository.</summary>
    public required string Id { get; init; }

    /// <summary>The display path, e.g. acme/payments-service. A label that may drift.</summary>
    public required string Path { get; init; }

    /// <summary>
    /// What this repository authenticates with: <see cref="RepositoryCredentialModes"/>,
    /// or null - and <b>null means required</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The registrar's assertion, not the control plane learning what a
    /// provider key means. A repository a runner reaches over <c>file://</c> -
    /// an air-gapped mirror, a bind-mounted checkout - has nothing to
    /// authenticate to, and demanding a credential for it produced a flight
    /// that could not be flown: the claim demanded a reference, the runner
    /// refused an empty secret, and the local adapter refused any secret.
    /// </para>
    /// <para>
    /// Absence means required, the unadmitted-push rule again: a registration
    /// written before this member existed means exactly what it meant.
    /// </para>
    /// </remarks>
    public string? Credential { get; init; }

    /// <summary>
    /// The fully-qualified ref work starts from when a flight's intent names
    /// none, or null - and <b>null means the flight has no repository</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Here because this is where WHO a repository is already lives.</b> A
    /// ticket intent is a provider and an id: it names no repository and
    /// therefore no ref, so a flight about a work item resolved to nothing at
    /// all and its runner materialized an empty tree. Which ref work starts
    /// from is the registrar's knowledge in exactly the way the provider key and
    /// the credential mode are, and it is nobody's downstream.
    /// </para>
    /// <para>
    /// <b>Absence is not a default branch.</b> <see cref="FlightRepo"/> is
    /// explicit that <i>a uri naming no ref produces no repository rather than a
    /// guessed default branch</i>; a control plane substituting
    /// <c>refs/heads/main</c> here would be that same guess with a longer path
    /// to it, and wrong on every repository whose trunk is called something
    /// else.
    /// </para>
    /// <para>
    /// <b>Fully qualified, refused otherwise.</b> A tag and a branch of one name
    /// are two different commits, so a bare <c>main</c> asks a reader to choose
    /// a namespace on the author's behalf. See <see cref="RepositoryRefs"/>.
    /// </para>
    /// </remarks>
    public string? Ref { get; init; }

    /// <summary>
    /// The directory in this repository under which every document is a
    /// narrowing (ADR-0018), or null - and <b>null is off</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Declaring it is a widening, and a registration always was one.</b>
    /// ADR-0016 § 6: a registry entry is reach that did not exist a moment ago,
    /// so there is no prior version to sit below and nothing to compute. Turning
    /// this on lets a source contribute to composition that could not before,
    /// which is the same sentence - so it takes the same gate with no new rule.
    /// </para>
    /// <para>
    /// <b>Null is off, and off is not empty.</b> See
    /// <see cref="RepositoryNarrowings"/>, which is where the rule lives and
    /// where the refusal reads. A registration written before this member
    /// existed means exactly what it meant, which is the same thing
    /// <see cref="Credential"/>'s absence means one member up.
    /// </para>
    /// </remarks>
    public string? Narrowings { get; init; }
}

/// <summary>
/// A registration deferred to its gate: the flight that carries it, who the
/// gate awaits, and what would widen.
/// </summary>
/// <remarks>
/// <para>
/// <b>One record for all three doors.</b> The done shapes carry required
/// members a pending answer cannot honestly fill - who registered it and
/// when, which have not happened yet - so the 202 body is its own type
/// rather than a done shape with the truth left blank.
/// </para>
/// <para>
/// <b>All three members are required.</b> A registration is a widening by
/// definition - reach that did not exist before - so there is always a
/// field to name, always a flight to point at, and always an approver the
/// gate awaits; a pending answer missing any of them is a wait with no
/// address.
/// </para>
/// </remarks>
[PinnedId("e2c9f716-d78c-4d24-a1c7-8759cdfdbdf8")]
public sealed record RegistrationPending
{
    /// <summary>The amend-envelope-shaped flight the registration rides.</summary>
    public required string Flight { get; init; }

    /// <summary>Who the gate awaits - a display a person can read, not an id.</summary>
    public required string Awaiting { get; init; }

    /// <summary>What the registration widens - the registry gaining a name.</summary>
    public required string Widens { get; init; }
}

/// <summary>One registered repository, and who made it nameable.</summary>
[PinnedId("ee1d0c6d-c310-4042-a57e-f750722c8c01")]
public sealed record RepositoryRegistered
{
    public required string Name { get; init; }

    public required string Provider { get; init; }

    /// <summary>The forge's immutable identifier - what flight identity resolves through.</summary>
    public required string Id { get; init; }

    public required string Path { get; init; }

    /// <summary>
    /// The resolved credential mode, said out loud.
    /// </summary>
    /// <remarks>
    /// Required here and nullable on the request, because the reader of a
    /// registration must not need the defaulting rule: an absent declaration
    /// and a declared <c>required</c> are the same fact, and they read the
    /// same on the way out.
    /// </remarks>
    public required string Credential { get; init; }

    /// <summary>
    /// The fully-qualified ref work starts from when a flight's intent names
    /// none, or null - and <b>null is different from any ref</b>.
    /// </summary>
    /// <remarks>
    /// <b>Nullable on the way out, like <see cref="Narrowings"/> and unlike
    /// <see cref="Credential"/>.</b> An absent credential and a declared
    /// <c>required</c> are the same fact, so that one resolves. Here they are
    /// different facts - a repository a ticket flight can start work on, and one
    /// where such a flight is refused - so the absence has to survive the round
    /// trip, or a reader cannot tell which they are looking at.
    /// </remarks>
    public string? Ref { get; init; }

    /// <summary>
    /// The directory under which every document in this repository is a
    /// narrowing (ADR-0018), or null - and <b>null is off</b>.
    /// </summary>
    /// <remarks>
    /// <b>Nullable, unlike <see cref="Credential"/> one member up, and the
    /// asymmetry is the point.</b> There, an absent declaration and a declared
    /// <c>required</c> are the same fact, so the answer is required and a reader
    /// never needs the defaulting rule. Here they are different facts - off
    /// versus on - so the absence survives the round trip as an absence, or a
    /// reader cannot tell a repository that is not governed from one whose every
    /// file is policy.
    /// </remarks>
    public string? Narrowings { get; init; }

    /// <summary>Who registered it - a display a person can read, not an id.</summary>
    public required string RegisteredBy { get; init; }

    public required DateTimeOffset RegisteredAt { get; init; }
}

/// <summary>The tenant's registered repositories: everything a flight can be about.</summary>
[PinnedId("8d944deb-547b-44b4-bd73-5becda1db94a")]
public sealed record RegisteredRepositories
{
    public required IReadOnlyList<RepositoryRegistered> Repositories { get; init; }
}
