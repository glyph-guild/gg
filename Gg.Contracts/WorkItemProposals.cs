using System.Text.Json;

namespace Gg.Contracts;

/// <summary>
/// What a proposal asks be done to a work item.
/// </summary>
/// <remarks>
/// <para>
/// <b>Closed, and the whole gamut is in it.</b> An agent triaging a backlog
/// needs all five: an item that does not exist has to be creatable, one that is
/// wrong has to be correctable, and a triage that could only re-score would be
/// a triage that files its findings nowhere. Narrowing the list would not make
/// the platform safer - it would move a refusal from a place that records one
/// to a place that leaves nothing behind, because a tool that never offered the
/// argument produces no verdict a person can read.
/// </para>
/// <para>
/// <b>What bounds a flight is the admission, not this.</b> The destination's
/// menu says which of these a particular flight may have performed, the way
/// <c>Destination.Opens</c> already bounds which work kinds a nomination may
/// name. This vocabulary is what the agent may ASK for; the answer is somewhere
/// else, and it is a person's.
/// </para>
/// <para>
/// <b>One list because three things read it.</b> The tool's schema offers it,
/// the extractor checks what came back against it, and the control plane writes
/// conditions over it. Two spellings of one menu is how an operation becomes
/// proposable and unadmittable at the same time - which reads as a refusal
/// nobody wrote.
/// </para>
/// <para>
/// <b>Verbs for the change, not for the tracker's API.</b> Every tracker spells
/// its own writes differently and some of them have no notion of a link at all;
/// what is stable is what a person meant. The adapter maps these onto whatever
/// the tracker in front of it calls them, which is the seam that lets a second
/// tracker arrive without moving the contract.
/// </para>
/// </remarks>
[VocabularyOf(VocabularyFingerprints.Fact)]
public static class WorkItemOperations
{
    /// <summary>An item that should exist and does not.</summary>
    /// <remarks>
    /// The one operation with no target, because the tracker has not made an id
    /// yet. Requiring one would make this the operation nobody can propose.
    /// </remarks>
    public const string Create = "create";

    /// <summary>The item's own text: its title, its body, what it is asking for.</summary>
    public const string Update = "update";

    /// <summary>A field on the item - its type, its state, its area, its iteration.</summary>
    /// <remarks>
    /// <b>Separate from <see cref="Update"/> because a person admits them
    /// differently.</b> Rewording an item is nearly free to undo; moving it to
    /// another team's area path is somebody else's Monday. A destination that
    /// wanted to permit one and not the other could not say so if both arrived
    /// under one name.
    /// </remarks>
    public const string Field = "field";

    /// <summary>A relationship to another item - a parent, a duplicate, a blocker.</summary>
    public const string Link = "link";

    /// <summary>What the flight thinks this item is worth, in the rubric's terms.</summary>
    /// <remarks>
    /// It is here rather than folded into <see cref="Field"/> because it is the
    /// one a person asked for. A tracker may hold it in an ordinary field, and
    /// admission may still want to say yes to scoring and no to re-fielding.
    /// </remarks>
    public const string Score = "score";

    /// <summary>Every operation, in the order a triage tends to reach for them.</summary>
    public static IReadOnlyList<string> All { get; } = [Create, Update, Field, Link, Score];
}

/// <summary>
/// The bounds a proposal is held to, wherever one is read.
/// </summary>
/// <remarks>
/// <para>
/// <b>Here rather than on the tool, because the tool is not the only reader.</b>
/// The server refuses an oversized argument at the call so the agent can fix it
/// and call again; the extractor refuses one in a transcript, where nobody can.
/// Two bounds would mean a proposal the tool took and the runner dropped, which
/// is the silent half of the failure this slice keeps designing against.
/// </para>
/// <para>
/// <b>Borrowed from what a nomination measured, and said so.</b> Real triage
/// reasons ran around 700-800 characters across three measured runs, so 2000 is
/// roughly three times what one needs and past it the agent is writing an
/// analysis rather than a reason. Nothing about a work item makes its reason a
/// different size from a nomination's, and inventing a second number here would
/// be a guess wearing a constant's clothes.
/// </para>
/// </remarks>
public static class WorkItemProposalLimits
{
    /// <summary>The most an item id may be.</summary>
    /// <remarks>
    /// An identifier a tracker issued, not a sentence. Trackers issue integers,
    /// keys and GUIDs; nothing legitimate here is long.
    /// </remarks>
    public const int MaxTarget = 128;

