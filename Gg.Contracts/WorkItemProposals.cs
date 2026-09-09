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
