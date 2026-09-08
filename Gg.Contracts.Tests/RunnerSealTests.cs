using System.Security.Cryptography;
using System.Text;
using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// What passes between a console and a runner cannot be read by what carries it.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the half step 3 declared and did not do.</b> The sealed types, the
/// capability, the registration key and the pin all exist; until this, nothing
/// actually sealed anything, and the ADR's "introduces and holds nothing" was a
/// property of types rather than of bytes.
/// </para>
/// <para>
/// <b>What is inside is why it is authenticated rather than merely
/// encrypted.</b> SDP and ICE candidates are acted on by both ends, so a relay
/// that could flip a byte without being caught could redirect a connection
/// without ever reading it.
/// </para>
/// </remarks>
public class RunnerSealTests
{
    private static ECDiffieHellman AKey() => ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

    private static string Public(ECDiffieHellman key) =>
        Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());

    private static readonly byte[] AnOffer =
        Encoding.UTF8.GetBytes("v=0\r\na=candidate:1 1 udp 2130706431 10.0.4.17 54321 typ host");

    [Test]
    public async Task An_offer_sealed_to_a_runner_opens_with_that_runners_key()
    {
        using var runner = AKey();
        using var ephemeral = AKey();

        var sealedOffer = RunnerSeal.SealOffer(Public(runner), ephemeral, AnOffer);

        var (offer, theirs) = RunnerSeal.OpenOffer(runner, sealedOffer);

        await Assert.That(offer).IsEquivalentTo(AnOffer);

        // AND WHO TO ANSWER, from the same call. The runner cannot get the
        // console's ephemeral key anywhere else - it is framed ahead of the
        // ciphertext and nothing but this reads the frame - so returning only
        // the plaintext would leave a runner holding something it could not
        // reply to.
        await Assert.That(theirs).IsEqualTo(Public(ephemeral));
    }

    [Test]
    public async Task An_answer_opens_with_the_key_the_console_pinned()
    {
        using var runner = AKey();
        using var ephemeral = AKey();
        var answer = Encoding.UTF8.GetBytes("v=0\r\na=candidate:2 1 udp 1 172.16.0.4 41398 typ host");

        var sealedAnswer = RunnerSeal.SealAnswer(Public(ephemeral), runner, answer);

        await Assert.That(RunnerSeal.OpenAnswer(ephemeral, Public(runner), sealedAnswer))
            .IsEquivalentTo(answer);
    }

    [Test]
    public async Task A_candidate_does_not_appear_in_what_is_relayed()
    {
        // THE WHOLE CLAIM, checked against bytes rather than against types.
        // ADR-0013 relays this through the control plane, and the private subnet
        // address inside is what this platform already declined to carry.
        using var runner = AKey();
        using var ephemeral = AKey();

        var sealedOffer = RunnerSeal.SealOffer(Public(runner), ephemeral, AnOffer);
        var asText = Encoding.UTF8.GetString(sealedOffer);

        await Assert.That(asText).DoesNotContain("10.0.4.17")
            .Because("a private address readable in the relayed bytes is the whole thing this "
                   + "is for.");
        await Assert.That(asText).DoesNotContain("candidate");
        await Assert.That(asText).DoesNotContain("v=0");
    }

    [Test]
    public async Task Somebody_elses_key_does_not_open_it()
    {
        using var runner = AKey();
        using var eavesdropper = AKey();
        using var ephemeral = AKey();

        var sealedOffer = RunnerSeal.SealOffer(Public(runner), ephemeral, AnOffer);

        var thrown = Assert.Throws<CryptographicException>(
            () => RunnerSeal.OpenOffer(eavesdropper, sealedOffer));

        await Assert.That(thrown).IsNotNull()
            .Because("an offer anybody's key opened would not be sealed to anything.");
    }

    [Test]
    public async Task An_answer_from_a_substituted_key_does_not_open()
    {
        // WHY THE PIN IS PASSED IN RATHER THAN READ OUT OF THE FRAME. Opening
        // with whatever arrived would authenticate the message against itself,
        // and a control plane that swapped the runner's key at the introduction
        // would be talking to the console with nothing to notice.
        using var pinned = AKey();
        using var impostor = AKey();
        using var ephemeral = AKey();

        var sealedAnswer = RunnerSeal.SealAnswer(
            Public(ephemeral), impostor, Encoding.UTF8.GetBytes("not from the runner"));

        var thrown = Assert.Throws<CryptographicException>(
            () => RunnerSeal.OpenAnswer(ephemeral, Public(pinned), sealedAnswer));

        await Assert.That(thrown).IsNotNull()
            .Because("this is the substitution the pin exists to catch, and catching it has to "
                   + "mean the answer does not open at all.");
    }

    [Test]
    public async Task A_flipped_byte_is_caught_rather_than_obeyed()
    {
        // AES-GCM rather than a stream cipher: what is inside is acted on by
        // both ends, so a relay able to change it without being caught could
        // redirect a connection without reading a thing.
        using var runner = AKey();
        using var ephemeral = AKey();

        var sealedOffer = RunnerSeal.SealOffer(Public(runner), ephemeral, AnOffer);
        sealedOffer[^1] ^= 0x01;

        var thrown = Assert.Throws<CryptographicException>(
            () => RunnerSeal.OpenOffer(runner, sealedOffer));

        await Assert.That(thrown).IsNotNull()
            .Because("a relay able to change what both ends act on could redirect a connection "
                   + "without ever reading it.");
    }

    [Test]
    public async Task An_offer_cannot_be_replayed_as_an_answer()
    {
        // TWO LABELS OVER ONE AGREEMENT. Both directions derive from the same
        // ECDH secret, so without separate labels the offer's key and the
        // answer's key would be the same value - and a replayed offer would open
        // where an answer was expected.
        using var runner = AKey();
        using var ephemeral = AKey();

        var sealedOffer = RunnerSeal.SealOffer(Public(runner), ephemeral, AnOffer);

        var thrown = Assert.Throws<CryptographicException>(
            () => RunnerSeal.OpenAnswer(ephemeral, Public(runner), sealedOffer));

        await Assert.That(thrown).IsNotNull()
            .Because("one agreement with one label would make these two keys the same value.");
    }

    [Test]
    public async Task Two_seals_of_one_message_are_different_bytes()
    {
        // A NONCE PER SEAL. Identical ciphertext for identical input would let
        // whatever relays this tell that the same offer was sent twice, which is
        // a thing about a person's session it has no business knowing.
        using var runner = AKey();
        using var ephemeral = AKey();

        var once = RunnerSeal.SealOffer(Public(runner), ephemeral, AnOffer);
        var twice = RunnerSeal.SealOffer(Public(runner), ephemeral, AnOffer);

        await Assert.That(once).IsNotEquivalentTo(twice);
    }

    [Test]
    public async Task A_rewritten_frame_says_so_rather_than_failing_as_a_bad_key()
    {
        // A relay that rewrote the frame and a key that simply does not match
        // are different problems, and a person needs to be able to tell them
        // apart from the message alone.
        using var runner = AKey();
        using var ephemeral = AKey();

        var sealedOffer = RunnerSeal.SealOffer(Public(runner), ephemeral, AnOffer);
        var truncated = sealedOffer[..8];

        var thrown = Assert.Throws<CryptographicException>(
            () => RunnerSeal.OpenOffer(runner, truncated));

        await Assert.That(thrown!.Message).Contains("rewritten or truncated");
    }
}