    /// <summary>The most a score may be.</summary>
    /// <remarks>
    /// <b>A string, and the bound is what keeps that honest.</b> A score is
    /// whatever the rubric says - <c>P1</c>, <c>8</c>, <c>high / 2 of 3
    /// reporters blocked</c> - so the contract will not type it. What it will
    /// do is stop it becoming the analysis: that belongs in the detail, where a
    /// later reader knows to expect something it has to interpret.
    /// </remarks>
    public const int MaxScore = 128;

    /// <summary>The most a reason may be.</summary>
    public const int MaxReason = FlightNomination.MaxReason;

    /// <summary>The most the opaque detail may be, serialized.</summary>
    /// <remarks>
    /// <para>
    /// <b>A bound on something nobody here reads is still a bound.</b> Nothing
    /// validates the detail's shape - that is what makes it re-interpretable
    /// later - but unbounded means an agent can put a repository in a fact, and
    /// the facts of one flight travel together in one batch.
    /// </para>
    /// <para>
    /// Larger than the reason by an order, because the reason is a sentence
    /// somebody reads and this is a structure somebody's program reads.
    /// </para>
    /// </remarks>
    public const int MaxDetail = 16 * 1024;
}

/// <summary>
/// One field an agent proposes be set, and what to.
/// </summary>
/// <remarks>
/// <para>
/// <b>A named pair rather than a dictionary entry.</b> A wire type somebody
/// audits is a list of things with names; a map is a shape whose keys nobody
/// declared - and the keys here are the whole question a destination's menu
/// answers, so they are the last thing that should be implicit.
/// </para>
/// <para>
/// <b>The path is the tracker's own spelling and this contract does not parse
/// it.</b> Whether <c>Custom.RiceScore</c> exists, what type it takes and who
/// may write it are the tracker's answers; what this side owes is that the
/// path an agent asked for is the path a person permitted, which is a
/// comparison and not an interpretation.
/// </para>
/// <para>
/// <b>The value is a string, on the score's reasoning.</b> What a field holds
/// belongs to the rubric and the tracker, and typing it here would decide for
/// every field of every tracker in a place nobody consults while writing one.
/// </para>
/// </remarks>
[PinnedId("b4e07a19-52dc-4f38-9a6e-1d83c5f2074b")]
public sealed record WorkItemFieldEdit
{
    /// <summary>The field's reference path, as the tracker spells it.</summary>
    public required string Path { get; init; }

    /// <summary>What to set it to.</summary>
    public required string Value { get; init; }

    /// <summary>The most a path may be.</summary>
    /// <remarks>A reference path is an identifier, not a sentence.</remarks>
    public const int MaxPath = 256;

    /// <summary>The most a value may be.</summary>
    /// <remarks>
    /// Larger than a path and far smaller than the detail: a field a person
    /// reads in a work item's form is a value, and a document belongs in the
    /// item's description rather than in one of its fields.
    /// </remarks>
    public const int MaxValue = 2048;
}

