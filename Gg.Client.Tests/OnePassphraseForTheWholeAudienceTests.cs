using Gg.Client;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// One passphrase moves a credential to every machine in the audience.
/// </summary>
/// <remarks>
/// <para>
/// <b>S64.5-03, and rule 3 of the slice.</b> <i>"A broadcast that asked per machine
/// would teach people to script it, and a scripted passphrase is a stored
/// passphrase."</i> That is the whole argument: the control is only a control while a
/// person is willing to type it, and asking eight times is how somebody ends up putting
/// it in a shell variable.
/// </para>
/// <para>
/// <b>It falls out of resolving the opener ONCE, outside the loop</b> rather than from
/// anything clever. The opener is a holder (slice sixty-four step 1), so one unlocked
/// key rewraps the content key per recipient - each rewrap is thirty-two bytes and needs
/// no further human.
/// </para>
/// <para>
/// <b>Asserted by counting prompts across a multi-recipient push</b>, because the shape
/// that fails this is a loop that happens to call the opener resolution inside itself -
/// which looks correct, works for one recipient, and asks N times for N.
/// </para>
/// </remarks>
public class OnePassphraseForTheWholeAudienceTests
{
    private const string Locator = "local:acme/widgets";
    private const string Secret = "ghp-not-a-real-token";
    private const string Passphrase = "correct horse battery staple";

    private static string ATempDir(string tag) =>
        Path.Combine(Path.GetTempPath(), $"gg-{tag}-" + Guid.NewGuid().ToString("N"));

    private sealed class Counts(params string[] replies) : ISecretPrompt
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

    private static CredentialAudienceRow ARow(string label) =>
        new(RunnerId: label + "-id", Label: label, Locator: Locator,
            Declared: true, Reported: false, Reachable: true, Through: null);

    [Test]
    public async Task Three_recipients_cost_one_passphrase()
    {
        var machine = MachineKey.LoadOrCreate(Path.Combine(ATempDir("mk"), "machine-key"));
        var store = new FileCredentialStore(ATempDir("store"), machine);

        var keyPath = Path.Combine(ATempDir("pk"), "person-key");
        PersonKey.Create(keyPath, Passphrase);
        store.Register(Locator, Secret, PersonKey.PublicHalfOf(keyPath));

        var prompt = new Counts(Passphrase);

        var sending = CredentialBroadcast.Opened(
            store, machine, Locator,
            [ARow("vmlinux001"), ARow("vmlinux002"), ARow("vmlinux003")],
            prompt, saying: null, personKeyPath: keyPath);

        await Assert.That(sending).IsNotNull()
            .Because("there is a credential and there are recipients, so there is something to do.");

        await Assert.That(prompt.Asked.Count).IsEqualTo(1)
            .Because("one passphrase, three machines. Asking per machine is what teaches somebody "
                   + "to script it. Asked: " + string.Join(" | ", prompt.Asked));
    }

    [Test]
    public async Task And_the_one_opener_can_rewrap_for_every_recipient()
    {
        // ONE PROMPT IS WORTH NOTHING IF THE RESULT ONLY WORKS ONCE. The opener is
        // handed back and used per recipient, so this checks it survives being used
        // more than once - a holder that consumed itself would pass the count above and
        // fail on the second machine.
        var machine = MachineKey.LoadOrCreate(Path.Combine(ATempDir("mk"), "machine-key"));
        var store = new FileCredentialStore(ATempDir("store"), machine);

        var keyPath = Path.Combine(ATempDir("pk"), "person-key");
        PersonKey.Create(keyPath, Passphrase);
        store.Register(Locator, Secret, PersonKey.PublicHalfOf(keyPath));

        var sending = CredentialBroadcast.Opened(
            store, machine, Locator, [ARow("a"), ARow("b")],
            new Counts(Passphrase), saying: null, personKeyPath: keyPath)!;

        for (var recipient = 0; recipient < 3; recipient++)
        {
            using var theirs = System.Security.Cryptography.ECDiffieHellman.Create(
                System.Security.Cryptography.ECCurve.NamedCurves.nistP256);

            var publicHalf = Convert.ToBase64String(
                theirs.PublicKey.ExportSubjectPublicKeyInfo());

            var forThem = CredentialSeal.Rewrap(sending.Envelope, sending.Opener, publicHalf);

            await Assert.That(CredentialSeal.Open(forThem, theirs)).IsEqualTo(Secret)
                .Because($"recipient {recipient} opens what the one unlocked key wrapped for it.");
        }
    }

    [Test]
    public async Task An_empty_audience_asks_for_nothing_at_all()
    {
        // NOBODY TO SEND TO IS NOT A REASON TO UNLOCK A KEY. A passphrase read and then
        // discarded is a passphrase typed for nothing, and the person would reasonably
        // assume something moved.
        var machine = MachineKey.LoadOrCreate(Path.Combine(ATempDir("mk"), "machine-key"));
        var store = new FileCredentialStore(ATempDir("store"), machine);

        var keyPath = Path.Combine(ATempDir("pk"), "person-key");
        PersonKey.Create(keyPath, Passphrase);
        store.Register(Locator, Secret, PersonKey.PublicHalfOf(keyPath));

        var prompt = new Counts(Passphrase);

        var sending = CredentialBroadcast.Opened(
            store, machine, Locator, [], prompt, saying: null, personKeyPath: keyPath);

        await Assert.That(sending).IsNull()
            .Because("nothing is moving, so nothing was opened.");

        await Assert.That(prompt.Asked).IsEmpty()
            .Because("and nobody was asked for a passphrase to achieve it.");
    }

    [Test]
    public async Task An_audience_of_only_unreachable_machines_asks_for_nothing_either()
    {
        // A POOL AND NOTHING ELSE. Every row present, none reachable - so there is a
        // list worth printing and no push to perform, and unlocking a key for it would
        // be the same passphrase-for-nothing.
        var machine = MachineKey.LoadOrCreate(Path.Combine(ATempDir("mk"), "machine-key"));
        var store = new FileCredentialStore(ATempDir("store"), machine);

        var keyPath = Path.Combine(ATempDir("pk"), "person-key");
        PersonKey.Create(keyPath, Passphrase);
        store.Register(Locator, Secret, PersonKey.PublicHalfOf(keyPath));

        var prompt = new Counts(Passphrase);

        var unreachable = new CredentialAudienceRow(
            RunnerId: "member-id", Label: "gg-pool-ui-2", Locator: Locator,
            Declared: false, Reported: false, Reachable: false, Through: "vmlinux001");

        var sending = CredentialBroadcast.Opened(
            store, machine, Locator, [unreachable], prompt, saying: null, personKeyPath: keyPath);

        await Assert.That(sending).IsNull();
        await Assert.That(prompt.Asked).IsEmpty()
            .Because("the list is still worth printing - that is S64.5-07 - but there is nothing "
                   + "to unlock a key for.");
    }
}
