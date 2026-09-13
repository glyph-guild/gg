using System.Security.Cryptography;
using System.Text;
using Gg.Contracts;
using Gg.Runner;

namespace Gg.Runner.Tests;

/// <summary>
/// A runner answers introductions only while somebody is flying it.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is ADR-0013's security argument, as a property rather than a
/// paragraph.</b> The risk of a debug channel was never that a runner can do
/// dangerous things — it already runs an agent over customer code with
/// credentials. It was capability without governance: a path no lease
/// authorises, no envelope scopes and no story records. Making driving a flight
/// removes that, and what makes it true here is that a session exists ONLY
/// inside a hold. When the lease ends the channels end with it.
/// </para>
/// <para>
/// <b>And a runner nobody wired to be driven cannot be.</b> <c>Gg.Runner</c>
/// never goes looking for the private key — it lives on the machine and never
/// leaves it — so the composition root either hands in a way to open a session
/// or does not.
/// </para>
/// </remarks>
public class OnlyWhileSomebodyIsFlyingItTests
{
    private static ECDiffieHellman AKey() => ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

    private sealed class NoLog : IReadOnlyLog
    {
        public TailRead Tail(int lines) => new([], false);
    }

    private static AskDispatch ADispatch() =>
        new(new WhatThisRunnerSays(new SilentObserver(), _ => new NoLog(), () => DateTimeOffset.UnixEpoch));

    /// <summary>An offer a console really sealed, so answering it means something.</summary>
    private static async Task<PendingIntroduction> AnOfferAsync(
        string id, ECDiffieHellman runnerKey)
    {
        using var console = new SIPSorcery.Net.RTCPeerConnection(
            new SIPSorcery.Net.RTCConfiguration { iceServers = [] });
        await console.createDataChannel("tail", null);
        await console.setLocalDescription(console.createOffer(null));

        using var ephemeral = AKey();

        return new PendingIntroduction
        {
            IntroductionId = id,
            Offer = new RunnerSealedOffer
            {
                Sealed = RunnerSeal.SealOffer(
                    Convert.ToBase64String(runnerKey.ExportSubjectPublicKeyInfo()),
                    ephemeral,
                    Encoding.UTF8.GetBytes(console.localDescription.sdp.ToString())),
            },
        };
    }

    [Test]
    public async Task It_answers_what_the_heartbeat_brought_and_posts_the_answer_outward()
    {
        using var runnerKey = AKey();
        var protocol = new FakeProtocol();

        using var session = new AttendedSession(
            runnerKey,
            new RunnerChannel([], TimeSpan.FromSeconds(5)),
            ADispatch(),
            new SilentObserver());

        await session.AnswerAllAsync(
            "a-runner", [await AnOfferAsync("intro-1", runnerKey)], protocol, CancellationToken.None);

        await Assert.That(protocol.Signalled).HasSingleItem()
            .Because("the answer goes back on the runner's own poll - outward, because nothing "
                   + "here listens and nothing opens a connection to this machine.");
        await Assert.That(protocol.Signalled[0].IntroductionId).IsEqualTo("intro-1");
        await Assert.That(session.Open).IsEqualTo(1)
            .Because("a peer handed over and not held is one the console cannot connect to.");
    }

    [Test]
    public async Task Closing_the_session_closes_every_channel_it_opened()
    {
        // THE LIFETIME PROPERTY. The session lives inside a hold, so this is
        // what happens the moment the lease ends - released, fenced, taken over,
        // or the hold simply returning.
        using var runnerKey = AKey();
        var protocol = new FakeProtocol();

        var session = new AttendedSession(
            runnerKey,
            new RunnerChannel([], TimeSpan.FromSeconds(5)),
            ADispatch(),
            new SilentObserver());

        await session.AnswerAllAsync(
            "a-runner",
            [await AnOfferAsync("intro-1", runnerKey), await AnOfferAsync("intro-2", runnerKey)],
            protocol,
            CancellationToken.None);

        await Assert.That(session.Open).IsEqualTo(2);

        session.Dispose();

        await Assert.That(session.Open).IsEqualTo(0)
            .Because("there is no standing capability to reach a runner: there is a flight, and "
                   + "while it is flying a person is talking to it.");
    }

    [Test]
    public async Task One_introduction_is_answered_once()
    {
        // A RETRIED HEARTBEAT OR A RE-DELIVERED OFFER would otherwise have this
        // runner answer twice, and the second answer arrives for a handshake
        // that already finished.
        using var runnerKey = AKey();
        var protocol = new FakeProtocol();

        using var session = new AttendedSession(
            runnerKey,
            new RunnerChannel([], TimeSpan.FromSeconds(5)),
            ADispatch(),
            new SilentObserver());

        var offer = await AnOfferAsync("intro-1", runnerKey);

        await session.AnswerAllAsync("a-runner", [offer], protocol, CancellationToken.None);
        await session.AnswerAllAsync("a-runner", [offer], protocol, CancellationToken.None);

        await Assert.That(protocol.Signalled.Count).IsEqualTo(1);
    }

    [Test]
    public async Task A_handshake_that_fails_is_said_rather_than_swallowed()
    {
        // THE RUNNER'S HALF OF A SENTENCE THE CONSOLE ALSO GETS. On a supervised
        // machine the journal is the only place this is visible at all - the
        // console saw a timeout and this is where somebody looks next.
        using var runnerKey = AKey();
        using var somebodyElse = AKey();
        using var ephemeral = AKey();

        var protocol = new FakeProtocol();
        var heard = new RecordingObserver();

        using var session = new AttendedSession(
            runnerKey,
            new RunnerChannel([], TimeSpan.FromSeconds(5)),
            ADispatch(),
            heard);

        await session.AnswerAllAsync(
            "a-runner",
            [new PendingIntroduction
            {
                IntroductionId = "intro-1",
                Offer = new RunnerSealedOffer
                {
                    // SEALED TO SOMEBODY ELSE, which is a substituted key or a
                    // rewritten frame and reads as neither a network problem nor
                    // a silent runner.
                    Sealed = RunnerSeal.SealOffer(
                        Convert.ToBase64String(somebodyElse.ExportSubjectPublicKeyInfo()),
                        ephemeral,
                        Encoding.UTF8.GetBytes("v=0\r\n")),
                },
            }],
            protocol,
            CancellationToken.None);

        await Assert.That(protocol.Signalled).IsEmpty()
            .Because("there is nothing to answer with, and posting anything would file a reply "
                   + "to a handshake that never opened.");
        await Assert.That(session.Open).IsEqualTo(0);
        await Assert.That(heard.Events.Any(e => e.StartsWith("not-reachable", StringComparison.Ordinal)))
            .IsTrue()
            .Because("a failure nobody records is a person at a console with nothing to read "
                   + "and a machine with nothing in its log.");
    }
}