/// <summary>
/// A change an agent proposes be made to a work item, and why.
/// </summary>
/// <remarks>
/// <para>
/// <b>The second fact in the vocabulary that is an agent's REQUEST</b>, beside
/// <see cref="FlightNomination"/>. Everything else is measured - a diff, a
/// commit, a session, a registry entry - and <see cref="HumanAccount"/> is a
/// person's own words. This one asks for something: that a tracker be changed.
/// It is marked as a request the way the nomination is, by its own kind and its
/// own slot on <see cref="FactEnvelope"/>, because a fact has no voice field
/// and a reader who could not tell the two apart would read an ask as a
/// finding.
/// </para>
/// <para>
/// <b>One fact per proposal, and that grain is the point.</b> A triage reads a
/// backlog and proposes a dozen changes to it; each arrives as its own fact so
/// each can be admitted or refused on its own. A fact carrying a list would
/// make the batch the unit a person decides on, and "yes to the re-field, no to
/// the link" would have nowhere to be said.
/// </para>
/// <para>
/// <b>It decides nothing.</b> The control plane holds it against the menu a
/// person wrote on the destination and refuses anything outside it; the write
/// is the runner's, afterwards, through a destination adapter. Nothing in this
/// path puts a tracker credential where an agent can reach it, which is what
/// makes proposing safe to grant at all.
/// </para>
/// <para>
/// <b>Four named members and one that is not, and the seam between them is
/// deliberate.</b> A proposal that was all blob would be unadmittable - a menu
/// cannot be matched against a field nothing declares. A proposal that was all
/// members would be this week guessing a schema on behalf of every rubric
/// anybody writes later. It is both, and <see cref="Detail"/> is the field
/// rather than the shape, so a reader can SEE which part was left open.
/// </para>
/// </remarks>
[FactKind(FactKinds.WorkItemProposal)]
[PinnedId("d3f21a4c-9b18-4e6a-8c53-0a7f1e2b6d94")]
public sealed record WorkItemProposal
{
    /// <summary>What is being proposed: one of <see cref="WorkItemOperations"/>.</summary>
    /// <remarks>
    /// Checked against the menu here because an operation nothing declares is
    /// one the control plane can neither admit nor refuse - it is a proposal
    /// nothing downstream can read, which is worse than either answer.
    /// </remarks>
    public required string Operation { get; init; }

    /// <summary>Why, in the agent's own words.</summary>
    /// <remarks>
    /// What makes the record worth reading, and what the person deciding
    /// actually reads. <i>Item 1421 was re-fielded</i> is a chore;
    /// <i>1421 was filed as a task and the repro attached to it crashes on
    /// startup</i> is a decision somebody can agree or disagree with.
    /// </remarks>
    public required string Reason { get; init; }

    /// <summary>
    /// The work item this is about, as the tracker spells its own ids, or null
    /// for a <see cref="WorkItemOperations.Create"/>.
    /// </summary>
    /// <remarks>
    /// <b>Declared and never parsed here.</b> Whether the tracker has this item,
    /// whether the destination's credential may write to it, and whether the
    /// item is one this flight was allowed near are all answered elsewhere - a
    /// runner is not an authority on somebody's backlog.
    /// </remarks>
    public string? Target { get; init; }

    /// <summary>
    /// What the flight thinks the item is worth, in the rubric's own terms, or
    /// null where nothing was scored.
    /// </summary>
    /// <remarks>
    /// <b>A string, on purpose.</b> A score is <c>P1</c>, or <c>8</c>, or
    /// <c>high, 2 of 3 reporters blocked</c> - which of those it is belongs to
    /// the envelope and the skill. Typing it as a number here would settle that
    /// once, for every tracker and every rubric, in a place nobody consults
    /// while writing one. Bounded so it cannot become the analysis: that
    /// belongs in <see cref="Detail"/>, where a reader knows to expect
    /// something it has to interpret.
    /// </remarks>
    public string? Score { get; init; }

