using System.Net;
using System.Text;
using Gg.Local;
using Gg.Runner.Intent;

namespace Gg.Runner.Tests;

/// <summary>
/// Everything one work item records, asked for one item at a time.
/// </summary>
/// <remarks>
/// <para>
/// <b>REPORTED FROM USE: "we're missing point and other fields".</b> The
/// listing carries seven, chosen so a person can pick work off them. A story
/// point is not one of the seven, and neither is an assignee, a priority or
/// whatever a team added a column for last quarter.
/// </para>
/// <para>
/// <b>And the listing cannot be the place to fix it.</b> The batch read asks
/// the tracker for five named fields for fifty rows; dropping that allowlist
/// to get the sixth would pull EVERY field of every row on every page - and
/// every field includes <c>System.Description</c>, which is the item's body.
/// <c>BrowseTool.Fields</c> says a body does not cross in a listing, in as many
/// words, and it is a rule about customer content rather than about bytes.
/// </para>
/// <para>
/// <b>So it is a second verb, and the history is the precedent.</b>
/// <c>get_work_item_history</c> is already a per-item call for the same reason:
/// a question about ONE item the person has opened, paid for when they open it.
/// For that item the body has already crossed - <c>get_work_item</c> returns
/// it - so asking for all of its fields gives away nothing a modal does not
/// already hold.
/// </para>
/// </remarks>
public class AWorkItemsFieldsTests
{
    private const string Host = "https://tracker.example/acme/widgets";
    private const string Secret = "a-registered-credential";

    private sealed class Recorder(Func<HttpRequestMessage, HttpResponseMessage> answer)
        : HttpMessageHandler
    {
        internal List<Uri> Seen { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Seen.Add(request.RequestUri!);
            return Task.FromResult(answer(request));
        }
    }

    private static (WiqlWorkItemSource Source, Recorder Seen) Answering(string body)
    {
        var recorder = new Recorder(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        });

        return (new WiqlWorkItemSource(Host, Secret, new HttpClient(recorder)), recorder);
    }

    private const string OneItem = """
        {"id":17864,
         "fields":{
           "System.Title":"Sonar cleanup: correctness risks",
           "System.State":"Active",
           "Microsoft.VSTS.Scheduling.StoryPoints":5,
           "System.AssignedTo":{"displayName":"A Colleague",
                                "uniqueName":"a.person@example.com"},
           "System.Rev":47,
           "Custom.Blocked":false}}
        """;

    [Test]
    public async Task It_asks_the_tracker_for_the_whole_item()
    {
        // NO ALLOWLIST. `fields=` on this endpoint is a list of what to send
        // back, so a request that names any field at all is a request that
        // cannot answer "everything" - which is the entire question here.
        var (source, seen) = Answering(OneItem);

        await source.FieldsAsync("17864");

        await Assert.That(seen.Seen[0].ToString()).Contains("/_apis/wit/workitems/17864");
        await Assert.That(seen.Seen[0].Query).DoesNotContain("fields=")
            .Because("naming five fields is how the listing stays cheap, and it is exactly why "
                   + "the listing could not answer this.");
    }

    [Test]
    public async Task Every_field_it_sent_comes_back_in_the_trackers_own_order()
    {
        var (source, _) = Answering(OneItem);

        var said = await source.FieldsAsync("17864");

        await Assert.That(said.Select(f => f.Name).ToList()).IsEquivalentTo((string[])
            ["System.Title", "System.State", "Microsoft.VSTS.Scheduling.StoryPoints",
             "System.AssignedTo", "System.Rev", "Custom.Blocked"])
            .Because("a tracker groups related fields together and an alphabetical sort would "
                   + "scatter them; this is the order somebody sees in the tracker's own UI.");
    }

    [Test]
    public async Task A_number_a_person_and_a_flag_are_all_readable()
    {
        // THE REASON NAMING THE FIELD WOULD NOT HAVE BEEN ENOUGH. A story point
        // is a number and an assignee is an object; a reader that took only
        // JSON strings would have sent both as blank, which reads as a tracker
        // that records nothing rather than as a reader that dropped it.
        var (source, _) = Answering(OneItem);

        var said = await source.FieldsAsync("17864");

        await Assert.That(said.Single(f => f.Name == "Microsoft.VSTS.Scheduling.StoryPoints").Value)
            .IsEqualTo("5");
        await Assert.That(said.Single(f => f.Name == "System.AssignedTo").Value)
            .IsEqualTo("A Colleague");
        await Assert.That(said.Single(f => f.Name == "Custom.Blocked").Value)
            .IsEqualTo("false");
    }

    [Test]
    public async Task An_item_with_no_fields_is_no_fields_and_not_a_failure()
    {
        var (source, _) = Answering("""{"id":17864}""");

        await Assert.That(await source.FieldsAsync("17864")).IsEmpty()
            .Because("a tracker that answered and had nothing to add is not a tracker that "
                   + "refused, which is the distinction every absence in this console draws.");
    }
}
