namespace Gg.Contracts;

/// <summary>
/// The routine maintenance actions a resident runner performs against a
/// managed pool. Closed at five; every member is mirrored into
/// <see cref="All"/>, because a declared value outside its own membership
/// list is refused by the very check that exists to admit it — the
/// <see cref="DestinationKinds"/> hole slice twelve's step 0 found, not
/// copied here.
/// </summary>
/// <remarks>
/// These are not loop moves. The maintain loop grants no moves and runs no
/// agent; <c>power-on</c> and <c>power-off</c> stay
/// <see cref="MoveKinds"/>' prophecy, for the strategy row that needs them.
/// </remarks>
[VocabularyOf(VocabularyFingerprints.Contract)]
public static class PoolActions
{
    /// <summary>Inspect a pool member and attest what was found. Changes nothing.</summary>
    public const string Verify = "verify";

    /// <summary>
    /// Make a pool member current and running: create it if absent, start it
    /// if stopped, converge it to the strategy's image if it drifted.
    /// Warming IS refresh — the decider decides one when demand exists.
    /// </summary>
    public const string Refresh = "refresh";

    /// <summary>
    /// Destroy a member and recreate it from the strategy's pinned image —
    /// what makes a reused environment trustworthy again after a flight.
    /// </summary>
    public const string Reset = "reset";

    /// <summary>
    /// Make the whole pool current with the pinned image: destroy and recreate
    /// every member made from something the strategy no longer names, and touch
    /// the ones already current not at all.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Not a wider refresh, and the difference is the pace.</b> Refresh is
    /// how a pool GROWS — one member per decision, with a hold-off tuned to
    /// that, and only while the pool is under its warm ceiling. A roll adds
    /// nobody: it replaces what is there, so it must be able to act AT the
    /// ceiling, which is exactly the state a pool sits in while its members go
    /// stale.
    /// </para>
    /// <para>
    /// <b>Whole-pool because the decider cannot see drift.</b> Which members
    /// are off the pin is knowable only where the containers are, by comparing
    /// what each says it was made FROM against what the strategy pins. A roll
    /// that converged one member would leave the rest waiting on a decision
    /// made from a count the control plane cannot measure.
    /// </para>
    /// </remarks>
    public const string Roll = "roll";

    /// <summary>
    /// Build the strategy's recipe, push the image to the pool's own registry,
    /// and attest the digest and the commit it was built from. Changes no member.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Slice forty-one.</b> A strategy may name where its image comes from
    /// (<see cref="EnvironmentStrategy.Build"/>); this is the act that turns that
    /// recipe into a digest. The digest does not become the pin here: it comes
    /// back through the strategy door as a new version, and the existing
    /// <see cref="Roll"/> converges the members from there.
    /// </para>
    /// <para>
    /// <b>An outward act, though it touches no member.</b> Its product is an
    /// image in a registry rather than its attestation, and it runs a recipe on
    /// the pool host's daemon through a proxy allowance the scope probe has to
    /// prove - which is exactly what the decider withholds an outward act for.
    /// </para>
    /// </remarks>
    public const string Build = "build";

    public static IReadOnlyList<string> All { get; } = [Verify, Refresh, Reset, Roll, Build];
}

/// <summary>
/// Whether a pool action's use is itself the outward act, or only a record.
/// </summary>
/// <remarks>
/// <para>
/// <b>MoveKinds' shape, second instance, deliberately its own table.</b>
/// <c>refresh</c>, <c>reset</c> and <c>roll</c> change a container on a customer's host —
/// their product faces no destination gate, so Article VI is the axis and
/// the classification is the control. <c>verify</c> only looks.
/// </para>
/// <para>
/// <b>The enforcement consumer is the control-plane decider:</b> an
/// outward-act action is decided only toward a pool whose latest attestation
/// carries a current scope probe — the resident runner reached outside its
/// declared inventory and was refused by something that is not us. Unproved
/// escalates, never acts; unknown is not false.
/// </para>
/// <para>
/// MoveKinds' enforced set stays correctly empty and untouched. Slice
/// eleven's pre-booking — the first outward move arrives with
/// maintain-environment — half-arrives: the work kind arrives, the loop
/// move does not.
/// </para>
/// </remarks>
[VocabularyOf(VocabularyFingerprints.Contract)]
public static class PoolActionKinds
{
    /// <summary>The action's use changes a customer's infrastructure.</summary>
    public const string OutwardAct = "outward-act";

