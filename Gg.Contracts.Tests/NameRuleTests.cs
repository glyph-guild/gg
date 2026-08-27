using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// What a named Airspace document may be called, now that a name is also a path.
/// </summary>
/// <remarks>
/// <para>
/// <b>The vocabulary was open to the point of hostility, and it was measured.</b>
/// Twelve candidate names were declared against a live control plane before this
/// rule existed; eleven were accepted, <c>../../etc/passwd</c> among them, and only
/// <c>@</c> was refused - because <c>@</c> is the qualified-version separator and
/// nothing else had ever needed a name to be anything in particular.
/// </para>
/// <para>
/// <b>ADR-0016 makes a name a file path, so the open vocabulary has to close a
/// little.</b> The three ways out were an escaping scheme, a manifest mapping paths
/// to names, and this one. The manifest is refused for the reason slice ten refused
/// a permission model - it would be a second source of truth about identity, free to
/// drift from the one the streams actually use. Escaping keeps the vocabulary open
/// and makes every path unreadable, and still cannot answer a case collision,
/// because the filesystem folds whatever it is handed.
/// </para>
/// <para>
/// <b>So the name is restricted and the mapping is the identity function.</b> There
/// is nothing to drift because there is nothing to map. ADR-0014 opened this
/// vocabulary deliberately - architects create names, so they cannot be an enum -
/// and this narrows it deliberately in turn: an architect may still coin any name
/// they like, out of the characters a path can hold.
/// </para>
/// <para>
/// <b>The refusal belongs where the name is created.</b> Refusing at pull would let
/// the estate reach a state the tool cannot render, and hand the repair to somebody
/// who did not cause it - slice eight's authoring-time principle, one vocabulary
/// over.
/// </para>
/// </remarks>
public class NameRuleTests
{
    [Test]
    [Arguments("payments")]
    [Arguments("team-payments")]
    [Arguments("pci")]
    [Arguments("migrate-data")]
    [Arguments("a")]
    [Arguments("a1")]
    [Arguments("9-lives")]
    public async Task A_name_a_path_can_hold_is_admitted(string name)
    {
        await Assert.That(AirspaceNames.Invalid(name)).IsNull();
    }

    [Test]
    [Arguments("payments/eu", "/")]
    [Arguments(".hidden", ".")]
    [Arguments("-force", "-")]
    [Arguments("pay ments", "' '")]
    [Arguments("pay\tments", "'\\t'")]
    [Arguments("payments.", ".")]
    [Arguments("payments-", "-")]
    [Arguments("Payments", "P")]
    [Arguments("pay:ments", ":")]
    [Arguments("pay\\ments", "\\")]
    public async Task A_name_a_path_cannot_hold_is_refused_naming_the_character(
        string name, string character)
    {
        var refusal = AirspaceNames.Invalid(name);

        await Assert.That(refusal).IsNotNull()
            .Because($"'{name}' cannot be a file path and has to be refused before it is one");
        await Assert.That(refusal!).Contains(character)
            .Because("a refusal that does not name the character leaves the author guessing "
                   + "which of them was the problem");
    }

    [Test]
    public async Task The_traversal_payload_the_live_probe_accepted_is_refused()
    {
        // Step 0 declared this against a running control plane and got a 202.
        // It is here as a named regression rather than as one more character
        // class, because a document name that is also a relative path out of
        // the tree is the reason this rule stopped being cosmetic.
        await Assert.That(AirspaceNames.Invalid("../../etc/passwd")).IsNotNull();
    }

    [Test]
    public async Task A_name_longer_than_a_path_component_is_refused_naming_the_length()
    {
        var refusal = AirspaceNames.Invalid(new string('a', 65));

        await Assert.That(refusal).IsNotNull();
        await Assert.That(refusal!).Contains("64")
            .Because("the bound is the refusal's only useful content - an author who "
                   + "cannot see the limit cannot get under it");
    }

    [Test]
    public async Task The_separator_refusal_survives_the_new_rule()
    {
        // The one rule that existed before this slice. It is subsumed by the
        // charset - '@' is not in it - but the REASON is not, and the reason is
        // what a person needs: a name carrying '@' makes payments@v4 unparseable.
        var refusal = AirspaceNames.Invalid("pay@ments");

        await Assert.That(refusal).IsNotNull();
        await Assert.That(refusal!).Contains("@");
    }

