using System.Net;

namespace Gg.Client.Tests;

/// <summary>
/// Every request gg makes states its protocol version, including the one it makes
/// with no credential.
/// </summary>
/// <remarks>
/// <para>
/// <b>Found on a real pool host, by a member that could not start.</b> The
/// container had everything it needed — the control-plane address and a nonce, put
/// there by the resident runner — reached the redeem call, and died:
/// </para>
/// <code>
/// Unhandled exception. Gg.Client.ProtocolTooOldException: This gg is too old for
/// the control plane. State GG-Protocol-Version. This control plane speaks 1..1.
///    at Gg.Client.ControlPlaneClient.RedeemMemberAsync
/// </code>
/// <para>
/// <b>The cause was building the request by hand.</b> Redeeming is the one call
/// with no session, and reaching for <c>new HttpRequestMessage(...)</c> rather than
/// the helper skipped the protocol header along with the credential — even though
/// that helper's session token has always been optional.
/// </para>
/// <para>
/// <b>Which makes this a test about the helper, not about one call.</b> The
/// refusal it produces is also the most misleading one in the protocol: <i>"this gg
/// is too old"</i>, on a binary built minutes earlier from the same commit as the
/// control plane.
/// </para>
/// </remarks>
public class RedeemStatesItsProtocolTests
{
    /// <summary>Captures the request and answers as the control plane would.</summary>
    private sealed class Capturing(HttpStatusCode status) : HttpMessageHandler
    {
        internal HttpRequestMessage? Seen { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Seen = request;

            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(
                    """{"runnerId":"a-member","runnerToken":"a-token","labels":[],"expiresAt":"2026-09-04T00:00:00+00:00"}""",
                    System.Text.Encoding.UTF8,
                    "application/json"),
            });
        }
    }

    private static ControlPlaneClient Against(Capturing handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://control.example.invalid") });

    [Test]
    public async Task Redeeming_states_the_protocol_version()
    {
        // THE DEFECT. Without the header the control plane refuses at the floor,
        // and the member dies before it can become anybody.
        var handler = new Capturing(HttpStatusCode.OK);

        _ = await Against(handler).RedeemMemberAsync("a-nonce");

        await Assert.That(handler.Seen!.Headers.Contains(GgVersions.ProtocolHeader))
            .IsTrue()
            .Because("a control plane that cannot see a version refuses at the floor, and the "
                   + "refusal says 'this gg is too old' about a binary built minutes ago.");
    }

    [Test]
    public async Task Redeeming_carries_no_credential_of_any_kind()
    {
        // THE ANCHOR, and the reason the request was hand-built in the first
        // place. A member has nothing to present; the nonce in the body IS the
        // authorization, and a session header here would be a bootstrap that
        // cannot start.
        var handler = new Capturing(HttpStatusCode.OK);

        _ = await Against(handler).RedeemMemberAsync("a-nonce");

        await Assert.That(handler.Seen!.Headers.Contains("X-Gg-Session")).IsFalse();
        await Assert.That(handler.Seen.Headers.Authorization).IsNull();
    }

    [Test]
    public async Task A_nonce_that_buys_nothing_answers_null_rather_than_throwing()
    {
        // Never minted, expired and already redeemed answer alike on purpose.
        // The caller reports a member that could not start; it does not retry,
        // because a spent nonce does not become unspent.
        var handler = new Capturing(HttpStatusCode.Conflict);

        await Assert.That(await Against(handler).RedeemMemberAsync("a-spent-nonce")).IsNull();
    }
}
