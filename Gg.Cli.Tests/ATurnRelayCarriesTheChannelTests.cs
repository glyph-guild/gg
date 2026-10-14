using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using Gg.Client;
using Gg.Contracts;
using Gg.Runner;
using SIPSorcery.Net;
using IceServer = Gg.Contracts.IceServer;

namespace Gg.Cli.Tests;

/// <summary>
/// A console and a runner with no direct path between them still talk, through
/// a relay (S78.1-01, ADR-0042).
/// </summary>
/// <remarks>
/// <para>
/// <b>Both ends are limited to relayed candidates</b>, so the only path is the
/// relay. Without that limit two peers on one machine connect host to host and
/// the test proves nothing about a relay. That is the one thing
/// <c>relayOnly</c> exists for.
/// </para>
/// <para>
/// <b>The relay is SIPSorcery's own, in process.</b> It is experimental and
/// unfit for a network anybody depends on (coturn is the real one), but it
/// speaks the same allocate, permission and channel-bind exchange. So a channel
/// that opens here proves the TURN client works in the build we ship. The whole
/// of ADR-0042 rested on that, and it is the first thing to fail if a library
/// upgrade breaks it.
/// </para>
/// <para>
/// <b>The servers come from the introduction</b>, the way the control plane will
/// hand them out. Each channel's own <c>stun-servers</c> is empty, so a channel
/// that ignored what it was handed would find no route.
/// </para>
/// </remarks>
[NotInParallel("a-real-webrtc-handshake")]
public class ATurnRelayCarriesTheChannelTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 14, 2, 0, 0, TimeSpan.Zero);

    private sealed class ALog(params string[] lines) : IReadOnlyLog
    {
        public TailRead Tail(int howMany) =>
            new(lines.TakeLast(howMany).ToArray(), lines.Length > howMany);
    }

    private static int FreePort()
    {
        using var probe = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)probe.Client.LocalEndPoint!).Port;
    }

    private static LeaseGranted Flying() => new()
    {
        LeaseId = "lease-78",
        Generation = 1,
        FlightId = "flight-78",
        FlightNumber = "GG-78",
        Repos = [],
        Credentials = [],
        ClassificationCeiling = Classifications.Internal,
        ClassificationRules = ClassificationRules.Default,
        ExpiresAt = T0.AddMinutes(30),
        RenewWithinSeconds = 30,
    };

    [Test]
    public async Task A_relayed_channel_opens_and_carries_an_answer()
    {
        var port = FreePort();

#pragma warning disable SIPSORCERY001 // a test relay, which is all it is fit for
        var relay = new TurnServer(new TurnServerConfig
        {
            ListenAddress = IPAddress.Loopback,
            Port = port,
            EnableTcp = false,
            Username = "console-or-runner",
            Password = "minted-for-this-test",
            Realm = "gg",
        });
#pragma warning restore SIPSORCERY001

        relay.Start();

        try
        {
            IceServer[] minted =
            [
                new()
                {
                    Urls = [$"turn:127.0.0.1:{port}"],
                    Username = "console-or-runner",
                    Credential = "minted-for-this-test",
                },
            ];

            using var runnerKey = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
            using var consoleKey = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

            var introduction = new RunnerIntroduction
            {
                IntroductionId = "intro-78",
                RunnerId = "01a06385-322f-7371-93a2-ce35db5c4fbe",
                RunnerPublicKey = Convert.ToBase64String(runnerKey.ExportSubjectPublicKeyInfo()),
                Capability = "a-capability",
                ExpiresAt = T0.AddMinutes(1),
                IceServers = minted,
            };

            HandshakeResult answered = new(null, HandshakeFailure.None, "not run", []);

            var console = new ConsoleChannel([], TimeSpan.FromSeconds(20), relayOnly: true);
            var runner = new RunnerChannel(
                [], TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(20), relayOnly: true);

            var reached = await console.ReachAsync(
                introduction,
                consoleKey,
                new PinnedRunnerKeys(Path.Combine(
                    Directory.CreateTempSubdirectory("gg-relay-").FullName,
                    "pinned-runner-keys.json")),
                T0,
                async (offer, ct) =>
                {
                    var says = new WhatThisRunnerSays(
                        new SilentObserver(), _ => new ALog("relayed", "and answered"), () => T0);
                    says.Claimed(Flying());

                    answered = await runner.AnswerAsync(
                        new PendingIntroduction
                        {
                            IntroductionId = "intro-78",
                            Offer = offer,
                            IceServers = minted,
                        },
                        runnerKey,
                        new AskDispatch(says),
                        ct);
                },
                _ => Task.FromResult(answered.Answer is { } a
                    ? new Collected(a, AnswerState.Arrived)
                    : Collected.NotYet));

            await Assert.That(reached.Failure).IsEqualTo(ReachFailure.None)
                .Because($"{reached.Said} The runner offered: "
                       + string.Join(" | ", answered.LocalCandidates));

            using var conversation = reached.Conversation!;
            using var serving = answered.Serving!;

            var said = await conversation.AskAsync(
                new RunnerAsk
                {
                    Kind = RunnerAskKinds.TailLog,
                    TailLog = new TailLogAsk { Lines = 1 },
                },
                TimeSpan.FromSeconds(10));

            await Assert.That(said?.Tail?.Lines).IsEquivalentTo(new[] { "and answered" })
                .Because("an open channel is a claim; an answer that came back over it is proof.");

            await Assert.That(answered.LocalCandidates.All(c => c.Contains("typ relay")))
                .IsTrue()
                .Because("limited to the relay, the runner offers nothing else, or the channel "
                       + "could have opened host to host and proved nothing.");

            await Assert.That(reached.Path).IsEqualTo(ChannelPath.Relayed)
                .Because("the console says which way its packets went.");
            await Assert.That(await serving.Opened).IsEqualTo(HandshakeFailure.None);
            await Assert.That(serving.Path).IsEqualTo(ChannelPath.Relayed)
                .Because("and so does the runner, for the journal a host's owner reads.");
        }
        finally
        {
            relay.Stop();
        }
    }
}
