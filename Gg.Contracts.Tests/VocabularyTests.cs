using Gg.Contracts.Authoring;
using System.Reflection;

namespace Gg.Contracts.Tests;

public class VocabularyTests
{
    /// <summary>
    /// Types that DESCRIBE the protocol rather than travel on it.
    /// </summary>
    /// <remarks>
    /// Excluded by namespace rather than by an attribute or a list of names: a
    /// namespace is self-documenting and is not applied by accident, and the
    /// exclusion stays one line no matter how the description grows. Nothing
    /// in here is ever serialized onto the wire, so a pinned id would be a
    /// promise about something that never crosses the boundary.
    /// </remarks>
    private const string DescriptionNamespace = "Gg.Contracts.Description";

    /// <summary>
    /// The parser's own result types, which are never serialized either.
    /// </summary>
    /// <remarks>
    /// <b>Added in slice fifteen, on the reasoning above rather than beside
    /// it.</b> EnvelopeYaml moved into this package so the control plane could
    /// parse a repository's narrowing itself (ADR-0018 § 5), and it brought
    /// records that carry a model, a diagnosis and some notes back to whoever
    /// asked, on the same machine. A pinned id on one would be a promise about
    /// something that never crosses a boundary - word for word why
    /// <see cref="DescriptionNamespace"/> is excluded.
    /// </remarks>
    private const string AuthoringNamespace = "Gg.Contracts.Authoring";

    private static readonly string[] NotOnTheWire = [DescriptionNamespace, AuthoringNamespace];

    private static List<Type> ContractTypes() =>
        typeof(PinnedIdAttribute).Assembly
            .GetExportedTypes()
            .Where(t => !t.IsAssignableTo(typeof(Attribute)))
            .Where(t => !(t.IsAbstract && t.IsSealed)) // exclude static classes (Vocabulary)
            .Where(t => !NotOnTheWire.Contains(t.Namespace, StringComparer.Ordinal))
            .Where(t => !t.IsEnum)
            .ToList();

    [Test]
    public async Task The_namespaces_excluded_from_the_wire_rules_all_exist()
    {
        // A stale exclusion is a hole held open for whatever is written next
        // under that namespace, and it would never fail on its own.
        var present = typeof(PinnedIdAttribute).Assembly
            .GetExportedTypes()
            .Select(t => t.Namespace)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        foreach (var excluded in NotOnTheWire)
        {
            await Assert.That(present).Contains(excluded)
                .Because($"'{excluded}' is excluded from the pinned-id and vocabulary rules "
                       + "and holds nothing, so the exclusion is protecting nothing and "
                       + "hiding whatever lands there next.");
        }
    }

    [Test]
    public async Task EveryContractTypeCarriesAPinnedId()
    {
        var unpinned = ContractTypes()
            .Where(t => t.GetCustomAttribute<PinnedIdAttribute>() is null)
            .Select(t => t.FullName)
            .ToList();

        await Assert.That(unpinned).IsEmpty()
            .Because("every wire type must carry [PinnedId] so renames never change wire identity");
    }

    [Test]
    public async Task EveryContractTypeAppearsInTheVocabulary()
    {
        var unregistered = ContractTypes()
            .Where(t => !Vocabulary.Types.Contains(t))
            .Select(t => t.FullName)
            .ToList();

        await Assert.That(unregistered).IsEmpty()
            .Because("adding a contract type without registering it in Vocabulary must fail the build");
    }

    [Test]
    public async Task TheVocabularyContainsNoStaleEntries()
    {
        var contractTypes = ContractTypes();
        var stale = Vocabulary.Types
            .Where(t => !contractTypes.Contains(t))
            .Select(t => t.FullName)
            .ToList();

        await Assert.That(stale).IsEmpty();
    }

    [Test]
    public async Task PinnedIdsAreUnique()
    {
        var duplicates = ContractTypes()
            .Select(t => t.GetCustomAttribute<PinnedIdAttribute>()?.Id)
            .Where(id => id is not null)
            .GroupBy(id => id)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        await Assert.That(duplicates).IsEmpty();
    }
}
