using System.Net;
using System.Text;
using Gg.Local;
using Gg.Runner.Intent;

namespace Gg.Runner.Tests;

/// <summary>
/// A browse can be narrowed, and the narrowing goes to the tracker.
/// </summary>
/// <remarks>
/// <para>
/// <b>The query was one string and nobody could change it.</b> Open work, most
/// recently touched first — a good default, and its own remark says so: "a
/// default and not a policy… changing it is one edit". This is that edit. A
/// person looking for their team's work in this sprint read fifty rows of
/// everybody's and gave up.
/// </para>
/// <para>
/// <b>It narrows the QUERY and not the rows that came back.</b> Filtering a page
/// this end can only ever filter what the page happened to contain, so a sprint
/// whose items fell outside the first fifty would read as "nothing there" when
/// it is not — an empty box that is lying, which is the failure the browse
/// pane's five endings exist to prevent.
/// </para>
/// <para>
/// <b>A value goes into a query string, so it is escaped where it goes in.</b>
/// The sink beside this already doubles quotes for exactly this reason and it is
/// copied rather than reinvented: an area path with an apostrophe in it is an
/// ordinary name, not an attack, and it has to work.
/// </para>
/// </remarks>
public class ABrowseThatNarrowsTests
{
    private const string Host = "https://tracker.example/acme/widgets";
    private const string Secret = "a-registered-credential";

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

    private const string OneId = """{"workItems":[{"id":26}]}""";

    private const string OneRow = """
        {"value":[{"id":26,"fields":{
          "System.Title":"The runner drops a lease",
          "System.State":"Active",
          "System.AreaPath":"Widgets\\Platform",
          "System.IterationPath":"Widgets\\Sprint 42",
          "System.ChangedDate":"2026-09-05T01:06:13Z"}}]}
        """;

    private static (WiqlWorkItemSource Source, Recorder Seen) Answering()
    {
        var recorder = new Recorder(request =>
            Json(request.Method == HttpMethod.Post ? OneId : OneRow));

        return (new WiqlWorkItemSource(Host, Secret, new HttpClient(recorder)), recorder);
    }

    [Test]
    public async Task With_no_filter_the_query_is_what_it_always_was()
    {
        var (source, seen) = Answering();

        _ = await source.BrowseAsync(cursor: null, limit: 50);

        await Assert.That(seen.Bodies[0]).Contains("<> 'Closed'")
            .Because("open work, most recently touched first, is what a list is when nobody "
                   + "said - and a filter nobody asked for must not change that.");

        await Assert.That(seen.Bodies[0]).DoesNotContain("AreaPath");
    }

    [Test]
    public async Task An_area_path_narrows_the_query_under_it()
    {
        var (source, seen) = Answering();

        _ = await source.BrowseAsync(
            cursor: null, limit: 50,
            filter: new WorkItemFilter(AreaPath: @"Widgets\Platform", Iteration: null, States: null));

        await Assert.That(seen.Bodies[0]).Contains("UNDER")
            .Because("an area path names a subtree, and a person who picks a team wants the "
                   + "work under it rather than the work filed exactly at it.");

        await Assert.That(seen.Bodies[0]).Contains(@"Widgets\\Platform");
    }

    [Test]
    public async Task A_sprint_narrows_it_to_that_one()
    {
        var (source, seen) = Answering();

        _ = await source.BrowseAsync(
            cursor: null, limit: 50,
            filter: new WorkItemFilter(null, @"Widgets\Sprint 42", null));

        await Assert.That(seen.Bodies[0]).Contains("IterationPath");
        await Assert.That(seen.Bodies[0]).Contains("Sprint 42");
    }

    [Test]
    public async Task States_replace_the_default_rather_than_adding_to_it()
    {
        var (source, seen) = Answering();

        _ = await source.BrowseAsync(
            cursor: null, limit: 50,
            filter: new WorkItemFilter(null, null, ["Closed"]));

        await Assert.That(seen.Bodies[0]).Contains("'Closed'");

        await Assert.That(seen.Bodies[0]).DoesNotContain("<> 'Closed'")
            .Because("asking for closed work and being handed the not-closed default on top "
                   + "of it would answer nothing, for ever, with no way to tell why.");
    }

    [Test]
    public async Task A_name_with_an_apostrophe_in_it_is_an_ordinary_name()
    {
        var (source, seen) = Answering();

        _ = await source.BrowseAsync(
            cursor: null, limit: 50,
            filter: new WorkItemFilter(@"Widgets\Kevin's team", null, null));

        await Assert.That(seen.Bodies[0]).Contains("Kevin''s team")
            .Because("the sink beside this doubles quotes for the same reason, and a team "
                   + "whose name has an apostrophe is a team that has to be browsable.");
    }

    [Test]
    public async Task A_listed_item_says_where_it_is_filed_and_when_it_is_due()
    {
        var (source, _) = Answering();

        var page = await source.BrowseAsync(cursor: null, limit: 50);

        await Assert.That(page.Items[0].AreaPath).IsEqualTo(@"Widgets\Platform")
            .Because("a filter a person cannot see the effect of is a filter they cannot "
                   + "trust: the column is how they know it took.");

        await Assert.That(page.Items[0].Iteration).IsEqualTo(@"Widgets\Sprint 42");
    }
}
