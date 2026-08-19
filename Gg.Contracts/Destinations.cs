namespace Gg.Contracts;

/// <summary>
/// Where a flight's work landed, once it was admitted.
/// </summary>
/// <remarks>
/// <para>
/// <b>Recorded because it happened, not to authorise it.</b> Admission is
/// decided in the control plane before anything is pushed; this is the runner
/// reporting what it then did, which is the same relationship every other fact
/// has to the decisions around it.
/// </para>
/// <para>
/// The branch and the pull request are both named. A landing nobody can trace
/// back to a flight is a branch nobody will ever delete, and a pull request
/// with no recorded flight is a change whose governance is an oral tradition.
/// </para>
/// </remarks>
[FactKind(FactKinds.DestinationLanded)]
[PinnedId("8c14a9e0-7b62-4d38-95af-2e6031c8b47d")]
public sealed record DestinationLanded
{
    /// <summary>Which destination, by its id in the envelope.</summary>
    public required string DestinationId { get; init; }

    /// <summary>
    /// The branch that was pushed.
    /// </summary>
    /// <remarks>
    /// Carries the flight number, because <c>GG-42</c> is the thing a person can
    /// type and the thing that ties a branch back to a record.
    /// </remarks>
    public required string Branch { get; init; }

    /// <summary>Where the pull request is, as a uri a person can open.</summary>
    public required string PullRequestUri { get; init; }

    /// <summary>The pull request's number at the provider.</summary>
    public required int PullRequestNumber { get; init; }

    /// <summary>The diagnosis, or null when there is nothing wrong.</summary>
    public static string? Validate(DestinationLanded landed)
    {
        ArgumentNullException.ThrowIfNull(landed);

        if (string.IsNullOrWhiteSpace(landed.DestinationId))
        {
            return "A landing names the destination that admitted it.";
        }

        if (string.IsNullOrWhiteSpace(landed.Branch))
        {
            return "A landing names the branch it pushed. A branch nobody can name is one nobody "
                 + "will delete.";
        }

        return landed.PullRequestNumber < 1
            ? "A pull request has a number at the provider, and this one does not."
            : string.IsNullOrWhiteSpace(landed.PullRequestUri)
                ? "A landing carries a uri a person can open."
                : null;
    }
}

/// <summary>
/// Whether this flight's work may land, decided by the control plane.
/// </summary>
/// <remarks>
/// <para>
/// <b>It arrives, or nothing is pushed.</b> A destination is a target PLUS its
/// admission conditions, and evaluating those conditions is evaluation - which
/// happens in the control plane and nowhere else. Article IX.
/// </para>
/// <para>
/// <b>The runner must not infer this from a verdict it can see.</b> It can see
/// the facts it produced and could compute an obligation itself; a runner that
/// did would be deciding, and a patched one would decide differently. So this
/// travels as a decision rather than as the inputs to one.
/// </para>
/// <para>
/// Absent means no. A batch response carrying nothing is a flight with no
/// destination, or one whose obligations are unmet, or a control plane too old
/// to answer - and every one of those is "do not push".
/// </para>
/// </remarks>
[PinnedId("b5e07f31-4a29-4c86-8d15-90fc23e7a641")]
/// <summary>
/// What a commit reference looks like, in one place.
/// </summary>
/// <remarks>
/// Hex and forty characters. A rule rather than a comment, because both a landing
/// fact and a gate carry one and a reference that is not a reference is the
/// well-formed wrong value that would send somebody to a commit nobody has.
/// </remarks>
public static class Commits
{
    public const int ShaLength = 40;

    public static bool IsSha(string? value) =>
        value is { Length: ShaLength } sha && sha.All(Uri.IsHexDigit);
}

