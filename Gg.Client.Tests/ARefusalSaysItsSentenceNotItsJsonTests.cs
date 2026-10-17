using System.Net;
using System.Text.RegularExpressions;
using Gg.Client;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// A control-plane refusal reaches the person as its sentence, never as the problem document
/// that carried it.
/// </summary>
/// <remarks>
/// <b>Seen live on 0.88.46.</b> <c>gg fly "x" --repo no-such-repo</c> printed the whole
/// problem+json body - type URL, title, status and all - because the launch read the body and
/// threw it as it came. The sentence a person can act on is the <c>detail</c> member. A body
/// that is not a problem document, or carries no detail, still arrives as it came.
/// </remarks>
public class ARefusalSaysItsSentenceNotItsJsonTests
{
    private const string Sentence =
        "This flight declares repository 'no-such-repo', which the envelope does not name. "
      + "Run gg repos to list the repositories it names.";

    private static string Problem(HttpStatusCode status) =>
        $$"""{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.1","title":"Refused","status":{{(int)status}},"detail":"{{Sentence}}"}""";

    private sealed class Refuses(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/problem+json"),
                RequestMessage = request,
            });
    }

    private static ControlPlaneClient Answering(HttpStatusCode status, string? body = null) =>
        new(new HttpClient(new Refuses(status, body ?? Problem(status)))
        {
            BaseAddress = new Uri("http://cp.test/"),
        });

    private static async Task<string> RefusalOf(Func<Task> call)
    {
        try
        {
            await call();
        }
        catch (Exception refused) when (refused is not TUnit.Assertions.Exceptions.AssertionException)
        {
            return refused.Message;
        }

        throw new InvalidOperationException("The call was expected to be refused and was not.");
    }

    private static FlightLaunchRequest Launch() => new()
    {
        Name = "x",
        Intent = new FlightIntent { Kind = FlightIntentKinds.Text, Text = "x" },
    };

    [Test]
    public async Task A_refused_launch_says_the_detail_and_nothing_else()
    {
        var said = await RefusalOf(() =>
            Answering(HttpStatusCode.BadRequest).LaunchFlightAsync("token", Launch()));

        await Assert.That(said).IsEqualTo(Sentence)
            .Because("gg fly printed the whole problem document, and only its detail is "
                   + "something a person can act on.");
    }

    [Test]
    public async Task A_refused_retire_says_the_detail()
    {
        var said = await RefusalOf(() =>
            Answering(HttpStatusCode.Conflict).RetireNamedAsync("token", "dev"));

        await Assert.That(said).IsEqualTo(Sentence);
    }

    [Test]
    public async Task A_refused_build_says_the_detail()
    {
        var said = await RefusalOf(() =>
            Answering(HttpStatusCode.Conflict).RequestBuildAsync("token", "dotnet"));

        await Assert.That(said).IsEqualTo(Sentence);
    }

    [Test]
    public async Task A_body_that_is_not_a_problem_document_arrives_as_it_came()
    {
        var said = await RefusalOf(() =>
            Answering(HttpStatusCode.BadRequest, "The intent is empty.\n")
                .LaunchFlightAsync("token", Launch()));

        await Assert.That(said).IsEqualTo("The intent is empty.");
    }

    [Test]
    public async Task No_refusal_is_built_from_the_raw_body()
    {
        // EVERY DOOR, by reading the source: each refusal is built in the method that owns
        // the route, so a per-method test would miss the next one written. A refusal built
        // straight from ReadAsStringAsync is the shape that printed JSON.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Gg.sln")))
        {
            dir = dir.Parent;
        }

        var source = File.ReadAllText(Path.Combine(dir!.FullName, "Gg.Client", "ControlPlaneClient.cs"));

        var raw = Regex.Matches(
                source,
                @"throw new \w+Exception\(\s*\(?\s*await response\.Content\.ReadAsStringAsync")
            .Select(m => source[..m.Index].Count(c => c == '\n') + 1)
            .ToList();

        await Assert.That(raw).IsEmpty()
            .Because("a refusal built from the raw body prints a problem document's JSON; "
                   + "lines: " + string.Join(", ", raw));
    }
}
