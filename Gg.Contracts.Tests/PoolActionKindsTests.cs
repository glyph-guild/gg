using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// Every pool action bears a kind, an unclassified action poisons, and the
/// outward set is exactly refresh and reset.
/// </summary>
/// <remarks>
/// <para>
/// <b>MoveKinds' shape, second instance — and the two vocabularies'
/// independence is itself asserted here.</b> A pool action is not a loop
/// move: the maintain loop grants no moves and runs no agent, and a loop
/// move nobody could ever grant would be a trap member. MoveKinds' enforced
/// set stays correctly empty; slice eleven's pre-booking ("the first outward
/// move arrives with maintain-environment") half-arrives — the work kind
/// arrives, the loop move does not.
/// </para>
/// <para>
/// <b>The enforcement consumer is the control-plane decider:</b> an
/// outward-act action is decided only toward a pool whose latest attestation
/// carries a current scope probe — unknown is not false, slice eleven's
/// shape applied to infrastructure.
/// </para>
/// </remarks>
public class PoolActionKindsTests
{
    [Test]
    public async Task Every_declared_action_is_in_its_own_All()
    {
        // The DestinationKinds hole, refused by construction: a declared
        // value outside its own membership list is refused by the check that
        // exists to admit it.
        await Assert.That(PoolActions.All)
            .IsEquivalentTo((string[])[PoolActions.Verify, PoolActions.Refresh, PoolActions.Reset]);
    }

    [Test]
    public async Task The_kind_table_is_total_over_the_actions_both_directions()
    {
        await Assert.That(PoolActionKinds.Table.Keys.Order(StringComparer.Ordinal).ToList())
            .IsEquivalentTo(PoolActions.All.Order(StringComparer.Ordinal).ToList())
            .Because("an action with no kind would default to something, and any default "
                   + "grants what nothing can recall.");
    }

    [Test]
    public async Task An_unclassified_action_poisons_naming_it()
    {
        // power-on and power-off stay MoveKinds' prophecy, for the strategy
        // row that needs them - they are not pool actions until somebody
        // classifies them, and this is where the classification is forced.
        var poisoned = Assert.Throws<InvalidOperationException>(
            () => PoolActionKinds.Of("power-on"));

        await Assert.That(poisoned!.Message).Contains("power-on");
    }

    [Test]
    public async Task The_outward_set_is_exactly_refresh_and_reset()
    {
        var outward = PoolActionKinds.Table
            .Where(entry => entry.Value == PoolActionKinds.OutwardAct)
            .Select(entry => entry.Key)
            .Order(StringComparer.Ordinal)
            .ToList();

        await Assert.That(outward).IsEquivalentTo((string[])["refresh", "reset"])
            .Because("refresh and reset change a container on a customer's host; verify "
                   + "only looks. Article VI is the axis.");
        await Assert.That(PoolActionKinds.Of(PoolActions.Verify))
            .IsEqualTo(PoolActionKinds.RecordOnly);
    }

    [Test]
    public async Task MoveKinds_anchor_stays_correctly_empty_and_untouched()
    {
        await Assert.That(MoveKinds.Table.Values.All(v => v == MoveKinds.RecordOnly)).IsTrue()
            .Because("the pool actions are their own vocabulary - if adding them moved "
                   + "MoveKinds, the two would be one list wearing two names, and the "
                   + "authoring refusal would start refusing envelopes over maintenance.");
    }
}
