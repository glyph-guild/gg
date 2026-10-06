using Gg.Client;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// A credential a person registers is sealed to that person, and the machine
/// they typed it on is not a holder of it.
/// </summary>
/// <remarks>
/// <para>
/// <b>S64.2-01, and the step that was added after step 1 was built.</b>
/// <c>FileCredentialStore.Write</c> seals to <c>[_key.Value.PublicKey]</c> where
/// <c>_key</c> is a <see cref="MachineKey"/>, so every credential at rest was
/// sealed to the MACHINE and to nothing else — and the only production use of
/// <c>PersonKey</c> anywhere was <c>gg key create</c>. A person was a holder of
/// nothing.
/// </para>
/// <para>
/// <b>Which would have made a passphrase at push time theatre.</b>
/// <c>person-key</c> and <c>machine-key</c> sit in the same directory, both mode
/// 0600, and the machine key is wrapped by nothing — so anything running as that
/// user reads it and rewraps the credential to any machine it can reach, with no
/// human present. A prompt in front of a capability it does not remove is the
/// thing this codebase already refuses in as many words: <i>"'we will be careful'
/// is not a control."</i>
/// </para>
/// <para>
/// <b>REGISTERING COSTS NO PASSPHRASE, which corrects the shape this was
/// proposed in.</b> Sealing needs only the person's PUBLIC half, and
/// <c>PersonKey.PublicHalfOf</c> reads it off disk without unlocking anything. A
/// passphrase prompt here would derive nothing and protect nothing — exactly the
/// theatre the step exists to remove — so the only thing not echoed on this path
/// is the secret itself. What costs a passphrase is MOVING a credential, which is
/// the distinction ADR-0037 Decision 2 draws.
/// </para>
/// <para>
/// <b>And `Write` keeps its meaning, which is not a detail.</b> It is reached by
/// <c>Reseal</c> on every read of a pre-sealing plaintext, and by
/// <c>LocalCredentialKeeper.KeepLocallyMinted</c> when a runner mints its own
/// agent token. Sealing those to a person would brick a machine that has no
/// person's key — so registering is a SEPARATE act rather than a changed one, and
/// <c>AnOlderCredentialStillResolvesTests</c> is the half that holds it.
/// </para>
/// </remarks>
public class ACredentialIsSealedToItsPersonTests
{
    private const string Locator = "local:acme/widgets";
    private const string Secret = "ghp-not-a-real-token";
    private const string Passphrase = "correct horse battery staple";

    private static string ATempDir(string tag) =>
        Path.Combine(Path.GetTempPath(), $"gg-{tag}-" + Guid.NewGuid().ToString("N"));

    private static FileCredentialStore AStore(out MachineKey machine)
    {
        machine = MachineKey.LoadOrCreate(Path.Combine(ATempDir("mk"), "machine-key"));
        return new FileCredentialStore(ATempDir("store"), machine);
    }

    private static string APersonsKey()
    {
        var path = Path.Combine(ATempDir("pk"), "person-key");
        PersonKey.Create(path, Passphrase);
        return path;
    }

    [Test]
    public async Task What_a_person_registers_is_sealed_to_the_person()
    {
        var store = AStore(out var machine);
        var keyPath = APersonsKey();

        store.Register(Locator, Secret, PersonKey.PublicHalfOf(keyPath));

        await Assert.That(store.HoldersOf(Locator)).Contains(PersonKey.PublicHalfOf(keyPath))
            .Because("the person who registered it is the holder, which is what makes moving it "
                   + "an act only they can perform.");

        await Assert.That(store.HoldersOf(Locator)).DoesNotContain(machine.PublicKey)
            .Because("if the machine key is a holder, it can move the credential with nobody "
                   + "present and the passphrase on a push guards nothing.");
    }

    [Test]
    public async Task And_this_machine_cannot_open_it()
    {
        var store = AStore(out _);

        store.Register(Locator, Secret, PersonKey.PublicHalfOf(APersonsKey()));

        await Assert.That(() => store.Read(Locator)).Throws<CredentialUnavailableException>()
            .Because("nothing on this machine holds the content key, and that is the property the "
                   + "whole step buys.");
    }

    [Test]
    public async Task The_person_opens_it_with_their_passphrase()
    {
        var store = AStore(out _);
        var keyPath = APersonsKey();

        store.Register(Locator, Secret, PersonKey.PublicHalfOf(keyPath));

        var person = PersonKey.Unlock(keyPath, Passphrase);

        await Assert.That(CredentialSeal.Open(store.SealedFor(Locator), person)).IsEqualTo(Secret)
            .Because("sealed to somebody means openable BY them - a store nobody can open is a "
                   + "store that lost a credential.");
    }

    [Test]
    public async Task Registering_asks_for_no_passphrase_because_it_would_derive_nothing()
    {
        // THE CORRECTION, ASSERTED SO IT CANNOT DRIFT BACK. Sealing takes a
        // public key. A prompt here would be a question whose answer is never
        // used, which is worse than no prompt: it teaches somebody that typing a
        // passphrase is what makes a credential safe, and then the one that
        // matters looks like more of the same.
        var store = AStore(out _);
        var keyPath = APersonsKey();

        // No passphrase passed, and the public half is readable without one.
        store.Register(Locator, Secret, PersonKey.PublicHalfOf(keyPath));

        await Assert.That(store.Holds(Locator)).IsTrue()
            .Because("a locked key is not needed to seal TO somebody, only to open what was "
                   + "sealed to them.");
    }

    [Test]
    public async Task A_person_with_no_key_is_told_to_make_one()
    {
        var store = AStore(out _);
        var absent = Path.Combine(ATempDir("none"), "person-key");

        var refused = Assert.Throws<CredentialUnavailableException>(
            () => store.Register(Locator, Secret, PersonKey.PublicHalfOf(absent)));

        await Assert.That(refused!.Message).Contains("gg key create")
            .Because("the act that would fix it is one command, and a person who has never minted "
                   + "a key has no way to guess that from 'there is no key'.");
    }

    [Test]
    public async Task A_registered_credential_names_one_holder_and_not_two()
    {
        // ONE HOLDER, DELIBERATELY. Sealing to the person AND the machine was the
        // option that was put and declined: it would leave the machine key able
        // to move the credential, so the prompt on a push would be a speed bump
        // rather than a control, and gg doctor's sentence would have to say so.
        var store = AStore(out _);

        store.Register(Locator, Secret, PersonKey.PublicHalfOf(APersonsKey()));

        await Assert.That(store.HoldersOf(Locator).Count).IsEqualTo(1)
            .Because("a second holder added at registration is a second holder nobody asked for, "
                   + "and `gg credential trust-this-machine` is how one is asked for.");
    }

    [Test]
    public async Task The_secret_is_the_only_thing_not_echoed_on_this_path()
    {
        // WHERE THE SECRET ENTERS, unchanged by this step. CredentialCommands
        // says it in a comment - "the one place a secret enters this process" -
        // and sealing it to a different holder must not add a second place.
        var store = AStore(out _);
        var keyPath = APersonsKey();

        store.Register(Locator, Secret, PersonKey.PublicHalfOf(keyPath));

        var onDisk = File.ReadAllText(store.SealedPathFor(Locator));

        await Assert.That(onDisk).DoesNotContain(Secret)
            .Because("it is sealed, so the value is not in the file - which is the same assertion "
                   + "the machine-sealed path already carries.");
    }
}
