namespace Gg.Contracts;

/// <summary>
/// Which ledger a closed vocabulary belongs to.
/// </summary>
/// <remarks>
/// Two, and there is deliberately no third. A vocabulary that belongs to neither is worth
/// a conversation rather than an escape hatch: an unused value in an enumeration of ledgers
/// is a hole built before there is a case for it.
/// </remarks>
[VocabularyOf(VocabularyFingerprints.Contract)]
public static class VocabularyFingerprints
{
    /// <summary>Its values can appear inside a fact a runner ships.</summary>
    public const string Fact = "fact";

    /// <summary>Its values appear on the wire, but never inside a fact.</summary>
    public const string Contract = "contract";

    public static IReadOnlyList<string> All { get; } = [Fact, Contract];
}

/// <summary>
/// Declares which ledger's fingerprint a closed vocabulary is part of.
/// </summary>
/// <remarks>
/// <para>
/// <b>Declared, because shape cannot answer this.</b> Discovery by shape is right about
/// WHICH types are closed vocabularies - it finds them all - and cannot be right about
/// which fingerprint each belongs to, because nothing about
/// <c>public static IReadOnlyList&lt;string&gt;</c> says whether the values reach a fact.
/// Attributing by shape is how a gate payload's vocabulary ended up moving the fact
/// vocabulary's fingerprint while no fact kind changed.
/// </para>
/// <para>
/// <b>Not by reachability.</b> These are string lists rather than typed enumerations: a
/// fact carrying a diff basis carries a <c>string</c>, so there is no edge in the type
/// graph from <c>change.manifest</c> to <see cref="DiffBasis"/>. Inferring membership from
/// the type graph would silently drop exactly the vocabulary whose invisibility this
/// mechanism was built to fix.
/// </para>
/// <para>
/// <b>A closure check makes declaring safe.</b> Every type matching the shape must carry
/// exactly one of these, so forgetting fails the build rather than quietly narrowing what
/// the fingerprint covers - which would be the shape-based attribution's defect returning
/// as an omission instead.
/// </para>
/// <para>
/// <b>When a vocabulary is on both sides, it is a fact.</b> Moves and executor rungs are
/// declared in an envelope AND reported inside <c>loop.outcome</c>; a change to either
/// changes what a fact can say, and that is the stricter obligation of the two.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class VocabularyOfAttribute(string fingerprint) : Attribute
{
    /// <summary>One of <see cref="VocabularyFingerprints"/>.</summary>
    public string Fingerprint { get; } = fingerprint;

    /// <summary>
    /// Whether the ORDER of the values is part of what they mean.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A fingerprint's normalisation must preserve everything meaningful.</b> Sorting
    /// before hashing is right for a SET - reordering how somebody happened to type a list
    /// is not a wire change and must not read as one - and wrong for a RANKING, where the
    /// order IS the content.
    /// </para>
    /// <para>
    /// <b>The case that forced it.</b> <see cref="Classifications"/> is a ranking: whether
    /// a fact may leave a customer's network is computed from whether its level sits at or
    /// below a ceiling, and that comparison reads the order. Sorted before hashing,
    /// reordering the levels would change what may cross and move no ledger - a silent
    /// change to an egress control, inside the ledger built to make silent changes
    /// impossible.
    /// </para>
    /// <para>
    /// A domain too broad cries wolf and a domain too narrow misses; a normalisation too
    /// aggressive misses in a way that looks like coverage, which is worse than both.
    /// </para>
    /// </remarks>
    public bool Ordered { get; init; }
}
