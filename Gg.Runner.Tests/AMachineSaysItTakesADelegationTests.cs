using System.Net;
using System.Text;
using Gg.Runner;

namespace Gg.Runner.Tests;

/// <summary>
/// A machine that takes agent sessions says on its heartbeat that it hands a session a
/// delegated credential, so a console mints one only for a machine that will use it
/// (ADR-0039 Amendment 2, Decision 13).
/// </summary>
public class AMachineSaysItTakesADelegationTests
{
    private sealed class Capturing : HttpMessageHandler
    {
        internal string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json"),
            };
        }
    }

    [Test]
    [Arguments(true, "\"takesAgentDelegation\":true")]
    [Arguments(null, "takesAgentDelegation")]
    public async Task The_beat_says_it_with_the_sessions(bool? accepts, string expected)
    {
        var handler = new Capturing();
        var client = new RunnerProtocolClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://cp.invalid/") }, "runner-token");

        _ = await client.HeartbeatAsync("runner-1", [], acceptsConfiguration: null, acceptsAgentSessions: accepts);

        if (accepts is true)
        {
            await Assert.That(handler.Body).Contains(expected);
        }
        else
        {
            await Assert.That(handler.Body).DoesNotContain(expected)
                .Because("a machine that takes no sessions has no session to hand a credential to.");
        }
    }
}
