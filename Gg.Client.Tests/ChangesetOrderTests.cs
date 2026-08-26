using Gg.Client;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// A changeset is ordered, not atomic — tightenings first.
/// </summary>
/// <remarks>
/// <para>
/// <b>Composition is safe at an instant; a sequence of applies visits instants
/// nobody authored.</b> Land the work kind that grants a move before the
/// narrowing that constrains it, and the estate spends the interval between two
/// gates holding a capability nothing governs — a window whose contents no
/// reviewer ever saw, produced by two changes each of which was reviewed.
/// </para>
/// <para>
/// <b>The fix is order rather than atomicity.</b> Apply tightenings before
/// widenings, so every intermediate state is at or below both endpoints: never
/// looser than where the estate started, never looser than where it is going.
/// ADR-0014 proved order cannot matter for COMPOSING documents; this is where it
/// does matter — applying them — and the safe order falls out of the same
/// operators.
/// </para>
/// <para>
/// <b>And the failure mode is what decides it.</b> A gate rejected mid-changeset
/// leaves the estate STRICTER than intended: flights may be blocked and will say
/// so by name, and nothing is ungoverned. Atomicity across per-name streams would
/// buy a rollback protocol to prevent a failure that is already safe in the only
/// direction that matters.
/// </para>
/// </remarks>
public class ChangesetOrderTests
{
    private static DocumentChange Change(string name, string direction) => new()
    {
        Name = name,
        Path = $"airspace/narrowings/{name}.yaml",
        Direction = direction,
        Field = direction == "widening" ? "context.scope" : null,
    };

    [Test]
    public async Task Tightenings_go_first()
    {
        var ordered = Changeset.InSafeOrder(
        [
            Change("widens-payments", "widening"),
            Change("tightens-pci", "tightening"),
            Change("widens-billing", "widening"),
            Change("tightens-sox", "tightening"),
        ]);

        await Assert.That(ordered.Take(2).Select(c => c.Direction).Distinct())
            .IsEquivalentTo(new[] { "tightening" })
            .Because("every intermediate state has to be at or below both endpoints, and "
                   + "a widening landing first is the window nobody authored.");
        await Assert.That(ordered.Skip(2).Select(c => c.Direction).Distinct())
            .IsEquivalentTo(new[] { "widening" });
    }

    [Test]
    public async Task Retirements_go_last_of_all()
    {
        // A retirement removes every constraint in a document at once, so it is
        // the widest thing in any changeset it appears in. Ordering it with the
        // other widenings would be arbitrary; ordering it last is not.
        var ordered = Changeset.InSafeOrder(
        [
            Change("widens-payments", "widening"),
            Change("retires-pci", "retirement"),
            Change("tightens-sox", "tightening"),
        ]);

        await Assert.That(ordered.Select(c => c.Name)).IsEquivalentTo(new[]
        {
            "tightens-sox", "widens-payments", "retires-pci",
        });
    }

    [Test]
    public async Task Order_within_a_direction_is_stable_and_by_name()
    {
        // Two applies at the same direction cannot make each other unsafe, so
        // the order between them is free - and a free order should be the same
        // one every time, or two people running the same changeset see two
        // different flight sequences and neither can be reviewed against the
        // other.
        var ordered = Changeset.InSafeOrder(
        [
            Change("zebra", "tightening"),
            Change("alpha", "tightening"),
            Change("mike", "tightening"),
        ]);

        await Assert.That(ordered.Select(c => c.Name))
            .IsEquivalentTo(new[] { "alpha", "mike", "zebra" });
    }

    [Test]
    public async Task An_empty_changeset_is_an_empty_changeset()
    {
        await Assert.That(Changeset.InSafeOrder([])).IsEmpty();
    }

    [Test]
    public async Task A_changeset_of_one_direction_is_left_alone_but_for_the_naming()
    {
        // THE POISON TWIN for the sort. An ordering that dropped or duplicated
        // an entry would satisfy every assertion above about relative position
        // while silently changing what gets applied.
        var changes = new[]
        {
            Change("alpha", "widening"), Change("mike", "widening"), Change("zebra", "widening"),
        };

        var ordered = Changeset.InSafeOrder(changes);

        await Assert.That(ordered.Count).IsEqualTo(3)
            .Because("ordering a changeset changes the order and nothing else - a lost "
                   + "entry is a change somebody wrote that never happened.");
        await Assert.That(ordered.Select(c => c.Name))
            .IsEquivalentTo(changes.Select(c => c.Name));
    }

    [Test]
    public async Task A_direction_nobody_declared_is_refused_rather_than_sorted_somewhere()
    {
        // Unknown is not neutral. A direction this build does not know cannot be
        // placed safely, and guessing a position would put an unreviewed change
        // in an interval nobody authored - which is the exact thing the ordering
        // exists to prevent.
        await Assert.That(() => Changeset.InSafeOrder([Change("payments", "sideways")]))
            .Throws<InvalidOperationException>();
    }
}