/// <summary>
/// A branch reached the remote, and nothing was proposed.
/// </summary>
/// <remarks>
/// <para>
/// <b>Its own kind, because its name has to be true of what it reports.</b> A push
/// happens under the first gate; a proposal happens under the second. Reporting the
/// first on <c>destination.landed</c> would make that fact fire when nothing had
/// landed.
/// </para>
/// <para>
/// <b>The commit is the point.</b> A pending decision is about a commit, and a gate
/// whose work exists only in a working tree on somebody's machine is a gate nobody
/// can act on. This is what ADR-0006's by-reference evidence means for a gate: the
/// reference is a sha.
/// </para>
/// <para>
/// <b>It names no destination.</b> A push under a pending decision was cleared by
/// the first gate and admitted nowhere, and naming a destination would be a record
/// claiming permission nobody granted.
/// </para>
/// </remarks>
[FactKind(FactKinds.DestinationPushed)]
[PinnedId("2a7f4c81-6b03-4d95-8e12-c740b9a53f26")]
public sealed record DestinationPushed
{
    /// <summary>Which repository, of the ones the flight holds.</summary>
    public required string Slug { get; init; }

    /// <summary>The branch that was pushed. Carries the flight number.</summary>
    public required string Branch { get; init; }

    /// <summary>The commit the branch is at.</summary>
    public required string Commit { get; init; }

    /// <summary>
    /// Whether this push is work KEPT rather than work offered.
    /// </summary>
    /// <remarks>
    /// <b>A <c>gg/</c> branch with no pull request is not a proposal, and the fact
    /// that names it has to say so.</b> Otherwise a reader counting this platform's
    /// branches cannot tell work that was admitted from work that was merely
    /// preserved so somebody could take the flight over - and the two mean opposite
    /// things about whether anybody is expected to review it.
    /// <para>
    /// Null means what it always meant: a push on the ordinary landing path, with
    /// or without a proposal following it.
    /// </para>
    /// </remarks>
    public bool? Preserved { get; init; }

    /// <summary>The diagnosis, or null when there is nothing wrong.</summary>
    public static string? Validate(DestinationPushed pushed)
    {
        ArgumentNullException.ThrowIfNull(pushed);

        if (string.IsNullOrWhiteSpace(pushed.Slug))
        {
            return "A push names the repository it wrote to.";
        }

        if (string.IsNullOrWhiteSpace(pushed.Branch))
        {
            return "A push names the branch it wrote. A branch nobody can name is one nobody will "
                 + "delete.";
        }

        return Commits.IsSha(pushed.Commit)
            ? null
            : $"A push names the commit it wrote, and '{pushed.Commit}' is not one. The commit is "
            + "what a pending decision is about, so a push without one describes work nobody can "
            + "find.";
    }
}

/// <summary>
/// Permission to push the branch, which is not permission to propose a change.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two gates, because conflating them ships a defect.</b> Slice two gated the
/// push on full admission. A human gate needs the branch pushed <b>before</b>
/// anybody is asked - work under review cannot live only in a working tree that is
/// about to be released - and moving the push earlier without splitting the
/// permission means a flight with a violated obligation pushes its work anyway.
/// </para>
/// <para>
/// So this is granted when <b>no machine obligation is violated</b>, and
/// <see cref="DestinationAdmission"/> is granted when every <c>requires</c> is
/// satisfied. A separate type rather than a flag, because a boolean on one object
/// is one misread away from being ignored.
/// </para>
/// <para>
/// <b>Absent means no</b>, exactly as admission does, and a runner must never
/// derive one permission from the other.
/// </para>
/// </remarks>
[PinnedId("3d95f8c1-7e42-4a06-b8d3-51c07f2a94e6")]
public sealed record BranchPush
{
    /// <summary>The branch to push. Named by the control plane, which knows the flight number.</summary>
    public required string Branch { get; init; }

    /// <summary>The ref the work was based on, carried so a later proposal has a base.</summary>
    public required string BaseRef { get; init; }

    /// <summary>Which repository, of the ones this flight holds.</summary>
    public required string Slug { get; init; }

    /// <summary>
    /// Why the work may be preserved, for the record.
    /// </summary>
    /// <remarks>
    /// Preserving is not landing, and the reason says so. A push under a pending
    /// gate is the control plane making sure a decision has something to be about.
    /// </remarks>
    public required string Reason { get; init; }
}

