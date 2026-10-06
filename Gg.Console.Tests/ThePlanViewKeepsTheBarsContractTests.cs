namespace Gg.Console.Tests;

/// <summary>
/// <b>S66.3-04</b> - the bar's new letters (<c>p</c>, <c>x</c>, <c>J</c>, <c>K</c>) are taken only
/// while it is open, the prefix stays the only key charged to the child, and escape still closes.
/// </summary>
public class ThePlanViewKeepsTheBarsContractTests
{
    [Test]
    public async Task Closed_the_new_letters_go_to_the_child()
    {
        var closed = new HostedPanel(HostedView.Closed, 0);

        foreach (var key in (char[])['p', 'x', 'J', 'K'])
        {
            await Assert.That(HostedBar.Takes(closed, HostedGesture.Typed, PlanSessionFixture.Key(key))).IsFalse()
                .Because($"'{key}' typed to the agent is the agent's while the panel is shut.");
        }
    }

    [Test]
    public async Task The_edits_are_asked_only_in_the_plan_view()
    {
        var plan = new HostedPanel(HostedView.Plan, 0);
        var envelope = new HostedPanel(HostedView.Envelope, 0);

        await Assert.That(HostedBar.Edit(plan, HostedGesture.Typed, PlanSessionFixture.Key('x'))).IsEqualTo(PlanEdit.Drop);
        await Assert.That(HostedBar.Edit(plan, HostedGesture.Typed, PlanSessionFixture.Key('J'))).IsEqualTo(PlanEdit.MoveDown);
        await Assert.That(HostedBar.Edit(plan, HostedGesture.Typed, PlanSessionFixture.Key('K'))).IsEqualTo(PlanEdit.MoveUp);
        await Assert.That(HostedBar.Edit(envelope, HostedGesture.Typed, PlanSessionFixture.Key('x'))).IsEqualTo(PlanEdit.None)
            .Because("a drop key in the envelope view would delete a leg nobody could see.");
    }

    [Test]
    public async Task P_opens_the_plan_and_escape_still_closes()
    {
        var open = HostedBar.Next(new HostedPanel(HostedView.Envelope, 0), HostedGesture.Typed, PlanSessionFixture.Key('p'));
        await Assert.That(open.Showing).IsEqualTo(HostedView.Plan);

        var shut = HostedBar.Next(open, HostedGesture.Typed, PlanSessionFixture.Escape);
        await Assert.That(shut.Showing).IsEqualTo(HostedView.Closed);
    }

    [Test]
    public async Task In_the_plan_view_j_and_k_choose_a_leg()
    {
        var plan = new HostedPanel(HostedView.Plan, 0);

        var down = HostedBar.Next(plan, HostedGesture.Typed, PlanSessionFixture.Key('j'));
        await Assert.That(down.Leg).IsEqualTo(1);
        await Assert.That(HostedBar.Next(down, HostedGesture.Typed, PlanSessionFixture.Key('k')).Leg).IsEqualTo(0);
    }
}
