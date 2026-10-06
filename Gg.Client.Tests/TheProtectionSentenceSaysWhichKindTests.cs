using Gg.Client;

namespace Gg.Client.Tests;

/// <summary>
/// `gg doctor` says what the store actually protects, and after step 2 that is
/// two different things at once.
/// </summary>
/// <remarks>
/// <para>
/// <b>S64.2-04.</b> <c>ICredentialStore.Protection</c> is printed verbatim and its
/// own rule is that it <i>"must not imply protection the store does not have"</i>.
/// Before step 2 one sentence was true of the whole directory: everything was
/// sealed to this machine's key, and the honesty clause said what that bought —
/// <i>"a copy of this directory alone ... opens nowhere"</i>.
/// </para>
/// <para>
/// <b>Now a directory can hold two kinds, and the clause is true of only one.</b>
/// A credential sealed to a PERSON is not openable by anything on this machine, so
/// for that one the sentence understates: an attacker running as this user gets
/// nothing at all, not even with the machine key. A credential this machine is a
/// holder of is exactly as protected as it was. One sentence that is true of both
/// may not exist, which is why this criterion was written expecting a line per
/// kind rather than a reworded sentence.
/// </para>
/// <para>
/// <b>It counts rather than describing an intention</b>, which is the shape the
/// property already has for the plaintext migration — and for the same reason: a
/// machine mid-anything holds a mixture, and a sentence that named only the
/// majority would be the lie the rule forbids.
/// </para>
/// <para>
/// <b>Nothing here opens anything.</b> Whether this machine is a holder is a
/// question about the wrapped keys in the envelope, which <c>HoldersOf</c> already
/// answers without deriving a content key — the same guarantee <c>Holds</c>
/// carries.
/// </para>
/// </remarks>
public class TheProtectionSentenceSaysWhichKindTests
{
    private const string Mine = "local:acme/mine";
    private const string Theirs = "local:acme/theirs";
    private const string Secret = "ghp-not-a-real-token";
    private const string Passphrase = "correct horse battery staple";

    private static string ATempDir(string tag) =>
        Path.Combine(Path.GetTempPath(), $"gg-{tag}-" + Guid.NewGuid().ToString("N"));

    private static FileCredentialStore AStore()
    {
        return new FileCredentialStore(
            ATempDir("store"),
            MachineKey.LoadOrCreate(Path.Combine(ATempDir("mk"), "machine-key")));
    }

    private static string APersonsKey()
    {
        var path = Path.Combine(ATempDir("pk"), "person-key");
        PersonKey.Create(path, Passphrase);
        return path;
    }

    [Test]
    public async Task A_store_of_only_machine_sealed_credentials_says_what_it_always_said()
    {
        // UNCHANGED FOR A RUNNER, which is most of the fleet. A pool host holds
        // only credentials pushed to it, sealed to its own key, and its doctor
        // output must not start talking about people who are not there.
        var store = AStore();
        store.Write(Mine, Secret);

        await Assert.That(store.Protection).Contains("sealed to this machine's own key")
            .Because("that is what it is, and a runner's operator reads this sentence to find out "
                   + "whether a stolen disk matters.");

        await Assert.That(store.Protection).Contains("opens nowhere")
            .Because("the honesty clause ADR-0037 requires be kept saying in as many words.");
    }

    [Test]
    public async Task A_store_of_only_person_sealed_credentials_says_this_machine_cannot_open_them()
    {
        var store = AStore();
        store.Register(Theirs, Secret, PersonKey.PublicHalfOf(APersonsKey()));

        await Assert.That(store.Protection).Contains("cannot open")
            .Because("this is a stronger claim than the machine-sealed one and it is true: nothing "
                   + "running as this user can get at the value, with or without the machine key.");
    }

    [Test]
    public async Task A_mixed_store_says_how_many_of_each()
    {
        var store = AStore();
        store.Write(Mine, Secret);
        store.Register(Theirs, Secret, PersonKey.PublicHalfOf(APersonsKey()));

        var said = store.Protection;

        await Assert.That(said).Contains("1")
            .Because("it counts rather than describing an intention, which is how the sentence "
                   + "stays true of a directory holding both. Said: " + said);

        foreach (var half in (string[])["this machine", "cannot open"])
        {
            await Assert.That(said).Contains(half)
                .Because($"both kinds are present, so a sentence naming only one is the lie the "
                       + $"rule forbids. Missing '{half}'. Said: " + said);
        }
    }

    [Test]
    public async Task The_sentence_never_claims_more_than_the_directory_holds()
    {
        // THE RULE, ASSERTED DIRECTLY. A store with a person-sealed credential in
        // it must not say the machine key opens everything, and a store with a
        // machine-sealed one must not say nothing here can open them.
        var onlyMachine = AStore();
        onlyMachine.Write(Mine, Secret);

        await Assert.That(onlyMachine.Protection).DoesNotContain("cannot open")
            .Because("this machine CAN open what it sealed for itself, and claiming otherwise "
                   + "would send an operator looking for a person who does not exist.");

        var onlyPerson = AStore();
        onlyPerson.Register(Theirs, Secret, PersonKey.PublicHalfOf(APersonsKey()));

        await Assert.That(onlyPerson.Protection)
            .DoesNotContain("Anything running as this user can read that key and open them")
            .Because("nothing here can open them, so repeating the machine-sealed caveat would "
                   + "overstate the exposure in the other direction.");
    }

    [Test]
    public async Task And_the_plaintext_migration_clause_still_appears_beside_it()
    {
        // THREE SHAPES AT ONCE IS POSSIBLE on a machine that predates sealing and
        // has since had a credential registered. The pre-sealing clause is not
        // replaced by the new one; a reader needs both.
        var store = AStore();
        store.Register(Theirs, Secret, PersonKey.PublicHalfOf(APersonsKey()));

        var path = store.PathFor(Mine);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "ghp-from-before-sealing");

        await Assert.That(store.Protection).Contains("plaintext")
            .Because("a value sitting in the clear is the most important thing this sentence can "
                   + "say, and a new clause must not have pushed it out. Said: " + store.Protection);
    }

    [Test]
    public async Task Asking_opens_nothing()
    {
        // THE GUARANTEE THAT MAKES THIS SENTENCE SAFE TO PRINT ANYWHERE. If
        // answering it needed a content key, a doctor run on a laptop would
        // prompt for a passphrase - and a diagnostic that cannot run without one
        // is a diagnostic nobody runs.
        var store = AStore();
        store.Register(Theirs, Secret, PersonKey.PublicHalfOf(APersonsKey()));

        var said = store.Protection;

        await Assert.That(said).DoesNotContain(Secret)
            .Because("it was never decrypted, which is why there is no passphrase in the way of "
                   + "`gg doctor`.");
    }
}