    /// <summary>The action observes; the attestation is its whole product.</summary>
    public const string RecordOnly = "record-only";

    public static IReadOnlyList<string> All { get; } = [OutwardAct, RecordOnly];

    /// <summary>Every action, classified. A dictionary so totality is assertable.</summary>
    public static IReadOnlyDictionary<string, string> Table { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [PoolActions.Verify] = RecordOnly,
            [PoolActions.Refresh] = OutwardAct,
            [PoolActions.Reset] = OutwardAct,
            [PoolActions.Roll] = OutwardAct,

            // THE FOURTH, AND IT TOUCHES NO MEMBER. Outward because the
            // attestation is not its whole product - an image is - and because
            // it reaches the daemon through an allowance the probe must prove.
            [PoolActions.Build] = OutwardAct,
        };

    /// <summary>The kind, or a throw for an action nobody classified.</summary>
    /// <remarks>
    /// The silent default would be record-only, and record-only is the answer
    /// that grants what nothing can recall — <see cref="MoveKinds.Of"/>'s
    /// rule, one vocabulary over.
    /// </remarks>
    public static string Of(string action) =>
        Table.TryGetValue(action, out var kind)
            ? kind
            : throw new InvalidOperationException(
                $"'{action}' is not a classified pool action. Every action bears a kind, "
              + "and an unclassified one poisons rather than defaulting.");
}

/// <summary>What a pool attestation may conclude.</summary>
[VocabularyOf(VocabularyFingerprints.Contract)]
public static class PoolOutcomes
{
    public const string Verified = "verified";

    public const string Failed = "failed";

    public static IReadOnlyList<string> All { get; } = [Verified, Failed];
}

/// <summary>
/// A routine action's outcome, attested by the resident runner.
/// </summary>
/// <remarks>
/// <para>
/// <b>Not a fact, deliberately.</b> The fact plumbing is lease-welded at
/// four points — the batch carries only a generation, the endpoint is
/// lease-scoped, the ship call takes a lease id, and the idempotency key
/// needs a flight id — and a routine action has no flight. The attestation
/// gets its own record and its own prefix; the kinds-that-cross count stays
/// nine, asserted deliberately.
/// </para>
/// <para>
/// <b>Digests, hashes and stamps only</b> — what the action did to a
/// container, never what is inside one. Held over the member types by
/// <c>PoolAttestationTests</c>.
/// </para>
/// </remarks>
[PinnedId("fd78d7f1-c893-44c5-94a1-bda8cba3d59d")]
public sealed record PoolAttestation
{
    /// <summary>The runner's own id for this attestation — the idempotency key (UUIDv7).</summary>
    public required Guid AttestationId { get; init; }

    /// <summary>The pool the strategy declared.</summary>
    public required string Pool { get; init; }

    /// <summary>One of <see cref="PoolActions"/>.</summary>
    public required string Action { get; init; }

    /// <summary>The decided action this answers, when it answers one.</summary>
    public Guid? ActionId { get; init; }

    /// <summary>One of <see cref="PoolOutcomes"/> — the discriminator between the two tiers.</summary>
    public required string Outcome { get; init; }

    /// <summary>The image digest the member converged to, when the action knows it.</summary>
    public string? ImageDigest { get; init; }

