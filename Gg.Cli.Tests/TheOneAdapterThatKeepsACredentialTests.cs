using Gg.Client;
using Gg.Cli;

namespace Gg.Cli.Tests;

/// <summary>
/// The runner's keep-a-credential port, joined to the local store.
/// </summary>
/// <remarks>
/// <para>
/// <b>Here for <see cref="LocalCredentialResolver"/>'s reason, which is the
/// architecture rather than a preference.</b> <c>Gg.Runner</c> deliberately
/// cannot reference <c>Gg.Client</c> - a runner must be structurally unable to
/// hold a developer's session - and <c>Gg.Client</c> has no business knowing
/// what a runner is. So the two are joined at the top, by the binary that is
/// already both. Its sibling reads; this one writes.
/// </para>
/// <para>
/// <b>It answers rather than throws, and that is not politeness.</b> What calls
/// it is a dispatch arm on a channel a hostile peer is at the other end of, and
/// an exception out of that arm is a peer that can end a runner's conversation
/// whenever it likes. A refusal is an answer; a crash is a denial of service
/// with a stack trace.
/// </para>
/// </remarks>
public class TheOneAdapterThatKeepsACredentialTests
{
    private const string TheSecret = "ghp-not-a-real-token-4d16-9f3b";

    private sealed class ARefusingStore : ICredentialStore
    {
        public string Root => "/nowhere";

        public string Protection => "nothing, this is a test";

        public string PathFor(string locator) => throw new ArgumentException("no");

        public void Write(string locator, string secret) =>
            throw new ArgumentException("that locator is not one");

        public string? Read(string locator) => null;

        public bool Remove(string locator) => false;
    }

    [Test]
    public async Task What_it_is_given_reaches_the_store_the_runner_reads()
    {
        // THE SAME STORE `gg credential add` WRITES AND THE RESOLVER READS,
        // which is the whole point of there being one adapter per direction
        // rather than one path per feature: a credential placed over the
        // channel and one typed at a prompt have to be the same file, or a
        // flight resolves one and not the other.
        var root = Path.Combine(
            Path.GetTempPath(), $"gg-keep-{Guid.NewGuid():N}");

        try
        {
            var store = new FileCredentialStore(root);
            var keeper = new LocalCredentialKeeper(store);

            await Assert.That(keeper.Keep("local:acme/widgets", TheSecret)).IsTrue();
            await Assert.That(store.Read("local:acme/widgets")).IsEqualTo(TheSecret);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Test]
    public async Task A_store_that_refuses_is_an_answer_rather_than_a_crash()
    {
        var keeper = new LocalCredentialKeeper(new ARefusingStore());

        await Assert.That(keeper.Keep("local:acme/widgets", TheSecret)).IsFalse()
            .Because("the caller is a dispatch arm on a channel a hostile peer is on the "
                   + "other end of, and an exception there ends the conversation.");
    }
}
