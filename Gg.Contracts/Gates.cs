namespace Gg.Contracts;

/// <summary>
/// One decision waiting on a person.
/// </summary>
/// <remarks>
/// <para>
/// <b>Five things, and the fifth is the one that makes the list worth reading.</b>
/// The flight, the obligation, the commit, who may decide - and <b>why the
/// obligation attached</b>. "A decision is waiting" is a chore; "a decision is
/// waiting because this flight touched migrations/0002_backfill.sql" is a reason to
/// look.
/// </para>
/// <para>
/// That last field is not new work. It is the attribution
/// <c>ObligationAttributed</c> already records for <c>gg why</c>, which turns out
/// to be the gate list's most important column.
/// </para>
/// <para>
/// <b>The commit is why the branch is pushed before anybody is asked.</b> A gate
/// whose work exists only in a working tree on somebody's machine is a gate nobody
/// can act on.
/// </para>
/// </remarks>
[PinnedId("6c14a8e0-93b7-4d52-8a61-f07e2c95b3d4")]
public sealed record PendingGate
{
    /// <summary>Which flight, as a person types it. GG-42.</summary>
    public required string FlightNumber { get; init; }

    /// <summary>Which obligation is waiting, by its id in the envelope.</summary>
    public required string ObligationId { get; init; }

    /// <summary>Who may decide, from the envelope.</summary>
    public required string Approver { get; init; }

    /// <summary>The branch the work was pushed to.</summary>
    public required string Branch { get; init; }

    /// <summary>The commit under review.</summary>
    public required string Commit { get; init; }

    /// <summary>
    /// The evidence manifest hash this gate was opened against.
    /// </summary>
    /// <remarks>
    /// <b>What a decision about this gate is scoped to.</b> An approval recorded without
    /// it would be an approval of the obligation rather than of the work - approve one
    /// migration, let the loop touch four more, and the obligation is still satisfied
    /// because nothing recorded what was approved. That is a privilege escalation with
    /// the deciding person's signature on it.
    /// </remarks>
    public required string ManifestHash { get; init; }

    /// <summary>
    /// The condition as the envelope wrote it, or null when it always applies.
    /// </summary>
    /// <remarks>
    /// Null means the obligation declares no condition. It never means the
    /// condition could not be read - a gate is only opened for an obligation that
    /// attached, and an unreadable condition halts the flight instead.
    /// </remarks>
    public string? Condition { get; init; }

    /// <summary>Why the obligation attached, in the Engine's words.</summary>
    public required string Because { get; init; }

    /// <summary>Since when. Oldest first is the order the list is read in.</summary>
    public required DateTimeOffset AwaitingSince { get; init; }

    /// <summary>
    /// Which attempt this flight is on.
    /// </summary>
    /// <remarks>
    /// <b>Recorded, not enforced.</b> Rejecting a gate sends the work back for another
    /// attempt, and nothing bounds how many times that can happen - <c>budget.attempts</c>
    /// was never built, only wall-clock. A person is the rate limiter, since every cycle
    /// needs a decision, but "no limit" and "a limit nobody wrote down" look identical in
    /// a record. This is the number that tells them apart, and it is where the bound goes
    /// when there is one.
    /// </remarks>
    public required int Attempt { get; init; }
}

/// <summary>
/// Everything waiting on a person, oldest first.
/// </summary>
/// <remarks>
/// <b>A list and nothing else.</b> There is no way to answer a gate from this
/// surface, deliberately: nothing an agent can call may unstick a flight, and the
/// cheapest way to guarantee that is for the verb that shows gates to have no verb
/// beside it that resolves one.
/// </remarks>
[PinnedId("b28e5f31-40c9-4a7d-95e2-6d1873fa0c4b")]
public sealed record GateList
{
    /// <summary>One entry per waiting decision, by <c>AwaitingSince</c> then flight.</summary>
    public required IReadOnlyList<PendingGate> Gates { get; init; }
}