    /// <summary>Lock hashes observed inside the member, when the action gathered them.</summary>
    /// <remarks>
    /// <b>The accessor delivers that, and the initializer does not.</b> This
    /// member is init-only, so System.Text.Json cannot set it after
    /// construction and builds the object through a creator that assigns every
    /// member from an argument array - this one as null when the key is absent,
    /// overwriting the <c>= []</c>. Non-nullable is a promise to every caller
    /// that it can be dereferenced; <c>AbsentCollectionsSurviveTheWireTests</c>
    /// holds it for the whole contract.
    /// </remarks>
    public IReadOnlyList<LockHash> Locks
    {
        get => field ?? [];
        init;
    } = [];

    /// <summary>One of <see cref="EnvironmentProvenance"/>, when the action can say.</summary>
    public string? Provenance { get; init; }

    /// <summary>
    /// When this session last proved the scope bound — reached outside the
    /// declared inventory and was refused by the proxy. Null means unproved,
    /// and unknown is not false: no outward act is decided toward it.
    /// </summary>
    public DateTimeOffset? ScopeProbedAt { get; init; }

    /// <summary>The runner's clock at measurement — the ledger's order key.</summary>
    public required DateTimeOffset MeasuredAt { get; init; }

    /// <summary>What went wrong. Required when the outcome is failed.</summary>
    public string? Diagnosis { get; init; }

    /// <summary>
    /// The commit a build resolved its recipe's ref to and built from. Only a
    /// build carries one; a good build must.
    /// </summary>
    /// <remarks>
    /// <b>Never the ref.</b> Refs move, and reporting one back is the answer that
    /// looks like an answer and is not - the same rule a sweep's skill commit is
    /// held to. Beside <see cref="ImageDigest"/>, it is what lets the control
    /// plane believe a strategy's <c>built-from</c>: a digest and a commit that a
    /// build it decided actually attested.
    /// </remarks>
    public string? RecipeCommit { get; init; }

    /// <summary>
    /// The schema's own rule, shared so the runner and the control plane
    /// cannot disagree about what a valid attestation is.
    /// </summary>
    public static string? Validate(PoolAttestation attestation)
    {
        ArgumentNullException.ThrowIfNull(attestation);

        if ((attestation.AttestationId.ToString("N")[12]) != '7')
        {
            return $"attestationId '{attestation.AttestationId}' is not a UUIDv7. The id "
                 + "is the idempotency key, and its ordering is the version's.";
        }

        if (string.IsNullOrWhiteSpace(attestation.Pool))
        {
            return "An attestation must name its pool - pool is blank.";
        }

        if (!PoolActions.All.Contains(attestation.Action, StringComparer.Ordinal))
        {
            return $"'{attestation.Action}' is not a pool action this version knows. "
                 + $"Expected one of: {string.Join(", ", PoolActions.All)}.";
        }

        if (!PoolOutcomes.All.Contains(attestation.Outcome, StringComparer.Ordinal))
        {
            return $"'{attestation.Outcome}' is not an outcome this version knows. "
                 + $"Expected one of: {string.Join(", ", PoolOutcomes.All)}.";
        }

        if (string.Equals(attestation.Outcome, PoolOutcomes.Failed, StringComparison.Ordinal)
            && string.IsNullOrWhiteSpace(attestation.Diagnosis))
        {
            return "A failed attestation carries no diagnosis. The failure is the "
                 + "discriminator between the two maintenance tiers, and a failure that "
                 + "cannot say why escalates nothing.";
        }

        if (attestation.Provenance is { } provenance
            && !EnvironmentProvenance.All.Contains(provenance, StringComparer.Ordinal))
        {
            return $"'{provenance}' is not a provenance this version knows. Expected one "
                 + $"of: {string.Join(", ", EnvironmentProvenance.All)}.";
        }

        var build = string.Equals(attestation.Action, PoolActions.Build, StringComparison.Ordinal);

        if (!build && attestation.RecipeCommit is not null)
        {
            return $"A {attestation.Action} attestation says it built from a commit. Only a "
                 + "build builds, and a recipe commit anywhere else is a claim about a build "
                 + "that did not happen - one a reader of the ledger would believe.";
        }

        if (build && string.Equals(attestation.Outcome, PoolOutcomes.Verified, StringComparison.Ordinal))
        {
            if (attestation.RecipeCommit is null)
            {
                return "A build that succeeded does not say which commit it built. That commit "
                     + "is the only thing tracing the image back to a reviewed recipe.";
            }

            if (string.IsNullOrWhiteSpace(attestation.ImageDigest))
            {
                return "A build that succeeded does not say what it pushed, so there is no "
                     + "digest to move the pin to.";
            }
        }

        if (attestation.RecipeCommit is { } commit && !GitObjectIds.IsOne(commit))
        {
            return $"'{commit}' is not a commit. A commit is forty or sixty-four hex digits, and "
                 + "reporting the ref back is the one answer that looks like an answer and is "
                 + "not.";
        }

        return null;
    }
}

