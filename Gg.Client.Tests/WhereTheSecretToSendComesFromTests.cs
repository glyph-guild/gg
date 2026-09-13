using Gg.Client;

namespace Gg.Client.Tests;

/// <summary>
/// Sending a credential to a runner uses the copy this machine already has, and
/// asks for one only when it has none.
/// </summary>
/// <remarks>
/// <para>
/// <b>The ordinary case is a person who has already run
/// <c>gg credential add</c>.</b> They registered the reference, the secret went
/// to a 0600 file, and now a machine that cannot reach that file needs it too.
/// Asking them to paste the token a second time would be asking them to go and
/// find it again — and the most likely place they would find it is wherever they
/// were told not to keep it.
/// </para>
/// <para>
/// <b>Prompted when this machine has none, which is a real case rather than a
/// fallback.</b> A credential that only a pool member needs was never added
/// here, and a refusal saying "register it locally first" would make somebody
/// store a secret on a laptop purely to move it to a container.
/// </para>
/// <para>
/// <b>One named place where the secret enters, either way.</b>
/// <c>ISecretPrompt</c>'s own remark: <i>a port rather than a call to
/// Console.ReadKey, so a test can answer it - and, more to the point, so there
/// is a single named place where a secret enters. It is a short list to
/// audit.</i> This adds a second door to that list and nothing else.
/// </para>
/// </remarks>
public class WhereTheSecretToSendComesFromTests
{
    private const string Stored = "ghp-stored-4d16-9f3b-not-real";
    private const string Typed = "ghp-typed-2a7c-05e8-not-real";

    private sealed class AStore(string? secret) : ICredentialStore
    {
        public string Root => "/nowhere";

        public string Protection => "nothing, this is a test";

        public string PathFor(string locator) => "/nowhere/x";

        public void Write(string locator, string value) { }

        public string? Read(string locator) => secret;

        public bool Remove(string locator) => false;
    }

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

    [Test]
    public async Task The_copy_this_machine_has_is_the_one_that_travels()
    {
        var prompt = new APrompt(Typed);

        var found = SendACredential.SecretFor(
            new AStore(Stored), "local:acme/widgets", prompt, _ => { });

        await Assert.That(found).IsEqualTo(Stored);
        await Assert.That(prompt.Asked).IsEqualTo(0)
            .Because("asking for a token gg already holds sends somebody to go and find it "
                   + "again, and the likeliest place they find it is where they were told "
                   + "not to keep it.");
    }

    [Test]
    public async Task A_machine_that_does_not_have_it_asks()
    {
        var prompt = new APrompt(Typed);

        var found = SendACredential.SecretFor(
            new AStore(null), "local:acme/widgets", prompt, _ => { });

        await Assert.That(found).IsEqualTo(Typed);
        await Assert.That(prompt.Asked).IsEqualTo(1)
            .Because("a credential only a pool member needs was never added here, and "
                   + "refusing would make somebody store a secret on a laptop purely to "
                   + "move it to a container.");
    }

    [Test]
    public async Task What_it_says_out_loud_names_the_locator_and_never_the_value()
    {
        // THE RUNNER RULE'S CONSOLE-SIDE TWIN. A line saying where the secret
        // came from is worth printing - a person needs to know whether they are
        // about to send the one they think they are - and the one thing it may
        // not contain is the secret.
        foreach (var store in (ICredentialStore[])[new AStore(Stored), new AStore(null)])
        {
            var said = new List<string>();

            var found = SendACredential.SecretFor(
                store, "local:acme/widgets", new APrompt(Typed), said.Add);

            var all = string.Join(" | ", said);

            await Assert.That(all).DoesNotContain(found, StringComparison.Ordinal);
            await Assert.That(all).Contains("local:acme/widgets", StringComparison.Ordinal)
                .Because("which credential is about to be sent is the fact somebody checks "
                       + "before they send it - and naming it is what makes the silence "
                       + "about the value meaningful rather than total.");
        }
    }

    [Test]
    public async Task An_empty_answer_is_refused_rather_than_sent()
    {
        // NoCredentialResolver'S DISPOSITION: "there is deliberately no
        // 'resolved to nothing': an empty secret is a secret that fetches
        // nothing and fails much later, in a place with no way back to here."
        // Placing one on a runner is exactly that, at a distance.
        await Assert.That(SendACredential.SecretFor(
                new AStore(null), "local:acme/widgets", new APrompt(""), _ => { }))
            .IsNull()
            .Because("an empty secret written to a runner fails at the forge with nothing "
                   + "pointing back at the moment somebody pressed return.");
    }
}
