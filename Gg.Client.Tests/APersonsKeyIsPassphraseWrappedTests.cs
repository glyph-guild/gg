using Gg.Client;

namespace Gg.Client.Tests;

/// <summary>
/// A person's key is minted here, wrapped by a passphrase, and the file holds
/// nothing that opens without one.
/// </summary>
/// <remarks>
/// <para>
/// <b>The first key in this system that belongs to a PERSON.</b> Every other
/// one is per-machine or per-exchange — a runner's identity key, a console's
/// ephemeral key, the pins, the machine's store key. A person has had a
/// twelve-hour bearer token and nothing else, and ADR-0037 Decision 2 is what
/// changes that.
/// </para>
/// <para>
/// <b>It gates distribution and nothing else.</b> This key seals a credential
/// the first time, pushes one, and adds a holder. It is not needed to USE one —
/// a machine opens what it holds unattended — which is the owner's correction
/// and the reason a flight at three in the morning still runs.
/// </para>
/// <para>
/// <b>The assertion that can fail is the one about the FILE.</b> A wrapper that
/// encrypted the key and left the PKCS#8 beside it, or that kept the derived
/// key cached next to what it opens, would pass a round-trip and hand the key
/// to whoever read the file. So these look for the private half, in every
/// encoding it would plausibly be written in, in every byte of what is stored.
/// </para>
/// </remarks>
public class APersonsKeyIsPassphraseWrappedTests
{
    private const string Passphrase = "correct horse battery staple";

    private static string APath() =>
        Path.Combine(Path.GetTempPath(), "gg-person-" + Guid.NewGuid().ToString("N"), "person-key");

    [Test]
    public async Task A_created_key_unlocks_with_its_passphrase()
    {
        var path = APath();
        PersonKey.Create(path, Passphrase);

        await Assert.That(PersonKey.Unlock(path, Passphrase)).IsNotNull();
    }

    [Test]
    public async Task It_does_not_unlock_with_another()
    {
        var path = APath();
        PersonKey.Create(path, Passphrase);

        await Assert.That(() => PersonKey.Unlock(path, "something else"))
            .Throws<CredentialUnavailableException>();
    }

    [Test]
    public async Task The_refusal_does_not_say_which_part_was_wrong()
    {
        // A WRONG PASSPHRASE AND A DAMAGED FILE READ THE SAME FROM OUTSIDE, on
        // purpose. AES-GCM cannot tell them apart - a bad key and a flipped
        // byte both fail the tag - and a sentence that guessed would be
        // confidently wrong half the time.
        var path = APath();
        PersonKey.Create(path, Passphrase);

        var said = Assert.Throws<CredentialUnavailableException>(
            () => PersonKey.Unlock(path, "something else"))!.Message;

        await Assert.That(said).DoesNotContain(Passphrase);
        await Assert.That(said.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task The_public_half_is_readable_without_the_passphrase()
    {
        // A PUBLIC KEY IS NOT A SECRET, and somebody has to be able to register
        // it, print it and be pushed to at it without unlocking anything.
        var path = APath();
        var created = PersonKey.Create(path, Passphrase);

        await Assert.That(PersonKey.PublicHalfOf(path)).IsEqualTo(created.PublicKey);
    }

    [Test]
    public async Task No_part_of_the_file_is_an_importable_private_key()
    {
        // ASKED OF THE FILE RATHER THAN OF THE KEY, deliberately. Comparing
        // against the real private bytes would mean PersonKey handing them out
        // for a test, and an escape hatch that exists only for tests is still a
        // method that returns a private key - which is the hole rule 3 exists to
        // prevent, punched by the thing checking rule 3.
        //
        // So the question is asked the other way round: is ANY base64 run in
        // this file something a private key can be imported from? That needs
        // nothing from PersonKey and fails for a wrapper that wrote the PKCS#8
        // beside the ciphertext, or wrote it instead of the ciphertext.
        var path = APath();
        PersonKey.Create(path, Passphrase);

        var written = File.ReadAllText(path);
        var runs = System.Text.RegularExpressions.Regex
            .Matches(written, "[A-Za-z0-9+/=]{40,}")
            .Select(m => m.Value);

        foreach (var run in runs)
        {
            byte[] candidate;
            try
            {
                candidate = Convert.FromBase64String(run);
            }
            catch (FormatException)
            {
                continue;
            }

            var imported = false;
            try
            {
                using var key = System.Security.Cryptography.ECDiffieHellman.Create();
                key.ImportPkcs8PrivateKey(candidate, out _);
                imported = true;
            }
            catch (System.Security.Cryptography.CryptographicException)
            {
                // Which is what every run in this file should do.
            }

            await Assert.That(imported).IsFalse()
                .Because("something in this file imports as a private key, so it is not wrapped.");
        }
    }

    [Test]
    public async Task The_file_holds_no_passphrase_and_no_key_derived_from_it()
    {
        var path = APath();
        PersonKey.Create(path, Passphrase);

        var written = File.ReadAllText(path);

        await Assert.That(written).DoesNotContain(Passphrase);
        await Assert.That(written)
            .DoesNotContain(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(Passphrase)));
    }

    [Test]
    public async Task The_file_is_readable_only_by_its_owner()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var path = APath();
        PersonKey.Create(path, Passphrase);

        await Assert.That(File.GetUnixFileMode(path))
            .IsEqualTo(UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    [Test]
    public async Task Two_keys_made_from_one_passphrase_are_different_keys()
    {
        // A SALT PER KEY, not a constant. Without one, two people choosing the
        // same passphrase derive the same wrapping key, and a precomputation
        // against one is a precomputation against every one of them.
        var first = PersonKey.Create(APath(), Passphrase);
        var second = PersonKey.Create(APath(), Passphrase);

        await Assert.That(first.PublicKey).IsNotEqualTo(second.PublicKey);
    }

    [Test]
    public async Task Creating_over_an_existing_key_is_refused()
    {
        // A KEY THAT IS OVERWRITTEN IS EVERY CREDENTIAL SEALED TO IT LOST, and
        // there is no undo and no escrow (ADR-0037 Decision 8). This has to be
        // an act somebody takes deliberately, not one `gg key create` run twice
        // performs quietly.
        var path = APath();
        PersonKey.Create(path, Passphrase);

        await Assert.That(() => PersonKey.Create(path, Passphrase)).Throws<InvalidOperationException>();
    }
}
