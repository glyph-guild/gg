using System.Net;
using System.Text;
using Gg.Local;
using Gg.Runner.Intent;

namespace Gg.Runner.Tests;

/// <summary>
/// What there is to filter by, asked of the tracker rather than typed.
/// </summary>
/// <remarks>
/// <para>
/// <b>A filter a person has to spell is a filter they get wrong once and
/// abandon.</b> An area path is a backslash-separated tree with a team's
/// punctuation in it; a sprint is whatever somebody named a fortnight. Typing
/// either produces an empty list that is indistinguishable from a sprint with
/// no work in it - the exact confusion the browse endings exist to end.
/// </para>
/// <para>
/// <b>The tracker's own shape is not the query's shape.</b> Classification
/// nodes come back with the tree's own segment wedged in the middle -
/// <c>\Widgets\Area\Platform</c> - and a query that asks for that path matches
/// nothing, silently. Converting it here, once, beside the query that consumes
/// it, is the only place that conversion can be checked.
/// </para>
/// <para>
/// <b>States come from the types, unioned.</b> A project has several work item
/// types and they disagree about states; a person filtering does not care which
/// type a state belongs to, and offering three lists to pick from would be
/// asking them to know.
/// </para>
/// </remarks>
public class FacetsComeFromTheTrackerTests
{
    private const string Host = "https://tracker.example/acme/widgets";
    private const string Secret = "a-registered-credential";

    private const string Areas = """
        {"name":"Widgets","structureType":"area","path":"\\Widgets\\Area","hasChildren":true,
         "children":[
           {"name":"Platform","structureType":"area","path":"\\Widgets\\Area\\Platform"},
           {"name":"Kevin's team","structureType":"area",
            "path":"\\Widgets\\Area\\Platform\\Kevin's team"}]}
        """;

    private const string Iterations = """
        {"name":"Widgets","structureType":"iteration","path":"\\Widgets\\Iteration",
         "hasChildren":true,
         "children":[{"name":"Sprint 42","structureType":"iteration",
                      "path":"\\Widgets\\Iteration\\Sprint 42"}]}
        """;

    private const string Types = """
        {"value":[
          {"name":"Bug","states":[{"name":"New"},{"name":"Active"},{"name":"Closed"}]},
          {"name":"Task","states":[{"name":"New"},{"name":"Doing"},{"name":"Closed"}]}]}
        """;

    private sealed class Recorder : HttpMessageHandler
    {
        internal List<Uri> Uris { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!;
            Uris.Add(uri);

            var body = uri.AbsoluteUri.Contains("Areas", StringComparison.OrdinalIgnoreCase)
                ? Areas
                : uri.AbsoluteUri.Contains("Iterations", StringComparison.OrdinalIgnoreCase)
                    ? Iterations
                    : Types;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }

    private static (WiqlWorkItemSource Source, Recorder Seen) Answering()
    {
        var recorder = new Recorder();
        return (new WiqlWorkItemSource(Host, Secret, new HttpClient(recorder)), recorder);
    }

    [Test]
    public async Task An_area_path_comes_back_shaped_the_way_a_query_wants_it()
    {
        var (source, _) = Answering();

        var facets = await source.FacetsAsync();

        await Assert.That(facets.AreaPaths).Contains(@"Widgets\Platform")
            .Because("the tracker wedges the tree's own name into the middle of the path, and "
                   + "a query asking for \\Widgets\\Area\\Platform matches nothing - silently, "
                   + "which is the worst way for a filter to be wrong.");

        await Assert.That(facets.AreaPaths).Contains(@"Widgets\Platform\Kevin's team")
            .Because("the tree is walked to the bottom: a person filters by the team they are "
                   + "on, which is a leaf, not by the project root.");
    }

    [Test]
    public async Task The_root_is_offered_too_because_it_is_a_real_answer()
    {
        var (source, _) = Answering();

        var facets = await source.FacetsAsync();

        await Assert.That(facets.AreaPaths).Contains("Widgets")
            .Because("everything, said explicitly, is a choice a person makes on purpose after "
                   + "narrowing - and it is where items land when nobody filed them deeper.");
    }

    [Test]
    public async Task A_sprint_comes_back_the_same_way()
    {
        var (source, _) = Answering();

        var facets = await source.FacetsAsync();

        await Assert.That(facets.Iterations).Contains(@"Widgets\Sprint 42");
    }

    [Test]
    public async Task States_are_every_types_states_and_each_one_once()
    {
        var (source, _) = Answering();

        var facets = await source.FacetsAsync();

        await Assert.That(facets.States).IsEquivalentTo(
            (string[])["New", "Active", "Closed", "Doing"])
            .Because("a person filtering does not care which type a state belongs to; offering "
                   + "one list per type would be asking them to know.");
    }

    [Test]
    public async Task It_asks_the_tracker_for_the_tree_rather_than_the_top_of_it()
    {
        var (source, seen) = Answering();

        _ = await source.FacetsAsync();

        await Assert.That(seen.Uris.Select(uri => uri.AbsoluteUri)).Contains(
            uri => uri.Contains("classificationnodes", StringComparison.OrdinalIgnoreCase));

        await Assert.That(seen.Uris.Select(uri => uri.Query)).Contains(
            query => query.Contains("depth", StringComparison.OrdinalIgnoreCase))
            .Because("a node read with no depth answers the root and the fact that it has "
                   + "children, which is a list with one useless entry in it.");
    }
}
