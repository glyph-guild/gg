using System.Net;
using System.Text;
using Gg.Local;
using Gg.Runner.Intent;

namespace Gg.Runner.Tests;

/// <summary>
/// The tracker shape this binary can speak, and the host it speaks it to.
/// </summary>
/// <remarks>
/// <para>
/// <b>Named for the shape, like every adapter here.</b> A tracker that answers
/// <c>_apis/wit</c> and queries in WIQL is a CONVENTION, and which forge speaks
/// it is the deployment's business - the same split
/// <c>PathScopedGitVcsAdapter</c> makes when it keeps its own path segment in
/// the class and takes its host in a constructor. That is what lets this be C#
/// in this repository rather than a script on somebody's runner.
/// </para>
/// <para>
/// <b>Matched to a reader that has been running in production.</b> The six
/// fields, their labels and their order are what a deployment already proved
/// useful, and this port changes the language underneath without changing what
/// any agent sees. Changing both at once would leave any shift in agent
/// behaviour unattributable to either.
/// </para>
/// <para>
/// <b>Browse is new, because the script never had it.</b> Two calls, because
/// the shape gives ids and fields separately: a query returns ids, a batch read
/// returns the columns a list shows. The cursor is an offset into the answered
/// id list rather than anything the tracker issues, which is honest about what
/// this shape actually supports.
/// </para>
/// </remarks>
public class WiqlWorkItemSourceTests
{
    private const string Host = "https://tracker.example/acme/widgets";
    private const string Secret = "a-registered-credential";

    private sealed class Recorder : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _answer;

        internal Recorder(Func<HttpRequestMessage, HttpResponseMessage> answer) => _answer = answer;

        internal List<(HttpMethod Method, Uri Uri, string? Authorization, string Body)> Seen { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Seen.Add((
                request.Method,
                request.RequestUri!,
                request.Headers.Authorization?.Parameter,
                request.Content is null
                    ? ""
                    : await request.Content.ReadAsStringAsync(cancellationToken)));

            return _answer(request);
        }
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

    private static (WiqlWorkItemSource Source, Recorder Seen) SourceThatAnswers(
        Func<HttpRequestMessage, HttpResponseMessage> answer)
    {
        var recorder = new Recorder(answer);
        return (new WiqlWorkItemSource(Host, Secret, new HttpClient(recorder)), recorder);
    }

    private const string OneItem = """
        {"id":26,"fields":{
          "System.WorkItemType":"Bug",
          "System.State":"Active",
          "System.Title":"The runner drops a lease",
          "System.Description":"<div>It <b>drops</b> it.<br>Twice.</div>",
          "Microsoft.VSTS.Common.AcceptanceCriteria":"<p>It stops dropping it.</p>",
          "System.Tags":"runner; lease"
        }}
        """;

    [Test]
    public async Task It_reads_one_item_from_the_path_the_shape_names()
    {
        var (source, seen) = SourceThatAnswers(_ => Json(OneItem));

        await source.ReadAsync("26");

        await Assert.That(seen.Seen).Count().IsEqualTo(1);
        await Assert.That(seen.Seen[0].Method).IsEqualTo(HttpMethod.Get);
        await Assert.That(seen.Seen[0].Uri.ToString())
            .IsEqualTo($"{Host}/_apis/wit/workitems/26?api-version=7.1");
    }

    [Test]
    public async Task The_credential_travels_as_basic_auth_and_the_username_is_empty()
    {
        // THE SHAPE'S OWN CONVENTION: the token goes in the password half of
        // basic auth with no user. Getting this wrong produces a 401 that reads
        // like a bad credential rather than a bad request, which is the most
        // expensive kind of wrong here.
        var (source, seen) = SourceThatAnswers(_ => Json(OneItem));

        await source.ReadAsync("26");

        await Assert.That(seen.Seen[0].Authorization)
            .IsEqualTo(Convert.ToBase64String(Encoding.UTF8.GetBytes(":" + Secret)));
    }

    [Test]
    public async Task It_projects_the_six_fields_the_deployment_settled_on()
    {
        var (source, _) = SourceThatAnswers(_ => Json(OneItem));

        var item = await source.ReadAsync("26");

        await Assert.That(item).IsNotNull();
        await Assert.That(item!.Id).IsEqualTo("26");
        await Assert.That(item.Type).IsEqualTo("Bug");
        await Assert.That(item.State).IsEqualTo("Active");
        await Assert.That(item.Title).IsEqualTo("The runner drops a lease");
        await Assert.That(item.Tags).IsEqualTo("runner; lease");
    }

    [Test]
    public async Task Markup_is_stripped_because_an_agent_reads_prose()
    {
        // The tracker stores descriptions as html. A reader that handed the
        // markup through would spend the agent's context on tags, and the
        // installed script strips it - so this one does too, identically.
        var (source, _) = SourceThatAnswers(_ => Json(OneItem));

        var item = await source.ReadAsync("26");

        await Assert.That(item!.Description).DoesNotContain("<");
        await Assert.That(item.Description).Contains("drops");
        await Assert.That(item.AcceptanceCriteria).IsEqualTo("It stops dropping it.");
    }

