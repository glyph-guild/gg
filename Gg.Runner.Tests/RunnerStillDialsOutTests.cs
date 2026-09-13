using System.Security.Cryptography;
using System.Text;
using Gg.Contracts;
using Gg.Runner;

namespace Gg.Runner.Tests;

/// <summary>
/// A runner answering a handshake still never listens.
/// </summary>
/// <remarks>
/// <para>
/// <b>The posture the whole transport choice rests on.</b> ADR-0013 chose WebRTC
/// partly because "ICE means neither peer runs a listening service — both dial
/// out and hole-punch — so the runner keeps its outbound-only posture". Without
/// that property this would be a server, and a networking library in the runner
/// would not have been admissible at all.
/// </para>
/// <para>
/// <b>The first version of this test counted the machine's listening sockets
/// before and after, and it was wrong.</b> It passed locally and failed on CI
/// with "New listeners: 45119" — a port belonging to another test assembly,
/// because `dotnet test` runs them as concurrent processes and a machine-wide
/// count attributes anybody's socket to whoever is looking. A measurement that
/// cannot say whose it is cannot support a claim about us.
/// </para>
/// <para>
/// <b>What is attributable is what ICE itself offered.</b> A listening socket in
/// ICE terms is a TCP candidate — <c>typ host tcptype passive</c> is a peer
/// saying "connect to me". So the property is that every candidate this runner
/// offers is UDP, which is exactly "nothing here accepts connections", is
/// deterministic, and belongs to this handshake rather than to the machine.
/// </para>
/// </remarks>
public class RunnerStillDialsOutTests
{
    private static ECDiffieHellman AKey() => ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

    private sealed class NoLog : IReadOnlyLog
    {
        public TailRead Tail(int lines) => new([], false);
    }

    private static AskDispatch ADispatch() =>
        new(new WhatThisRunnerSays(new SilentObserver(), _ => new NoLog(), () => DateTimeOffset.UnixEpoch));

    /// <summary>
    /// A real handshake against a real offer, so gathering actually happens.
    /// </summary>
    /// <remarks>
    /// <b>The first version handed the answerer something it could not parse,
    /// on the reasoning that ICE binds before the description is rejected.</b>
    /// It does - and the method then RETURNS before any candidate has arrived,
    /// so the list was empty about a third of the time and only under load. It
    /// passed alone and failed in the full run, which is the worst way for a
    /// test to be wrong.
    ///
    /// So this makes a genuine offer. It costs a second peer connection and
    /// buys a measurement that is about something.
    /// </remarks>
    private static async Task<HandshakeResult> AfterGatheringAsync()
    {
        var channel = new RunnerChannel([], TimeSpan.FromSeconds(5));

        using var runner = AKey();
        using var ephemeral = AKey();

        using var console = new SIPSorcery.Net.RTCPeerConnection(
            new SIPSorcery.Net.RTCConfiguration { iceServers = [] });

        // THE CHANNEL IS WHAT PUTS AN SCTP M-LINE IN THE OFFER AT ALL, which is
        // the spike's first finding: an offer made without one describes no
        // data channel and the answer has nothing to agree to.
        await console.createDataChannel("tail", null);

        var offer = console.createOffer(null);
        await console.setLocalDescription(offer);

        var sealedOffer = RunnerSeal.SealOffer(
            Convert.ToBase64String(runner.ExportSubjectPublicKeyInfo()),
            ephemeral,
            Encoding.UTF8.GetBytes(console.localDescription.sdp.ToString()));

        return await channel.AnswerAsync(
            new PendingIntroduction
            {
                IntroductionId = "intro-1",
                Offer = new RunnerSealedOffer { Sealed = sealedOffer },
            },
            runner,
            ADispatch());
    }

    [Test]
    public async Task Nothing_this_runner_offers_asks_to_be_connected_to()
    {
        var result = await AfterGatheringAsync();

        await Assert.That(result.LocalCandidates).IsNotEmpty()
            .Because("a runner that gathered nothing proves nothing about what it gathers.");

        var listening = result.LocalCandidates
            .Where(c => c.Contains(" tcp ", StringComparison.OrdinalIgnoreCase)
                     || c.Contains("tcptype", StringComparison.OrdinalIgnoreCase))
            .ToList();

        await Assert.That(listening).IsEmpty()
            .Because("a TCP candidate is a peer saying connect to me, which is the posture ICE "
                   + "was chosen to preserve the absence of. Found: "
                   + string.Join("; ", listening));
    }

    [Test]
    public async Task Every_candidate_says_udp_rather_than_merely_not_saying_tcp()
    {
        // THE OTHER DIRECTION, because "contains no tcp" passes on a line that
        // says nothing at all - an empty string, or a format that changed.
        var result = await AfterGatheringAsync();

        foreach (var candidate in result.LocalCandidates)
        {
            await Assert.That(candidate.Contains(" udp ", StringComparison.OrdinalIgnoreCase))
                .IsTrue()
                .Because($"'{candidate}' does not say what protocol it is, so the check above "
                       + "was reading a shape it did not recognise.");
        }
    }

    [Test]
    public async Task The_scan_would_notice_a_candidate_that_asked_to_be_connected_to()
    {
        // The poison twin, against the line ICE-TCP really produces. Without it
        // both assertions above pass on a matcher that finds nothing ever.
        const string passive =
            "a=candidate:1 1 tcp 2105524479 10.0.4.17 9 typ host tcptype passive";
        const string dialling =
            "a=candidate:2 1 udp 2113937663 10.0.4.17 54321 typ host generation 0";

        await Assert.That(passive.Contains(" tcp ", StringComparison.OrdinalIgnoreCase)).IsTrue();
        await Assert.That(passive.Contains("tcptype", StringComparison.OrdinalIgnoreCase)).IsTrue();
        await Assert.That(dialling.Contains(" tcp ", StringComparison.OrdinalIgnoreCase)).IsFalse()
            .Because("the ordinary candidate must not be flagged, or the rule refuses every "
                   + "handshake.");
    }
}
