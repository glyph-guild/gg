using Gg.Contracts.Description;

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

    /// <summary>
    /// What this nomination is about, when it is about something other than the
    /// flight it came from.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>ABSENT MEANS WHAT IT HAS ALWAYS MEANT</b>, which is why this is
    /// optional rather than required with a default. A nomination with no
    /// subject is a flight saying <i>here is what should happen next</i>, and
    /// the control plane keys it on the flight it came from - the arrangement
    /// every nominator built so far runs on, and the one that is still correct
    /// for all of them.
    /// </para>
    /// <para>
    /// <b>Present is a pass saying <i>here are three pieces of work</i>.</b>
    /// Those three collapse into one row unless each names what it is about,
    /// because the board supersedes per <c>(nominator, subject)</c> and one
    /// nominator with one subject is one row by construction. A watch already
    /// stands many rows from one sweep for exactly this reason, and
    /// <see cref="SweepNomination.Subject"/> is the member that lets it - this
    /// is that member, on the fact an agent inside a flight ships.
    /// </para>
    /// <para>
    /// <b>A piece of work, not a work item.</b> Some legs trace back to a
    /// tracker and some do not; what goes here is whatever names the thing to
    /// the nominator, and a plan whose legs are sentences is the case this has
    /// to survive.
    /// </para>
    /// </remarks>
    public string? Subject { get; init; }

    /// <summary>
    /// The subject of the leg this one follows, when it follows one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A plan's legs all become claimable at once, and nothing said so.</b>
    /// On a one-runner tenant a three-leg plan runs in whatever order the queue
    /// happens to offer, so every plan whose legs truly depend on each other has
    /// been relying on luck. Slice fifty-seven.
    /// </para>
    /// <para>
    /// <b>A SUBJECT, not an index.</b> A plan's legs are rarely a total order —
    /// two of three are commonly independent — and an index would make an agent
    /// invent a sequence it does not believe in. This names the identifier the
    /// nominator already supplies, and which the board already keys on.
    /// </para>
    /// <para>
    /// <b>Null is no constraint</b>, which is every nomination in the field.
    /// Absent and null are one answer, and neither is a claim to anything.
    /// </para>
    /// <para>
    /// <b>Whether the subject EXISTS is not knowable here.</b> That is a
    /// question about the other nominations of the same plan, and one nomination
    /// does not know them: the control plane resolves it when the board is
    /// approved, and refuses a cycle there, because an agent nominating one leg
    /// at a time cannot see the cycle it is about to close. What this type
    /// refuses is only what a single nomination can be wrong about by itself.
    /// </para>
    /// </remarks>
    public string? After { get; init; }

    /// <summary>
    /// Which version of that subject was nominated, when there is one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What makes a re-proposal converge instead of accumulating.</b> The
    /// board supersedes an older version of the same subject and skips an
    /// identical one, so a pass that runs twice over an unchanged plan writes
    /// no second row and opens no second flight. <see cref="Subject"/> says
    /// which row; this says whether it is the same one.
    /// </para>
    /// <para>
    /// <b>Only alongside a subject.</b> A version with nothing to be a version
    /// OF is a claim about something the nominator did not name, and refusing
    /// it here is cheaper than a store deciding later what it was about.
    /// </para>
    /// </remarks>
    public string? Version { get; init; }

    /// <summary>
    /// The itinerary this nomination belongs to, or null to have one minted.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Absent mints, present revises.</b> A pass that names none is
    /// proposing a plan for the first time and gets an itinerary of its own; a
    /// pass that names one is revising that plan, and its legs supersede,
    /// replace or drop the legs already standing under it.
    /// </para>
    /// <para>
    /// <b>Legal without a <see cref="Subject"/>, and that is not an
    /// oversight.</b> An unplanned flight joins an itinerary by nominating
    /// under it, and what it has to say is <i>what should happen next</i> -
    /// the subjectless reading, keyed on the flight it came from, which is
    /// distinct per flight and so collapses nothing.
    /// </para>
    /// <para>
    /// <b>A reference, read by <see cref="ItineraryRef"/>.</b> Either form: an
    /// agent that read <c>ITN-7</c> off a prompt names it the way it saw it,
    /// and one holding the id names that. Refused here rather than accepted
    /// and resolved to nothing, because a plan revising an itinerary that does
    /// not parse is a plan that would silently have minted a second one.
    /// </para>
    /// </remarks>
    public string? Itinerary { get; init; }

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

    /// <summary>The most a subject may be.</summary>
    /// <remarks>
    /// <see cref="SweepNomination.MaxSubject"/> itself rather than the same
    /// number written twice. The two members are the same member on two tools,
    /// and a bound that could drift between them would let a subject be sayable
    /// by a sweep and refused from a flight.
    /// </remarks>
    public const int MaxSubject = SweepNomination.MaxSubject;

    /// <summary>The most a version may be.</summary>
    public const int MaxVersion = SweepNomination.MaxVersion;

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

        // AN IDENTITY, NEVER TEXT. Past these a subject is a sentence, and a
        // sentence here is a work item's prose arriving in a store under an
        // identity's name - SweepNomination.Invalid's rule, on the same two
        // members.
        if (nomination.Subject is { } subject)
        {
            if (string.IsNullOrWhiteSpace(subject))
            {
                return "A nomination's subject is blank. Leave it out rather than sending an "
                     + "empty one: null says this is about the flight it came from, and an "
                     + "empty string says it is about something that was not named.";
            }

            if (subject.Length > MaxSubject)
            {
                return $"A nomination's subject is at most {MaxSubject} characters and this one "
                     + $"is {subject.Length}. It is what names the thing, not a description of "
                     + "it.";
            }
        }

        if (nomination.After is { } after)
        {
            if (string.IsNullOrWhiteSpace(after))
            {
                return "A nomination's `after` is blank. Leave it out rather than sending an "
                     + "empty one: absence means this leg follows nothing, and blank means "
                     + "somebody meant to say which leg and did not.";
            }

            if (after.Length > MaxSubject)
            {
                return $"A nomination's `after` is at most {MaxSubject} characters and this one "
                     + $"is {after.Length}. It holds a SUBJECT, so a longer value names a "
                     + "subject no leg can have.";
            }

            // THE ONE CYCLE A SINGLE NOMINATION CAN SEE. Every longer one needs
            // the other legs, which is the board's to check when it has them
            // all - and a leg that waits for itself never runs, with nothing
            // downstream able to say why.
            if (string.Equals(after, nomination.Subject, StringComparison.Ordinal))
            {
                return $"A nomination follows '{after}', which is its own subject. A leg that "
                     + "waits for itself never runs.";
            }

            // AN UNNAMEABLE LEG CANNOT BE ORDERED. The board supersedes per
            // (nominator, subject), so legs with no subject collapse into one
            // row - and an edge into a row about to be overwritten is an order
            // nobody can honour.
            if (nomination.Subject is null)
            {
                return $"A nomination follows '{after}' and names no subject of its own. One "
                     + "nomination with no subject is a single piece of work, and a single "
                     + "piece of work has nothing to follow.";
            }
        }

        if (nomination.Version is { } version)
        {
            if (nomination.Subject is null)
            {
                return "A nomination names a version and no subject, so it is a version of "
                     + "something it did not name. A subjectless nomination is about the flight "
                     + "it came from, and that flight's version is not the nominator's to state.";
            }

            if (string.IsNullOrWhiteSpace(version))
            {
                return "A nomination's version is blank. Leave it out rather than sending an "
                     + "empty one: null says the subject has no version worth comparing, and an "
                     + "empty string is one that compares equal to nothing.";
            }

            if (version.Length > MaxVersion)
            {
                return $"A nomination's version is at most {MaxVersion} characters and this one "
                     + $"is {version.Length}. It is compared for equality, not read.";
            }
        }

        // READ HERE RATHER THAN DECLARED, unlike the work kind: an itinerary
        // reference has a rule, the rule lives in the contract, and a plan that
        // named an unparseable one would quietly have minted a second itinerary
        // instead of revising the one it meant.
        if (nomination.Itinerary is { } itinerary
            && !ItineraryRef.TryParse(itinerary, out _))
        {
            return $"'{itinerary}' is not an itinerary reference. It is the number a person was "
                 + $"shown - {ItineraryRef.Format(7)} - or the id underneath it, and a pass that "
                 + "names neither is revising nothing.";
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

/// <summary>
/// An airspace document a flight drafted, and the name it is for.
/// </summary>
/// <remarks>
/// <para>
/// <b>A request, like its neighbour in this file.</b> Nothing here applies
/// anything: the control plane holds it as a proposal, and the gate the tenant's
/// own envelope declares is what decides.
/// </para>
/// <para>
/// <b>The role is named though the document implies it.</b> The parser forks by
/// role and would reach an answer alone - but then a document that parses as the
/// wrong thing lands as that wrong thing, silently. Naming it lets the two
/// disagree, and a disagreement is something that can be refused.
/// </para>
/// <para>
/// <b>The text travels whole, unlike a transcript.</b> A transcript is customer
/// content and crosses as a reference; an airspace document is the tenant's own
/// governance text, which already crosses whole every time somebody applies one.
/// No new disposition, and no member a body could hide in that is not already the
/// document itself.
/// </para>
/// <para>
/// <b>No <c>based-on</c> member, deliberately.</b> The document carries its own,
/// and a second copy is a second thing to disagree with the first.
/// </para>
/// </remarks>
[FactKind(FactKinds.DocumentProposal)]
[PinnedId("c02da844-26e8-4d69-99f5-9b5f7186a234")]
public sealed record DocumentProposal
{
    /// <summary>Which role it claims to be, from <see cref="Roles"/>.</summary>
    public required string Role { get; init; }

    /// <summary>The declared name in the tenant's topology it is for.</summary>
    public required string Name { get; init; }

    /// <summary>The document, as its author wrote it.</summary>
    public required string Document { get; init; }
}
