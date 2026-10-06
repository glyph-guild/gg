namespace Gg.Console.Tests;

/// <summary>
/// <b>S66.4-01</b> - when the session ends, the console names the proposed <c>ITN-n</c> and its
/// waiting gate, or says the draft was kept and where.
/// </summary>
/// <remarks>
/// Nothing is opened, answered or sent by the console (rule 7): it reads what the server left.
/// </remarks>
public class APlanSessionSaysWhatItLeftTests
{
    [Test]
    public async Task A_proposed_plan_is_named_with_its_gate()
    {
        using var session = new PlanSessionFixture();
        session.ThreeLegs(result:
            "proposed ITN-7 - nothing opens until its gate is answered\n  pass 0199\n"
          + "  waits on plan-reviewed, answered by platform-owner\n\nDraft 'console'");

        var said = session.Run();

        await Assert.That(said).Contains("ITN-7");
        await Assert.That(said).Contains("plan-reviewed");
    }

    [Test]
    public async Task A_draft_not_yet_proposed_is_said_to_be_kept()
    {
        using var session = new PlanSessionFixture();
        session.ThreeLegs();

        var said = session.Run();

        await Assert.That(said).Contains("kept");
        await Assert.That(said).Contains(session.Drafts.PathOf("console"));
    }
}