    /// <summary>
    /// Everything else about this proposal, in whatever shape the agent chose.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>NOTHING HERE READS IT, and that is the whole feature.</b> The
    /// questions somebody will ask of a score six months from now are not
    /// knowable today - which rubric, how confident, what was ruled out - and a
    /// schema invented now would answer the wrong ones and silently discard the
    /// right ones. So it travels whole, and another agent on another day
    /// re-interprets it.
    /// </para>
    /// <para>
    /// <b>A field rather than the shape.</b> Making the whole fact free-form
    /// would have been simpler and worse: "which part of this record is
    /// open-ended" would then be a question you answer by knowing the history,
    /// instead of by looking at where the opacity is declared.
    /// </para>
    /// <para>
    /// <b>Structured, not a string of JSON.</b> A string would be opaque to the
    /// control plane's own storage as well as to its readers, so the later
    /// re-evaluation this exists for would begin by parsing something twice.
    /// Its SIZE is bounded even though its shape is not: the facts of one
    /// flight travel together in one batch, and unbounded means a repository
    /// can arrive in one.
    /// </para>
    /// </remarks>
    public JsonElement? Detail { get; init; }

    /// <summary>
    /// The fields this proposal would set, when its operation is
    /// <see cref="WorkItemOperations.Field"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>NAMED, because admission admits on what it can see.</b> The adapter
    /// reads a title and a state out of <see cref="Detail"/>, which is a
    /// convention between it and the skill and is fine for something nobody
    /// gates. These are gated: the destination carries a menu of the paths it
    /// permits, and a menu can only be applied to something the control plane
    /// can read. In the detail they would be checked by the runner, and the
    /// runner is not an authority.
    /// </para>
    /// <para>
    /// <b>On <c>field</c> and nothing else.</b> A link that carried field edits
    /// would be a proposal whose operation and whose content disagree - held
    /// against a menu while performing something else - so the pairing is
    /// validated rather than assumed.
    /// </para>
    /// </remarks>
    public IReadOnlyList<WorkItemFieldEdit>? Fields { get; init; }

    /// <summary>The most fields one proposal may set.</summary>
    /// <remarks>
    /// A scoring pass fills a form, and a form has a form's worth of fields.
    /// Past this an agent is rewriting an item rather than scoring one.
    /// </remarks>
    public const int MaxFields = 32;

    /// <summary>The diagnosis, or null when there is nothing wrong.</summary>
    public static string? Validate(WorkItemProposal proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);

        if (!WorkItemOperations.All.Contains(proposal.Operation, StringComparer.Ordinal))
        {
            return $"Unknown work-item operation '{proposal.Operation}'. Expected one of: "
                 + string.Join(", ", WorkItemOperations.All) + ".";
        }

        if (string.IsNullOrWhiteSpace(proposal.Reason))
        {
            return "A proposal says why. One with no reason is a change with no record of "
                 + "what it rested on, which is the half that makes it reviewable.";
        }

        if (proposal.Reason.Length > WorkItemProposalLimits.MaxReason)
        {
            return $"A proposal's reason is at most {WorkItemProposalLimits.MaxReason} "
                 + $"characters and this one is {proposal.Reason.Length}. Past that the agent "
                 + "is writing an analysis, which belongs in the detail.";
        }

        // CREATE IS THE EXCEPTION AND IT IS THE ONLY ONE. An item that does not
        // exist has no id; every other operation is ABOUT one, and a change with
        // no subject is one the adapter would have to guess the target of.
        var creating = string.Equals(
            proposal.Operation, WorkItemOperations.Create, StringComparison.Ordinal);

        if (proposal.Target is null)
        {
            if (!creating)
            {
                return $"A '{proposal.Operation}' proposal is about a work item and this one "
                     + "names none. Give a target, or propose "
                     + $"'{WorkItemOperations.Create}' if the item does not exist yet.";
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(proposal.Target))
            {
                return "A proposal's target is blank. Leave it out rather than sending an "
                     + "empty one: null says the item does not exist yet, and an empty "
                     + "string says one was named and produced nothing.";
            }

            if (proposal.Target.Length > WorkItemProposalLimits.MaxTarget)
            {
                return $"A proposal's target is at most {WorkItemProposalLimits.MaxTarget} "
                     + $"characters and this one is {proposal.Target.Length}. It is an "
                     + "identifier a tracker issued, not a sentence.";
            }
        }

