using System.Security.Cryptography;
using Gg.Contracts;

namespace Gg.Runner;

/// <summary>
/// The channels a runner serves while somebody is flying it by hand.
/// </summary>
/// <remarks>
/// <para>
/// <b>Its lifetime is the lease's, and that is the whole security argument.</b>
/// ADR-0013 chose to make driving a runner a flight rather than a side channel
/// because the risk was never that a runner can do dangerous things — it already
/// runs an agent over customer code with credentials — it was capability without
/// governance: a path no lease authorises, no envelope scopes and no story
/// records. One of these exists only inside a hold, so when the lease ends the
/// channels end with it. There is no standing way to reach a runner.
/// </para>
/// <para>
/// <b>A runner built without a key cannot be reached at all.</b> The private half
/// lives on the machine and never leaves it, and <c>Gg.Runner</c> does not go
/// looking for it: the composition root hands one in, or does not. So "this
/// runner may be driven" is a wiring decision somebody made rather than a
/// capability every runner has by default.
/// </para>
/// <para>
/// <b>Introductions are answered ONLY here.</b> The idle loop beats too, and its
/// beats carry introductions the same way — they are ignored, because nothing is
/// holding a lease to authorise them. That is the difference between a runner a
/// person is flying and a runner somebody knows the id of.
/// </para>
/// </remarks>
public sealed class AttendedSession(
    ECDiffieHellman runnerKey,
    RunnerChannel channel,
    AskDispatch dispatch,
    IRunnerObserver observer) : IDisposable
{
    private readonly List<Served> _serving = [];
    private readonly HashSet<string> _answered = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();

    /// <summary>How many conversations this session has open.</summary>
    /// <remarks>
    /// Read by the loop so it can say so, and by a test so "the channel closed
    /// when the lease ended" is a number rather than a hope.
    /// </remarks>
    public int Open
    {
        get
        {
            lock (_gate)
            {
                return _serving.Count;
            }
        }
    }

    /// <summary>
    /// Answers everything this beat brought, and posts each answer outward.
    /// </summary>
    /// <remarks>
    /// <b>Each introduction once.</b> The control plane takes an offer rather
    /// than reading it, so a second beat should not see the same one — but a
    /// retried heartbeat or a relay that re-delivered would have this runner
    /// answer twice, and the second answer arrives for a handshake that already
    /// finished. Remembering the ids is cheaper than reasoning about whether
    /// that can happen.
    /// </remarks>
    public async Task AnswerAllAsync(
        string runnerId,
        IReadOnlyList<PendingIntroduction> introductions,
        IRunnerProtocol protocol,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(introductions);
        ArgumentNullException.ThrowIfNull(protocol);

        foreach (var pending in introductions)
        {
            lock (_gate)
            {
                if (!_answered.Add(pending.IntroductionId))
                {
                    continue;
                }
            }

            var result = await channel.AnswerAsync(
                pending, runnerKey, dispatch, cancellationToken);

            if (result.Answer is not { } answer)
            {
                // SAID, NOT SWALLOWED. A handshake that fails here is a person
                // at a console seeing nothing, and the sentence names which end
                // gave up - which is the difference between somebody checking a
                // network and somebody checking a machine.
                observer.CannotBeFlownByHand(result.Said);
                continue;
            }

            if (result.Serving is { } serving)
            {
                lock (_gate)
                {
                    _serving.Add(serving);
                }

                // AND WHETHER ANYBODY ACTUALLY ARRIVED, narrated when it is
                // known rather than waited for here. An answer that was relayed
                // and never connected is the failure a person cannot see from
                // their own end - their console says "no route", and only this
                // side can say whether ICE never connected or connected with no
                // channel on it. Not awaited, because the answer still has to be
                // posted before anybody can arrive at all.
                _ = NarrateArrivalAsync(serving, pending.IntroductionId);
            }

            await protocol.SignalAsync(
                runnerId,
                new RunnerSignalAnswer
                {
                    IntroductionId = pending.IntroductionId,
                    Answer = answer,
                },
                cancellationToken);
        }
    }

    /// <summary>
    /// Says whether the console that was introduced ever turned up.
    /// </summary>
    /// <remarks>
    /// <b>Nothing awaits this and nothing may throw out of it.</b> It runs
    /// alongside a session a person is using; an exception escaping here would
    /// take down a runner over a diagnostic.
    /// </remarks>
    private async Task NarrateArrivalAsync(Served serving, string introductionId)
    {
        try
        {
            if (await serving.Opened is var how && how is not HandshakeFailure.None)
            {
                observer.CannotBeFlownByHand(
                    $"introduction {introductionId} was answered and nobody arrived: {how}. "
                  + (how is HandshakeFailure.NoRouteBetweenUs
                        ? "ICE never connected, which is the network between the two machines "
                        + "rather than either of them."
                        : "A route was found and no channel opened on it, which is this end."));
            }
        }
        catch (Exception narrating) when (narrating is not OperationCanceledException)
        {
            observer.CannotBeFlownByHand($"could not tell whether anybody arrived: {narrating}");
        }
    }

    /// <summary>Closes every channel this session opened.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            foreach (var serving in _serving)
            {
                serving.Dispose();
            }

            _serving.Clear();
        }
    }
}
