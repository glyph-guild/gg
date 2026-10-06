using Gg.Client;

namespace Gg.Console.Tests;

/// <summary>
/// <b>S66.3-02</b> - a leg another waits for is not dropped from the panel, and a draft that does
/// not parse is not written.
/// </summary>
/// <remarks>
/// The panel edits through <see cref="ItineraryDrafts"/> and by the tool server's own drop rule
/// (slice sixty-six rule 4), so neither door can leave a draft the other would refuse.
/// </remarks>
public class APanelEditKeepsTheDraftsRulesTests
{
    [Test]
    public async Task A_leg_another_waits_for_is_not_dropped_and_the_panel_says_why()
    {
        using var session = new PlanSessionFixture();
        session.ThreeLegs();

        session.Run(PlanSessionFixture.Prefix, PlanSessionFixture.Key('p'), PlanSessionFixture.Key('x'));

        var legs = ((DraftRead.Held)session.Drafts.Read("console")).Draft.Legs;
        await Assert.That(legs.Count).IsEqualTo(3);
        await Assert.That(session.Frames[^1]).Contains("the walk")
            .Because("the panel names the leg that waits, which is the one to change first.");
        await Assert.That(session.Frames[^1]).Contains("not dropped");
    }

    [Test]
    public async Task A_draft_that_does_not_parse_is_left_as_it_is()
    {
        using var session = new PlanSessionFixture();
        Directory.CreateDirectory(session.Root);
        var mangled = "intent: [unclosed\nlegs:\n  - subject: x\n";
        await File.WriteAllTextAsync(session.Drafts.PathOf("console"), mangled);

        session.Run(PlanSessionFixture.Prefix, PlanSessionFixture.Key('p'), PlanSessionFixture.Key('x'));

        await Assert.That(await File.ReadAllTextAsync(session.Drafts.PathOf("console"))).IsEqualTo(mangled);
        await Assert.That(session.Frames[^1]).Contains("does not read");
    }
}
