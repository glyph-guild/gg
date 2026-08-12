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
