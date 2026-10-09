using Gg.Client;

namespace Gg.Client.Tests;

/// <summary>
/// A plaintext credential written after the sealed one beside it is the value,
/// and is resealed over it.
/// </summary>
/// <remarks>
/// <b>Measured on a developer's machine.</b> A tracker token was sealed, then
/// rotated by a binary from before sealing, which cannot see a <c>.sealed</c>
/// file and so wrote a <c>.secret</c> beside it. The store preferred the sealed
/// entry unconditionally, so every read answered with the replaced token: the
/// console's browse tab said 401 for days while the working token sat one file
/// over, and <c>gg doctor</c> said every credential resolved.
/// </remarks>
public class ANewerPlaintextCredentialWinsTests
{
    private const string Locator = "local:acme/widgets";
    private const string Replaced = "token-that-was-rotated-away";
    private const string Rotated = "token-written-by-an-older-gg";

    private static FileCredentialStore AStore()
    {
        var root = Path.Combine(Path.GetTempPath(), "gg-newer-" + Guid.NewGuid().ToString("N"));
        var keyPath = Path.Combine(
            Path.GetTempPath(), "gg-newer-key-" + Guid.NewGuid().ToString("N"), "k");

        return new FileCredentialStore(root, MachineKey.LoadOrCreate(keyPath));
    }

    /// <summary>A sealed entry, then a plaintext one written after it.</summary>
    private static FileCredentialStore SealedThenPlaintext()
    {
        var store = AStore();
        store.Write(Locator, Replaced);

        var plaintext = store.PathFor(Locator);
        File.WriteAllText(plaintext, Rotated);

        // Stated rather than slept for: a filesystem with coarse timestamps would
        // otherwise make the two writes look simultaneous.
        File.SetLastWriteTimeUtc(
            plaintext, File.GetLastWriteTimeUtc(store.SealedPathFor(Locator)).AddMinutes(1));

        return store;
    }

    [Test]
    public async Task The_newer_plaintext_is_what_a_read_returns()
    {
        var store = SealedThenPlaintext();

        await Assert.That(store.Read(Locator)).IsEqualTo(Rotated);
    }

    [Test]
    public async Task And_it_is_resealed_over_the_stale_entry()
    {
        var store = SealedThenPlaintext();

        store.Read(Locator);

        await Assert.That(File.Exists(store.PathFor(Locator))).IsFalse();
        await Assert.That(store.Read(Locator)).IsEqualTo(Rotated)
            .Because("the reseal must replace the stale envelope, not sit beside it.");
    }

    [Test]
    public async Task How_it_rests_is_said_of_the_newer_file()
    {
        var store = SealedThenPlaintext();

        await Assert.That(store.RestingOf(Locator)).IsEqualTo(CredentialResting.Plaintext);
        await Assert.That(store.HoldersOf(Locator)).IsEmpty();
    }

    [Test]
    public async Task An_older_plaintext_still_loses_to_the_sealed_entry()
    {
        var store = AStore();

        var plaintext = store.PathFor(Locator);
        Directory.CreateDirectory(Path.GetDirectoryName(plaintext)!);
        File.WriteAllText(plaintext, Rotated);
        File.SetLastWriteTimeUtc(plaintext, DateTime.UtcNow.AddDays(-1));

        // Sealed through the envelope path, which leaves a plaintext file alone
        // only when one is written after it - so put it back, older.
        store.Write(Locator, Replaced);
        File.WriteAllText(plaintext, Rotated);
        File.SetLastWriteTimeUtc(
            plaintext, File.GetLastWriteTimeUtc(store.SealedPathFor(Locator)).AddMinutes(-1));

        await Assert.That(store.Read(Locator)).IsEqualTo(Replaced);
    }
}
