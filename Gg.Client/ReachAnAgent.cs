using System.Security.Cryptography;
using Gg.Contracts;
using SIPSorcery.Net;

namespace Gg.Client;

/// <summary>
/// An `agent` channel as frames: what a console sends a machine's session, and what
/// the machine sends back (slice seventy, ADR-0039).
/// </summary>
/// <remarks>
/// <b>An interface, so the mux holds no WebRTC.</b> The console's mux draws a remote
/// session through this and nothing else; the data channel behind it is this
/// project's, and a test's double is a list.
/// </remarks>
public interface IAgentLink : IDisposable
{
    void Send(AgentFrame frame);

    /// <summary>A frame arrived from the machine.</summary>
    event Action<AgentFrame>? Heard;

    /// <summary>The channel is gone, and nothing more will arrive.</summary>
    event Action? Closed;
}

/// <summary>An <see cref="IAgentLink"/> over a reached data channel.</summary>
public sealed class ChannelAgentLink : IAgentLink
{
    private readonly Conversation _conversation;
    private readonly RTCDataChannel _channel;

    public ChannelAgentLink(Conversation conversation)
    {
        ArgumentNullException.ThrowIfNull(conversation);

        _conversation = conversation;
        _channel = conversation.Channel;
        _channel.onmessage += (_, _, data) =>
        {
            if (AgentFrameCodec.Decode(data) is { } frame)
            {
                Heard?.Invoke(frame);
            }
        };
        _channel.onclose += () => Closed?.Invoke();
    }

    public event Action<AgentFrame>? Heard;

    public event Action? Closed;

    public void Send(AgentFrame frame)
    {
        try
        {
            _channel.send(AgentFrameCodec.Encode(frame));
        }
        catch (Exception gone) when (gone is InvalidOperationException or ObjectDisposedException)
        {
            Closed?.Invoke();
        }
    }

    public void Dispose() => _conversation.Dispose();
}

/// <summary>A machine a person might start a session on, as the fleet read names it.</summary>
public sealed record AgentMachine(string RunnerId, string Label, string State);

/// <summary>
/// Reaches a machine for an ad hoc agent session: the introduction, the handshake,
/// and the channel (slice seventy, ADR-0039 Decision 6).
/// </summary>
/// <remarks>
/// <b><c>WatchARunner</c>'s path, with a different purpose and a different
/// label.</b> The control plane decides who may be introduced and whether the
/// machine takes sessions at all; this only asks, and says what it was told.
/// </remarks>
public sealed class ReachAnAgent(ControlPlaneClient control, ConsoleChannel channel)
{
    public static string Purpose => RunnerCapabilityPurposes.DriveAnAgent;

    /// <summary>
    /// The machines worth offering: beating, not a pool's maintainer, and saying on their
    /// last heartbeat that they accept ad hoc sessions (slice seventy-one) - so a pool member
    /// that never opted in is not a choice that only refuses once reached.
    /// </summary>
    public async Task<IReadOnlyList<AgentMachine>> MachinesAsync(
        string sessionToken, CancellationToken cancellationToken = default)
    {
        var fleet = await control.ListRunnersAsync(sessionToken, cancellationToken);

        return
        [
            .. fleet.Runners
                .Where(r => !string.Equals(r.State, "offline", StringComparison.Ordinal)
                         && !RunnerReach.Maintains(r.State)
                         && r.AcceptsAgentSessions is true)
                .OrderBy(r => r.Label, StringComparer.Ordinal)
                .Select(r => new AgentMachine(r.RunnerId, r.Label, RunnerReach.Derived(r.State))),
        ];
    }

    /// <summary>A link to the machine's sessions, or the sentence saying why not.</summary>
    public async Task<(IAgentLink? Link, string Said)> ReachAsync(
        string sessionToken,
        string runnerId,
        PinnedRunnerKeys pins,
        DateTimeOffset now,
        Action<string>? saying = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pins);

        using var ephemeral = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

        var introduced = await control.IntroduceRunnerAsync(
            sessionToken,
            runnerId,
            Convert.ToBase64String(ephemeral.ExportSubjectPublicKeyInfo()),
            Purpose,
            cancellationToken);

        if (introduced.Introduction is not { } introduction)
        {
            return (null, introduced.Said);
        }

        var reached = await channel.ReachAsync(
            introduction,
            ephemeral,
            pins,
            now,
            (offer, token) => control.LeaveOfferAsync(sessionToken, introduction.IntroductionId, offer, token),
            token => control.CollectAnswerAsync(sessionToken, introduction.IntroductionId, token),
            saying,
            cancellationToken,
            channelLabel: AgentChannel.Label);

        return reached.Conversation is { } conversation
            ? (new ChannelAgentLink(conversation), reached.Said)
            : (null, reached.Said);
    }
}
