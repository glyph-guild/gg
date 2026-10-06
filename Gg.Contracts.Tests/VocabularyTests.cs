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
            .Where(t => !t.IsInterface) // see TheOnlyInterfacesHereAreSeamsNotShapes
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

    /// <summary>
    /// An interface here is a SEAM — behaviour somebody implements — and a seam
    /// never travels, so the hole that exclusion opens is closed here rather than
    /// left for whoever adds the second one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why interfaces are excluded above.</b> <c>IAgreeAsAHolder</c> arrived in
    /// slice sixty-four as the shape a rewrap asks a holder for: a public half, and
    /// an agreement it performs. It declares two members and carries no data, so a
    /// pinned id on it would be <i>"a promise about something that never crosses
    /// the boundary"</i> — word for word the reason the two excluded namespaces
    /// give — and registering it in <c>Vocabulary</c> would put a thing with no
    /// serializable shape into the list of things that have one.
    /// </para>
    /// <para>
    /// <b>And the hole it would otherwise open.</b> If a wire type declared a
    /// MEMBER typed as one of these interfaces, that member's concrete type would
    /// be chosen at runtime, serialized, and never scanned by anything above —
    /// which is exactly the kind of silence these three tests exist to prevent. So
    /// this asserts the distinction holds rather than trusting the word "seam": an
    /// interface may be implemented, and may be passed to a method, and may not be
    /// the type of anything a wire record carries.
    /// </para>
    /// <para>
    /// <b>It looks through <c>IReadOnlyList&lt;T&gt;</c></b>, because a list of
    /// them would be the same hole with one more layer of wrapping, and the
    /// framework's own collection interfaces are not what this is about.
    /// </para>
    /// </remarks>
    [Test]
    public async Task TheOnlyInterfacesHereAreSeamsNotShapes()
    {
        var seams = typeof(PinnedIdAttribute).Assembly
            .GetExportedTypes()
            .Where(t => t.IsInterface)
            .ToHashSet();

        static IEnumerable<Type> Beneath(Type type) =>
            type.IsConstructedGenericType ? [type, .. type.GetGenericArguments()] : [type];

        var carried = Vocabulary.Types
            .SelectMany(wire => wire
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .SelectMany(member => Beneath(member.PropertyType)
                    .Where(seams.Contains)
                    .Select(found => $"{wire.Name}.{member.Name} carries {found.Name}")))
            .ToList();

        await Assert.That(carried).IsEmpty()
            .Because("an interface declared here is behaviour, and it is excluded from the "
                   + "pinned-id and vocabulary rules on the grounds that it never travels. A wire "
                   + "type carrying one would make that false, and nothing else would notice.");
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
