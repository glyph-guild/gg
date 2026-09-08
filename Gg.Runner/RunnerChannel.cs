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
/// <param name="LocalCandidates">
/// The ways this runner offered to be reached, as ICE candidate lines.
/// </param>
/// <remarks>
/// <b>The candidates are reported because a person debugging needs them and
/// because a test needs them.</b> "No route between us" is a sentence somebody
/// can only act on if they can see what each end offered - one host candidate
/// and no server-reflexive one is a STUN problem, not a firewall - and it is
/// also the only attributable way to check that nothing here listens, since a
/// machine-wide socket count belongs to whatever else is running.
/// </remarks>
public sealed record HandshakeResult(
    RunnerSealedAnswer? Answer,
    HandshakeFailure Failure,
    string Said,
    IReadOnlyList<string> LocalCandidates,
    Served? Serving = null);

/// <summary>
/// The peer a runner holds open for the console that asked for it.
/// </summary>
/// <remarks>
/// <para>
/// <b>This exists because a handshake and a conversation have different
/// lifetimes, and for one commit they did not.</b> <see cref="RunnerChannel"/>
/// held its peer connection in a <c>using</c>, so it was disposed the instant
/// the answer was returned — before the console had collected it, let alone
/// connected. The method said it served the channel it brought and could not
/// serve anything, and every test read the candidates off the returned record
/// and so never noticed. A handshake half is testable with no far end; SERVING
/// is not.
/// </para>
/// <para>
/// <b>Arrival is bounded and the conversation is not.</b> The bound is the
/// introduction's own lifetime: a console that has not turned up before the
/// thing that authorised it expired is not going to, and holding a peer for it
/// would let anybody who can open introductions pin a runner's memory by going
/// quiet. Once somebody is actually on the other end, how long they stay is
/// theirs — a person reading a log is not doing anything wrong by reading it
/// slowly — so the owner disposes this when the conversation is over.
/// </para>
/// </remarks>
public sealed class Served : IDisposable
{
    private readonly RTCPeerConnection _peer;
    private readonly CancellationTokenSource _letGo = new();

    internal Served(RTCPeerConnection peer, Task opened, TimeSpan arrivalBound)
    {
        _peer = peer;
        Opened = WaitAsync(peer, opened, arrivalBound, _letGo.Token);
    }

    /// <summary>How it turned out: <c>None</c>, or why nobody arrived.</summary>
    /// <remarks>
    /// <b>The two failures here are ones only this side can tell apart.</b> ICE
    /// never connecting is a network; ICE connecting with no channel on it is
    /// us. Both were unreachable while the peer was disposed at return, which is
    /// what an enum member no code path can produce usually means.
    /// </remarks>
    public Task<HandshakeFailure> Opened { get; }

    private static async Task<HandshakeFailure> WaitAsync(
        RTCPeerConnection peer, Task opened, TimeSpan arrivalBound, CancellationToken letGo)
    {
        if (await Task.WhenAny(opened, Task.Delay(arrivalBound, letGo)) == opened)
        {
            return HandshakeFailure.None;
        }

        // SIPSorcery's ICE states stop at `connected`; there is no `completed`,
        // so this is the whole of "a route was found".
        var why = peer.iceConnectionState is RTCIceConnectionState.connected
            ? HandshakeFailure.ChannelNeverOpened
            : HandshakeFailure.NoRouteBetweenUs;

        peer.close();
        return why;
    }

    public void Dispose()
    {
        _letGo.Cancel();
        _letGo.Dispose();
        _peer.close();
    }
}

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
    IReadOnlyList<string> stunServers, TimeSpan patience, TimeSpan? arrivalBound = null)
{
    /// <summary>
    /// How long a peer is held for a console that has not turned up.
    /// </summary>
    /// <remarks>
    /// <b>One minute because that is what an introduction lasts.</b> The control
    /// plane mints them with a sixty second life, so a console that has not
    /// arrived by then is holding something that no longer authorises it.
    /// Anything longer is a way to make a runner keep peers for conversations
    /// that will not happen.
    /// </remarks>
    private readonly TimeSpan _arrivalBound = arrivalBound ?? TimeSpan.FromMinutes(1);

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
              + $"different runner, or something rewrote it in transit: {opening.Message}",
                []);
        }

        var offer = Encoding.UTF8.GetString(offerBytes);

        // NOT A `using`. This peer has to outlive the method that made it: the
        // console cannot connect until the answer has been relayed, which cannot
        // happen until this returns. Every path that does not hand it over
        // closes it below.
        var peer = new RTCPeerConnection(new RTCConfiguration
        {
            iceServers = [.. stunServers.Select(u => new RTCIceServer { urls = u })],
        });

        // WHICH LEAVES CANCELLATION AS THE ONE EXIT NOT WRITTEN HERE. The
        // registration is disposed on the way out either way, so a cancelled
        // handshake closes its peer and a served one is not closed by a token
        // whose conversation has outlived this method.
        await using var closeItIfWeAreStopped = cancellationToken.Register(peer.close);

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
            peer.close();

            return new HandshakeResult(
                null, HandshakeFailure.OfferWasNotSdp,
                "The offer opened and was not something this runner could answer. The seal is "
              + "fine and what was inside it is not.",
                Offered(gathered));
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
            "answered",
            Offered(gathered),
            // THE COUNTDOWN STARTS HERE rather than at the peer's creation,
            // because gathering has just spent some of the caller's patience and
            // the console has not been told anything yet.
            new Served(peer, opened.Task, _arrivalBound));
    }

    private static IReadOnlyList<string> Offered(List<string> gathered)
    {
        lock (gathered)
        {
            return [.. gathered];
        }
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
