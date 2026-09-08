using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Gg.Contracts;
using SIPSorcery.Net;

namespace Gg.Client;

/// <summary>Why a person did not get to see a runner, in words they can act on.</summary>
/// <remarks>
/// <b>Each of these sends somebody somewhere different.</b> A changed key is a
/// person to talk to; no route is a network; a silent runner is a machine. A
/// single "could not connect" would send them to all three in turn, starting
/// with the wrong one.
/// </remarks>
public enum ReachFailure
{
    /// <summary>It worked.</summary>
    None,

    /// <summary>The runner's key is not the one this console pinned.</summary>
    KeyChanged,

    /// <summary>The runner never answered the introduction.</summary>
    RunnerNeverAnswered,

    /// <summary>It answered and this console could not open what it sent.</summary>
    AnswerWouldNotOpen,

    /// <summary>Both ends spoke and no route between them was found.</summary>
    NoRouteBetweenUs,
}

/// <summary>What reaching a runner produced.</summary>
public sealed record Reached(
    Conversation? Conversation, ReachFailure Failure, string Said);

/// <summary>
/// A console asking one runner about itself, for as long as the channel lasts.
/// </summary>
/// <remarks>
/// <b>One ask in flight at a time, deliberately.</b> The protocol is a request
/// and a bounded response with no correlation id, because a correlation id is
/// the first half of multiplexing and multiplexing is the first half of a
/// general channel. A person watching a log does not need two questions at once.
/// </remarks>
public sealed class Conversation(RTCPeerConnection peer, RTCDataChannel channel) : IDisposable
{
    private readonly SemaphoreSlim _oneAtATime = new(1, 1);

    /// <summary>Asks, and waits for the one answer.</summary>
    public async Task<RunnerSaid?> AskAsync(
        RunnerAsk ask, TimeSpan patience, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ask);

        await _oneAtATime.WaitAsync(cancellationToken);

        try
        {
            var said = new TaskCompletionSource<RunnerSaid?>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            void Heard(RTCDataChannel _, DataChannelPayloadProtocols __, byte[] data)
            {
                try
                {
                    said.TrySetResult(
                        JsonSerializer.Deserialize(data, ConsoleChannelJson.Default.RunnerSaid));
                }
                catch (JsonException)
                {
                    // A RUNNER IS HOSTILE AND MAY SEND ANYTHING. Nothing it can
                    // send should make this console throw where a person is
                    // waiting; an unreadable answer is no answer.
                    said.TrySetResult(null);
                }
            }

            channel.onmessage += Heard;

            try
            {
                channel.send(
                    JsonSerializer.SerializeToUtf8Bytes(ask, ConsoleChannelJson.Default.RunnerAsk));

                using var patient = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                patient.CancelAfter(patience);

                var finished = await Task.WhenAny(
                    said.Task, Task.Delay(Timeout.Infinite, patient.Token));

                return finished == said.Task ? await said.Task : null;
            }
            finally
            {
                channel.onmessage -= Heard;
            }
        }
        finally
        {
            _oneAtATime.Release();
        }
    }

    public void Dispose()
    {
        _oneAtATime.Dispose();
        peer.close();
    }
}