/// <summary>One decided maintenance action, served to the pull point.</summary>
/// <remarks>
/// <b>Serving is the claim</b>, control-plane-side: an action appears here
/// exactly once. The image is the strategy's CURRENT pinned image, stamped at
/// serve time, so a refresh decided under one policy converges to the policy
/// in force when it runs.
/// </remarks>
[PinnedId("0ddf8827-ac6b-4415-aa14-1c0247134345")]
public sealed record PoolAction
{
    public required Guid ActionId { get; init; }

    public required string Pool { get; init; }

    /// <summary>One of <see cref="PoolActions"/>.</summary>
    public required string Action { get; init; }

    /// <summary>The strategy's pinned image, for refresh and reset. Null for verify.</summary>
    public string? Image { get; init; }

    /// <summary>The strategy version the action was decided under, e.g. payments-pool@v2.</summary>
    public required string StrategyVersion { get; init; }

    public required DateTimeOffset DecidedAt { get; init; }

    /// <summary>
    /// Where a build's recipe is, resolved when the build was decided. Only a
    /// build carries one; null for every other action.
    /// </summary>
    public PoolRecipe? Recipe { get; init; }
}

/// <summary>
/// A build's recipe as the runner fetches it: the repository, the directory,
/// and the Dockerfile inside it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Resolved control-plane-side, fetched runner-side.</b> A strategy names its
/// recipe by the tenant's registry name, which never leaves the control plane;
/// what crosses is the provider and slug the registry held when the build was
/// decided - the coordinate type the runner's VCS port already takes, as a
/// sweep's skill does since 0.187.0. The ref rides in
/// <see cref="LeaseRepoRef.PinnedRef"/> and the runner resolves it.
/// </para>
/// <para>
/// <b>Names only.</b> Nothing here can carry a Dockerfile's text: the machine
/// that builds reads the recipe with its own credential, and the control plane
/// never does.
/// </para>
/// </remarks>
[PinnedId("1967ec15-abe2-4f7a-8c7d-328d98ddad98")]
public sealed record PoolRecipe
{
    /// <summary>The recipe's repository, with the strategy's ref as its pinned ref.</summary>
    public required LeaseRepoRef Repository { get; init; }

    /// <summary>The recipe's directory in it: the whole build context.</summary>
    public required string Path { get; init; }

    /// <summary>The Dockerfile inside <see cref="Path"/>, or null for <c>Dockerfile</c>.</summary>
    public string? Dockerfile { get; init; }
}

/// <summary>The decided actions a pull answered with.</summary>
[PinnedId("d766a7e6-c477-474a-90de-c932aa069c69")]
public sealed record PoolActionList
{
    public required IReadOnlyList<PoolAction> Actions { get; init; }
}

/// <summary>The latest attestation per (pool, action), as the read side serves it.</summary>
[PinnedId("a5dca5ad-dbed-4c58-a821-22424b1d4323")]
public sealed record PoolStatus
{
    public required string Pool { get; init; }

    public required string Action { get; init; }

    public required string Outcome { get; init; }

