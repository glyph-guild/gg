using Gg.Client;

namespace Gg.Client.Tests;

/// <summary>
/// A machine's credential store lists the locators it holds, which is what its runner
/// reports on every beat (slice seventy-three).
/// </summary>
/// <remarks>
/// <b>Read off the file names, opening nothing</b> - <c>Holds</c>' guarantee, and the
/// reason saying where a credential is never decrypts one.
/// </remarks>
public class AStoreSaysWhatItHoldsTests
{
    [Test]
    public async Task A_written_credential_is_listed_and_a_removed_one_is_not()
    {
        var root = Directory.CreateTempSubdirectory("gg-held-").FullName;
        var store = new FileCredentialStore(root, MachineKey.LoadOrCreate(Path.Combine(root, "machine.key")));

        store.Write("local:analysis/sonarcloud", "not-a-real-token");
        store.Write("local:jdx/jdnext", "not-a-real-token");

        await Assert.That(store.Held()).IsEquivalentTo(
            new[] { "local:analysis/sonarcloud", "local:jdx/jdnext" });

        store.Remove("local:jdx/jdnext");

        await Assert.That(store.Held()).IsEquivalentTo(new[] { "local:analysis/sonarcloud" });
    }

    [Test]
    public async Task An_empty_store_holds_none()
    {
        var root = Path.Combine(Path.GetTempPath(), "gg-held-absent-" + Guid.NewGuid().ToString("N"));
        var store = new FileCredentialStore(
            root, MachineKey.LoadOrCreate(Path.Combine(Path.GetTempPath(), "gg-held-key-" + Guid.NewGuid().ToString("N"))));

        await Assert.That(store.Held()).IsEmpty();
    }
}
