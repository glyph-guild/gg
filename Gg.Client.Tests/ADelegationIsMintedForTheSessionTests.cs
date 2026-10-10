using System.Net;
using System.Text;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// Reaching a machine remembers the introduction, and a delegation for a session is minted
/// against it - or none, when the control plane refuses (ADR-0039 Amendment 2).
/// </summary>
public class ADelegationIsMintedForTheSessionTests
{
    private sealed class Answering(HttpStatusCode status, string json) : HttpMessageHandler
    {
        internal string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }

    private static ReachAnAgent Reach(Answering handler) => new(
        new ControlPlaneClient(new HttpClient(handler) { BaseAddress = new Uri("https://cp.invalid/") }),
        new ConsoleChannel([], TimeSpan.FromSeconds(1)));

    [Test]
    public async Task The_frame_carries_what_the_control_plane_minted()
    {
        var handler = new Answering(HttpStatusCode.OK,
            """{"delegationId":"d1","token":"t0k3n","expiresAt":"2026-10-10T16:00:00Z"}""");

        var frame = await Reach(handler).DelegationAsync("session", "runner-2", "intro-9", "a1b2");

        await Assert.That(frame!.Token).IsEqualTo("t0k3n");
        await Assert.That(handler.Body).Contains("\"agentSessionId\":\"a1b2\"").And.Contains("\"introductionId\":\"intro-9\"");
    }

    [Test]
    [Arguments(HttpStatusCode.Conflict)]
    [Arguments(HttpStatusCode.NotFound)]
    public async Task A_refusal_is_no_credential_and_the_session_starts_without_one(HttpStatusCode refused)
    {
        var frame = await Reach(new Answering(refused, "{}")).DelegationAsync("session", "runner-2", "intro-9", "a1b2");

        await Assert.That(frame).IsNull();
    }
}
