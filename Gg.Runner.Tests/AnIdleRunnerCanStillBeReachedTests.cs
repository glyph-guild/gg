using System.Security.Cryptography;
using Gg.Contracts;

namespace Gg.Runner.Tests;

/// <summary>
/// A runner answers an introduction whenever it is beating, flying or not.
/// </summary>
/// <remarks>
/// <para>
/// <b>The old rule was that a flight authorised the conversation.</b> A session
/// existed inside <c>WorkAsync</c> and nowhere else, so an idle beat carried
/// introductions the runner deliberately dropped - "not a check, but a session
/// that only exists while a flight does". The cost was the moment a person most
/// wants to be looking: they cannot already be attached when work arrives,
/// because attaching is what they are not allowed to do until it has.
/// </para>
/// <para>
/// <b>What replaces it is the conversation's own lifetime.</b> The runner
/// answers while it is beating; a conversation nobody is having is let go. What
/// can be READ does not change - the same two verbs, and a tail that is still
/// this machine's current flight and never a journal - and who may ask does not
/// change either, because only the control plane mints an introduction and only
/// for the principal who registered the runner.
/// </para>
/// <para>
/// <b>The two offer tests here pass today and are the point of the file.</b>
/// Hoisting the session out of the flight breaks a gate that reads
/// <c>session is null</c> to mean "I am idle" - and breaks it SILENTLY, in the
/// direction where a production runner stops applying offered configuration and
/// nothing anywhere goes red. They are characterisations, committed with the
/// failing test so the fix cannot take them with it.
/// </para>
/// </remarks>
public class AnIdleRunnerCanStillBeReachedTests
{
    private static readonly DateTimeOffset T0 = DateTimeOffset.UnixEpoch;

    private static OfferedConfiguration AnOffer() => new()
    {
        Version = "offer@7",
        OfferedAt = T0,
        Settings = [new OfferedSetting { Key = "stun-servers", Value = "stun:relay.invalid:3478" }],
    };

    [Test]
    public async Task An_idle_beat_is_answered_rather_than_dropped()
    {
        using var stopping = new CancellationTokenSource();
        using var key = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

        var protocol = new FakeProtocol();
        protocol.Introductions.Enqueue(new PendingIntroduction
        {
            IntroductionId = "introduction-1",
            Offer = new RunnerSealedOffer { Sealed = [1, 2, 3, 4] },
        });

        var observer = new RecordingObserver();
        var says = new WhatThisRunnerSays(observer, _ => new NoLog(), () => T0);

        // A SEALED OFFER NOTHING CAN OPEN, so the runner says it could not - and
        // saying so is the only evidence that it looked at all. Counting what
        // the fake handed over would pass on a runner that ignores every
        // introduction it is given.
        var rounds = 0;

        await new RunnerLoop(protocol, new MovableClock(T0),
                (_, _) =>
                {
                    if (++rounds > 1)
                    {
                        stopping.Cancel();
                    }

                    return Task.CompletedTask;
                },
                says, new NoCredentialResolver(), new NoWorkspace(),
                attendedSessions: _ => new AttendedSession(
                    key,
                    new RunnerChannel([], TimeSpan.FromMilliseconds(50)),
                    new AskDispatch(says),
                    observer))
            .RunAsync("runner-1", ["linux"], stopping.Token);

        await Assert.That(observer.Events.Any(
                e => e.StartsWith("not-reachable:", StringComparison.Ordinal))).IsTrue()
            .Because("an introduction rides every beat, and an idle runner beats - so a "
                   + "runner that only looks while it is flying cannot be reached in the "
                   + "one state a person wants to attach in. Said: "
                   + string.Join(" | ", observer.Events));
    }

    [Test]
    public async Task A_runner_that_can_be_driven_still_takes_an_offer_while_idle()
    {
        // PASSES TODAY, AND IS HERE TO GO ON PASSING. The offer gate reads
        // `session is null' to mean "I am idle". Once a session exists whether
        // or not a flight does, that reading inverts - and inverts silently, on
        // every production runner, because every one of them is wired with an
        // identity key and no existing test wires a session at all.
        using var stopping = new CancellationTokenSource();
        using var key = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

        var seen = new List<OfferedConfiguration>();
        var protocol = new FakeProtocol { Offered = AnOffer() };
        var observer = new RecordingObserver();
        var says = new WhatThisRunnerSays(observer, _ => new NoLog(), () => T0);

        await new RunnerLoop(protocol, new MovableClock(T0),
                (_, _) => { stopping.Cancel(); return Task.CompletedTask; },
                says, new NoCredentialResolver(), new NoWorkspace(),
                offered: seen.Add,
                attendedSessions: _ => new AttendedSession(
                    key,
                    new RunnerChannel([], TimeSpan.FromMilliseconds(50)),
                    new AskDispatch(says),
                    observer))
            .RunAsync("runner-1", ["linux"], stopping.Token);

        await Assert.That(seen.Select(o => o.Version)).Contains("offer@7")
            .Because("a machine that can be watched is not a machine that stops being "
                   + "configured, and a fleet quietly frozen at whatever it booted with is "
                   + "the kind of regression nobody finds for a week.");
    }

    // THE OTHER HALF OF THE SAME GATE lives with the flying harness, as
    // BeatsWhileFlyingTests.An_offer_is_still_not_reported_while_a_flight_is_held.
    // It was prose there and nothing else, which is why it is asserted now.

    private sealed class NoLog : IReadOnlyLog
    {
        public TailRead Tail(int lines) => new([], false);
    }
}
