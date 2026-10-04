using Gg.Client;

namespace Gg.Client.Tests;

/// <summary>
/// What a send says about a credential it cannot open.
/// </summary>
/// <remarks>
/// <para>
/// <b>Written from a walk, because nothing in a test harness produced it.</b>
/// A credential directory sealed on vmlinux001 was carried to vmlinux002 —
/// ADR-0037's own falsifier — and `gg credential send` printed two sentences in
/// a row that contradict each other:
/// </para>
/// <code>
/// gg: sending the credential this machine holds for local:walk/s59
/// gg: this machine holds no credential for local:walk/s59
/// </code>
/// <para>
/// <b>Both are wrong.</b> The first is said BEFORE the read that fails, so it
/// promises something that then does not happen. The second says the machine
/// holds nothing, when it holds a credential it cannot OPEN — which is rule 9's
/// exact distinction, broken in the one verb this slice added.
/// </para>
/// <para>
/// <b>A person reading that goes to the wrong place.</b> "Holds no credential"
/// sends them to `gg credential add`, which would seal a second copy beside one
/// that was never the problem. What they need to know is that the credential
/// here belongs to another machine.
/// </para>
/// </remarks>
public class ASendSaysWhatItFoundTests
{
    private const string Locator = "local:acme/widgets";

    private static string ATempDirectory() =>
        Path.Combine(Path.GetTempPath(), "gg-send-said-" + Guid.NewGuid().ToString("N"));

    private sealed class APrompt(string answer) : ISecretPrompt
    {
        public int Asked { get; private set; }

        public string ReadSecret(string prompt)
        {
            Asked++;
            return answer;
        }

        public string ReadLine(string prompt) => "";
    }

    /// <summary>One machine's credentials, read through another machine's key.</summary>
    private static (FileCredentialStore Store, MachineKey Key) ACarriedStore()
    {
        var root = ATempDirectory();

        new FileCredentialStore(root, MachineKey.LoadOrCreate(ATempDirectory() + "/sealed-it"))
            .Write(Locator, "ghp-sealed-somewhere-else");

        var key = MachineKey.LoadOrCreate(ATempDirectory() + "/carried-to");
        return (new FileCredentialStore(root, key), key);
    }

    [Test]
    public async Task It_does_not_promise_to_send_what_it_cannot_open()
    {
        var (store, key) = ACarriedStore();
        var said = new List<string>();

        _ = SendACredential.EnvelopeFor(store, key, Locator, new APrompt("typed"), said.Add);

        await Assert.That(said.Any(s => s.Contains("sending the credential this machine holds")))
            .IsFalse()
            .Because("it is said before the read that fails, so it promises something that then "
                   + "does not happen. Said: " + string.Join(" | ", said));
    }

    [Test]
    public async Task It_does_not_say_the_machine_holds_nothing()
    {
        // RULE 9, in the verb this slice added. "Holds no credential" sends a
        // person to `gg credential add`, which would seal a second copy beside
        // one that was never the problem.
        var (store, key) = ACarriedStore();
        var said = new List<string>();

        _ = SendACredential.EnvelopeFor(store, key, Locator, new APrompt("typed"), said.Add);

        await Assert.That(said.Any(s => s.Contains("holds no credential"))).IsFalse()
            .Because("it holds one; it cannot open it, which is a different fact and a "
                   + "different remedy. Said: " + string.Join(" | ", said));
    }

    [Test]
    public async Task It_says_the_credential_here_belongs_to_another_machine()
    {
        var (store, key) = ACarriedStore();
        var said = new List<string>();

        _ = SendACredential.EnvelopeFor(store, key, Locator, new APrompt("typed"), said.Add);

        var all = string.Join(" | ", said);

        await Assert.That(all).Contains(Locator);
        await Assert.That(all.Contains("cannot open", StringComparison.OrdinalIgnoreCase)).IsTrue()
            .Because("that is the fact, and the one a person can act on. Said: " + all);
    }

    [Test]
    public async Task It_still_prompts_so_the_send_can_go_ahead()
    {
        // TELLING SOMEBODY IS NOT REFUSING THEM. They may well have the value
        // and want it on that runner; what they must not be told is that this
        // machine has nothing.
        var (store, key) = ACarriedStore();
        var prompt = new APrompt("typed");

        var envelope = SendACredential.EnvelopeFor(store, key, Locator, prompt, _ => { });

        await Assert.That(prompt.Asked).IsEqualTo(1);
        await Assert.That(envelope).IsNotNull();
    }

    [Test]
    public async Task A_machine_that_really_holds_none_still_says_so()
    {
        // THE OTHER ARM MUST NOT MOVE. An empty store is a different fact and
        // keeps its own sentence, or this fix has just traded one wrong message
        // for another.
        var store = new FileCredentialStore(
            ATempDirectory(), MachineKey.LoadOrCreate(ATempDirectory() + "/k"));
        var said = new List<string>();

        _ = SendACredential.EnvelopeFor(
            store, MachineKey.LoadOrCreate(ATempDirectory() + "/k2"), Locator,
            new APrompt("typed"), said.Add);

        await Assert.That(string.Join(" | ", said)).Contains("holds no credential");
    }

    [Test]
    public async Task And_a_machine_that_can_open_it_still_says_it_is_sending_it()
    {
        var root = ATempDirectory();
        var key = MachineKey.LoadOrCreate(ATempDirectory() + "/mine");
        var store = new FileCredentialStore(root, key);
        store.Write(Locator, "ghp-sealed-here");

        var said = new List<string>();
        _ = SendACredential.EnvelopeFor(store, key, Locator, new APrompt("typed"), said.Add);

        await Assert.That(string.Join(" | ", said))
            .Contains("sending the credential this machine holds");
    }
}
