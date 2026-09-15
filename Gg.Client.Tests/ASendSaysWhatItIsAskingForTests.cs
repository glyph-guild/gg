using Gg.Client;

namespace Gg.Client.Tests;

/// <summary>
/// When a send has to ask a person for the secret, the caller may say what it
/// is asking for.
/// </summary>
/// <remarks>
/// <para>
/// <b>"Secret for local:agent/claude (not echoed)" is a locator, not an
/// instruction.</b> A person sending a forge credential knows what to paste; a
/// person sending an agent's token has to have MINTED one first, with a command
/// they may never have run - and the prompt is the one line they read before
/// typing. So the caller that knows which kind of credential this is can hand
/// over the sentence, and the default stays what it was for everything else.
/// </para>
/// <para>
/// <b>The prompt is the only thing that changes.</b> Where the secret comes from
/// - this machine's store first, a person second - and what is said about it
/// are <c>WhereTheSecretToSendComesFromTests</c>' rules, unchanged.
/// </para>
/// </remarks>
public class ASendSaysWhatItIsAskingForTests
{
    private const string Typed = "sk-ant-oat01-typed-by-the-person";

    private sealed class AnEmptyStore : ICredentialStore
    {
        public string Root => "/nowhere";

        public string Protection => "nothing, this is a test";

        public string PathFor(string locator) => "/nowhere/x";

        public void Write(string locator, string value) { }

        public string? Read(string locator) => null;

        public bool Holds(string locator) => false;

        public bool Remove(string locator) => false;
    }

    private sealed class APrompt : ISecretPrompt
    {
        public string? Asked { get; private set; }

        public string ReadSecret(string prompt)
        {
            Asked = prompt;
            return Typed;
        }

        public string ReadLine(string prompt) => "";
    }

    [Test]
    public async Task The_callers_sentence_is_what_the_person_reads()
    {
        var prompt = new APrompt();

        var secret = SendACredential.SecretFor(
            new AnEmptyStore(), "local:agent/claude", prompt, _ => { },
            asking: "Long-lived token for claude, from `claude setup-token` on this machine (not echoed): ");

        await Assert.That(secret).IsEqualTo(Typed);
        await Assert.That(prompt.Asked).Contains("claude setup-token")
            .Because("the person has to know which command mints the thing they are being "
                   + "asked to paste, and this line is the only place they are told.");
        await Assert.That(prompt.Asked).Contains("not echoed")
            .Because("and that it will not appear on their screen, which is what lets them "
                   + "paste it in front of somebody.");
    }

    [Test]
    public async Task Without_one_the_prompt_names_the_locator_as_it_always_did()
    {
        // The anchor: every existing caller passes nothing and gets the sentence
        // that names the locator.
        var prompt = new APrompt();

        _ = SendACredential.SecretFor(new AnEmptyStore(), "local:acme/widgets", prompt, _ => { });

        await Assert.That(prompt.Asked).Contains("local:acme/widgets");
        await Assert.That(prompt.Asked).Contains("not echoed");
    }
}
