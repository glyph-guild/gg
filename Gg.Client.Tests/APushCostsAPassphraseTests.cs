using Gg.Client;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// Moving a credential a person holds costs that person's passphrase, and the
/// thing performing the rewrap is their key.
/// </summary>
/// <remarks>
/// <para>
/// <b>S64.3-01, and ADR-0037 Decision 2 finally becoming true.</b> <i>"A person
/// has a key, and nothing else opens a credential"</i> — and until this step the
/// push read <c>MachineKey.LoadOrCreate()</c> and rewrapped with that, so
/// anything running as the user could move a credential to any machine it could
/// reach with no human in the act.
/// </para>
/// <para>
/// <b>WHO HOLDS IT DECIDES, not where the envelope came from</b>, and the
/// alternative was worse. "A stored credential costs a passphrase, a typed one
/// does not" sounds right and is wrong: a credential sealed to THIS MACHINE — one
/// pushed here, one from before step 2, one this machine minted for its own agent
/// — is already openable by the machine with nobody present, so demanding a
/// passphrase to move it would be asking for a control that the machine key
/// already renders void. The honest rule is the one the envelope itself answers:
/// if the machine is a holder, the machine opens it; otherwise the person does,
/// and that costs a passphrase.
/// </para>
/// <para>
/// <b>Which makes S64.2-05's "no flag day" true of sends as well.</b> Every
/// machine-sealed credential in the field keeps pushing exactly as it does today,
/// with no prompt, because nothing about what the machine can already do changed.
/// </para>
/// <para>
/// <b>And it names a consequence of trusting a machine out loud.</b> Once
/// <c>gg credential trust-this-machine</c> has run, that machine is a holder — so
/// it can push the credential onward with no passphrase. That is what trusting it
/// MEANS, and it is the price ADR-0037 Decision 5 states: <i>"a host that holds a
/// credential can open it, so a push to a host is a decision about that host"</i>.
/// A person who is not told that would learn it from a push they did not make.
/// </para>
/// </remarks>
public class APushCostsAPassphraseTests
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

    /// <summary>A prompt that answers with whatever it is given, and remembers asking.</summary>
    private sealed class Answers(params string[] replies) : ISecretPrompt
    {
        private int _at;

        internal List<string> Asked { get; } = [];

        public string ReadSecret(string prompt)
        {
            Asked.Add(prompt);
            return _at < replies.Length ? replies[_at++] : "";
        }

        public string ReadLine(string prompt)
        {
            Asked.Add(prompt);
            return _at < replies.Length ? replies[_at++] : "";
        }
    }

    [Test]
    public async Task A_credential_sealed_to_a_person_is_opened_by_that_person()
    {
        var store = AStore(out _);
        var keyPath = APersonsKey();
        store.Register(Locator, Secret, PersonKey.PublicHalfOf(keyPath));

        var prompt = new Answers(Passphrase);

        var sending = SendACredential.EnvelopeFor(
            store, MachineKey.LoadOrCreate(Path.Combine(ATempDir("mk2"), "machine-key")),
            Locator, prompt, saying: null, personKeyPath: keyPath);

        await Assert.That(sending).IsNotNull()
            .Because("the person holds it, so there is something to send.");

        await Assert.That(prompt.Asked).IsNotEmpty()
            .Because("a passphrase was asked for, which is the whole of Decision 2.");

        await Assert.That(CredentialSeal.Open(sending!.Envelope, sending.Opener)).IsEqualTo(Secret)
            .Because("the opener handed back is the one that can open the envelope handed back - "
                   + "a pair that disagreed would fail on the recipient's machine.");
    }

    [Test]
    public async Task And_the_prompt_says_what_the_passphrase_is_for()
    {
        var store = AStore(out _);
        var keyPath = APersonsKey();
        store.Register(Locator, Secret, PersonKey.PublicHalfOf(keyPath));

        var prompt = new Answers(Passphrase);

        _ = SendACredential.EnvelopeFor(
            store, MachineKey.LoadOrCreate(Path.Combine(ATempDir("mk2"), "machine-key")),
            Locator, prompt, saying: null, personKeyPath: keyPath);

        await Assert.That(prompt.Asked[0]).Contains("passphrase", StringComparison.OrdinalIgnoreCase)
            .Because("somebody being asked for a secret has to know WHICH secret. Asked: "
                   + prompt.Asked[0]);

        await Assert.That(prompt.Asked[0]).DoesNotContain("Secret for", StringComparison.Ordinal)
            .Because("this is not the credential's value - asking for one with the other's words "
                   + "is how a person pastes a token into a passphrase prompt.");
    }

    [Test]
    public async Task A_wrong_passphrase_moves_nothing_and_says_why()
    {
        var store = AStore(out _);
        var keyPath = APersonsKey();
        store.Register(Locator, Secret, PersonKey.PublicHalfOf(keyPath));

        var refused = Assert.Throws<CredentialUnavailableException>(() => SendACredential.EnvelopeFor(
            store, MachineKey.LoadOrCreate(Path.Combine(ATempDir("mk2"), "machine-key")),
            Locator, new Answers("not the passphrase"), saying: null, personKeyPath: keyPath));

        await Assert.That(refused!.Message).DoesNotContain(Secret)
            .Because("a refusal about a credential is not a place to print one.");

        await Assert.That(refused.Message).Contains("passphrase")
            .Because("PersonKey already words this - 'either the passphrase is wrong or the file "
                   + "has been damaged; nothing here can tell which' - and a second wording would "
                   + "be a second thing to keep true.");
    }

    [Test]
    public async Task A_credential_this_machine_holds_costs_nothing_and_that_is_deliberate()
    {
        // NO FLAG DAY FOR SENDS. Every machine-sealed credential in the field - one
        // pushed here, one from before step 2, one this machine minted for its own
        // agent - pushes exactly as it does today. Demanding a passphrase to move
        // something the machine key can already open with nobody present would be
        // asking for a control the machine key renders void.
        var store = AStore(out var machine);
        store.Write(Locator, Secret);

        var prompt = new Answers(Passphrase);

        var sending = SendACredential.EnvelopeFor(
            store, machine, Locator, prompt, saying: null, personKeyPath: APersonsKey());

        await Assert.That(prompt.Asked).IsEmpty()
            .Because("nothing needed unwrapping by a person: the machine is a holder.");

        await Assert.That(CredentialSeal.Open(sending!.Envelope, sending.Opener)).IsEqualTo(Secret)
            .Because("and it still sends.");
    }

    [Test]
    public async Task A_trusted_machine_can_push_it_onward_with_no_passphrase()
    {
        // THE PRICE OF TRUSTING A MACHINE, ASSERTED SO IT IS NOT A SURPRISE. Decision
        // 5 states it: "a host that holds a credential can open it, so a push to a
        // host is a decision about that host". Once trust-this-machine has run, that
        // machine can move the credential on - which is what trusting it means, and
        // a person who was not told would learn it from a push they did not make.
        var store = AStore(out var machine);
        var keyPath = APersonsKey();
        store.Register(Locator, Secret, PersonKey.PublicHalfOf(keyPath));
        store.TrustThisMachine(Locator, PersonKey.Unlock(keyPath, Passphrase));

        var prompt = new Answers(Passphrase);

        var sending = SendACredential.EnvelopeFor(
            store, machine, Locator, prompt, saying: null, personKeyPath: keyPath);

        await Assert.That(prompt.Asked).IsEmpty()
            .Because("the machine is a holder now, so it opens the credential itself - and that "
                   + "is the thing trusting it bought.");

        await Assert.That(CredentialSeal.Open(sending!.Envelope, sending.Opener)).IsEqualTo(Secret);
    }

    [Test]
    public async Task A_typed_value_costs_no_passphrase_either()
    {
        // NOTHING TO UNWRAP. Somebody who types the value has it; sealing it to this
        // machine in memory and rewrapping with the same key is what the path already
        // does, and a passphrase in front of it would guard a secret the person just
        // supplied.
        var store = AStore(out var machine);

        var prompt = new Answers(Secret);

        var sending = SendACredential.EnvelopeFor(
            store, machine, Locator, prompt, saying: null, personKeyPath: APersonsKey());

        await Assert.That(prompt.Asked.Count).IsEqualTo(1)
            .Because("one prompt, for the value. Asked: " + string.Join(" | ", prompt.Asked));

        await Assert.That(prompt.Asked[0]).Contains("Secret for", StringComparison.Ordinal)
            .Because("and it is the value that was asked for, not a passphrase.");

        await Assert.That(CredentialSeal.Open(sending!.Envelope, sending.Opener)).IsEqualTo(Secret);
    }
}