        if (string.Equals(proposal.Operation, WorkItemOperations.Score, StringComparison.Ordinal)
            && proposal.Score is null)
        {
            return $"A '{WorkItemOperations.Score}' proposal with no score says an item should "
                 + "be scored without saying what to.";
        }

        if (proposal.Score is { } score)
        {
            if (string.IsNullOrWhiteSpace(score))
            {
                return "A proposal's score is blank. Leave it out rather than sending an "
                     + "empty one: null says nothing was scored, and an empty string says a "
                     + "rubric was applied and produced nothing.";
            }

            if (score.Length > WorkItemProposalLimits.MaxScore)
            {
                return $"A proposal's score is at most {WorkItemProposalLimits.MaxScore} "
                     + $"characters and this one is {score.Length}. A score is a verdict in "
                     + "the rubric's terms; the reasoning behind it belongs in the detail.";
            }
        }

        var setting = string.Equals(
            proposal.Operation, WorkItemOperations.Field, StringComparison.Ordinal);

        if (proposal.Fields is { } edits)
        {
            if (!setting)
            {
                return $"A '{proposal.Operation}' proposal carries field edits, and only "
                     + $"'{WorkItemOperations.Field}' sets fields. The operation says what "
                     + "happens and the edits say what changes; a proposal where those "
                     + "disagree means whatever its reader believes.";
            }

            if (edits.Count == 0)
            {
                return $"A '{WorkItemOperations.Field}' proposal names no field to set. Leave "
                     + "the list out rather than sending an empty one - absent says this is "
                     + "not a field proposal, and empty says one was attempted and produced "
                     + "nothing.";
            }

            if (edits.Count > MaxFields)
            {
                return $"A proposal sets at most {MaxFields} fields and this one sets "
                     + $"{edits.Count}. Past that it is rewriting an item rather than "
                     + "scoring one.";
            }

            if (Bad(edits) is { } badEdit)
            {
                return badEdit;
            }
        }
        else if (setting)
        {
            return $"A '{WorkItemOperations.Field}' proposal says an item should change "
                 + "without saying how. Name the fields it would set.";
        }

        // THE ONE MEASUREMENT TAKEN OF SOMETHING NOBODY HERE READS. Its shape
        // stays the agent's; its size does not.
        if (proposal.Detail is { } detail)
        {
            if (detail.ValueKind != JsonValueKind.Object)
            {
                return "A proposal's detail is an object - whatever a later reader would "
                     + "want, in fields it can find. A bare value has nothing to find it by.";
            }

            var written = detail.GetRawText().Length;
            if (written > WorkItemProposalLimits.MaxDetail)
            {
                return $"A proposal's detail is at most {WorkItemProposalLimits.MaxDetail} "
                     + $"characters and this one is {written}. Nothing reads it, which is "
                     + "why it is bounded: the facts of one flight travel in one batch.";
            }
        }

        return null;
    }

    /// <summary>The diagnosis for the first bad edit, or null.</summary>
    /// <remarks>
    /// <b>A blank VALUE is refused and it is the subtle one.</b> An empty
    /// string reads to a tracker as clearing the field, and clearing is a
    /// change nobody proposed - so it is a different act wearing the same
    /// shape, which is exactly what a bound is for.
    /// </remarks>
    private static string? Bad(IReadOnlyList<WorkItemFieldEdit> edits)
    {
        foreach (var edit in edits)
        {
            if (string.IsNullOrWhiteSpace(edit.Path))
            {
                return "A field edit names no path. A path that names nothing matches no "
                     + "menu entry and reaches no field.";
            }

            if (edit.Path.Length > WorkItemFieldEdit.MaxPath)
            {
                return $"A field path is at most {WorkItemFieldEdit.MaxPath} characters and "
                     + $"this one is {edit.Path.Length}. It is a reference path, not a "
                     + "sentence.";
            }

            if (string.IsNullOrWhiteSpace(edit.Value))
            {
                return $"The edit to '{edit.Path}' has a blank value. An empty string reads "
                     + "to a tracker as CLEARING the field, and clearing is a change nobody "
                     + "proposed - leave the edit out instead.";
            }

            if (edit.Value.Length > WorkItemFieldEdit.MaxValue)
            {
                return $"The edit to '{edit.Path}' is {edit.Value.Length} characters and a "
                     + $"field value is at most {WorkItemFieldEdit.MaxValue}. A document "
                     + "belongs in the item's description rather than one of its fields.";
            }
        }

        return null;
    }
}