    [Test]
    public async Task A_blank_is_refused_as_a_blank_rather_than_as_a_character()
    {
        var refusal = AirspaceNames.Invalid("");

        await Assert.That(refusal).IsNotNull();
        await Assert.That(refusal!.ToLowerInvariant()).Contains("blank")
            .Because("'' contains no character to name, so the refusal has to say what "
                   + "is actually wrong with it");
    }

    [Test]
    public async Task Case_is_refused_at_the_name_because_the_filesystem_will_not_refuse_it()
    {
        // Payments and payments are two streams, two version counters and two
        // topology rows - and one file on the filesystem this team develops on.
        // Nothing downstream can recover: every comparison in the system is
        // ordinal and the stream id is a hash over the name's bytes.
        await Assert.That(AirspaceNames.Invalid("Payments")).IsNotNull();
        await Assert.That(AirspaceNames.Invalid("payments")).IsNull();
    }

    [Test]
    public async Task Nothing_else_in_the_contract_decides_whether_a_name_is_legal()
    {
        // The poison twin for rule 2. A second implementation of this rule is
        // the manifest hazard wearing a different coat - two computations that
        // agree today and drift the first time one of them is fixed. The
        // control plane consumes this method; it does not carry a copy.
        var deciders = typeof(AirspaceNames).Assembly.GetTypes()
            .SelectMany(t => t.GetMethods(System.Reflection.BindingFlags.Public
                                        | System.Reflection.BindingFlags.Static
                                        | System.Reflection.BindingFlags.DeclaredOnly))
            .Where(m => m.Name is "Invalid" or "IsValidName" or "ValidateName")
            .Where(m => m.GetParameters().Length == 1
                     && m.GetParameters()[0].ParameterType == typeof(string))
            .Select(m => $"{m.DeclaringType!.Name}.{m.Name}")
            .Where(n => !NotAboutNames.ContainsKey(n))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        await Assert.That(deciders).IsEquivalentTo(new[] { "AirspaceNames.Invalid" })
            .Because("one computation decides what a name may be, or the path mapping "
                   + "becomes the second source of identity ADR-0016 refuses");
    }

    /// <summary>
    /// Refusals this scan sees and is not about, each with its reason.
    /// </summary>
    /// <remarks>
    /// <b>Exempted by scope rather than by spelling.</b> The scan finds any
    /// public static <c>Invalid(string)</c> in the contract, which is what makes
    /// it worth having - a second NAME rule is exactly the drift it exists to
    /// catch, and one added under a different method name would slip past a
    /// narrower pattern. But a computation about something that is not a name
    /// still matches, and the honest fix is to say which and why rather than to
    /// rename it out of the way.
    /// </remarks>
    private static readonly Dictionary<string, string> NotAboutNames = new(StringComparer.Ordinal)
    {
        [$"{nameof(RepositoryNarrowings)}.{nameof(RepositoryNarrowings.Invalid)}"] =
            "decides what a PATH may be, not what a name may be: a directory with separators "
          + "inside somebody else's repository. AirspaceNames governs one path COMPONENT in a "
          + "working copy and would refuse '.goodgrief/narrowings/' on its first character, so "
          + "sharing the computation is the mistake rather than the fix. Slice thirteen's rule, "
          + "which this file already carries: one computation per KIND of name.",
    };

    [Test]
    public async Task Every_exemption_from_the_single_decider_scan_says_why()
    {
        foreach (var (decider, reason) in NotAboutNames)
        {
            await Assert.That(reason.Length).IsGreaterThan(60)
                .Because($"'{decider}' is exempt from the one rule that stops a second name "
                       + "computation existing, and a one-word reason is how the next one gets "
                       + "waved through.");
        }
    }

    [Test]
    public async Task An_exemption_that_no_longer_names_anything_is_an_error()
    {
        // The staleness half. An exemption for a method somebody deleted is a
        // hole standing open for whatever is written next under that name -
        // StrategyRoundTripTests' ratchet carries the same check for covered
        // members, and this file did not.
        var present = typeof(AirspaceNames).Assembly.GetTypes()
            .SelectMany(t => t.GetMethods(System.Reflection.BindingFlags.Public
                                        | System.Reflection.BindingFlags.Static
                                        | System.Reflection.BindingFlags.DeclaredOnly))
            .Select(m => $"{m.DeclaringType!.Name}.{m.Name}")
            .ToHashSet(StringComparer.Ordinal);

        var stale = NotAboutNames.Keys.Where(k => !present.Contains(k)).ToList();

        await Assert.That(stale).IsEmpty()
            .Because("an exemption naming nothing is a hole held open for whatever is written "
                   + "next under that name. Found: " + string.Join(", ", stale));
    }
}
