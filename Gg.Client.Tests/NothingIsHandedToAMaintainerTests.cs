using System.Net;
using System.Text;
using System.Text.Json;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// A runner the fleet reads as <c>maintaining</c> is refused before anything is
/// minted, by every verb that reaches a machine.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is alive and it cannot be reached.</b> A pool maintainer reports on
/// its pool every few seconds and never beats, and an introduction is picked up
/// on a heartbeat. Treating it as reachable because it is not <c>offline</c>
/// buys twenty seconds of silence and then a sentence about a machine that is
/// fine.
/// </para>
/// <para>
/// <b>Nothing past the fleet read.</b> The handler answers the fleet and
/// nothing else, so an introduction attempted anyway shows up in what was
/// asked.
/// </para>
/// </remarks>
public class NothingIsHandedToAMaintainerTests
{

    /// <summary>A key and an envelope, for a test that never gets as far as using either.</summary>
    /// <remarks>
    /// The refusal here happens BEFORE an introduction is minted, so nothing is
    /// ever rewrapped - these exist only to satisfy the signature, which is
    /// itself the point: there is no overload that takes a plaintext value.
    /// </remarks>
    private static System.Security.Cryptography.ECDiffieHellman AKey() =>
        System.Security.Cryptography.ECDiffieHellman.Create(
            System.Security.Cryptography.ECCurve.NamedCurves.nistP256);

    /// <summary>The same key, in the shape a send now asks for.</summary>
    /// <remarks>
    /// A send takes a HOLDER rather than a key (ADR-0037 Decision 2), so that the
    /// thing performing the rewrap can be a person's key or a card. This wraps a
    /// key this test holds, which is what the machine path does.
    /// </remarks>
    private static Gg.Contracts.IAgreeAsAHolder AHolder() =>
        Gg.Contracts.RunnerSeal.AsAHolder(AKey());

    private static Gg.Contracts.SealedCredential Sealed() =>
        Gg.Contracts.CredentialSeal.Seal(
            "not-a-real-secret",
            [Convert.ToBase64String(AKey().PublicKey.ExportSubjectPublicKeyInfo())]);
    private const string Maintainer = "01a0632b-e971-7000-8000-000000000000";

    private static readonly DateTimeOffset Now = new(2026, 9, 17, 4, 0, 0, TimeSpan.Zero);

    private sealed class OnlyTheFleet : HttpMessageHandler
    {
        internal List<string> Asked { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Asked.Add($"{request.Method} {request.RequestUri!.AbsolutePath}");

            if (request.Method != HttpMethod.Get || request.RequestUri.AbsolutePath != "/v1/runners")
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
            }

            var body = JsonSerializer.Serialize(
                new RunnerList
                {
                    Runners =
                    [
                        new RunnerSummary
                        {
                            RunnerId = Maintainer,
                            Label = "vmlinux001:maintain",
                            State = "maintaining",
                            LastHeartbeatAt = Now.AddSeconds(-4),
                        },
                    ],
                },
                ProtocolJsonContext.Default.RunnerList);

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class NoPrompt : ISecretPrompt
    {
        public string ReadSecret(string prompt) =>
            throw new InvalidOperationException("nothing should be asked for");

        public string ReadLine(string prompt) =>
            throw new InvalidOperationException("nothing should be asked for");
    }

    private static (ControlPlaneClient Control, ConsoleChannel Channel, OnlyTheFleet Handler,
        PinnedRunnerKeys Pins) Parts()
    {
        var handler = new OnlyTheFleet();
        var control = new ControlPlaneClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://cp.invalid/") });
        var pins = new PinnedRunnerKeys(
            Path.Combine(Path.GetTempPath(), $"gg-pins-{Guid.NewGuid():N}.json"));
        return (control, new ConsoleChannel([], TimeSpan.FromSeconds(1)), handler, pins);
    }

    [Test]
    public async Task Watching_a_maintainer_is_refused_before_an_introduction()
    {
        var (control, channel, handler, pins) = Parts();

        var watched = await new WatchARunner(control, channel).WatchAsync(
            "session", Maintainer, pins, lines: 20, Now, _ => { }, follow: false);

        await Assert.That(watched.Outcome).IsEqualTo(WatchOutcome.Offline)
            .Because("it is not beating, which is what that outcome says. Said: " + watched.Said);
        await Assert.That(watched.Said).Contains("maintains");
        await Assert.That(handler.Asked).IsEquivalentTo(new List<string> { "GET /v1/runners" });
    }

    [Test]
    public async Task Sending_a_maintainer_a_credential_is_refused_before_an_introduction()
    {
        var (control, channel, handler, pins) = Parts();

        var sent = await new SendACredential(control, channel).SendAsync(
            "session", Maintainer, "secret://acme/widgets", Sealed(), AHolder(), pins, Now);

        await Assert.That(sent.Outcome).IsEqualTo(SendOutcome.Offline)
            .Because("a secret typed for a machine nothing can reach is a secret typed for "
                   + "nothing. Said: " + sent.Said);
        await Assert.That(sent.Said).Contains("maintains");
        await Assert.That(handler.Asked).IsEquivalentTo(new List<string> { "GET /v1/runners" });
    }

    [Test]
    public async Task Logging_a_maintainers_agent_in_is_refused_before_an_introduction()
    {
        var (control, channel, handler, pins) = Parts();

        var loggedIn = await new LogAnAgentIn(control, channel).LoginAsync(
            "session", Maintainer, "claude", pins, Now, new NoPrompt());

        await Assert.That(loggedIn.Outcome).IsEqualTo(LoginOutcome.Offline)
            .Because("a maintainer holds no agent to log in. Said: " + loggedIn.Said);
        await Assert.That(loggedIn.Said).Contains("maintains");
        await Assert.That(handler.Asked).IsEquivalentTo(new List<string> { "GET /v1/runners" });
    }
}
