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
}
