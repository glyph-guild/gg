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

    /// <summary>
    /// The branch the work was pushed to, or null when the decision is not about
    /// a repository.
    /// </summary>
    /// <remarks>
    /// See <see cref="Commit"/> for why these became nullable and what it costs.
    /// </remarks>
    public string? Branch { get; init; }

    /// <summary>
    /// The commit under review, or null when the decision is not about a
    /// repository.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nullable because a destination is not always somewhere code goes.</b>
    /// An envelope-change flight has no repository, no runner and no commit, and
    /// somebody still has to decide it. Requiring these two made <i>flight</i>
    /// mean <i>agent run against a branch</i> in the one type a person reads when
    /// they are asked to answer for something.
    /// </para>
    /// <para>
    /// <b>Null, never an empty string.</b> A gate carrying <c>commit: ""</c>
    /// makes "no commit" and "a commit nobody recorded" the same value - Article
    /// XI's failure with the fields swapped, and the harder one to notice,
    /// because the renderer prints a blank either way.
    /// </para>
    /// <para>
    /// <b>The rule these encoded is unchanged.</b> A gate about work in a
    /// repository still waits for the push - a decision about a tree on somebody's
    /// machine is a decision nobody can act on - and that is now a rule about
    /// repository destinations rather than a rule about gates. A reader that
    /// treated a present commit as proof the work is fetchable is still right.
    /// </para>
    /// </remarks>
    public string? Commit { get; init; }

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

    /// <summary>
    /// What an agent proposed, when admission opened this flight from a
    /// nomination. Null for a flight somebody asked for.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is the control the note rides on, and it is the only one.</b> A
    /// nomination's note reaches the next agent's prompt, and a work item is
    /// writable by more people than an envelope is - so there is a path from an
    /// issue tracker into an agent's context. What bounds it is a tenant putting
    /// a person in front of flight-opening with <c>requires:</c>, and that
    /// person reading this before they approve. A gate that did not show what
    /// will be carried would make approving a signature on something unseen.
    /// </para>
    /// <para>
    /// <b>Null rather than an empty proposal.</b> Most gates in an estate are on
    /// flights a person asked for; a block reading "nominated by: none" over
    /// four empty fields would be noise on every one of them. Absence is
    /// silence, the rule <see cref="Commit"/> already states one field over.
    /// </para>
    /// </remarks>
    public GateNomination? Nomination { get; init; }
}

/// <summary>
/// What a nominating agent proposed, as the reviewer sees it before the flight
/// it opened can proceed.
/// </summary>
/// <remarks>
/// <para>
/// <b>Its own type because it is one fact with one standing.</b> Flattened onto
/// <see cref="PendingGate"/> these five would sit beside the platform's own
/// fields and read as the platform's, which is exactly the confusion a person
/// answering a gate cannot afford. Grouped, the renderer has one thing to
/// attribute and one null to check.
/// </para>
/// <para>
/// <b>The reason is required and everything else is not, and that is the
/// discriminator.</b> Admission records why it opened a flight whenever it opens
/// one - "null for a flight somebody asked for" - so a proposal without a
/// reason is not a proposal. A work kind, by contrast, is on flights nobody
/// nominated, and keying on it would attribute a person's choice to an agent.
/// </para>
/// </remarks>
[PinnedId("4f8c31d5-2e07-4a96-b3d8-91c5e0a7462b")]
public sealed record GateNomination
{
    /// <summary>Why admission opened the flight, in the agent's own words.</summary>
    public required string Reason { get; init; }

    /// <summary>The work kind it nominated, when it named one.</summary>
    public string? WorkKind { get; init; }

    /// <summary>
    /// What the nominating agent would tell whoever picks the work up.
    /// </summary>
    /// <remarks>
    /// <b>Prose an agent wrote, so its line breaks are its own.</b> A note laid
    /// out over three lines was written to be read that way, and the renderer
    /// indents continuations rather than flattening them - the rule
    /// <c>GatePresentationTests</c> established for the question in
    /// <see cref="PendingGate.Because"/>.
    /// </remarks>
    public string? Note { get; init; }

    /// <summary>The environment it selected from the destination's menu, if any.</summary>
    public string? Environment { get; init; }

    /// <summary>The repository it selected from the destination's menu, if any.</summary>
    public string? Repository { get; init; }
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