/// <summary>
/// Whether a field path is on a menu somebody wrote.
/// </summary>
/// <remarks>
/// <para>
/// <b>ONE MATCHER, READ BY THREE.</b> Composition intersects two menus, the
/// direction comparator asks whether one reaches further than another, and
/// admission asks whether an edit is permitted. All three are the same
/// question, and three spellings of it is how a field becomes writable and
/// unadmittable at once — the tool's three-name hazard one member over.
/// </para>
/// <para>
/// <b>A wildcard is why this is a function and not a set operation.</b> Set
/// difference answers correctly in one direction and backwards in the other:
/// <c>{Custom.*}</c> minus <c>{Custom.Score}</c> is a widening and right,
/// while <c>{Custom.Score}</c> minus <c>{Custom.*}</c> is a widening reported
/// on a NARROWING. A governance rule that cries wolf is one people learn to
/// approve past, which is worse than not having it.
/// </para>
/// <para>
/// <b>A prefix, not a substring.</b> <c>Custom.*</c> matches
/// <c>Custom.RiceScore</c> and must not match <c>NotCustom.Thing</c>, which a
/// naive contains would.
/// </para>
/// </remarks>
public static class WorkItemFields
{
    /// <summary>The suffix that makes a menu entry a prefix rather than a path.</summary>
    public const string Wildcard = "*";

    /// <summary>Whether <paramref name="path"/> is permitted by <paramref name="menu"/>.</summary>
    public static bool Matches(string path, IReadOnlyList<string> menu)
    {
        ArgumentNullException.ThrowIfNull(menu);

        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        foreach (var entry in menu)
        {
            if (entry.EndsWith(Wildcard, StringComparison.Ordinal))
            {
                // THE PREFIX IS WHAT PRECEDES THE STAR, so `Custom.*` permits
                // `Custom.RiceScore` and refuses `NotCustom.Thing`. An entry
                // that is only a star never reaches here: Envelope.Validate
                // refuses it, because a menu permitting everything is not one.
                if (path.StartsWith(entry[..^Wildcard.Length], StringComparison.Ordinal))
                {
                    return true;
                }

                continue;
            }

            if (string.Equals(entry, path, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether <paramref name="narrower"/> reaches no further than
    /// <paramref name="wider"/>.
    /// </summary>
    /// <remarks>
    /// <b>What "no further" means for a wildcard.</b> An exact path is covered
    /// when the wider menu matches it. A prefix entry is covered only by an
    /// equal-or-shorter prefix — <c>Custom.*</c> does not cover
    /// <c>Custom.Score.*</c>'s parent, and nothing covers a prefix except a
    /// prefix that contains it, because a path-shaped entry permits exactly
    /// one field and a prefix permits a family.
    /// </remarks>
    public static bool Covers(IReadOnlyList<string> wider, IReadOnlyList<string> narrower)
    {
        ArgumentNullException.ThrowIfNull(wider);
        ArgumentNullException.ThrowIfNull(narrower);

        foreach (var entry in narrower)
        {
            var covered = entry.EndsWith(Wildcard, StringComparison.Ordinal)
                ? wider.Any(w => w.EndsWith(Wildcard, StringComparison.Ordinal)
                              && entry[..^Wildcard.Length].StartsWith(
                                     w[..^Wildcard.Length], StringComparison.Ordinal))
                : Matches(entry, wider);

            if (!covered)
            {
                return false;
            }
        }

        return true;
    }
}
