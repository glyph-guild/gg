namespace Gg.Console.Tests;

/// <summary>
/// <b>S66.3-03</b> - after a panel edit the verdicts are marked as from before it, until the next
/// result.
/// </summary>
/// <remarks>
/// The verdicts are the agent's last result, and a panel edit happened after it (rule 5). Shown
/// unmarked, they would describe a plan that no longer exists as if it did.
/// </remarks>
public class StaleVerdictsAreMarkedTests
{
    [Test]
    public async Task An_edit_marks_the_verdicts_until_the_server_says_something_new()
    {
        using var session = new PlanSessionFixture();
        session.ThreeLegs();

        session.Run(PlanSessionFixture.Prefix, PlanSessionFixture.Key('p'),
            PlanSessionFixture.Key('j'), PlanSessionFixture.Key('x'));

        await Assert.That(session.Frames[^1]).Contains("from before your edit");
    }

    [Test]
    public async Task Without_an_edit_nothing_is_marked()
    {
        using var session = new PlanSessionFixture();
        session.ThreeLegs();

        session.Run(PlanSessionFixture.Prefix, PlanSessionFixture.Key('p'));

        await Assert.That(session.Frames[^1]).DoesNotContain("from before your edit");
    }
}
