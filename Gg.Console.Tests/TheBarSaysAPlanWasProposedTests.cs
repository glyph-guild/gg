using Gg.Client;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// The plan session's bar, its plan panel and the sentence it ends with say what the draft was
/// proposed as, from the record beside it (slice sixty-eight, S68.2-01).
/// </summary>
/// <remarks>
/// <b>The owner's words, 2026-10-07: "the bar needs to be clear when itinerary plans have been
/// submitted".</b> It never said so: ITN-61 was proposed and the bar read as it had before, the
/// agent carried on in the same draft, and ITN-62 proposed its legs a second time.
/// </remarks>
public class TheBarSaysAPlanWasProposedTests
{
    private static void Proposed(PlanSessionFixture session) =>
        session.Drafts.KeepProposed("console", new ProposedPlan(
            "ITN-61", "0199a3b1-0000-7000-8000-000000000001", ["plan-reviewed"],
            session.Drafts.DigestOf("console"), DateTimeOffset.UnixEpoch));

    [Test]
    public async Task The_bar_says_proposed_as()
    {
        using var session = new PlanSessionFixture();
        session.ThreeLegs();
        Proposed(session);

        session.Run(PlanSessionFixture.Key('a'));

        await Assert.That(session.Frames[^1]).Contains("proposed as ITN-61 · waiting on plan-reviewed");
    }

    [Test]
    public async Task An_edit_since_says_proposing_again_replaces_it()
    {
        using var session = new PlanSessionFixture();
        session.ThreeLegs();
        Proposed(session);
        _ = session.Drafts.Change("console", d => d with
        {
            Legs = [.. d.Legs, new FlightNomination { Subject = "the docs", WorkKind = "implement", Reason = "later" }],
        });

        session.Run(PlanSessionFixture.Key('a'));

        await Assert.That(session.Frames[^1]).Contains("changed since ITN-61 was proposed · proposing again replaces it");
    }

    [Test]
    public async Task The_panel_opens_with_it()
    {
        using var session = new PlanSessionFixture();
        session.ThreeLegs();
        Proposed(session);

        session.Run(PlanSessionFixture.Prefix);

        await Assert.That(session.Frames[^1]).Contains("proposed as ITN-61");
    }

    [Test]
    public async Task The_session_ends_saying_what_was_proposed()
    {
        using var session = new PlanSessionFixture();
        session.ThreeLegs(result: "Draft 'console', kept at somewhere\nLegs (3):");
        Proposed(session);

        var left = session.Run();

        await Assert.That(left).Contains("proposed as ITN-61")
            .Because("the last result moved on to a later tool call, and the record is what remembers.");
    }

    [Test]
    public async Task A_draft_never_proposed_says_nothing_of_it()
    {
        using var session = new PlanSessionFixture();
        session.ThreeLegs();

        session.Run(PlanSessionFixture.Key('a'));

        await Assert.That(session.Frames[^1]).DoesNotContain("proposed as");
    }
}
