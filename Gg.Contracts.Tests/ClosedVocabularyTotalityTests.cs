using Gg.Contracts;
using System.Reflection;

namespace Gg.Contracts.Tests;

/// <summary>
/// Every value a closed vocabulary declares is in one of its own lists.
/// </summary>
/// <remarks>
/// <para>
/// <b>The general form of the bug <see cref="ReasonFamilyTotalityTests"/> was
/// written for.</b> <c>stale-working-copy</c> shipped in 0.65.0 as a
/// <c>const</c> with a sentence, and with no entry in <c>ReasonKinds.All</c>.
/// Nothing noticed: the closed-vocabulary fingerprint reads the LIST, so a value
/// that never reaches the list is invisible to the guard whose entire job is
/// noticing new values, and it surfaced as a 500 in the control plane.
/// </para>
/// <para>
/// <b>That guard was written for reason kinds specifically, and the defect is
/// not specific to them.</b> Any vocabulary here can grow a constant that never
/// reaches its list, and every one of them would fail the same silent way. So
/// this walks every closed vocabulary the fingerprint mechanism already
/// discovers, rather than the one that happened to bite.
/// </para>
/// <para>
/// <b>Exemptions are data with a written reason</b>, the merge-operator shape,
/// so a constant that legitimately is not a vocabulary value says why rather
/// than being silently tolerated by a narrower rule.
/// </para>
/// </remarks>
public class ClosedVocabularyTotalityTests
{
    /// <summary>Constants inside a vocabulary type that are not values of it.</summary>
    private static readonly IReadOnlyDictionary<string, string> Exemptions =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["AttachmentConditions.MovesUsedPrefix"] =
                "a prefix rather than a value - a real condition is this plus a move name, "
              + "so the constant alone is never a member of the vocabulary",
            ["AttachmentConditions.TouchesPrefix"] =
                "a prefix rather than a value - a real condition is this plus a glob, so the "
              + "constant alone is never a member of the vocabulary",
            ["ProtocolSurface.SessionHeader"] =
                "a header name. ProtocolSurface is discovered by shape because it carries "
              + "GovernedPrefixes, and a header is not a member of that vocabulary",
            ["ProtocolSurface.RunnerHeader"] =
                "a header name, for the same reason as SessionHeader",
            ["ProtocolSurface.SupportedProtocolsHeader"] =
                "a header name, for the same reason as SessionHeader",
        };

    private static IReadOnlyList<string> ValuesOf(Type type) =>
        [.. type.GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(p => p.PropertyType == typeof(IReadOnlyList<string>))
            .SelectMany(p => p.GetValue(null) as IReadOnlyList<string> ?? [])];

    private static IReadOnlyList<FieldInfo> ConstantsOf(Type type) =>
        [.. type.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f is { IsLiteral: true, IsInitOnly: false })
            .Where(f => f.FieldType == typeof(string))
            .OrderBy(f => f.Name, StringComparer.Ordinal)];

    [Test]
    public async Task Every_declared_value_reaches_a_list_of_its_own_vocabulary()
    {
        var orphans = new List<string>();

        foreach (var type in ClosedVocabularies.Discovered())
        {
            var values = ValuesOf(type);

            foreach (var constant in ConstantsOf(type))
            {
                var name = $"{type.Name}.{constant.Name}";

                if (Exemptions.ContainsKey(name))
                {
                    continue;
                }

                if (constant.GetValue(null) is string value
                    && !values.Contains(value, StringComparer.Ordinal))
                {
                    orphans.Add($"{name} = '{value}'");
                }
            }
        }

        await Assert.That(orphans).IsEmpty()
            .Because("a value declared but never listed is invisible to the fingerprint that "
                   + "exists to notice new values, which is how stale-working-copy reached "
                   + "production and threw. Found: " + string.Join(", ", orphans));
    }

    [Test]
    public async Task An_exemption_says_why()
    {
        foreach (var (member, reason) in Exemptions)
        {
            await Assert.That(reason.Length).IsGreaterThan(10)
                .Because($"{member} is exempt for '{reason}', which is not a reason somebody "
                       + "can disagree with later");
        }
    }

    [Test]
    public async Task The_walk_finds_the_vocabularies_it_is_walking()
    {
        // LIVENESS. The assertion above is an absence, and an absence is
        // satisfied by a walk that stopped looking. Naming two vocabularies
        // from opposite fingerprints proves discovery still reaches both.
        var discovered = ClosedVocabularies.Discovered().Select(t => t.Name).ToList();

        await Assert.That(discovered).Contains(nameof(ReasonKinds));
        await Assert.That(discovered).Contains(nameof(FactKinds));
    }

    [Test]
    public async Task A_vocabulary_with_an_unlisted_value_is_caught()
    {
        // THE POISON TWIN, planted rather than hoped for: the check above is
        // run against a type that IS wrong, so "no orphans" cannot be what a
        // broken reflection walk returns.
        var values = ValuesOf(typeof(PlantedVocabulary));
        var orphans = ConstantsOf(typeof(PlantedVocabulary))
            .Where(f => f.GetValue(null) is string v && !values.Contains(v, StringComparer.Ordinal))
            .Select(f => f.Name)
            .ToList();

        await Assert.That(orphans).IsEquivalentTo((string[])[nameof(PlantedVocabulary.Forgotten)])
            .Because("the walk names the constant somebody has to add to the list.");
    }

    /// <summary>A vocabulary that forgot one, so the walk can be seen catching it.</summary>
    private static class PlantedVocabulary
    {
        public const string Remembered = "remembered";

        public const string Forgotten = "forgotten";

        public static IReadOnlyList<string> All { get; } = [Remembered];
    }
}
