using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// S77.1-06: an abstract work kind need not say where its work goes.
/// </summary>
/// <remarks>
/// <para>
/// <b>Implement is the case.</b> Its loop, model and instructions are what is
/// worth tuning once; a pull request and a preview differ only in where the
/// work goes. An abstract base declares no destination and is validated only
/// through the leaves that do.
/// </para>
/// <para>
/// <b>Abstract is not inherited.</b> A child of an abstract base is flyable
/// unless it says otherwise, which is what makes the base useful.
/// </para>
/// </remarks>
public class AnAbstractWorkKindIsNeverFlownTests
{
    private static string Abstract => "abstract: true\n" + WorkKindFixtures.Base[
        ..WorkKindFixtures.Base.IndexOf("destinations:", StringComparison.Ordinal)];

    [Test]
    public async Task An_abstract_kind_may_declare_no_destination()
    {
        var read = EnvelopeYaml.Parse(Abstract);

        await Assert.That(read.Diagnosis).IsNull();
        await Assert.That(read.Envelope!.Abstract).IsTrue();
        await Assert.That(read.Envelope.Destinations).IsEmpty();
        await Assert.That(EnvelopeText.Render(read.Envelope)).StartsWith("abstract: true\n");
    }

    [Test]
    public async Task A_kind_that_is_not_abstract_still_needs_one()
    {
        var read = EnvelopeYaml.Parse(Abstract["abstract: true\n".Length..]);

        await Assert.That(read.Diagnosis).IsNotNull();
    }

    [Test]
    public async Task A_child_that_adds_the_destination_is_a_flyable_leaf()
    {
        var resolved = WorkKindChain.Resolve(
            "implement",
            WorkKindFixtures.Parsed(Abstract),
            [("implement-pr", WorkKindFixtures.Extension("""
                extends: implement
                destinations:
                  none:
                    kind: none
                    requires:
                      - investigate-finished
                """))]);

        await Assert.That(resolved.Refused).IsNull();
        await Assert.That(resolved.Effective!.Abstract).IsFalse()
            .Because("abstract is what a document says about itself, not what it hands down.");
        await Assert.That(resolved.Effective.Destinations.Single().Id).IsEqualTo("none");
    }

    [Test]
    public async Task A_child_may_itself_be_abstract()
    {
        var resolved = WorkKindChain.Resolve(
            "implement",
            WorkKindFixtures.Parsed(Abstract),
            [("implement-careful", WorkKindFixtures.Extension("""
                extends: implement
                abstract: true
                brief: "Implement it carefully."
                """))]);

        await Assert.That(resolved.Refused).IsNull();
        await Assert.That(resolved.Effective!.Abstract).IsTrue();
    }
}
