using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// A work kind names the families whose earlier findings its flights are handed (S79.2-01,
/// ADR-0041 Decision 9).
/// </summary>
/// <remarks>
/// <b>Work-kind only, like <c>produces:</c></b>: which earlier agents' words reach a flight
/// is a property of the kind of work. And <b>adding a family widens</b>, as learned advice
/// does: it is text from a machine reaching every later flight of the kind.
/// </remarks>
public class AKindDeclaresWhatItConsultsTests
{
    private const string Consulting = "consults:\n  - investigate\n";

    [Test]
    public async Task A_work_kind_reads_and_writes_back_what_it_consults()
    {
        var read = WorkKindFixtures.Parsed(WorkKindFixtures.Base + "\n" + Consulting);

        await Assert.That(read.Consults).IsEquivalentTo(new[] { "investigate" });

        var written = EnvelopeText.Render(read);
        await Assert.That(written).Contains(Consulting);
        await Assert.That(WorkKindFixtures.Parsed(written).Consults).IsEquivalentTo(new[] { "investigate" });

        await Assert.That(WorkKindFixtures.Parsed(WorkKindFixtures.Base).Consults).IsNull()
            .Because("a kind that says nothing consults only its own family, and a pull must not "
                   + "write a line nobody authored.");
    }

    [Test]
    public async Task A_child_inherits_what_its_parent_consults_and_replaces_it_by_naming_its_own()
    {
        var parent = WorkKindFixtures.Base + "\n" + Consulting;

        var silent = WorkKindChain.Resolve(
            "investigate", WorkKindFixtures.Parsed(parent),
            [("investigate-instructions", WorkKindFixtures.Extension(WorkKindFixtures.Child))]);
        await Assert.That(silent.Effective!.Consults).IsEquivalentTo(new[] { "investigate" });

        var naming = WorkKindChain.Resolve(
            "investigate", WorkKindFixtures.Parsed(parent),
            [("investigate-instructions",
              WorkKindFixtures.Extension(WorkKindFixtures.Child + "\nconsults:\n  - review\n"))]);
        await Assert.That(naming.Effective!.Consults).IsEquivalentTo(new[] { "review" })
            .Because("a list replaces whole down a chain, as produces: does.");

        var child = WorkKindFixtures.Extension(WorkKindFixtures.Child + "\nconsults:\n  - review\n");
        await Assert.That(EnvelopeText.Render(child)).Contains("consults:\n  - review\n");
    }

    [Test]
    public async Task Only_a_work_kind_consults_and_only_work_kind_names()
    {
        var valid = WorkKindFixtures.Parsed(WorkKindFixtures.Base + "\n" + Consulting);
        await Assert.That(Envelope.Validate(valid, Roles.WorkKind)).IsNull();

        var root = valid with { Accepts = null, Produces = null };
        await Assert.That(Envelope.Validate(root, Roles.Root)).Contains("consults")
            .Because("the floor governs every kind at once, so it has no family to consult.");

        var badName = valid with { Consults = ["Not A Name"] };
        await Assert.That(Envelope.Validate(badName, Roles.WorkKind)).Contains("Not A Name");
    }

    [Test]
    public async Task Consulting_one_more_family_widens_and_consulting_fewer_does_not()
    {
        var none = WorkKindFixtures.Parsed(WorkKindFixtures.Base);
        var one = none with { Consults = ["investigate"] };

        await Assert.That(EnvelopeDirection.Widening(none, one)).IsNotNull()
            .Because("another family's findings are a machine's words reaching every later flight "
                   + "of this kind, as learned advice is.");
        await Assert.That(EnvelopeDirection.Widening(one, none)).IsNull();
    }
}
