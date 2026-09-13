using System.Net;
using System.Text;
using Gg.Local;
using Gg.Runner.Intent;

namespace Gg.Runner.Tests;

/// <summary>
/// What has happened to a work item, in the order it happened.
/// </summary>
/// <remarks>
/// <para>
/// <b>A body says what somebody wants; a history says what has already been
/// tried.</b> Picking work off a title and a state is picking without either.
/// The thing that makes an item safe to start is usually in neither field: it
/// was reopened twice, or somebody left a comment last week saying what is
/// actually blocking it.
/// </para>
/// <para>
/// <b>One call, because the shape already carries both.</b> Field revisions and
/// the discussion arrive together — a comment is a revision of a field like any
/// other — so asking twice would be two round trips to reassemble one ordering
/// that the tracker already has.
/// </para>
/// <para>
/// <b>Named for the shape and not for a forge</b>, like everything else on this
/// adapter: a revision has a when, a who and a what, and which tracker spells
/// them which way is the deployment's business.
/// </para>
/// </remarks>
public class AWorkItemsHistoryTests
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

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

    private static (WiqlWorkItemSource Source, Recorder Seen) Answering(string body)
    {
        var recorder = new Recorder(_ => Json(body));
        return (new WiqlWorkItemSource(Host, Secret, new HttpClient(recorder)), recorder);
    }

    private const string TwoRevisions = """
        {"count":2,"value":[
          {"rev":2,
           "revisedBy":{"displayName":"Kevin Deenanauth"},
           "fields":{
             "System.ChangedDate":{"newValue":"2026-09-01T10:00:00Z"},
             "System.State":{"oldValue":"New","newValue":"Active"}}},
          {"rev":3,
           "revisedBy":{"displayName":"A Colleague"},
           "fields":{
             "System.ChangedDate":{"newValue":"2026-09-04T16:30:00Z"},
             "System.History":{"newValue":"Blocked on the credential rollout."}}}
        ]}
        """;

    [Test]
    public async Task It_asks_the_tracker_for_the_items_revisions()
    {
        var (source, seen) = Answering(TwoRevisions);

        _ = await source.HistoryAsync("26");

        await Assert.That(seen).IsNotEmpty();
        await Assert.That(seen[0].ToString()).Contains("26")
            .Because("a history for the wrong item is worse than none, and the id is the "
                   + "only thing the person chose.");
    }

    [Test]
    public async Task A_field_that_changed_says_what_it_changed_from_and_to()
    {
        var (source, _) = Answering(TwoRevisions);

        var history = await source.HistoryAsync("26");

        var moved = history.Single(c => c.What.Contains("New", StringComparison.Ordinal));

        await Assert.That(moved.What).Contains("Active")
            .Because("`it changed' is not a fact somebody can use; what it changed FROM is "
                   + "half of why it matters.");

        await Assert.That(moved.Who).IsEqualTo("Kevin Deenanauth");
        await Assert.That(moved.When).IsEqualTo(DateTimeOffset.Parse("2026-09-01T10:00:00Z", null));
    }

    [Test]
    public async Task A_comment_is_carried_as_what_was_said()
    {
        var (source, _) = Answering(TwoRevisions);

        var history = await source.HistoryAsync("26");

        await Assert.That(history.Any(c =>
                c.What.Contains("Blocked on the credential rollout.", StringComparison.Ordinal)))
            .IsTrue()
            .Because("the discussion is usually where the reason lives, and it arrives as a "
                   + "revision of a field like any other.");
    }

    [Test]
    public async Task An_item_with_no_history_is_an_empty_list_and_not_a_failure()
    {
        var (source, _) = Answering("""{"count":0,"value":[]}""");

        await Assert.That(await source.HistoryAsync("26")).IsEmpty()
            .Because("nothing has happened to it yet, which is a thing to know rather than "
                   + "an error to report.");
    }
}
