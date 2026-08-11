namespace Gg.Contracts.Tests;

/// <summary>
/// What classification means, now that something could plausibly be sensitive.
/// </summary>
/// <remarks>
/// <para>
/// <c>classification_ceiling</c> has been a column since step 2 and has never
/// had a meaning. <c>change.manifest</c> is the first evidence a customer could
/// mind, so it needs one.
/// </para>
/// <para>
/// The rules live HERE, in the contract, and that is the point of the whole
/// arrangement: the runner filters with them and the control plane re-derives
/// with them, so a disagreement between the two can only come from a different
/// rule set or a different path - never from two implementations of the same
/// idea drifting apart. Which copy of the rules each side uses is a separate
/// question, and the answer is "its own".
/// </para>
/// </remarks>
public class ClassificationTests
{
    [Test]
    public async Task There_is_more_than_one_level_or_the_filter_is_untestable()
    {
        // The recurring defect in this codebase, and it would land squarely
        // here: with one level everything is permitted, nothing is filtered,
        // and "the filter works" passes on a system with no filter.
        await Assert.That(Classifications.Ordered.Count).IsGreaterThan(1);
        await Assert.That(Classifications.Ordered).Contains(Classifications.Public);
        await Assert.That(Classifications.Ordered).Contains(Classifications.Restricted);
    }

    [Test]
    public async Task Levels_are_ordered_by_sensitivity_rather_than_alphabetically()
    {
        // The order is the control. Sorted by name, "confidential" would sit
        // below "internal" and the ceiling would let the wrong things through.
        await Assert.That(Classifications.RankOf(Classifications.Public)!.Value)
            .IsLessThan(Classifications.RankOf(Classifications.Internal)!.Value);
        await Assert.That(Classifications.RankOf(Classifications.Internal)!.Value)
            .IsLessThan(Classifications.RankOf(Classifications.Confidential)!.Value);
        await Assert.That(Classifications.RankOf(Classifications.Confidential)!.Value)
            .IsLessThan(Classifications.RankOf(Classifications.Restricted)!.Value);
    }

    [Test]
    public async Task A_level_nobody_declared_halts_rather_than_being_treated_as_low()
    {
        // Article XI. An unknown level treated as public is the failure mode
        // this whole control exists to prevent, and it is the one a typo
        // produces.
        await Assert.That(Classifications.RankOf("secret-ish")).IsNull();
        await Assert.That(() => Classifications.IsAtOrBelow("secret-ish", Classifications.Restricted))
            .Throws<ArgumentException>();
        await Assert.That(() => Classifications.IsAtOrBelow(Classifications.Public, "very-high"))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task A_ceiling_permits_its_own_level_and_everything_below()
    {
        await Assert.That(Classifications.IsAtOrBelow(
            Classifications.Internal, Classifications.Internal)).IsTrue();
        await Assert.That(Classifications.IsAtOrBelow(
            Classifications.Public, Classifications.Internal)).IsTrue();
        await Assert.That(Classifications.IsAtOrBelow(
            Classifications.Confidential, Classifications.Internal)).IsFalse();
    }

    // ---- the rules ----

    private static IReadOnlyList<ClassificationRule> Rules(params (string Glob, string Level)[] rules) =>
        [.. rules.Select(r => new ClassificationRule { PathGlob = r.Glob, Classification = r.Level })];

    [Test]
    public async Task The_first_matching_rule_wins()
    {
        // Ordered, not scored. A rule set where the outcome depends on which
        // rule is "most specific" is one nobody can predict by reading it.
        var rules = Rules(
            ("docs/**", Classifications.Public),
            ("**", Classifications.Confidential));

        await Assert.That(ClassificationRules.Classify("docs/readme.md", rules))
            .IsEqualTo(Classifications.Public);
        await Assert.That(ClassificationRules.Classify("src/Program.cs", rules))
            .IsEqualTo(Classifications.Confidential);
    }

    [Test]
    public async Task A_path_no_rule_matches_takes_the_default_rather_than_the_lowest_level()
    {
        // Unclassified defaulting to public is how everything crosses. It
        // defaults to internal, which is the level a ceiling most commonly is -
        // so an unconfigured tenant gets the cautious answer.
        await Assert.That(ClassificationRules.Classify("anything", Rules()))
            .IsEqualTo(ClassificationRules.Unmatched);
        await Assert.That(ClassificationRules.Unmatched).IsEqualTo(Classifications.Internal);
    }

    [Test]
    public async Task A_single_star_stays_inside_one_path_segment()
    {
        var rules = Rules(("*.pem", Classifications.Restricted));

        await Assert.That(ClassificationRules.Classify("key.pem", rules))
            .IsEqualTo(Classifications.Restricted);
        await Assert.That(ClassificationRules.Classify("certs/key.pem", rules))
            .IsEqualTo(ClassificationRules.Unmatched)
            .Because("a single star that crossed a slash would make every rule accidentally recursive.");
    }

    [Test]
    public async Task A_double_star_crosses_segments()
    {
        var rules = Rules(("**/*.pem", Classifications.Restricted));

        await Assert.That(ClassificationRules.Classify("certs/prod/key.pem", rules))
            .IsEqualTo(Classifications.Restricted);
        await Assert.That(ClassificationRules.Classify("key.pem", rules))
            .IsEqualTo(Classifications.Restricted)
            .Because("**/ matching zero segments is what people mean by it; the alternative needs "
                   + "every rule written twice.");
    }

    [Test]
    public async Task A_rule_naming_a_level_nobody_declared_is_refused()
    {
        await Assert.That(ClassificationRules.Validate(Rules(("**", "top-secret")))).IsNotNull();
        await Assert.That(ClassificationRules.Validate(Rules(("**", Classifications.Public)))).IsNull();
    }

    [Test]
    public async Task An_empty_glob_is_refused_rather_than_matching_everything()
    {
        // The worst possible failure of a rule set: a blank pattern that
        // silently classifies every path.
        await Assert.That(ClassificationRules.Validate(Rules(("", Classifications.Public)))).IsNotNull();
    }

    [Test]
    public async Task Classification_is_case_sensitive_the_way_paths_are()
    {
        // A rule for *.pem must not be dodged by naming a file KEY.PEM on a
        // case-sensitive filesystem, and must not accidentally match a
        // different file on a case-insensitive one. Paths are compared as
        // given; the rule set is the thing to fix, not the comparison.
        var rules = Rules(("*.pem", Classifications.Restricted));

        await Assert.That(ClassificationRules.Classify("KEY.PEM", rules))
            .IsEqualTo(ClassificationRules.Unmatched);
    }

    [Test]
    public async Task The_default_rules_are_cautious_about_the_obvious_things()
    {
        // A tenant that configures nothing still gets something defensible.
        // Not a substitute for their own rules, and the ceiling is what
        // actually decides - but an empty default would make the feature
        // arrive switched off.
        foreach (var secretish in (string[])
                 ["deploy/key.pem", ".env", "config/secrets.yaml", "id_rsa"])
        {
            await Assert.That(Classifications.RankOf(
                ClassificationRules.Classify(secretish, ClassificationRules.Default))!.Value)
                .IsGreaterThan(Classifications.RankOf(Classifications.Internal)!.Value)
                .Because($"'{secretish}' is the kind of path a default rule set exists for.");
        }

        await Assert.That(ClassificationRules.Classify("src/Program.cs", ClassificationRules.Default))
            .IsEqualTo(ClassificationRules.Unmatched);
    }
}
