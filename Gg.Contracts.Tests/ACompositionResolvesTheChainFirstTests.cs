using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// S77.1-05: composition resolves the chain, then composes as it always has.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two steps, so ADR-0014 stays true where it governs.</b> Order-freedom is
/// what stops a caller changing what governs a flight by shuffling a list, and
/// specialization is ordered by nature. So the chain's order comes from the
/// verified parent links, and everything after resolution is the operator
/// table, unchanged.
/// </para>
/// <para>
/// <b>An abstract leaf is never flown.</b> Composition is what a flight is
/// governed by, and an abstract kind's whole point is that nothing is.
/// </para>
/// </remarks>
public class ACompositionResolvesTheChainFirstTests
{
    private static string Rendered(params EnvelopeLayer[] layers) =>
        EnvelopeText.RenderComposed(
            EnvelopeComposition.Compose(layers).Composed
            ?? throw new InvalidOperationException(EnvelopeComposition.Compose(layers).Refused));

    [Test]
    public async Task A_chain_composes_to_root_and_its_resolved_work_kind()
    {
        var resolved = WorkKindFixtures.Resolve(WorkKindFixtures.Child).Effective!;
        var asOne = ChainFixtures.Base() with { Name = "investigate-instructions", Document = resolved };

        await Assert.That(Rendered(ChainFixtures.Floor(), ChainFixtures.Base(), ChainFixtures.Child()))
            .IsEqualTo(Rendered(ChainFixtures.Floor(), asOne))
            .Because("the chain is resolved before anything composes, so what composes is one "
                   + "work kind - the leaf, as it reads over its parents.");
    }

    [Test]
    public async Task Shuffling_the_layers_changes_nothing()
    {
        var forward = Rendered(ChainFixtures.Floor(), ChainFixtures.Base(), ChainFixtures.Child());
        var shuffled = Rendered(ChainFixtures.Child(), ChainFixtures.Floor(), ChainFixtures.Base());

        await Assert.That(shuffled).IsEqualTo(forward)
            .Because("the chain's order is its parent links, not the list a caller handed in.");
    }

    [Test]
    public async Task Root_speaks_first_and_the_leaf_after_its_parents()
    {
        var composed = EnvelopeComposition.Compose(
            [ChainFixtures.Floor(), ChainFixtures.Base(), ChainFixtures.Child()]).Composed!;

        await Assert.That(composed.Instructions.Select(i => i.Text)).IsEquivalentTo(
            new[]
            {
                "Keep your summary short.",
                "Read the work item.",
                "Write the analysis to Custom.ClaudeCodePlan as html.",
            },
            TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(composed.Destinations.Single().Id).IsEqualTo("ado");
    }

    [Test]
    public async Task An_abstract_leaf_is_refused()
    {
        var careful = ChainFixtures.Child(text: WorkKindFixtures.Child.Replace(
            "extends: investigate\n", "extends: investigate\nabstract: true\n"));

        var refused = EnvelopeComposition.Compose(
            [ChainFixtures.Floor(), ChainFixtures.Base(), careful]).Refused;

        await Assert.That(refused).IsNotNull();
        await Assert.That(refused!).Contains("abstract");
    }
}
