using System.Net;
using System.Text;
using System.Text.Json;
using Gg.Runner.Intent;

namespace Gg.Runner.Tests;

/// <summary>
/// A watch's query reaches the tracker as written, and every row comes back
/// with its revision.
/// </summary>
/// <remarks>
/// <para>
/// <b>Verbatim, because it was reviewed verbatim.</b> A browse builds its query
/// from three filters; a watch's is a whole query somebody approved, and any
/// rewriting here - an added clause, a changed order - would be a sweep of
/// something other than what the gate saw.
/// </para>
/// <para>
/// <b>The revision is <c>rev</c> on each item the batch read answers</b>, which
/// the tracker sends with every item unasked, so reading it costs no extra
/// round trip.
/// </para>
/// </remarks>
public class AWatchsQueryRunsVerbatimTests
{
    private const string Host = "https://tracker.example/acme/widgets";

    private const string Watched =
        "SELECT [System.Id] FROM WorkItems WHERE [System.Tags] CONTAINS 'needs-review' "
      + "ORDER BY [System.ChangedDate] DESC";

    private sealed class Recorder(Func<HttpRequestMessage, HttpResponseMessage> answer)
        : HttpMessageHandler
    {
        internal List<string> Bodies { get; } = [];

        internal List<Uri> Uris { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Uris.Add(request.RequestUri!);

            if (request.Content is not null)
            {
                Bodies.Add(await request.Content.ReadAsStringAsync(cancellationToken));
            }

            return answer(request);
        }
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

    private const string ThreeIds = """{"workItems":[{"id":31},{"id":26},{"id":40}]}""";

    private const string TwoRows = """
        {"value":[
          {"id":26,"rev":7,"fields":{"System.Title":"Drops a lease","System.State":"Active"}},
          {"id":31,"rev":12,"fields":{"System.Title":"Loses a beat","System.State":"New"}}]}
        """;

    private static (WiqlWorkItemSource Source, Recorder Seen) Answering()
    {
        var recorder = new Recorder(request =>
            Json(request.Method == HttpMethod.Post ? ThreeIds : TwoRows));

        return (new WiqlWorkItemSource(Host, "a-credential", new HttpClient(recorder)), recorder);
    }

    [Test]
    public async Task The_query_reaches_the_tracker_as_it_was_written()
    {
        var (source, seen) = Answering();

        _ = await source.QueryAsync(Watched, cursor: null, limit: 2);

        using var body = JsonDocument.Parse(seen.Bodies.Single());

        await Assert.That(body.RootElement.GetProperty("query").GetString()).IsEqualTo(Watched)
            .Because("the gate reviewed this string; a sweep of any other one is a sweep "
                   + "nobody approved.");
    }

    [Test]
    public async Task Rows_come_back_in_the_querys_order_with_their_revisions()
    {
        var (source, _) = Answering();

        var page = await source.QueryAsync(Watched, cursor: null, limit: 2);

        await Assert.That(string.Join(',', page.Items.Select(i => i.Id))).IsEqualTo("31,26")
            .Because("the query said what order this is in, and the batch read answers in its "
                   + "own.");
        await Assert.That(string.Join(',', page.Items.Select(i => i.Revision))).IsEqualTo("12,7");
        await Assert.That(page.NextCursor).IsEqualTo("2")
            .Because("three matched and two were read, so there is a next page.");
    }

    [Test]
    public async Task The_cursor_continues_where_the_last_page_ended()
    {
        var (source, seen) = Answering();

        _ = await source.QueryAsync(Watched, cursor: "2", limit: 2);

        await Assert.That(seen.Uris.Last().Query).Contains("ids=40")
            .Because("the third id is the only one past the cursor.");
    }
}
