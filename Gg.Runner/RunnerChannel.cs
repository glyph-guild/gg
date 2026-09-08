using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Gg.Contracts;
using SIPSorcery.Net;

namespace Gg.Runner;

/// <summary>Why a handshake did not happen, in words a person can act on.</summary>
/// <remarks>
/// <b>Which END gave up is the whole content of this type.</b> ADR-0013's build
/// plan asks for it by name, and the reason is that "no log" and "could not
/// reach the runner" are two different sentences: one sends somebody to look at
/// a machine and the other sends them to look at a network. A single failure
/// string would collapse them, which is this system's named worst failure
/// wearing an error message.
/// </remarks>
public enum HandshakeFailure
{
    /// <summary>It worked.</summary>
    None,

    /// <summary>The offer would not open. Somebody else's key, or a rewritten frame.</summary>
    OfferWouldNotOpen,

    /// <summary>The offer opened and was not an offer.</summary>
    OfferWasNotSdp,

    /// <summary>Neither end found a way through. This is the network.</summary>
    NoRouteBetweenUs,

    /// <summary>A route was found and the channel never opened. This is us.</summary>
    ChannelNeverOpened,
}

/// <summary>What answering an introduction produced.</summary>
public sealed record HandshakeResult(
    RunnerSealedAnswer? Answer, HandshakeFailure Failure, string Said);

/// <summary>
/// The runner's half of a handshake: it answers, and it never listens.
/// </summary>
/// <remarks>
/// <para>
/// <b>ICE is what makes this possible at all.</b> Neither peer runs a listening
/// service — both dial out and hole-punch — so a runner keeps the outbound-only
/// posture every other thing it does already has. Measured in step 0 between a
/// laptop behind a home NAT and a hosted machine behind a stateful firewall,
/// with no rule added anywhere: both sides nominated the peer's
/// server-reflexive candidate.
/// </para>
/// <para>
/// <b>Candidates are collected from the event, not read back off the local
/// description.</b> That is the step 0 spike's most expensive lesson: SIPSorcery
/// surfaces gathered candidates through <c>onicecandidate</c> and does not
/// rewrite <c>localDescription</c>, so reading it back produces an answer
/// carrying only a host candidate — which fails exactly the way a blocked
/// network does, and took two runs to tell apart.
/// </para>
/// <para>
/// <b>The data channel is answered, never opened.</b> The console creates it,
/// because the side that creates a channel is the side that decides what
/// channels exist — and a runner that could open one would be a runner that
/// could start a conversation nobody asked for.
/// </para>
/// </remarks>
public sealed class RunnerChannel(
    IReadOnlyList<string> stunServers, TimeSpan patience)
{
    /// <summary>
    /// Opens one introduction, answers it, and serves the channel it brings.
    /// </summary>
    /// <remarks>
    /// <b>The answer is sealed to the key the OFFER carried.</b> The runner has
    /// no other way to know who to reply to, which is why
    /// <see cref="RunnerSeal.OpenOffer"/> hands both back at once.
    /// </remarks>
    public async Task<HandshakeResult> AnswerAsync(
        PendingIntroduction pending,
        ECDiffieHellman runnerKey,
        AskDispatch dispatch,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pending);
        ArgumentNullException.ThrowIfNull(dispatch);

        byte[] offerBytes;
        string theirEphemeralKey;

        try
        {
            (offerBytes, theirEphemeralKey) =
                RunnerSeal.OpenOffer(runnerKey, pending.Offer.Sealed);
        }
        catch (CryptographicException opening)
        {
            // THE FIRST THING THAT CAN GO WRONG IS NOT A NETWORK PROBLEM, and
            // saying so here is what stops somebody chasing a firewall over a
            // substituted key.
            return new HandshakeResult(
                null, HandshakeFailure.OfferWouldNotOpen,
                "The offer would not open with this runner's key. Either it was sealed to a "
              + $"different runner, or something rewrote it in transit: {opening.Message}");
        }

        var offer = Encoding.UTF8.GetString(offerBytes);

        using var peer = new RTCPeerConnection(new RTCConfiguration
        {
            iceServers = [.. stunServers.Select(u => new RTCIceServer { urls = u })],
        });

        var opened = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        // ANSWERED, NEVER OPENED. The console creates the channel; this side
        // serves whatever arrives on it and can start nothing of its own.
        peer.ondatachannel += channel =>
        {
            channel.onopen += () => opened.TrySetResult(true);
            channel.onmessage += (_, _, data) => Serve(dispatch, channel, data);
        };

        var gathered = new List<string>();
        peer.onicecandidate += candidate =>
        {
            lock (gathered)
            {
                gathered.Add($"a=candidate:{candidate}");
            }
        };

        if (peer.setRemoteDescription(new RTCSessionDescriptionInit
            {
                type = RTCSdpType.offer,
                sdp = offer,
            }) is not SetDescriptionResultEnum.OK)
        {
            return new HandshakeResult(
                null, HandshakeFailure.OfferWasNotSdp,
                "The offer opened and was not something this runner could answer. The seal is "
              + "fine and what was inside it is not.");
        }

        var answer = peer.createAnswer(null);
        await peer.setLocalDescription(answer);

        var deadline = DateTimeOffset.UtcNow + patience;

        while (peer.iceGatheringState != RTCIceGatheringState.complete
            && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        }

        // COLLECTED FROM THE EVENT, NOT READ BACK. The step 0 lesson: reading
        // localDescription here yields an answer with one host candidate, which
        // is indistinguishable from a network that refused.
        var sdp = peer.localDescription.sdp.ToString().TrimEnd('\r', '\n');

        lock (gathered)
        {
            var missing = gathered
                .Where(c => !sdp.Contains(c, StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal);

            sdp = sdp + "\r\n" + string.Join("\r\n", missing) + "\r\n";
        }

        return new HandshakeResult(
            new RunnerSealedAnswer
            {
                RunnerId = pending.IntroductionId,
                Sealed = RunnerSeal.SealAnswer(
                    theirEphemeralKey, runnerKey, Encoding.UTF8.GetBytes(sdp)),
            },
            HandshakeFailure.None,
            "answered");
    }

    /// <summary>
    /// Answers one message on the channel, or says nothing at all.
    /// </summary>
    /// <remarks>
    /// <b>A message this runner cannot parse is dropped exactly as an unknown
    /// kind is.</b> Sending an error back would be a second shape on a channel
    /// whose whole defence is that it carries two — and a peer that can make a
    /// runner emit anything it did not ask for has a channel wider than the
    /// vocabulary says.
    /// </remarks>
    private static void Serve(AskDispatch dispatch, RTCDataChannel channel, byte[] data)
    {
        RunnerAsk? ask;

        try
        {
            ask = JsonSerializer.Deserialize(data, ChannelJson.Default.RunnerAsk);
        }
        catch (JsonException)
        {
            return;
        }

        if (ask is null || dispatch.Answer(ask) is not { } said)
        {
            return;
        }

        channel.send(JsonSerializer.SerializeToUtf8Bytes(said, ChannelJson.Default.RunnerSaid));
    }
}

/// <summary>What crosses the data channel, and nothing else.</summary>
/// <remarks>
/// <b>Its own context rather than the protocol's.</b> <c>RunnerJsonContext</c>
/// is what a runner says to the CONTROL PLANE; this is what it says to a person's
/// console over a channel the ADR calls hostile. Keeping them apart means a type
/// added to one is not silently serialisable over the other.
/// </remarks>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(RunnerAsk))]
[JsonSerializable(typeof(RunnerSaid))]
public sealed partial class ChannelJson : JsonSerializerContext;
