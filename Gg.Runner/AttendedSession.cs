using System.Security.Cryptography;
using Gg.Contracts;

namespace Gg.Runner;

/// <summary>
/// The channels a runner serves to whoever has been introduced to it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Its lifetime is a conversation's, and that is the security argument
/// now.</b> ADR-0013 chose to make driving a runner a flight rather than a side
/// channel because the risk was never that a runner can do dangerous things — it
/// already runs an agent over customer code with credentials — it was capability
/// without governance. One of these used to exist only inside a hold, which
/// bounded it and also made a runner unreachable in the one state a person wants
/// to attach in: waiting for work. So the bound moved to the other end. A
/// channel nobody is asking anything of is let go, and stopping the runner ends
/// every conversation at once.
/// </para>
/// <para>
/// <b>What can be READ did not widen.</b> Only the control plane mints an
/// introduction, only for the principal who REGISTERED this runner, sealed to a
/// pinned key and expiring in a minute. The channel carries two read-only verbs,
/// and the tail is this machine's current flight — never a journal, never
/// another machine's. A registrant who attaches now sees whatever this machine
/// claims next, including a flight somebody else in the tenant opened; that is
/// the same text they could already read over ssh on a machine they own, which
/// is the argument the runner modal makes for the ssh line beside it.
/// </para>
/// <para>
/// <b>A runner built without a key cannot be reached at all.</b> The private half
/// lives on the machine and never leaves it, and <c>Gg.Runner</c> does not go
/// looking for it: the composition root hands one in, or does not. So "this
/// runner may be driven" is a wiring decision somebody made rather than a
/// capability every runner has by default.
/// </para>
/// <para>
/// <b>Introductions are answered here, on any beat.</b> They used to be ignored
/// on an idle one, on the grounds that nothing was holding a lease to authorise
/// them — which read as a second lock and worked as a closed door: the beat that
/// carries an introduction to a waiting machine is exactly the beat a person
/// needs answered.
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
                // AND LET GO OF, here rather than at the next sweep. The peer
                // is already closed by the arrival bound; what is left is this
                // session's reference to it, and a list that only grows is how
                // a runner ends up holding every handshake that ever failed.
                Forget(serving);

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

    /// <summary>
    /// How long a conversation is kept with nobody asking anything on it.
    /// </summary>
    /// <remarks>
    /// <b>Ten minutes, and the number is a person rather than a protocol.</b> A
    /// watcher polls once a second, so a live conversation is never quiet for
    /// more than a moment; what this measures is somebody who walked away, shut
    /// a laptop, or lost a network. Long enough that a suspended machine coming
    /// back finds its watch still there, short enough that a runner does not
    /// accumulate peers nobody is on the other end of.
    /// </remarks>
    public static TimeSpan QuietFor { get; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Whether one conversation is over.
    /// </summary>
    /// <remarks>
    /// <b>Pure, because the interesting half cannot be reached with a live
    /// peer.</b> A channel somebody is using must survive a sweep, and building
    /// that state for a test means a second process on the other end of a real
    /// ICE connection. The decision is one line; keeping it where it can be
    /// asked directly is what makes the liveness half assertable at all.
    /// </remarks>
    public static bool IsOver(bool gone, DateTimeOffset lastHeard, DateTimeOffset now) =>
        gone || now - lastHeard > QuietFor;

    /// <summary>
    /// Lets go of every conversation that is over.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is what replaced the flight as the bound.</b> A session used to
    /// live inside a hold, so a landing closed everything it had opened. It
    /// lives for the run now, and without this a console that connected and was
    /// then killed would leave a peer connection alive for the life of the
    /// runner - a leak, and the standing capability this slice traded away.
    /// </para>
    /// <para>
    /// <b>Three endings, and they are not the same one.</b> Nobody arrived, the
    /// peer closed, or nobody has asked anything for <see cref="QuietFor"/>.
    /// Only the last needs the clock, and it is the one a person walking away
    /// from a terminal produces.
    /// </para>
    /// </remarks>
    public void ForgetTheQuiet(DateTimeOffset now)
    {
        lock (_gate)
        {
            for (var i = _serving.Count - 1; i >= 0; i--)
            {
                var serving = _serving[i];

                if (!IsOver(serving.Gone, serving.LastHeard, now))
                {
                    continue;
                }

                serving.Dispose();
                _serving.RemoveAt(i);
            }
        }
    }

    /// <summary>Lets go of one conversation, wherever it ended.</summary>
    private void Forget(Served serving)
    {
        lock (_gate)
        {
            if (_serving.Remove(serving))
            {
                serving.Dispose();
            }
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
