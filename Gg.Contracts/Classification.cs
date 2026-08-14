namespace Gg.Contracts;

/// <summary>
/// How sensitive an item is, on a scale a ceiling can be compared against.
/// </summary>
/// <remarks>
/// <para>
/// <b>More than one level, deliberately.</b> With a single level everything is
/// permitted, nothing is ever filtered, and every test of the filter passes on
/// a system that has no filter - which is this codebase's recurring defect and
/// would land squarely on the control that matters most.
/// </para>
/// <para>
/// Ordered by sensitivity rather than by name. Sorted alphabetically
/// <c>confidential</c> sits below <c>internal</c>, and a ceiling of internal
/// would let confidential material through.
/// </para>
/// </remarks>
[VocabularyOf(VocabularyFingerprints.Fact, Ordered = true)]
public static class Classifications
{
    /// <summary>Publishable. Documentation, licences, the things already public.</summary>
    public const string Public = "public";

    /// <summary>Ordinary source. The default for anything no rule names.</summary>
    public const string Internal = "internal";

    /// <summary>Sensitive by content: customer data, contracts, keys of a kind.</summary>
    public const string Confidential = "confidential";

    /// <summary>Never leaves. Credentials, private keys, anything regulated.</summary>
    public const string Restricted = "restricted";

    /// <summary>Least sensitive first. The order IS the control.</summary>
    public static IReadOnlyList<string> Ordered { get; } = [Public, Internal, Confidential, Restricted];

    /// <summary>How sensitive, or null when nothing declared this level.</summary>
    /// <remarks>
    /// Null rather than a default, because Article XI: a level nobody declared
    /// must halt rather than be treated as low, and a typo in a tenant's
    /// configuration must not be the same as switching the control off.
    /// </remarks>
    public static int? RankOf(string? level)
    {
        var index = level is null ? -1 : Ordered.ToList().IndexOf(level);
        return index < 0 ? null : index;
    }

    /// <summary>Whether an item at this level may cross a given ceiling.</summary>
    /// <remarks>
    /// Throws on either side being unknown. A comparison that quietly answered
    /// "yes" for a level it did not recognise would be the exact failure this
    /// control exists to prevent.
    /// </remarks>
    public static bool IsAtOrBelow(string level, string ceiling)
    {
        var item = RankOf(level)
            ?? throw new ArgumentException(
                $"'{level}' is not a classification. Expected one of: {string.Join(", ", Ordered)}.",
                nameof(level));

        var limit = RankOf(ceiling)
            ?? throw new ArgumentException(
                $"'{ceiling}' is not a classification ceiling. Expected one of: "
              + string.Join(", ", Ordered) + ". A ceiling nobody declared must halt rather than "
              + "permit everything.",
                nameof(ceiling));

        return item <= limit;
    }
}

/// <summary>One path pattern and the level it confers.</summary>
/// <remarks>
/// Tenant-configurable. The rule SET is the tenant's; the matching is this
/// package's, so the runner's filter and the control plane's re-derivation
/// cannot disagree for any reason other than a different rule set or a
/// different path.
/// </remarks>
[PinnedId("d3f01a67-9b24-4e58-8c71-05a6e2b4d938")]
public sealed record ClassificationRule
{
    /// <summary>
    /// A glob. <c>*</c> stays inside one path segment, <c>**</c> crosses them.
    /// </summary>
    public required string PathGlob { get; init; }

    /// <summary>One of <see cref="Classifications"/>.</summary>
    public required string Classification { get; init; }
}

/// <summary>
/// Classifying a path. One implementation, used by both sides.
/// </summary>
/// <remarks>
/// <para>
/// <b>Sharing the matcher is the point, and it is not the same as sharing the
/// answer.</b> The control plane re-derives with its OWN copy of the tenant's
/// rules and never reads the label a runner submitted - a patched runner would
/// simply label everything public, and a re-validation that trusted the label
/// would pass on every item it was built to catch.
/// </para>
/// <para>
/// What sharing the matcher buys is that a disagreement between the two
/// answers means something: a different rule set, or a different path. Never
/// two implementations of one idea drifting apart, which would make every
/// disagreement uninteresting and so make all of them ignored.
/// </para>
/// </remarks>
public static class ClassificationRules
{
    /// <summary>
    /// What a path takes when no rule names it.
    /// </summary>
    /// <remarks>
    /// Internal rather than public. Unclassified defaulting to the lowest level
    /// is how everything crosses; defaulting to the level most ceilings sit at
    /// means an unconfigured tenant gets the cautious answer rather than the
    /// convenient one.
    /// </remarks>
    public const string Unmatched = Classifications.Internal;

