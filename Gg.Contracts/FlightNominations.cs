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

    /// <summary>
    /// What the nominating agent would tell whoever picks this up, or null when
    /// it has nothing to add.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What the first agent LEARNED, not a restatement of the item.</b>
    /// Measured before it was designed: three real triage runs against a work
    /// item describing a defect that did not exist all read the code, found the
    /// described behaviour already correct, and spent the note saying which
    /// question to ask the reporter instead. That is the case for carrying one
    /// at all - a second agent starting from the item alone would have written
    /// the fix the item asked for.
    /// </para>
    /// <para>
    /// <b>ADVICE, NEVER AUTHORITY</b>, the rule <see cref="Reason"/> and
    /// <c>LeaseFeedback</c> already hold. It reaches the next prompt fenced and
    /// attributed as an agent's words, and it grants nothing: scope, moves and
    /// budget come from the envelope, and an instruction to exceed them fails at
    /// the manifest check. All three measured notes were shaped that way without
    /// being asked - a warning not to start coding, the evidence, then what to
    /// confirm with the reporter.
    /// </para>
    /// <para>
    /// <b>Optional, so nothing already made has to change.</b> Null when there
    /// is nothing to add; blank is refused, because a classifier that wrote an
    /// empty string produced a field instead of declining to fill one, and a
    /// fenced block with nothing in it attributes silence to somebody.
    /// </para>
    /// <para>
    /// <b>One hop, and this type cannot enforce that.</b> A flight opened from a
    /// nomination carries its note; a flight opened from THAT flight does not.
    /// The rule lives where flights are opened, because a fact has no way to
    /// know how many times it has been forwarded - which is why it is written
    /// here as the thing the admission path owes.
    /// </para>
    /// </remarks>
    public string? Note { get; init; }

    /// <summary>
    /// The environment this work should run in, or null when the classifier
    /// selected none.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Declared, never parsed.</b> Nothing reads an environment out of the
    /// reason or the note. A selection lifted from prose would be a governance
    /// decision made by a regex - and the prose in question came from a work
    /// item, which in most organisations more people can write than can edit the
    /// envelope.
    /// </para>
    /// <para>
    /// <b>A value bounded by a menu, not a permission.</b> The destination's
    /// <c>may-select</c> says which environments may be named here, and anything
    /// outside it is refused rather than clamped: clamping to the nearest
    /// permitted value would be the platform choosing where somebody else's work
    /// runs and reporting success. What makes it safe to ask for is that the
    /// menu was written by a person and the answer is checked against it.
    /// </para>
    /// <para>
    /// <b>Bounded like <see cref="WorkKind"/> and not like
    /// <see cref="Reason"/>.</b> It is a name in the tenant's chart, so
    /// something long enough to be an argument is a classifier explaining itself
    /// in a field admission matches exactly.
    /// </para>
    /// </remarks>
    public string? Environment { get; init; }

    /// <summary>
    /// The repository this work should be done in, or null when the classifier
    /// selected none.
    /// </summary>
    /// <remarks>
    /// A registered slug, bounded and refused the way <see cref="Environment"/>
    /// is and for the same reasons. Ingress already refuses one the tenant has
    /// not registered; the destination's menu is the door in front of that.
    /// </remarks>
    public string? Repository { get; init; }

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

    /// <summary>The most a note may be.</summary>
    /// <remarks>
    /// <b>Measured, and the same as <see cref="MaxReason"/> because the
    /// measurement said so.</b> Three real triage runs wrote 728, 774 and 833
    /// characters - the same magnitude as the reason's own measured ~700. Two
    /// fields a classifier fills in one breath, both bounded at what one of them
    /// was measured to need, and nothing here justifies letting the note run
    /// longer than the reason it sits beside.
    /// </remarks>
    public const int MaxNote = MaxReason;

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

        // A NAME, NEVER PROSE, and blank refused rather than carried - the rule
        // the note beside them holds, for the reason it holds it.
        foreach (var (what, selected) in ((string, string?)[])
            [("environment", nomination.Environment), ("repository", nomination.Repository)])
        {
            if (selected is null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(selected))
            {
                return $"A nomination's {what} is blank. Leave it out rather than sending an "
                     + "empty one: null says no selection was made, and an empty string says "
                     + "one was attempted and produced nothing.";
            }

            if (selected.Length > MaxWorkKind)
            {
                return $"A nominated {what} is at most {MaxWorkKind} characters and this one "
                     + $"is {selected.Length}. It is a name admission matches exactly, not a "
                     + "sentence.";
            }
        }

        if (nomination.Note is { } note)
        {
            if (string.IsNullOrWhiteSpace(note))
            {
                return "A nomination's note is what the classifier would tell whoever picks "
                     + "this up, and this one is blank. Leave it out rather than sending an "
                     + "empty one: null says there is nothing to add, and an empty string "
                     + "renders a fenced block attributing silence to an agent.";
            }

            if (note.Length > MaxNote)
            {
                return $"A nomination's note is at most {MaxNote} characters and this one is "
                     + $"{note.Length}. Real notes measure around 800, so past this it is an "
                     + "analysis rather than a handover - and it is refused rather than "
                     + "truncated, because half a note reads as a whole one.";
            }
        }

        return nomination.Reason.Length > MaxReason
            ? $"A nomination's reason is at most {MaxReason} characters and this one is "
            + $"{nomination.Reason.Length}. Past that it is an analysis rather than a reason."
            : null;
    }
}