[PinnedId("f0a37b62-1c94-4e58-8d05-92b6e1cf47a3")]
public sealed record DestinationAdmission
{
    /// <summary>Which destination, by its id in the envelope.</summary>
    public required string DestinationId { get; init; }

    /// <summary>The branch to push. Named by the control plane, which knows the flight number.</summary>
    public required string Branch { get; init; }

    /// <summary>The base the pull request opens against.</summary>
    public required string BaseRef { get; init; }

    /// <summary>Which repository, of the ones this flight holds.</summary>
    public required string Slug { get; init; }

    /// <summary>
    /// Why it was admitted, for the record.
    /// </summary>
    /// <remarks>
    /// A sentence naming the obligations that held. A decision with no stated
    /// reason is one nobody can audit later, and this is the decision that lets
    /// a machine write to somebody's repository.
    /// </remarks>
    public required string Reason { get; init; }
}

/// <summary>
/// How a branch is named, so a person can trace it back.
/// </summary>
/// <remarks>
/// <para>
/// <b>Declared in the contract because both sides need the same answer.</b> The
/// control plane names the branch in the admission and the runner pushes it, and
/// a runner that swept branches by a prefix it derived itself would miss the
/// ones the control plane named. Two derivations of one name is how a flight
/// ends up unable to find the branch it just created.
/// </para>
/// <para>
/// Not a wire type - nothing serializes it. It is a RULE that crosses, which is
/// the other thing this package is for.
/// </para>
/// </remarks>
public static class DestinationBranch
{
    /// <summary>The prefix every branch this platform creates carries.</summary>
    public const string Prefix = "gg/";

    /// <summary>
    /// The branch for a flight, carrying its number.
    /// </summary>
    /// <remarks>
    /// <c>GG-42</c> is the thing a person can type and the thing that ties a
    /// branch back to a record. A name nobody can trace is a branch nobody will
    /// ever delete.
    /// </remarks>
    public static string For(string flightNumber) => Prefix + Safe(flightNumber);

    /// <summary>
    /// The branch for work KEPT so somebody can take the flight over.
    /// </summary>
    /// <remarks>
    /// <b>A different name from <see cref="For"/>, because they are different
    /// facts.</b> A flight preserved for handoff and the same flight later admitted
    /// must not fight over one ref: the second push would either be refused as an
    /// existing branch or would overwrite the thing somebody was about to take over.
    /// <para>
    /// Still under <see cref="Prefix"/>, so whatever cleans up this platform's
    /// branches still sees it. A branch nobody recognises is a branch nobody deletes.
    /// </para>
    /// </remarks>
    public static string ForHandoff(string flightNumber) =>
        Prefix + "handoff/" + Safe(flightNumber);

    /// <summary>Whether this branch is one kept for a handoff rather than offered.</summary>
    /// <remarks>
    /// <b>So a runner reports what it was told to push rather than deciding.</b> The
    /// control plane chooses the branch; this lets the runner say which KIND of push
    /// it made without inferring a governance answer from a string it matched itself.
    /// A prefix check written at the call site would be the runner deriving policy,
    /// which is the thing Article IX is about.
    /// </remarks>
    public static bool IsHandoff(string branch) =>
        branch is not null && branch.StartsWith(Prefix + "handoff/", StringComparison.Ordinal);

    /// <summary>Whether this is a branch this platform would have created.</summary>
    public static bool IsOurs(string branch) =>
        branch is not null && branch.StartsWith(Prefix, StringComparison.Ordinal);

    /// <summary>
    /// A component safe in a ref name.
    /// </summary>
    /// <remarks>
    /// The flight number comes from the control plane and is <c>GG-42</c>
    /// shaped, so this removes nothing in a healthy system. It is here because a
    /// ref name is passed to git, and git has opinions about what is in one -
    /// notably that <c>..</c> is forbidden, which is why the dot is not in the
    /// allowed set at all rather than allowed-and-then-collapsed.
    /// </remarks>
    private static string Safe(string component) =>
        new([.. component.Select(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' ? c : '-')]);
}
