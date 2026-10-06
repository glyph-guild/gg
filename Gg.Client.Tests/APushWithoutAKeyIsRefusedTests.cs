using Gg.Client;

namespace Gg.Client.Tests;

/// <summary>
/// A machine with no person key is told to mint one. It does not quietly fall back
/// to the machine key.
/// </summary>
/// <remarks>
/// <para>
/// <b>S64.3-03, and the failure mode that would undo the whole step.</b> The
/// tempting shape is "unlock the person's key, and if there isn't one use the
/// machine's" — which works on every machine, never refuses anybody, and restores
/// exactly the hole step 2 was written to close. A fallback is indistinguishable
/// from the old behaviour and nothing would ever have reported it.
/// </para>
/// <para>
/// <b>It is not the same question as "can this machine open it".</b> A credential
/// sealed to this machine is opened by this machine, with no person and no
/// passphrase, deliberately — that is <c>APushCostsAPassphraseTests</c>' no-flag-day
/// half. This is about a credential that needs a PERSON and a machine that has no
/// person's key on it: the honest answer is a refusal naming one command, not a
/// quiet substitution of a key that cannot open it anyway.
/// </para>
/// <para>
/// <b>And the refusal has to be the right one of two.</b> "There is no key" and
/// "this key cannot open that credential" send a person to different places, and
/// <c>SaidWhenNoHolder</c>'s remark is the argument: collapsing two refusals is
/// <i>"an afternoon spent on a file that was always fine"</i>.
/// </para>
/// </remarks>
public class APushWithoutAKeyIsRefusedTests
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

    private sealed class NeverAsked : ISecretPrompt
    {
        public string ReadSecret(string prompt) =>
            throw new InvalidOperationException(
                "nothing should have been asked for: there is no key to unlock. Asked: " + prompt);

        public string ReadLine(string prompt) =>
            throw new InvalidOperationException(
                "nothing should have been asked for: there is no key to unlock. Asked: " + prompt);
    }

    [Test]
    public async Task With_no_person_key_a_push_is_refused_naming_the_command_that_makes_one()
    {
        var store = AStore(out var machine);

        // Sealed to SOMEBODY ELSE's key, so the machine is not a holder and a person
        // is needed - and this machine has no person's key.
        var absentKey = Path.Combine(ATempDir("none"), "person-key");
        var somebodyElse = Path.Combine(ATempDir("pk"), "person-key");
        PersonKey.Create(somebodyElse, Passphrase);
        store.Register(Locator, Secret, PersonKey.PublicHalfOf(somebodyElse));

        var refused = Assert.Throws<CredentialUnavailableException>(() => SendACredential.EnvelopeFor(
            store, machine, Locator, new NeverAsked(), saying: null, personKeyPath: absentKey));

        await Assert.That(refused!.Message).Contains("gg key create")
            .Because("one command fixes it, and nobody guesses the name of a verb from 'there is "
                   + "no key'.");
    }

    [Test]
    public async Task And_nothing_is_asked_for_before_the_refusal()
    {
        // THE PROMPT THROWS IF IT IS TOUCHED, which is how this is asserted rather
        // than hoped: a person with no key who was asked for a passphrase first would
        // type one, have it refused, and reasonably conclude they had typed it wrong.
        var store = AStore(out var machine);
        var somebodyElse = Path.Combine(ATempDir("pk"), "person-key");
        PersonKey.Create(somebodyElse, Passphrase);
        store.Register(Locator, Secret, PersonKey.PublicHalfOf(somebodyElse));

        await Assert.That(() => SendACredential.EnvelopeFor(
                store, machine, Locator, new NeverAsked(), saying: null,
                personKeyPath: Path.Combine(ATempDir("none"), "person-key")))
            .Throws<CredentialUnavailableException>()
            .Because("and not an InvalidOperationException from the prompt, which is what this "
                   + "test fails with if the order is wrong.");
    }

    [Test]
    public async Task It_does_not_fall_back_to_the_machine_key()
    {
        // THE ONE THAT WOULD UNDO THE STEP. A fallback here is the old behaviour
        // wearing new words: it never refuses, so nothing ever reports it, and the
        // passphrase becomes a thing that happens on some machines.
        var store = AStore(out var machine);
        var somebodyElse = Path.Combine(ATempDir("pk"), "person-key");
        PersonKey.Create(somebodyElse, Passphrase);
        store.Register(Locator, Secret, PersonKey.PublicHalfOf(somebodyElse));

        var refused = Assert.Throws<CredentialUnavailableException>(() => SendACredential.EnvelopeFor(
            store, machine, Locator, new NeverAsked(), saying: null,
            personKeyPath: Path.Combine(ATempDir("none"), "person-key")));

        await Assert.That(refused!.Message).DoesNotContain("this machine's own key")
            .Because("the machine key is not an answer to 'who can open this' here - it is not a "
                   + "holder, so substituting it would produce an envelope that opens nowhere.");
    }

    [Test]
    public async Task A_key_that_exists_but_is_not_a_holder_is_refused_differently()
    {
        // TWO REFUSALS, NOT ONE. "There is no key" sends somebody to `gg key create`;
        // "that key cannot open this" sends them to ask for a push. Collapsing them
        // is the afternoon SaidWhenNoHolder's remark is about.
        var store = AStore(out var machine);
        var somebodyElse = Path.Combine(ATempDir("pk"), "person-key");
        PersonKey.Create(somebodyElse, Passphrase);
        store.Register(Locator, Secret, PersonKey.PublicHalfOf(somebodyElse));

        var mine = Path.Combine(ATempDir("mine"), "person-key");
        PersonKey.Create(mine, Passphrase);

        var refused = Assert.Throws<CredentialUnavailableException>(() => SendACredential.EnvelopeFor(
            store, machine, Locator, new Answers(Passphrase), saying: null, personKeyPath: mine));

        await Assert.That(refused!.Message).DoesNotContain("gg key create")
            .Because("there IS a key - telling somebody to mint one when they have one sends them "
                   + "to overwrite the key they already hold.");

        await Assert.That(refused.Message).Contains("never pushed here")
            .Because("SaidWhenNoHolder is the sentence for this, and it names who CAN open it.");
    }

    private sealed class Answers(params string[] replies) : ISecretPrompt
    {
        private int _at;

        public string ReadSecret(string prompt) => _at < replies.Length ? replies[_at++] : "";

        public string ReadLine(string prompt) => _at < replies.Length ? replies[_at++] : "";
    }
}
