using System.Net;
using System.Text;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// The client asks for a delegated session at the declared route (ADR-0039 Amendment 2).
/// </summary>
public class ADelegationIsAskedForTests
{
    private sealed class Answering : HttpMessageHandler
    {
        internal HttpRequestMessage? Asked { get; private set; }

        internal string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Asked = request;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"delegationId":"d1","token":"t0k3n","expiresAt":"2026-10-10T16:00:00Z"}""",
                    Encoding.UTF8, "application/json"),
            };
        }
    }

    [Test]
    public async Task It_posts_the_introduction_and_reads_back_the_delegation()
    {
        var handler = new Answering();
        var client = new ControlPlaneClient(new HttpClient(handler) { BaseAddress = new Uri("https://cp.invalid/") });

        var delegation = await client.DelegateAsync(
            "session", new AgentDelegationRequest { RunnerId = "r2", AgentSessionId = "a1b2", IntroductionId = "i9" });

        await Assert.That(handler.Asked!.Method).IsEqualTo(HttpMethod.Post);
        await Assert.That(handler.Asked.RequestUri!.AbsolutePath).IsEqualTo("/v1/auth/delegations");
        await Assert.That(handler.Body).Contains("\"introductionId\":\"i9\"");
        await Assert.That(delegation.Token).IsEqualTo("t0k3n");
    }
}
