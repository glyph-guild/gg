using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// `gg key create` checks the session before it asks for anything, and before it
/// writes a key.
/// </summary>
/// <remarks>
/// <para>
/// <b>The rule is already written down one verb over, and minting broke it.</b>
/// <c>AddAsync</c> says: <i>"The order is deliberate and it is the order of the
/// failures. The session is checked FIRST, before the prompt, because asking
/// somebody for a token and then telling them to log in has taken a secret into a
/// process for nothing."</i>
/// </para>
/// <para>
/// <b>Minting did worse than waste a prompt — it left a key that can never be
/// registered.</b> It read a passphrase, wrote the wrapped key, and only then
/// discovered there was no session; the registration failure is reported and does
/// not fail the verb, which is right, because the key IS on disk. But
/// <c>gg key create</c> refuses to overwrite an existing key, and nothing else
/// registers a public half — so the only route out is deleting the key, which is
/// exactly the destructive act that refusal exists to prevent. The owner walked
/// into this on 2026-10-05 while not signed in.
/// </para>
/// <para>
/// <b>Checked before the PROMPT, not merely before the write.</b> A passphrase
/// typed for nothing is a passphrase typed, and somebody who types one twice and is
/// then told to log in has no way to know whether a key was written.
/// </para>
/// </remarks>
public class MintingAKeyChecksTheSessionFirstTests
{
    /// <summary>A prompt that records whether it was ever asked.</summary>
    private sealed class CountingPrompt : ISecretPrompt
    {
        public int Asked { get; private set; }

        public string ReadSecret(string prompt)
        {
            Asked++;
            return "a-passphrase";
        }

        public string ReadLine(string prompt)
        {
            Asked++;
            return "somebody";
        }
    }

    private static CredentialCommands WithNoSession(ISecretPrompt prompt, string root) =>
        new(new ControlPlaneClient(new HttpClient { BaseAddress = new Uri("http://127.0.0.1:1") }),
            new HeldSessionStore(null),
            new FileCredentialStore(root),
            prompt);

    [Test]
    public async Task It_refuses_before_asking_for_a_passphrase()
    {
        var prompt = new CountingPrompt();
        var root = Path.Combine(Path.GetTempPath(), "gg-mint-" + Guid.NewGuid().ToString("N"));

        await Assert.That(async () => await WithNoSession(prompt, root).CreateKeyAsync())
            .Throws<NotSignedInException>();

        await Assert.That(prompt.Asked).IsEqualTo(0)
            .Because("a passphrase typed for nothing is a passphrase typed, and this one is typed "
                   + "twice - the rule AddAsync states in those words.");
    }

    [Test]
    public async Task And_writes_no_key()
    {
        // THE HALF THAT MATTERS MORE. A key written without a session cannot be
        // registered afterwards: create refuses to overwrite and nothing else
        // registers a public half, so the only way forward is deleting it.
        var root = Path.Combine(Path.GetTempPath(), "gg-mint-" + Guid.NewGuid().ToString("N"));
        var keyPath = Path.Combine(root, "person-key");

        await Assert.That(async () =>
                await WithNoSession(new CountingPrompt(), root).CreateKeyAsync(keyPath: keyPath))
            .Throws<NotSignedInException>();

        await Assert.That(File.Exists(keyPath)).IsFalse()
            .Because("an unregistered key is a dead end, and leaving one behind turns a missing "
                   + "login into a file somebody has to be told to delete.");
    }

    [Test]
    public async Task The_refusal_is_the_one_every_other_verb_gives()
    {
        // ONE SENTENCE FOR ONE STATE. "Not signed in. Run gg login." is what add,
        // list and remove already say, and a second wording for the same condition
        // is a second thing to search for when it happens.
        var root = Path.Combine(Path.GetTempPath(), "gg-mint-" + Guid.NewGuid().ToString("N"));

        try
        {
            _ = await WithNoSession(new CountingPrompt(), root).CreateKeyAsync();
            Assert.Fail("it should have refused");
        }
        catch (NotSignedInException refused)
        {
            await Assert.That(refused.Message).Contains("gg login");
        }
    }
}
