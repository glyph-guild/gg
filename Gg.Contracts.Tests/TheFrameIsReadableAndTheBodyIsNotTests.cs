using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// Who sealed something is readable without a key; what they said is not.
/// </summary>
/// <remarks>
/// <para>
/// <b>This exists to close a hash that was written and never read.</b> An
/// introduction is minted with the console's ephemeral public key and the
/// control plane stores its hash against the row, with a comment saying the
/// runner can then check who it is answering. The runner cannot — 0.128.0
/// removed the capability from the offer for exactly that reason, so the runner
/// never sees anything to check against. The party that CAN check is the one
/// holding the hash, and this is what lets it: the sealer's public half is
/// framed in front of the ciphertext, in the clear, by construction.
/// </para>
/// <para>
/// <b>The frame is the address and the ciphertext is the letter.</b> Reading one
/// is not reading the other, and the assertions below say so in both directions
/// — no key is passed in, and nothing about the plaintext is obtainable.
/// </para>
/// </remarks>
public class TheFrameIsReadableAndTheBodyIsNotTests
{
    private static ECDiffieHellman AKey() => ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

    private static string Public(ECDiffieHellman key) =>
        Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());

    [Test]
    public async Task The_key_that_sealed_an_offer_is_readable_with_no_key_at_all()
    {
        using var runner = AKey();
        using var console = AKey();

        var sealedOffer = RunnerSeal.SealOffer(
            Public(runner), console, Encoding.UTF8.GetBytes("v=0\r\n"));

        await Assert.That(RunnerSeal.EphemeralKeyOf(sealedOffer)).IsEqualTo(Public(console))
            .Because("this is the value the console sent to mint the introduction, so it is the "
                   + "value whose hash the control plane already stored - and comparing them is "
                   + "the check that member was added for.");
    }

    [Test]
    public async Task It_is_the_same_key_the_runner_reads_when_it_opens_the_offer()
    {
        // TWO READERS, ONE FRAME. The runner gets this key from OpenOffer
        // because it needs somewhere to send the answer; the control plane gets
        // it from the frame because it has a hash to compare. They must not be
        // able to disagree, so they read through the same private helper and
        // this asserts they agree.
        using var runner = AKey();
        using var console = AKey();

        var sealedOffer = RunnerSeal.SealOffer(
            Public(runner), console, Encoding.UTF8.GetBytes("v=0\r\n"));

        var (_, asTheRunnerSawIt) = RunnerSeal.OpenOffer(runner, sealedOffer);

        await Assert.That(RunnerSeal.EphemeralKeyOf(sealedOffer)).IsEqualTo(asTheRunnerSawIt);
    }

    [Test]
    public async Task Reading_the_frame_says_nothing_about_what_was_said()
    {
        // THE HALF THAT MAKES THIS SAFE TO PUT IN THE CONTROL PLANE. If the
        // frame leaked the body, "the relay cannot read what it relays" would
        // stop being true the moment the relay read the frame.
        using var runner = AKey();
        using var console = AKey();

        const string secret = "a=candidate:1 1 udp 2113937663 10.0.4.17 54321 typ host";

        var sealedOffer = RunnerSeal.SealOffer(
            Public(runner), console, Encoding.UTF8.GetBytes(secret));

        var frame = RunnerSeal.EphemeralKeyOf(sealedOffer);

        await Assert.That(frame).DoesNotContain("candidate");
        await Assert.That(Encoding.UTF8.GetString(Convert.FromBase64String(frame)))
            .DoesNotContain("10.0.4.17")
            .Because("a private subnet address reaching the control plane readable is the thing "
                   + "sealing this was for.");
    }

    [Test]
    public async Task A_frame_that_does_not_describe_its_own_contents_is_refused()
    {
        // AND REFUSED THE SAME WAY OPENING REFUSES IT, because a control plane
        // that accepted a frame the runner will later reject would file an
        // offer nobody can answer.
        var truncated = new byte[] { 0, 0, 0 };

        var lying = new byte[32];
        BinaryPrimitives.WriteInt32BigEndian(lying, 9999);

        var negative = new byte[64];
        BinaryPrimitives.WriteInt32BigEndian(negative, -1);

        foreach (var bad in (byte[][])[truncated, lying, negative])
        {
            var refused = Assert.Throws<CryptographicException>(
                () => RunnerSeal.EphemeralKeyOf(bad));

            await Assert.That(refused).IsNotNull();
        }
    }

    [Test]
    public async Task An_answer_frames_the_runners_key_the_same_way()
    {
        // The liveness half: every assertion above would pass on a method that
        // only ever worked for offers, and the frame is one format.
        using var runner = AKey();
        using var console = AKey();

        var sealedAnswer = RunnerSeal.SealAnswer(
            Public(console), runner, Encoding.UTF8.GetBytes("v=0\r\n"));

        await Assert.That(RunnerSeal.EphemeralKeyOf(sealedAnswer)).IsEqualTo(Public(runner));
    }
}
