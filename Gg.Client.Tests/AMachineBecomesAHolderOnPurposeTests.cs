using Gg.Client;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// A machine becomes a holder of a credential because somebody said so, and
/// saying so costs that person's passphrase.
/// </summary>
/// <remarks>
/// <para>
/// <b>S64.2-02, and the half that makes S64.2-01 liveable.</b> A credential
/// sealed to a person alone cannot be resolved by the machine it was typed on,
/// which is right for a laptop that registers credentials and pushes them, and
/// wrong for the same laptop running a flight by hand. So the machine may be
/// ADDED — once, deliberately, by the person who holds it.
/// </para>
/// <para>
/// <b>This is the one act in the step that genuinely needs a passphrase</b>,
/// because it is a rewrap: the content key is unwrapped under the person's key
/// and wrapped again for the machine. Registering needs only a public half and
/// asks for nothing (<c>ACredentialIsSealedToItsPersonTests</c>); this unwraps,
/// so it cannot be performed by anything that is not the person.
/// </para>
/// <para>
/// <b>And it is Decision 8's second holder, pointed at a machine.</b> The ADR
/// already says adding a holder is <i>"a rewrap by somebody who already holds
/// it"</i>, so this is not new machinery — it is <c>Rewrap</c> with the
/// recipient being the local machine rather than a runner across a channel.
/// </para>
/// <para>
/// <b>What it is NOT is a way back to sealing to both by default.</b> The option
/// of making the machine a holder at registration was put and declined. The
/// difference between that and this is that this one is in a shell history.
/// </para>
/// </remarks>
public class AMachineBecomesAHolderOnPurposeTests
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

    private static FileCredentialStore ARegisteredCredential(
        out MachineKey machine, out string keyPath)
    {
        var store = AStore(out machine);
        keyPath = APersonsKey();
        store.Register(Locator, Secret, PersonKey.PublicHalfOf(keyPath));
        return store;
    }

    [Test]
    public async Task A_trusted_machine_resolves_the_credential()
    {
        var store = ARegisteredCredential(out _, out var keyPath);

        store.TrustThisMachine(Locator, PersonKey.Unlock(keyPath, Passphrase));

        await Assert.That(store.Read(Locator)).IsEqualTo(Secret)
            .Because("the whole point of the act is that this machine can then resolve it with "
                   + "nobody present.");
    }

    [Test]
    public async Task And_the_person_is_still_a_holder_afterwards()
    {
        // IT ADDS RATHER THAN MOVES, which Rewrap's own remark already states: a
        // push must not cost the pusher its own access. Trusting a machine that
        // took the credential away from the person who registered it would mean
        // they could never push it anywhere else.
        var store = ARegisteredCredential(out var machine, out var keyPath);

        store.TrustThisMachine(Locator, PersonKey.Unlock(keyPath, Passphrase));

        await Assert.That(store.HoldersOf(Locator)).Contains(PersonKey.PublicHalfOf(keyPath))
            .Because("they registered it; trusting a machine is not handing it over.");

        await Assert.That(store.HoldersOf(Locator)).Contains(machine.PublicKey)
            .Because("and the machine is now a holder, which is the act itself.");
    }

    [Test]
    public async Task The_body_is_not_touched()
    {
        // DECISION 3 AGAIN, LOCALLY. Sealing again would mint a fresh nonce, so a
        // byte-identical ciphertext is what shows the credential was never
        // decrypted on the way to being shared with the machine.
        var store = ARegisteredCredential(out _, out var keyPath);
        var before = store.SealedFor(Locator).Ciphertext;

        store.TrustThisMachine(Locator, PersonKey.Unlock(keyPath, Passphrase));

        await Assert.That(store.SealedFor(Locator).Ciphertext).IsEqualTo(before)
            .Because("thirty-two bytes were rewrapped; the credential itself never came out.");
    }

    [Test]
    public async Task A_wrong_passphrase_changes_nothing()
    {
        var store = ARegisteredCredential(out var machine, out var keyPath);

        await Assert.That(() => PersonKey.Unlock(keyPath, "not the passphrase"))
            .Throws<CredentialUnavailableException>()
            .Because("the key does not open, so the act cannot even be attempted - which is where "
                   + "a wrong passphrase should stop.");

        await Assert.That(store.HoldersOf(Locator)).DoesNotContain(machine.PublicKey)
            .Because("nothing changed, and a half-performed rewrap would be a credential in a "
                   + "state nobody designed.");
    }

    [Test]
    public async Task A_person_who_does_not_hold_it_is_refused_and_told_why()
    {
        // NOT SEALED TO YOU IS NOT CORRUPT, reaching the local act. Somebody with
        // a key that was never a holder - a second maintainer on a shared machine,
        // or a key re-minted after the old one was lost - must be sent to ask for
        // a push rather than to suspect the file.
        var store = ARegisteredCredential(out _, out _);
        var stranger = PersonKey.Unlock(APersonsKey(), Passphrase);

        var refused = Assert.Throws<CredentialUnavailableException>(
            () => store.TrustThisMachine(Locator, stranger));

        await Assert.That(refused!.Message).DoesNotContain(Secret)
            .Because("a refusal about a credential is not a place to print one.");

        await Assert.That(refused.Message).Contains("never pushed here")
            .Because("SaidWhenNoHolder is the sentence that sends somebody to the right place, and "
                   + "a second wording for the same situation is a second thing to keep true.");
    }

    [Test]
    public async Task Trusting_a_machine_that_already_holds_it_is_not_an_error()
    {
        // RUNNING IT TWICE IS WHAT PEOPLE DO, after forgetting whether they did.
        // Rewrap throws ArgumentException for a holder it already has - that is
        // right for Rewrap, whose caller is deciding something, and wrong to
        // surface to somebody who asked for a state that already obtains.
        var store = ARegisteredCredential(out _, out var keyPath);

        store.TrustThisMachine(Locator, PersonKey.Unlock(keyPath, Passphrase));
        store.TrustThisMachine(Locator, PersonKey.Unlock(keyPath, Passphrase));

        await Assert.That(store.Read(Locator)).IsEqualTo(Secret)
            .Because("asking twice for a machine to be trusted leaves it trusted, not broken.");

        await Assert.That(store.HoldersOf(Locator).Count).IsEqualTo(2)
            .Because("and it does not accumulate a wrapped key per attempt - one per holder is "
                   + "what WrappedFor depends on.");
    }
}