    public string? ImageDigest { get; init; }

    public DateTimeOffset? ScopeProbedAt { get; init; }

    public required DateTimeOffset MeasuredAt { get; init; }

    public string? Diagnosis { get; init; }
}

/// <summary>Every managed pool's current state — what gg pools renders.</summary>
[PinnedId("e3a68c65-79a2-4b7c-af9b-0523b2099e3c")]
public sealed record PoolLedger
{
    public required IReadOnlyList<PoolStatus> Pools { get; init; }
}

/// <summary>
/// What a resident runner asks for when it is about to warm a member.
/// </summary>
/// <remarks>
/// <b>The member's name is in the path, not here.</b> A credential is minted FOR
/// one member; a body that named a second one would be a different request wearing
/// this one's authorization.
/// </remarks>
[PinnedId("b353c73a-24b6-45dd-a970-4f09d7d70b06")]
public sealed record MemberCredentialRequest
{
    /// <summary>The protocol this member will speak, so a mismatch fails at mint.</summary>
    public required int ProtocolVersion { get; init; }
}

/// <summary>
/// What the resident is handed to place in a member: a single-use nonce, and
/// when it stops being worth anything.
/// </summary>
/// <remarks>
/// <para>
/// <b>A nonce rather than the credential itself.</b> A member's environment is
/// readable through the scope proxy for the life of the container
/// (<c>GET /containers/gg-pool-*/json</c>), so what goes in must be worthless
/// once spent. The member exchanges it over its own connection, and an inspect
/// afterwards finds a burnt one.
/// </para>
/// <para>
/// <b>No runner id and no token here.</b> Those exist only after the member
/// redeems, which is what makes the exchange the moment the identity comes into
/// being rather than the moment it is copied.
/// </para>
/// </remarks>
[PinnedId("3bc1dc75-7847-4556-a217-916d1a41282d")]
public sealed record MemberCredentialMinted
{
    /// <summary>Single-use, and spent by the first redemption that presents it.</summary>
    public required string Nonce { get; init; }

    /// <summary>After this the nonce buys nothing, redeemed or not.</summary>
    public required DateTimeOffset ExpiresAt { get; init; }
}

/// <summary>
/// The identity a member receives in exchange for its nonce.
/// </summary>
/// <remarks>
/// <para>
/// <b>A runner identity and nothing wider.</b> Article VIII at the one seam that
/// would break it: a session token here would put a developer's whole surface
/// inside a container running a customer's code. There is no principal, no
/// session, and nothing about the tenant beyond what a runner already presents.
/// </para>
/// <para>
/// <b>Short-lived on purpose.</b> Thirty days is the RESIDENT's cadence, set by a
/// person signing in. A member is created and destroyed by machinery and should
/// outlive its own credential by as little as possible - and reset revokes, which
/// is only a boundary if the credential dies with the container.
/// </para>
/// </remarks>
[PinnedId("c515801d-3766-4f48-9c0a-f094b861613a")]
public sealed record MemberCredentialIssued
{
    public required string RunnerId { get; init; }

    public required string RunnerToken { get; init; }

    /// <summary>The labels this member may advertise, decided at mint.</summary>
    /// <remarks>
    /// <b>From the token, never from the heartbeat.</b> A runner declares its own
    /// labels today, so a laptop and a member are indistinguishable to the
    /// matcher. A member's come from the strategy in force when it was minted, so
    /// the pool's own count can stop being polluted by whatever else is online.
    /// </remarks>
    public required IReadOnlyList<string> Labels { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }
}

/// <summary>The nonce a member presents to become a runner.</summary>
/// <remarks>
/// One field, and that is the point: the nonce IS the authorization, so anything
/// else here would be a claim the caller makes about itself before it has an
/// identity.
/// </remarks>
[PinnedId("fbc2736b-d43e-45ca-8f9a-82989c3bb987")]
public sealed record MemberCredentialRedemption
{
    public required string Nonce { get; init; }
}
