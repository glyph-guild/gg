using Gg.Client;

namespace Gg.Console.Tests;

/// <summary>
/// <b>S66.3-01</b> - moving or dropping a leg from the panel writes the draft, and the tool
/// server's next result shows it.
/// </summary>
/// <remarks>
/// <b>The criterion slice sixty-six exists for</b>: a person and an agent edit one draft. The
/// server reads the file fresh on every call (slice sixty-three rule 3), so a write here is in its
/// next result with nothing passed between the two.
/// </remarks>
public class APanelEditIsTheAgentsToSeeTests
{
    private static IReadOnlyList<string?> Subjects(PlanSessionFixture session) =>
        [.. ((DraftRead.Held)session.Drafts.Read("console")).Draft.Legs.Select(l => l.Subject)];

    [Test]
    public async Task Dropping_the_selected_leg_writes_the_draft()
    {
        using var session = new PlanSessionFixture();
        session.ThreeLegs();

        // OPEN, THE PLAN, DOWN TO THE SECOND LEG, DROP IT.
        session.Run(PlanSessionFixture.Prefix, PlanSessionFixture.Key('p'),
            PlanSessionFixture.Key('j'), PlanSessionFixture.Key('x'));

        await Assert.That(Subjects(session)).IsEquivalentTo((string?[])["the icon", "the walk"]);
    }

    [Test]
    public async Task Moving_a_leg_down_writes_the_new_order()
    {
        using var session = new PlanSessionFixture();
        session.ThreeLegs();

        session.Run(PlanSessionFixture.Prefix, PlanSessionFixture.Key('p'), PlanSessionFixture.Key('J'));

        await Assert.That(Subjects(session).ToList())
            .IsEquivalentTo((string?[])["the padding", "the icon", "the walk"]);
        await Assert.That(Subjects(session)[0]).IsEqualTo("the padding")
            .Because("order is the edit, so it is asserted in order.");
    }
}
