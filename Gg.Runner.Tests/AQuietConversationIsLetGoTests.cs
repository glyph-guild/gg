using System.Security.Cryptography;
using System.Text;
using Gg.Contracts;
using SIPSorcery.Net;

namespace Gg.Runner.Tests;

/// <summary>
/// A conversation nobody is having is let go, and one in use never is.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is what replaced the flight as the bound.</b> A session used to live
/// inside a hold, so a landing closed every channel it had opened and nothing
/// could accumulate. It lives for the run now - which is what lets somebody
/// attach to a machine that is waiting - and the list it keeps was only ever
/// added to. A console that connects and is then killed would leave a peer
/// connection and its sockets alive for the life of the runner: a leak, and the
/// very standing capability this slice traded away.
/// </para>
/// <para>
/// <b>Three ways a conversation ends, and they are not the same one.</b> Nobody
/// arrived; the peer closed; nobody has asked anything for long enough. The last
/// is the only one that needs a clock, and it is the one a person walking away
/// from a terminal produces.
/// </para>
/// <para>
/// <b>The liveness half is the half that matters.</b> A sweep that let go of a
/// channel somebody was using would end a watch mid-flight, which is worse than
/// the leak - so the test for it is here rather than implied.
/// </para>
/// </remarks>
public class AQuietConversationIsLetGoTests
{
    private static readonly DateTimeOffset T0 = DateTimeOffset.UnixEpoch;

    private static ECDiffieHellman AKey() => ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

    private sealed class NoLog : IReadOnlyLog
    {
        public TailRead Tail(int lines) => new([], false);
    }

    private static AskDispatch ADispatch() =>
        new(new WhatThisRunnerSays(new SilentObserver(), _ => new NoLog(), () => T0));

    /// <summary>An offer a console really sealed, so answering it means something.</summary>
    private static async Task<PendingIntroduction> AnOfferAsync(string id, ECDiffieHellman runnerKey)
    {
        using var ephemeral = AKey();
        using var console = new RTCPeerConnection(new RTCConfiguration { iceServers = [] });
        await console.createDataChannel("tail", null);
        await console.setLocalDescription(console.createOffer(null));

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

    private static async Task<AttendedSession> AnsweredAsync(
        ECDiffieHellman runnerKey, Func<DateTimeOffset> now)
    {
        var session = new AttendedSession(
            runnerKey,
            new RunnerChannel([], TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30), now),
            ADispatch(),
            new SilentObserver());

        await session.AnswerAllAsync(
            "a-runner", [await AnOfferAsync("intro-1", runnerKey)], new FakeProtocol(),
            CancellationToken.None);

        return session;
    }

    [Test]
    public async Task One_nobody_has_spoken_on_is_let_go_when_it_goes_quiet()
    {
        var now = T0;
        using var runnerKey = AKey();
        using var session = await AnsweredAsync(runnerKey, () => now);

        await Assert.That(session.Open).IsEqualTo(1);

        // JUST INSIDE THE BOUND. A sweep that let go here would be a watch that
        // ended while somebody was still reading what was already on screen.
        now = T0 + AttendedSession.QuietFor - TimeSpan.FromSeconds(1);
        session.ForgetTheQuiet(now);

        await Assert.That(session.Open).IsEqualTo(1)
            .Because("the bound has not passed yet, and a conversation is not over because "
                   + "nobody has typed for a while.");

        now = T0 + AttendedSession.QuietFor + TimeSpan.FromSeconds(1);
        session.ForgetTheQuiet(now);

        await Assert.That(session.Open).IsEqualTo(0)
            .Because("a channel nobody is asking anything of is what a person who closed "
                   + "their laptop leaves behind, and holding it for the life of the runner "
                   + "is the standing way in this slice exists to avoid.");
    }

    [Test]
    public async Task A_sweep_does_not_touch_one_that_is_being_used()
    {
        var now = T0;
        using var runnerKey = AKey();
        using var session = await AnsweredAsync(runnerKey, () => now);

        // ASKED AT THE LAST MOMENT, which is what somebody watching does: the
        // console polls once a second, so on a live watch the gap between asks
        // is never near the bound.
        now = T0 + AttendedSession.QuietFor - TimeSpan.FromSeconds(1);
        session.Heard(now);

        now = T0 + AttendedSession.QuietFor + TimeSpan.FromSeconds(1);
        session.ForgetTheQuiet(now);

        await Assert.That(session.Open).IsEqualTo(1)
            .Because("letting go of a channel somebody is using ends a watch mid-flight, "
                   + "which is worse than the leak this sweep exists for.");
    }

    [Test]
    public async Task And_the_sweep_is_run_by_the_beat()
    {
        // WHERE IT HAS TO BE, and the only clock the runner already turns. A
        // sweep nothing calls is the same defect as a probe that runs only in
        // the suite - measured here over the loop's own source, because there
        // is no other place a bound could be enforced from.
        var loop = SourceOf("RunnerLoop.cs");

        await Assert.That(loop).Contains("ForgetTheQuiet")
            .Because("the beat is the runner's own heartbeat and the one thing that happens "
                   + "whether or not anybody is watching.");
    }

    private static string SourceOf(string file)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Gg.sln")))
        {
            dir = dir.Parent;
        }

        var root = (dir ?? throw new InvalidOperationException("Gg.sln not found")).FullName;

        return File.ReadAllText(Path.Combine(root, "Gg.Runner", file));
    }
}
