using Gg.Client;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// A new plan session starts on an empty draft of its own; the drafts before it are kept as they
/// are (owner, 2026-10-07: "the last plan is always stuck whenever I start a new itinerary
/// session").
/// </summary>
/// <remarks>
/// <b>Found on the owner's machine.</b> Every session drafted <c>console</c>, so ITN-63's legs were
/// still in front of each new agent - which the opening prompt then told to carry on from them.
/// Nothing recorded that draft as proposed (it went up before proposals were recorded), so "skip a
/// proposed draft" would not have caught it: a new session takes a name nothing has used.
/// </remarks>
public class APlanSessionStartsOnAFreshDraftTests
{
    private static ItineraryDrafts DraftsIn(out string root)
    {
        root = Directory.CreateTempSubdirectory("gg-fresh-draft-").FullName;
        return new ItineraryDrafts(root);
    }

    private static void Written(ItineraryDrafts drafts, string name) =>
        _ = drafts.Change(name, d => d with
        {
            Intent = FlightIntent.Of("an older plan"),
            Legs = [new FlightNomination { Subject = "a leg", WorkKind = "implement", Reason = "kept" }],
        });

    [Test]
    public async Task With_no_drafts_it_is_console()
    {
        var drafts = DraftsIn(out var root);
        try
        {
            await Assert.That(PtyPlanSession.FreshDraft(drafts, [])).IsEqualTo("console");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task A_draft_already_written_is_not_reopened()
    {
        var drafts = DraftsIn(out var root);
        try
        {
            Written(drafts, "console");

            var fresh = PtyPlanSession.FreshDraft(drafts, []);

            await Assert.That(fresh).IsEqualTo("console-2");
            await Assert.That(drafts.Read(fresh)).IsEqualTo(new DraftRead.Held(PlanDraft.Empty))
                .Because("a new session opens on nothing, not on the last plan.");
            await Assert.That(((DraftRead.Held)drafts.Read("console")).Draft.Legs.Count).IsEqualTo(1)
                .Because("the older draft is kept exactly as it was.");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task A_name_with_only_a_result_or_a_proposal_left_is_used_too()
    {
        var drafts = DraftsIn(out var root);
        try
        {
            drafts.KeepResult("console", "proposed ITN-1");
            drafts.KeepProposed("console-2", new ProposedPlan("ITN-2", "GG-2", [], "", DateTimeOffset.UnixEpoch));

            await Assert.That(PtyPlanSession.FreshDraft(drafts, [])).IsEqualTo("console-3");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task A_name_a_live_agent_holds_is_not_taken()
    {
        var drafts = DraftsIn(out var root);
        try
        {
            await Assert.That(PtyPlanSession.FreshDraft(drafts, ["plan · console", "Claude Code"]))
                .IsEqualTo("console-2");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task There_is_always_another_name()
    {
        var drafts = DraftsIn(out var root);
        try
        {
            foreach (var name in Enumerable.Range(1, 12).Select(n => n == 1 ? "console" : $"console-{n}"))
            {
                Written(drafts, name);
            }

            await Assert.That(PtyPlanSession.FreshDraft(drafts, [])).IsEqualTo("console-13")
                .Because("drafts are kept, so the names run past the nine agents the mux holds.");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
