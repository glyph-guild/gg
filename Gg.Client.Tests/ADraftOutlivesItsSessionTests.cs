using Gg.Contracts;
using Gg.Local;

namespace Gg.Client.Tests;

/// <summary>
/// <b>S63.1-03</b> - the draft lives under <c>StateRoot/itineraries</c>, named by <c>--draft</c>,
/// and survives the server being restarted.
/// </summary>
/// <remarks>
/// Closing the agent loses nothing (ADR-0038 Decision 8), because the server holds no copy
/// between calls - every call reads the file fresh (rule 3).
/// </remarks>
public class ADraftOutlivesItsSessionTests
{
    [Test]
    public async Task A_named_draft_lives_under_the_state_root()
    {
        var drafts = ItineraryDrafts.ForThisMachine(stateHome: "/somewhere/state");

        await Assert.That(drafts.PathOf("icons"))
            .IsEqualTo(Path.Combine(LocalPaths.StateRoot("/somewhere/state"), "itineraries", "icons.yaml"));
    }

    [Test]
    [Arguments("../escape")]
    [Arguments("a/b")]
    [Arguments("")]
    [Arguments(".hidden")]
    public async Task A_name_that_is_not_a_plain_word_is_refused(string name)
    {
        await Assert.That(ItineraryDrafts.Refused(name)).IsNotNull()
            .Because($"'{name}' would put the draft somewhere other than the drafts directory.");
    }

    [Test]
    public async Task A_new_store_over_the_same_directory_reads_what_the_last_one_wrote()
    {
        var root = Directory.CreateTempSubdirectory("gg-drafts-").FullName;
        try
        {
            _ = new ItineraryDrafts(root).Change("icons", d => d with
            {
                Intent = FlightIntent.Of("three findings"),
                Legs = [new FlightNomination { Subject = "the icon", WorkKind = "implement", Reason = "named" }],
            });

            var read = new ItineraryDrafts(root).Read("icons");

            var draft = (await Assert.That(read).IsTypeOf<DraftRead.Held>())!.Draft;
            await Assert.That(draft.Intent).IsEqualTo(FlightIntent.Of("three findings"));
            await Assert.That(draft.Legs.Single().Subject).IsEqualTo("the icon");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task A_draft_nobody_started_is_empty_rather_than_missing()
    {
        var root = Directory.CreateTempSubdirectory("gg-drafts-").FullName;
        try
        {
            var draft = (await Assert.That(new ItineraryDrafts(root).Read("fresh"))
                .IsTypeOf<DraftRead.Held>())!.Draft;

            await Assert.That(draft.Intent).IsNull();
            await Assert.That(draft.Legs).IsEmpty();
            await Assert.That(draft.Planner).IsEqualTo("plan");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
