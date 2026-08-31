using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Gg.Runner.Vcs;

namespace Gg.Runner.Tests;

/// <summary>
/// A proposal is authenticated with the credential the developer registered.
/// </summary>
/// <remarks>
/// <para>
/// <b>It never was.</b> <c>ProposeAsync</c> used the injected client for both
/// calls and never touched <c>request.Secret</c>, while <c>PushAsync</c> one
/// method up hands that same secret to <c>GitPush</c>. So the branch reached the
/// remote and the proposal could not open — for every provider, on every flight,
/// since the destination shipped.
/// </para>
/// <para>
/// <b>The real-forge test could not see it, and that is why this one is here.</b>
/// <c>AgainstRealRemoteTests</c> injects a client it has already put a bearer
/// token on; production builds <c>new HttpClient { BaseAddress = … }</c> and
/// nothing else. A test that hands the adapter a pre-authenticated client is
/// asking whether the forge works, not whether the adapter authenticates.
/// So every client here is <b>anonymous</b>, exactly as
/// <c>DestinationConfiguration.FromEnvironment</c> builds one.
/// </para>
/// <para>
/// <b>Both calls, and the first one is the subtle half.</b> The idempotency query
/// runs before the creation call, and an unauthenticated <c>GET</c> does not
/// error — it fails <c>IsSuccessStatusCode</c> and degrades silently to
/// <i>there is no existing proposal</i>. So an unauthenticated adapter that
/// somehow could create would open a <b>second</b> pull request on a branch that
/// already had one, which is worse than failing.
/// </para>
/// </remarks>
public class ProposalCredentialTests
{
    private const string Secret = "a-registered-credential";

    /// <summary>Every request the adapter made, and what it carried.</summary>
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

    private static HttpResponseMessage Json(HttpStatusCode code, string body) =>
        new(code) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };

    /// <summary>An adapter over an ANONYMOUS client, the way production builds one.</summary>
    private static (HttpsDestinationAdapter Adapter, Recorder Handler) Anonymous(
        Func<HttpRequestMessage, HttpResponseMessage> answer)
    {
        var handler = new Recorder(answer);

        return (new HttpsDestinationAdapter(
            "fixture",
            "forge.example",
            new HttpClient(handler) { BaseAddress = new Uri("https://api.forge.example/") }),
            handler);
    }

    private static LandingRequest Landing(string secret = Secret) => new()
    {
        WorkingDirectory = Path.GetTempPath(),
        Slug = "acme/widgets",
        Branch = "gg/GG-42",
        BaseRef = "main",
        Title = "GG-42: a change",
        Secret = secret,
    };

    // ---- the claim ----

    [Test]
    public async Task Both_calls_carry_the_credential_the_developer_registered()
    {
        var (adapter, handler) = Anonymous(request => request.Method == HttpMethod.Get
            ? Json(HttpStatusCode.OK, "[]")
            : Json(HttpStatusCode.Created, """{"number":7,"html_url":"https://forge.example/pr/7"}"""));

        await adapter.ProposeAsync(Landing(), CancellationToken.None);

        await Assert.That(handler.Seen.Count).IsEqualTo(2)
            .Because("the idempotency query and the creation call, in that order.");

        foreach (var (method, uri, authorization, _) in handler.Seen)
        {
            await Assert.That(authorization).IsEqualTo(Secret)
                .Because($"the {method} to {uri.AbsolutePath} went out with no credential, and "
                       + "the client is anonymous because that is how production builds it.");
        }
    }

    [Test]
    public async Task An_unauthenticated_query_would_open_a_second_proposal_on_the_same_branch()
    {
        // THE SUBTLE HALF. An unauthenticated GET does not error - it fails
        // IsSuccessStatusCode and degrades silently to "there is no existing
        // proposal", so the branch that already had one gets a second.
        var (adapter, handler) = Anonymous(request =>
        {
            if (request.Headers.Authorization is null)
            {
                return Json(HttpStatusCode.Unauthorized, """{"message":"requires authentication"}""");
            }

            return request.Method == HttpMethod.Get
                ? Json(HttpStatusCode.OK, """[{"number":3,"html_url":"https://forge.example/pr/3"}]""")
                : Json(HttpStatusCode.Created, """{"number":9,"html_url":"https://forge.example/pr/9"}""");
        });

        var outcome = await adapter.ProposeAsync(Landing(), CancellationToken.None);

        await Assert.That(outcome).IsTypeOf<LandingOutcome.Landed>();
        await Assert.That(((LandingOutcome.Landed)outcome).Number).IsEqualTo(3)
            .Because("the proposal that already exists is the one to report. Opening a second "
                   + "is the failure an unauthenticated query hides, because it looks exactly "
                   + "like a branch nobody had proposed yet.");
        await Assert.That(handler.Seen.Count).IsEqualTo(1)
            .Because("and nothing should have been created.");
    }

    [Test]
    public async Task A_proposal_with_no_credential_does_not_reach_the_network()
    {
        // Refused before the call rather than sent anonymously and refused by
        // the provider - which is the state that produced a sentence blaming a
        // credential nobody had presented.
        var (adapter, handler) = Anonymous(_ =>
            throw new InvalidOperationException("nothing should have been sent"));

        var outcome = await adapter.ProposeAsync(Landing(secret: ""), CancellationToken.None);

        await Assert.That(handler.Seen).IsEmpty();
        await Assert.That(outcome).IsNotTypeOf<LandingOutcome.Landed>();
    }

    [Test]
    public async Task The_credential_reaches_the_header_and_nothing_else()
    {
        // The API half of the rule GitInvocation already holds for git: the
        // secret goes where only the transport reads it. A uri is logged, a
        // body is echoed in a provider's error, and neither may carry it.
        var (adapter, handler) = Anonymous(request => request.Method == HttpMethod.Get
            ? Json(HttpStatusCode.OK, "[]")
            : Json(HttpStatusCode.Created, """{"number":7,"html_url":"https://forge.example/pr/7"}"""));

        await adapter.ProposeAsync(Landing(), CancellationToken.None);

        foreach (var (method, uri, _, body) in handler.Seen)
        {
            await Assert.That(uri.ToString()).DoesNotContain(Secret)
                .Because($"the {method} uri carries the secret, and a uri is the most logged "
                       + "string in any http client.");
            await Assert.That(body).DoesNotContain(Secret)
                .Because("a provider echoes a body back in its error text, and that text is "
                       + "put into a refusal a person reads.");
        }
    }
}
