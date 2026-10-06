using Gg.Client;

namespace Gg.Client.Tests;

/// <summary>
/// A push never falls back to the machine key, and what it offers instead is the
/// thing that would actually work.
/// </summary>
/// <remarks>
/// <para>
/// <b>S64.3-03 AS WRITTEN WAS WRONG, and the class is named after it so the
/// correction is findable.</b> The criterion said <i>"a machine with no person key
/// is told to mint one rather than falling back to its machine key"</i>. The second
/// half is right and is the thing that matters. The first half is bad advice:
/// <b>minting a new key does not open a credential sealed to the old one.</b>
/// Sending somebody to <c>gg key create</c> here would have them make a key that
/// cannot help, and on a machine that already has one it would invite them to
/// overwrite the key their other credentials are sealed to — which
/// <c>PersonKey</c>'s own refusal exists to prevent.
/// </para>
/// <para>
/// <b>Where <c>gg key create</c> IS the right advice is registering</b>, which step
/// 2 does: there has to be a key to seal a NEW credential to, and
/// <c>ACredentialIsSealedToItsPersonTests</c> asserts that sentence. Opening an
/// existing one is a different question with a different answer.
/// </para>
/// <para>
/// <b>So the honest answer is the one that was already written.</b> A credential
/// this machine holds and cannot open — because it is sealed to a person whose key
/// is not here, or to a key that is not a holder — produces the sentence
/// <c>EnvelopeFor</c> has had all along: <i>"this machine holds a credential for X
/// and cannot open it - it was sealed somewhere else. Type the value to send it
/// anyway, or push it here from the machine that holds it."</i> Two things that
/// work, and neither is a new key.
/// </para>
/// <para>
/// <b>And NO FALLBACK, which is the half worth a test.</b> Passing the machine key
/// as the opener for an envelope the machine cannot open would work on every
/// machine, refuse nobody, be indistinguishable from the behaviour step 2 removed —
/// and produce an envelope that opens nowhere, diagnosed on the recipient's machine.
/// What happens instead is a fresh envelope sealed to the machine around a value the
/// person typed, which is a legitimate use of that key rather than a substitution.
/// </para>
/// </remarks>
public class APushWithoutAKeyIsRefusedTests
{
    private const string Locator = "local:acme/widgets";
    private const string Stored = "ghp-the-one-in-the-store";
    private const string Typed = "ghp-the-one-they-typed";
    private const string Passphrase = "correct horse battery staple";

    private static string ATempDir(string tag) =>
        Path.Combine(Path.GetTempPath(), $"gg-{tag}-" + Guid.NewGuid().ToString("N"));

    private static FileCredentialStore AStore(out MachineKey machine)
    {
        machine = MachineKey.LoadOrCreate(Path.Combine(ATempDir("mk"), "machine-key"));
        return new FileCredentialStore(ATempDir("store"), machine);
    }

    /// <summary>A credential sealed to a person whose key is not on this machine.</summary>
    private static FileCredentialStore SomebodyElsesCredential(out MachineKey machine)
    {
        var store = AStore(out machine);
        var theirs = Path.Combine(ATempDir("theirs"), "person-key");
        PersonKey.Create(theirs, Passphrase);
        store.Register(Locator, Stored, PersonKey.PublicHalfOf(theirs));
        return store;
    }

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
    public async Task The_machine_key_is_never_the_opener_for_an_envelope_it_cannot_open()
    {
        // THE ONE THAT WOULD UNDO THE STEP, and the only way to see it is to check
        // WHICH envelope came back. A fallback returns the stored envelope with the
        // machine key beside it, and nothing fails until a runner cannot open what it
        // was sent.
        var store = SomebodyElsesCredential(out var machine);

        var sending = SendACredential.EnvelopeFor(
            store, machine, Locator, new Answers(Typed), saying: null,
            personKeyPath: Path.Combine(ATempDir("none"), "person-key"));

        await Assert.That(sending).IsNotNull()
            .Because("there is still a way to send it: the person typed the value.");

        await Assert.That(Gg.Contracts.CredentialSeal.Open(sending!.Envelope, sending.Opener))
            .IsEqualTo(Typed)
            .Because("the envelope that came back is a FRESH one around what they typed, sealed to "
                   + "this machine - a legitimate use of that key. Had it come back holding the "
                   + "stored value with the machine key beside it, this would read " + Stored
                   + " and the push would have produced something the recipient cannot open.");
    }

    [Test]
    public async Task And_the_person_is_told_what_would_actually_work()
    {
        var store = SomebodyElsesCredential(out var machine);
        var said = new List<string>();

        _ = SendACredential.EnvelopeFor(
            store, machine, Locator, new Answers(Typed), saying: said.Add,
            personKeyPath: Path.Combine(ATempDir("none"), "person-key"));

        var all = string.Join(" ", said);

        await Assert.That(all).Contains("cannot open it")
            .Because("this machine holds it and cannot open it, which is neither 'absent' nor a "
                   + "fault. Said: " + all);

        await Assert.That(all).Contains("push it here from the machine that holds it")
            .Because("that is one of the two things that work, and the one that keeps the sealed "
                   + "copy rather than minting a second. Said: " + all);

        await Assert.That(all).DoesNotContain("gg key create")
            .Because("a new key cannot open a credential sealed to an old one, and on a machine "
                   + "that already has a key this would invite somebody to overwrite the one "
                   + "their other credentials are sealed to. Said: " + all);
    }

    [Test]
    public async Task A_key_that_exists_but_holds_nothing_is_answered_the_same_way()
    {
        // THE TWO SITUATIONS ARE ONE SITUATION, which is what the criterion got
        // wrong by splitting them. "No key here" and "a key that is not a holder"
        // have the same remedy: be pushed a copy, or type the value.
        var store = SomebodyElsesCredential(out var machine);

        var mine = Path.Combine(ATempDir("mine"), "person-key");
        PersonKey.Create(mine, Passphrase);

        var prompt = new Answers(Typed);

        var sending = SendACredential.EnvelopeFor(
            store, machine, Locator, prompt, saying: null, personKeyPath: mine);

        await Assert.That(Gg.Contracts.CredentialSeal.Open(sending!.Envelope, sending.Opener))
            .IsEqualTo(Typed);

        await Assert.That(prompt.Asked.Count).IsEqualTo(1)
            .Because("one prompt, for the value - no passphrase was asked for, because unlocking a "
                   + "key that is not a holder would have been a question with no useful answer. "
                   + "Asked: " + string.Join(" | ", prompt.Asked));
    }

    [Test]
    public async Task Typing_nothing_sends_nothing()
    {
        // THE ESCAPE FROM THE ESCAPE HATCH. Somebody offered "type the value" who
        // does not have it presses return, and that has to be a no rather than an
        // empty credential pushed to a runner.
        var store = SomebodyElsesCredential(out var machine);

        var sending = SendACredential.EnvelopeFor(
            store, machine, Locator, new Answers(""), saying: null,
            personKeyPath: Path.Combine(ATempDir("none"), "person-key"));

        await Assert.That(sending).IsNull()
            .Because("an empty answer is how a person says they do not have it, and nothing is "
                   + "sent - which is what the caller turns into a sentence.");
    }
}
