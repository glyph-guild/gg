using System.Security.Cryptography;
using System.Text;
using Gg.Contracts;
using Gg.Runner;

namespace Gg.Runner.Tests;

/// <summary>
/// A handshake that does not happen says which end gave up.
/// </summary>
/// <remarks>
/// <para>
/// <b>"No log" and "could not reach the runner" are two different
/// sentences.</b> One sends somebody to look at a machine and the other sends
/// them to look at a network, and a single failure string would collapse them —
/// this system's named worst failure wearing an error message.
/// </para>
/// <para>
/// <b>The first thing that can go wrong is not a network problem</b>, which is
/// the distinction that costs the most to get wrong: a substituted or truncated
/// offer looks like silence, and somebody would spend a day on a firewall.
/// </para>
/// </remarks>
public class HandshakeFailsLegiblyTests
{
    private static ECDiffieHellman AKey() => ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

    private static string Public(ECDiffieHellman key) =>
        Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());

    private static AskDispatch ADispatch() =>
        new(new WhatThisRunnerSays(new SilentObserver(), _ => new NoLog(), () => DateTimeOffset.UnixEpoch));

    private sealed class NoLog : IReadOnlyLog
    {
        public TailRead Tail(int lines) => new([], false);
    }

    private static PendingIntroduction Carrying(byte[] sealedOffer) => new()
    {
        IntroductionId = "intro-1",
        Offer = new RunnerSealedOffer { Sealed = sealedOffer },
    };

    [Test]
    public async Task An_offer_sealed_to_another_runner_says_so_rather_than_timing_out()
    {
        // THE EXPENSIVE CONFUSION. A runner that reported this as a connection
        // failure would send somebody to a firewall over a key.
        var channel = new RunnerChannel([], TimeSpan.FromSeconds(1));
        using var mine = AKey();
        using var theirs = AKey();
        using var ephemeral = AKey();

        var forSomebodyElse = RunnerSeal.SealOffer(
            Public(theirs), ephemeral, Encoding.UTF8.GetBytes("v=0"));

        var result = await channel.AnswerAsync(Carrying(forSomebodyElse), mine, ADispatch());

        await Assert.That(result.Failure).IsEqualTo(HandshakeFailure.OfferWouldNotOpen);
        await Assert.That(result.Answer).IsNull();
        await Assert.That(result.Said).Contains("sealed to a different runner")
            .Because("the sentence has to name both causes, because this runner cannot tell "
                   + "which of them happened.");
    }

    [Test]
    public async Task A_rewritten_offer_is_the_same_answer_and_says_the_other_cause()
    {
        var channel = new RunnerChannel([], TimeSpan.FromSeconds(1));
        using var mine = AKey();
        using var ephemeral = AKey();

        var sealedOffer = RunnerSeal.SealOffer(
            Public(mine), ephemeral, Encoding.UTF8.GetBytes("v=0"));
        sealedOffer[^1] ^= 0x01;

        var result = await channel.AnswerAsync(Carrying(sealedOffer), mine, ADispatch());

        await Assert.That(result.Failure).IsEqualTo(HandshakeFailure.OfferWouldNotOpen);
        await Assert.That(result.Said).Contains("rewrote it in transit");
    }

    [Test]
    public async Task Something_that_opens_and_is_not_an_offer_is_a_third_thing()
    {
        // THE SEAL IS FINE AND WHAT WAS INSIDE IT IS NOT, which is neither a key
        // problem nor a network one. Collapsing it into either would send
        // somebody after the wrong thing twice.
        var channel = new RunnerChannel([], TimeSpan.FromSeconds(1));
        using var mine = AKey();
        using var ephemeral = AKey();

        var notAnOffer = RunnerSeal.SealOffer(
            Public(mine), ephemeral, Encoding.UTF8.GetBytes("this is not sdp at all"));

        var result = await channel.AnswerAsync(Carrying(notAnOffer), mine, ADispatch());

        await Assert.That(result.Failure).IsEqualTo(HandshakeFailure.OfferWasNotSdp);
        await Assert.That(result.Said).Contains("The seal is fine");
    }

    [Test]
    public async Task Every_failure_says_something_a_person_could_act_on()
    {
        // A SENTENCE PER OUTCOME, checked as a shape rather than by reading. An
        // enum value nobody wrote a sentence for is a failure that reports a
        // number.
        var channel = new RunnerChannel([], TimeSpan.FromSeconds(1));
        using var mine = AKey();
        using var theirs = AKey();
        using var ephemeral = AKey();

        foreach (var offer in (byte[][])
                 [
                     RunnerSeal.SealOffer(Public(theirs), ephemeral, Encoding.UTF8.GetBytes("v=0")),
                     RunnerSeal.SealOffer(Public(mine), ephemeral, Encoding.UTF8.GetBytes("nope")),
                 ])
        {
            var result = await channel.AnswerAsync(Carrying(offer), mine, ADispatch());

            await Assert.That(result.Failure).IsNotEqualTo(HandshakeFailure.None);
            await Assert.That(result.Said.Length).IsGreaterThan(40)
                .Because("a failure whose whole account is a word is a failure nobody can act "
                       + "on, and this path is one a person is watching.");
        }
    }

    [Test]
    public async Task The_two_causes_it_cannot_tell_apart_are_both_named()
    {
        // HONESTY ABOUT WHAT THIS RUNNER CANNOT KNOW. A seal that does not open
        // is a substituted key OR a rewritten frame, and nothing on this side
        // distinguishes them - so the sentence says both rather than guessing
        // one and being wrong half the time.
        var channel = new RunnerChannel([], TimeSpan.FromSeconds(1));
        using var mine = AKey();
        using var theirs = AKey();
        using var ephemeral = AKey();

        var result = await channel.AnswerAsync(
            Carrying(RunnerSeal.SealOffer(Public(theirs), ephemeral, [1, 2, 3])),
            mine,
            ADispatch());

        await Assert.That(result.Said).Contains("sealed to a different runner");
        await Assert.That(result.Said).Contains("rewrote it in transit");
    }
}
