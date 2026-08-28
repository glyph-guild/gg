namespace Gg.Contracts;

/// <summary>
/// Whether a fact family describes a subject, the tree a flight touched, or the
/// flight itself.
/// </summary>
/// <remarks>
/// <para>
/// <b>ADR-0021 § 2's split, and it is what makes the re-key possible.</b>
/// ADR-0014's question 4 asks what can be measured about a <i>subject</i>, and
/// most of the vocabulary does not measure a subject at all. Without the
/// distinction, a subject-keyed totality guard has to demand a row for
/// <c>loop.outcome</c> — <i>which subject kinds produce this?</i> — and the only
/// answers are <i>all of them</i> and <i>none of them</i>, which are the same
/// answer written twice.
/// </para>
/// <para>
/// <b>It lives beside <see cref="FactKinds"/> because it is a property of the
/// vocabulary.</b> Whether a family needs a tree is not a control-plane opinion;
/// it follows from what the family IS. Keeping it on the far side of the
/// boundary meant either a second copy here — two computations of one question,
/// in the place where being wrong removes a gate — or an authoring refusal an
/// author could not get until they applied.
/// </para>
/// <para>
/// <b>Flight facts are outside the producibility question by construction.</b>
/// Not exempted, not defaulted, not given an <i>all subject kinds</i> row: the
/// question is never asked of them, because a family that measures an episode
/// is produced by every work kind that runs one. An exemption list would need
/// maintaining and could go stale; a category cannot.
/// </para>
/// <para>
/// <b>The classification is built from <see cref="FactKinds.All"/> and not from
/// the ADR.</b> ADR-0021 § 2's table names <c>budget.attempts</c> and
/// <c>check.verdict</c>, neither of which is a fact family — the first was never
/// built and the second is deliberately absent with its reason written in the
/// contract — while omitting <c>loop.transcript</c> and <c>handoff.account</c>,
/// both of which are. Both tables have nine rows and they are different nines.
/// </para>
/// </remarks>
[VocabularyOf(VocabularyFingerprints.Fact)]
public static class FactCategories
{
    /// <summary>A standing property of a thing that exists between flights.</summary>
    public const string Subject = "subject";

    /// <summary>Measured from the tree a flight touched. Needs a tree to exist.</summary>
    public const string Tree = "tree";

    /// <summary>Measures the episode. Every work kind that runs produces these.</summary>
    public const string Flight = "flight";

    public static IReadOnlyList<string> All { get; } = [Subject, Tree, Flight];

    private static readonly Dictionary<string, string> Categories = new(StringComparer.Ordinal)
    {
        // THE ONLY SUBJECT FACT WE SHIP, and it belongs to a subject kind that
        // is not admitted. ADR-0021 § 2 calls that the cheapest possible test of
        // whether any of this generalises: the fact exists and the subject does
        // not. An environment is not a subject kind, so nothing vetoes it.
        [FactKinds.EnvironmentIdentity] = Subject,

        // MEASURED FROM A TREE. A diff and the commit it was taken from. These
        // are the families the subject can veto, and the only ones.
        [FactKinds.ChangeManifest] = Tree,
        [FactKinds.SourceProvenance] = Tree,

        // THE EPISODE. What the loop did, what it said, who picked it up, and
        // where the work went.
        //
        // destination.pushed and destination.landed are here rather than under
        // Tree, and that is the correction to slice sixteen's map: both required
        // a repository, and both measure what an EPISODE did rather than a
        // standing property of a tree. A flight that pushed nowhere produced no
        // such fact for reasons that have nothing to do with whether it had a
        // tree to push from.
        [FactKinds.LoopOutcome] = Flight,
        [FactKinds.LoopTranscript] = Flight,
        [FactKinds.LoopDigest] = Flight,
        [FactKinds.HumanAccount] = Flight,
        [FactKinds.DestinationPushed] = Flight,
        [FactKinds.DestinationLanded] = Flight,
    };

    /// <summary>Whether this family has a decided category. The guard reads it.</summary>
    public static bool IsClassified(string family) =>
        family is not null && Categories.ContainsKey(family);

    /// <summary>
    /// The category, one of <see cref="All"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The family is unclassified. Deliberately not a fallback: <c>Flight</c>
    /// would keep every obligation applicable and <c>Tree</c> would remove rules
    /// from every subjectless flight, and both are guesses.
    /// </exception>
    public static string Of(string family) =>
        Categories.TryGetValue(family, out var category)
            ? category
            : throw new ArgumentOutOfRangeException(
                nameof(family),
                family,
                "No category is decided for this fact family, so nothing can say whether a work "
              + "kind could produce it. Answering either way would be a guess: 'flight' keeps "
              + "every obligation applicable and 'tree' removes rules from every subjectless "
              + "flight. Classify it.");
}
