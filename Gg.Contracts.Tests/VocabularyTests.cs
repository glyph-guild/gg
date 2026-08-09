using System.Reflection;

namespace Gg.Contracts.Tests;

public class VocabularyTests
{
    private static List<Type> ContractTypes() =>
        typeof(PinnedIdAttribute).Assembly
            .GetExportedTypes()
            .Where(t => !t.IsAssignableTo(typeof(Attribute)))
            .Where(t => !(t.IsAbstract && t.IsSealed)) // exclude static classes (Vocabulary)
            .ToList();

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