    [Test]
    public async Task A_line_break_in_markup_survives_as_a_line_break()
    {
        // STRIPPING IS NOT DELETING. `<br>` and `</div>` carry the only
        // paragraph structure the description has; a strip that closed them up
        // would hand the agent one run-on line and lose the shape of the
        // sentence the author wrote.
        var (source, _) = SourceThatAnswers(_ => Json(OneItem));

        var item = await source.ReadAsync("26");

        await Assert.That(item!.Description).Contains("\n")
            .Because("the markup said there was a line there.");
    }

    [Test]
    public async Task An_item_that_is_not_there_is_null_rather_than_a_throw()
    {
        // A MISSING ITEM IS AN ANSWER. The server turns null into "there is no
        // work item 26 at this tracker", which is a sentence an agent can stop
        // on; an exception would reach it as a tool that broke.
        var (source, _) = SourceThatAnswers(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        await Assert.That(await source.ReadAsync("404")).IsNull();
    }

    [Test]
    public async Task A_tracker_that_refuses_the_credential_says_so_rather_than_answering_nothing()
    {
        // 401 IS NOT 404, and collapsing them would be the worst failure this
        // reader has: an agent told the work item does not exist when the truth
        // is that the runner's credential expired.
        var (source, _) = SourceThatAnswers(
            _ => new HttpResponseMessage(HttpStatusCode.Unauthorized));

        await Assert.That(async () => await source.ReadAsync("26"))
            .Throws<HttpRequestException>();
    }

    [Test]
    public async Task Browsing_queries_for_ids_and_then_reads_their_columns()
    {
        var (source, seen) = SourceThatAnswers(request =>
            request.Method == HttpMethod.Post
                ? Json("""{"workItems":[{"id":26},{"id":27}]}""")
                : Json("""
                    {"value":[
                      {"id":26,"fields":{"System.Title":"One","System.State":"Active",
                        "System.ChangedDate":"2026-09-01T10:00:00Z"}},
                      {"id":27,"fields":{"System.Title":"Two","System.State":"New",
                        "System.ChangedDate":"2026-09-02T10:00:00Z"}}
                    ]}
                    """));

        var page = await source.BrowseAsync(cursor: null, limit: 50);

        await Assert.That(seen.Seen).Count().IsEqualTo(2);
        await Assert.That(seen.Seen[0].Method).IsEqualTo(HttpMethod.Post);
        await Assert.That(seen.Seen[0].Uri.ToString())
            .IsEqualTo($"{Host}/_apis/wit/wiql?api-version=7.1");
        await Assert.That(seen.Seen[0].Body).Contains("SELECT");

        await Assert.That(page.Items).Count().IsEqualTo(2);
        await Assert.That(page.Items[0].Id).IsEqualTo("26");
        await Assert.That(page.Items[0].Title).IsEqualTo("One");
        await Assert.That(page.Items[0].Url).Contains("26")
            .Because("a list a person picks from has to be a list they can open.");
    }

    [Test]
    public async Task A_query_answering_nothing_is_an_empty_page_and_no_second_call()
    {
        // THE ANCHOR. A batch read of zero ids is a request the shape rejects,
        // and the reader would report a tracker error where the honest answer
        // is that there is no work.
        var (source, seen) = SourceThatAnswers(_ => Json("""{"workItems":[]}"""));

        var page = await source.BrowseAsync(cursor: null, limit: 50);

        await Assert.That(page.Items).IsEmpty();
        await Assert.That(page.NextCursor).IsNull();
        await Assert.That(seen.Seen).Count().IsEqualTo(1)
            .Because("there was nothing to read the columns of.");
    }

    [Test]
    public async Task A_page_smaller_than_the_answer_offers_a_cursor_to_continue()
    {
        var (source, _) = SourceThatAnswers(request =>
            request.Method == HttpMethod.Post
                ? Json("""{"workItems":[{"id":26},{"id":27},{"id":28}]}""")
                : Json("""{"value":[{"id":26,"fields":{"System.Title":"One"}}]}"""));

        var page = await source.BrowseAsync(cursor: null, limit: 1);

        await Assert.That(page.NextCursor).IsNotNull()
            .Because("two of the three ids were not returned, so the list continues.");
    }

    [Test]
    public async Task The_last_page_offers_no_cursor()
    {
        // NULL IS THE END, and an empty string is not: a caller handed "" would
        // pass it back and be given the first page again, for ever.
        var (source, _) = SourceThatAnswers(request =>
            request.Method == HttpMethod.Post
                ? Json("""{"workItems":[{"id":26}]}""")
                : Json("""{"value":[{"id":26,"fields":{"System.Title":"One"}}]}"""));

        var page = await source.BrowseAsync(cursor: null, limit: 50);

        await Assert.That(page.NextCursor).IsNull();
    }

    [Test]
    public async Task The_source_names_no_forge_in_the_host_it_is_given()
    {
        // THE WHOLE REASON THIS CAN BE C# HERE. The shape is source and the
        // host is an argument, so the class carries no forge to trip
        // ProviderNeutralityTests - and a second tracker speaking the same
        // convention needs a different host and no new code.
        var (source, seen) = SourceThatAnswers(_ => Json(OneItem));

        await source.ReadAsync("26");

        await Assert.That(seen.Seen[0].Uri.ToString()).StartsWith(Host)
            .Because("whatever host the deployment named is the host that is read.");
    }
}