/// <summary>
/// The console's half of a handshake: it offers, and it decides what channels exist.
/// </summary>
/// <remarks>
/// <para>
/// <b>The console creates the data channel, and that is where the vocabulary is
/// enforced from.</b> A runner answers whatever arrives and can open nothing of
/// its own, so the set of channels that exist is decided by the side a person is
/// sitting at.
/// </para>
/// <para>
/// <b>The pin is checked before anything is sealed, not after.</b> Sealing to a
/// key and then noticing it changed would have sent the offer already — with the
/// candidates inside it, to whoever substituted the key.
/// </para>
/// </remarks>
public sealed class ConsoleChannel(IReadOnlyList<string> stunServers, TimeSpan patience)
{
    /// <summary>
    /// Reaches one runner, through an introduction the control plane minted.
    /// </summary>
    /// <param name="ephemeral">
    /// The key whose public half was sent to mint <paramref name="introduction"/>.
    /// </param>
    /// <param name="leaveAsync">Leaves the sealed offer where the runner will find it.</param>
    /// <param name="collectAsync">Asks whether the runner has answered yet.</param>
    /// <remarks>
    /// <b>The ephemeral key is PASSED IN rather than made here, and that is not
    /// a style choice.</b> <c>RunnerIntroductionRequest</c> carries the console's
    /// ephemeral public key, and the control plane stores its hash against the
    /// introduction — so a console that mints an introduction with one key and
    /// seals with another has declared something it did not do. Generating a
    /// second key here would compile, work today, and be wrong the moment
    /// anything checks the binding it already writes down.
    /// </remarks>
    public async Task<Reached> ReachAsync(
        RunnerIntroduction introduction,
        ECDiffieHellman ephemeral,
        PinnedRunnerKeys pins,
        DateTimeOffset now,
        Func<RunnerSealedOffer, CancellationToken, Task> leaveAsync,
        Func<CancellationToken, Task<RunnerSealedAnswer?>> collectAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(introduction);
        ArgumentNullException.ThrowIfNull(ephemeral);
        ArgumentNullException.ThrowIfNull(pins);
        ArgumentNullException.ThrowIfNull(leaveAsync);
        ArgumentNullException.ThrowIfNull(collectAsync);

        // BEFORE ANYTHING IS SEALED. Sealing first and checking after would have
        // sent the offer - candidates and all - to whoever substituted the key.
        if (pins.Check(introduction.RunnerId, introduction.RunnerPublicKey, now)
            is PinVerdict.Changed)
        {
            return new Reached(
                null, ReachFailure.KeyChanged,
                $"The key for runner {introduction.RunnerId} is not the one this console pinned. "
              + "A reinstall changes it legitimately and so does somebody substituting it, and "
              + "nothing here can tell which - so nothing was sent. If the machine was rebuilt, "
              + $"run: gg runner repin {introduction.RunnerId}");
        }

        var peer = new RTCPeerConnection(new RTCConfiguration
        {
            iceServers = [.. stunServers.Select(u => new RTCIceServer { urls = u })],
        });

        // EVERY FAILURE BELOW CLOSES THE PEER, and cancellation is the exit none
        // of them is written for. The registration is disposed on the way out,
        // so a conversation that outlives this method is not closed by a token
        // that meant "stop reaching".
        await using var closeItIfWeAreStopped = cancellationToken.Register(peer.close);

        var opened = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        // CREATED BEFORE THE OFFER, because its existence is what puts an SCTP
        // m-line in the SDP at all - the spike's first finding.
        var channel = await peer.createDataChannel("tail", null);
        channel.onopen += () => opened.TrySetResult(true);

        var gathered = new List<string>();
        peer.onicecandidate += candidate =>
        {
            lock (gathered)
            {
                gathered.Add($"a=candidate:{candidate}");
            }
        };

        var offer = peer.createOffer(null);
        await peer.setLocalDescription(offer);

        var deadline = now + patience;
        await GatheredAsync(peer, cancellationToken);

        await leaveAsync(
            new RunnerSealedOffer
            {
                Sealed = RunnerSeal.SealOffer(
                    introduction.RunnerPublicKey,
                    ephemeral,
                    Encoding.UTF8.GetBytes(WithCandidates(peer, gathered))),
            },
            cancellationToken);

        var answer = await WaitForAnswerAsync(collectAsync, cancellationToken);

        if (answer is null)
        {
            peer.close();
            return new Reached(
                null, ReachFailure.RunnerNeverAnswered,
                "The runner did not answer. It may be offline, or between heartbeats, or the "
              + "introduction expired before it came back - an introduction lasts a minute and a "
              + "runner picks one up within a second of being told there is one.");
        }

        byte[] answerSdp;

        try
        {
            // OPENED WITH THE KEY THIS CONSOLE PINNED, never the one that
            // arrived: opening with what came back would authenticate the
            // message against itself.
            answerSdp = RunnerSeal.OpenAnswer(
                ephemeral, introduction.RunnerPublicKey, answer.Sealed);
        }
        catch (CryptographicException opening)
        {
            peer.close();
            return new Reached(
                null, ReachFailure.AnswerWouldNotOpen,
                "The runner answered and this console could not open what it sent. That is a "
              + "different machine answering, or something rewriting the answer in transit: "
              + opening.Message);
        }

        peer.setRemoteDescription(new RTCSessionDescriptionInit
        {
            type = RTCSdpType.answer,
            sdp = Encoding.UTF8.GetString(answerSdp),
        });

        var finished = await Task.WhenAny(
            opened.Task, Task.Delay(Later(deadline, now), cancellationToken));

        if (finished != opened.Task)
        {
            peer.close();
            return new Reached(
                null, ReachFailure.NoRouteBetweenUs,
                "Both ends spoke and no route between them was found. This is the network "
              + "rather than either machine: hole punching needs one side's traffic to reach "
              + "the other, and something in between is refusing it.");
        }

        return new Reached(new Conversation(peer, channel), ReachFailure.None, "reached");
    }

    private static TimeSpan Later(DateTimeOffset deadline, DateTimeOffset from) =>
        deadline > from ? deadline - from : TimeSpan.FromSeconds(1);

    private async Task GatheredAsync(RTCPeerConnection peer, CancellationToken cancellationToken)
    {
        var until = DateTimeOffset.UtcNow + patience;

        while (peer.iceGatheringState != RTCIceGatheringState.complete
            && DateTimeOffset.UtcNow < until)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        }
    }

    /// <summary>
    /// The description with the candidates the event reported.
    /// </summary>
    /// <remarks>
    /// <b>The step 0 lesson, on this side too.</b> SIPSorcery does not rewrite
    /// <c>localDescription</c> as candidates arrive, so reading it back offers
    /// one host candidate - which fails exactly the way a blocked network does.
    /// </remarks>
    private static string WithCandidates(RTCPeerConnection peer, List<string> gathered)
    {
        var sdp = peer.localDescription.sdp.ToString().TrimEnd('\r', '\n');

        lock (gathered)
        {
            var missing = gathered
                .Where(c => !sdp.Contains(c, StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal);

            return sdp + "\r\n" + string.Join("\r\n", missing) + "\r\n";
        }
    }

    private async Task<RunnerSealedAnswer?> WaitForAnswerAsync(
        Func<CancellationToken, Task<RunnerSealedAnswer?>> collectAsync,
        CancellationToken cancellationToken)
    {
        var until = DateTimeOffset.UtcNow + patience;

        while (DateTimeOffset.UtcNow < until)
        {
            if (await collectAsync(cancellationToken) is { } answer)
            {
                return answer;
            }

            // POLLED RATHER THAN PUSHED, because the control plane is not in
            // this conversation - it holds an answer until somebody collects it,
            // and a push would make it a participant.
            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
        }

        return null;
    }
}

/// <summary>What crosses the data channel, from this side.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(RunnerAsk))]
[JsonSerializable(typeof(RunnerSaid))]
public sealed partial class ConsoleChannelJson : JsonSerializerContext;
