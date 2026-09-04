namespace Gg.Contracts;

/// <summary>
/// The work kind a classifier says this line of work needs, and why.
/// </summary>
/// <remarks>
/// <para>
/// <b>The only fact in the vocabulary that is an agent's REQUEST.</b> Everything
/// else is measured - a diff, a commit, a session, a registry entry - and
/// <see cref="HumanAccount"/> is a person's own words. This one asks for
/// something: that a flight of a particular work kind exist. It sits beside the
/// human account rather than among the measurements for that reason.
/// </para>
/// <para>
/// <b>Stated by construction, because a fact has no voice.</b>
/// <c>EvidenceVoices</c> is a member of <c>GateEvidenceItem</c> and of nothing
/// else, so there is no field here to mark this as a claim - and adding one
/// would be a new closed vocabulary with a gate behind it. What marks it is
/// what marks a person's account: its own kind, its own slot on
/// <see cref="FactEnvelope"/>, and a name that says it is a nomination rather
/// than a classification. A reader who could not tell the two apart would read
/// a request as a finding.
/// </para>
/// <para>
/// <b>It decides nothing.</b> The control plane holds it against the menu a
/// person wrote on the destination - <c>Destination.Opens</c> - and refuses
/// anything outside it. A work kind is the selection of a governance regime, so
/// an agent that could name any of them would be choosing its own moves. This
/// is the ask; admission is the answer, and a nomination the destination does
/// not permit opens nothing.
/// </para>
/// <para>
/// <b>Two members, and the shape is a ratchet.</b> The pressure runs one way:
/// every field somebody will want to add - a move the work needs, a scope, a
/// budget, a destination, an approver - makes this more useful and makes it
/// configuration an agent writes. <see cref="LeaseFeedback"/> holds the same
/// line travelling the other way, and for the same reason: what crosses is a
/// value, never a permission.
/// </para>
/// </remarks>
[FactKind(FactKinds.FlightNomination)]
[PinnedId("620b7b63-e87e-4320-80f5-274e2c44bf6e")]
public sealed record FlightNomination
{
    /// <summary>
    /// The work-kind name this loop nominates a flight be opened for.
    /// </summary>
    /// <remarks>
    /// Declared and never parsed here. Whether the tenant's topology knows this
    /// name, whether it plays the work-kind role, and whether the destination
    /// admitting it may open it are all the control plane's to answer - a
    /// runner is not an authority on the topology any more than it is on the
    /// envelope.
    /// </remarks>
    public required string WorkKind { get; init; }

    /// <summary>Why, in the agent's own words.</summary>
    /// <remarks>
    /// The reason is what makes the record worth reading: <i>a flight was
    /// opened</i> is a chore, and <i>a flight was opened because the item
    /// already named the root cause and the file to change</i> is a decision
    /// somebody can review. It is prose an agent wrote, so it crosses under the
    /// tenant's own cleanliness rules like every other sentence.
    /// </remarks>
    public required string Reason { get; init; }

    /// <summary>The most a nominated name may be.</summary>
    /// <remarks>
    /// A work kind is a name in a topology, and an unbounded one is a string
    /// somebody put a document in.
    /// </remarks>
    public const int MaxWorkKind = 128;

    /// <summary>The most a reason may be.</summary>
    /// <remarks>
    /// <b>Measured rather than guessed.</b> A real classifier's reason for a
    /// clear-cut item ran about 700 characters, so this is roughly three times
    /// what one needs. Past it the agent is writing an analysis, and a fact
    /// that carried one would be the reference disposition's job - which for a
    /// sentence a person reads while deciding something is the wrong shape.
    /// </remarks>
    public const int MaxReason = 2000;

    /// <summary>The diagnosis, or null when there is nothing wrong.</summary>
    public static string? Validate(FlightNomination nomination)
    {
        ArgumentNullException.ThrowIfNull(nomination);

        if (string.IsNullOrWhiteSpace(nomination.WorkKind))
        {
            return "A nomination names a work kind. One that names none is a classifier that "
                 + "produced a fact instead of declining, and declining is a real answer.";
        }

        if (nomination.WorkKind.Length > MaxWorkKind)
        {
            return $"A nominated work kind is at most {MaxWorkKind} characters and this one is "
                 + $"{nomination.WorkKind.Length}. It is a name in a topology, not a sentence.";
        }

        if (string.IsNullOrWhiteSpace(nomination.Reason))
        {
            return "A nomination says why. One with no reason is a decision with no record of "
                 + "what it rested on, which is the half that makes it reviewable.";
        }

        return nomination.Reason.Length > MaxReason
            ? $"A nomination's reason is at most {MaxReason} characters and this one is "
            + $"{nomination.Reason.Length}. Past that it is an analysis rather than a reason."
            : null;
    }
}
