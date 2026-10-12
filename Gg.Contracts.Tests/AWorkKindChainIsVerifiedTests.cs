using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// S77.1-04: several work-kind layers compose when they form one chain.
/// </summary>
/// <remarks>
/// <para>
/// <b>One chain, one leaf.</b> The flight is FOR one thing, declared at
/// creation, and that is still true: the leaf is the kind, and the documents
/// above it are what it was written against. Two leaves is two things.
/// </para>
/// <para>
/// <b>Verified from what was handed</b>, as every parent link already is: the
/// top is the one full document under root, each child names a work kind that
/// is present, and every work kind is reached walking down from the top.
/// </para>
/// </remarks>
public class AWorkKindChainIsVerifiedTests
{
    private static string? Refused(params EnvelopeLayer[] layers) =>
        EnvelopeComposition.Compose(layers).Refused;

    [Test]
    public async Task A_base_and_its_child_compose()
    {
        await Assert.That(Refused(ChainFixtures.Floor(), ChainFixtures.Base(), ChainFixtures.Child()))
            .IsNull();
    }

    [Test]
    public async Task A_child_alone_carries_the_extension_shape()
    {
        var both = ChainFixtures.Child() with { Document = WorkKindFixtures.Parsed(WorkKindFixtures.Base) };

        await Assert.That(Refused(ChainFixtures.Floor(), ChainFixtures.Base(), both)).IsNotNull()
            .Because("a work kind is a full document or an extension, never both.");
    }

    [Test]
    public async Task Two_leaves_are_refused()
    {
        var refused = Refused(
            ChainFixtures.Floor(), ChainFixtures.Base(),
            ChainFixtures.Brief("investigate-a", "investigate"),
            ChainFixtures.Brief("investigate-b", "investigate"));

        await Assert.That(refused).IsNotNull();
        await Assert.That(refused!).Contains("investigate-a");
    }

    [Test]
    public async Task A_child_under_root_is_refused()
    {
        await Assert.That(Refused(
                ChainFixtures.Floor(),
                ChainFixtures.Child(parent: Roles.Root, text: "extends: root\nbrief: \"x\"\n")))
            .IsNotNull()
            .Because("an extension extends a work kind; root is the floor, not a kind to change.");
    }

    [Test]
    public async Task A_full_document_under_a_work_kind_is_refused()
    {
        var second = ChainFixtures.Base() with { Name = "investigate-again", Parent = "investigate" };

        await Assert.That(Refused(ChainFixtures.Floor(), ChainFixtures.Base(), second)).IsNotNull()
            .Because("a document under a work kind says only what it changes.");
    }

    [Test]
    public async Task A_cycle_below_the_top_is_refused()
    {
        await Assert.That(Refused(
                ChainFixtures.Floor(), ChainFixtures.Base(),
                ChainFixtures.Brief("investigate-a", "investigate-b"),
                ChainFixtures.Brief("investigate-b", "investigate-a")))
            .IsNotNull();
    }

    [Test]
    public async Task A_chain_deeper_than_three_is_refused()
    {
        var refused = Refused(
            ChainFixtures.Floor(), ChainFixtures.Base(),
            ChainFixtures.Brief("investigate-a", "investigate"),
            ChainFixtures.Brief("investigate-b", "investigate-a"),
            ChainFixtures.Brief("investigate-c", "investigate-b"));

        await Assert.That(refused).IsNotNull();
        await Assert.That(refused!).Contains("investigate-c");
    }

    [Test]
    public async Task A_chain_of_three_composes()
    {
        await Assert.That(Refused(
                ChainFixtures.Floor(), ChainFixtures.Base(),
                ChainFixtures.Brief("investigate-a", "investigate"),
                ChainFixtures.Brief("investigate-b", "investigate-a")))
            .IsNull();
    }
}