    /// <summary>
    /// A cautious starting set for a tenant that has configured nothing.
    /// </summary>
    /// <remarks>
    /// Not a substitute for a tenant's own rules, and the ceiling is what
    /// actually decides. It exists because an empty default would make the
    /// whole control arrive switched off.
    /// </remarks>
    public static IReadOnlyList<ClassificationRule> Default { get; } =
    [
        new() { PathGlob = "**/*.pem", Classification = Classifications.Restricted },
        new() { PathGlob = "**/*.key", Classification = Classifications.Restricted },
        new() { PathGlob = "**/*.p12", Classification = Classifications.Restricted },
        new() { PathGlob = "**/*.pfx", Classification = Classifications.Restricted },
        new() { PathGlob = "**/id_rsa", Classification = Classifications.Restricted },
        new() { PathGlob = "**/id_ed25519", Classification = Classifications.Restricted },
        new() { PathGlob = "**/.env", Classification = Classifications.Restricted },
        new() { PathGlob = "**/.env.*", Classification = Classifications.Restricted },
        new() { PathGlob = "**/secrets.*", Classification = Classifications.Confidential },
        new() { PathGlob = "**/*.pk8", Classification = Classifications.Restricted },
    ];

    /// <summary>
    /// The level this path takes. First matching rule wins.
    /// </summary>
    /// <remarks>
    /// Ordered rather than scored. A rule set where the outcome depends on
    /// which pattern is "most specific" is one nobody can predict by reading
    /// it, and a tenant has to be able to predict it.
    /// </remarks>
    public static string Classify(string path, IReadOnlyList<ClassificationRule> rules)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(rules);

        foreach (var rule in rules)
        {
            if (Matches(path, rule.PathGlob))
            {
                return rule.Classification;
            }
        }

        return Unmatched;
    }

    /// <summary>The diagnosis, or null when the rule set is well formed.</summary>
    public static string? Validate(IReadOnlyList<ClassificationRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        foreach (var rule in rules)
        {
            if (string.IsNullOrWhiteSpace(rule.PathGlob))
            {
                // The worst failure a rule set can have: a blank pattern that
                // silently classifies every path in the repository.
                return "A classification rule needs a path pattern. An empty one would match "
                     + "everything, quietly.";
            }

            if (Classifications.RankOf(rule.Classification) is null)
            {
                return $"'{rule.PathGlob}' confers '{rule.Classification}', which is not a "
                     + "classification. Expected one of: " + string.Join(", ", Classifications.Ordered) + ".";
            }
        }

        return null;
    }

    /// <summary>
    /// Glob matching, deliberately small.
    /// </summary>
    /// <remarks>
    /// <c>*</c> matches within one segment and <c>**</c> across them, and
    /// <c>**/</c> matches zero segments so a rule does not have to be written
    /// twice to catch a file at the root. Case-sensitive, the way paths are:
    /// a rule for <c>*.pem</c> that also caught <c>notes.PEMBROKE</c> would be
    /// a rule nobody could reason about.
    /// </remarks>
    private static bool Matches(string path, string glob)
    {
        // "**/" is allowed to match nothing, which is what people mean by it.
        if (glob.StartsWith("**/", StringComparison.Ordinal)
            && Matches(path, glob[3..]))
        {
            return true;
        }

        return Walk(path, 0, glob, 0);
    }

    private static bool Walk(string path, int p, string glob, int g)
    {
        while (g < glob.Length)
        {
            if (glob[g] == '*')
            {
                var crossesSegments = g + 1 < glob.Length && glob[g + 1] == '*';
                var rest = g + (crossesSegments ? 2 : 1);

                // Try every length this star could consume, shortest first.
                for (var take = 0; p + take <= path.Length; take++)
                {
                    if (!crossesSegments && take > 0 && path[p + take - 1] == '/')
                    {
                        break;
                    }
                    if (Walk(path, p + take, glob, rest))
                    {
                        return true;
                    }
                }

                return false;
            }

            if (p >= path.Length || path[p] != glob[g])
            {
                return false;
            }

            p++;
            g++;
        }

        return p == path.Length;
    }
}
