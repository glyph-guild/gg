using Gg.Client;

namespace Gg.Client.Tests;

/// <summary>
/// What a machine keeps on disk is sealed to that machine's own key, and the
/// value is not recoverable from the file.
/// </summary>
/// <remarks>
/// <para>
/// <b>ADR-0037 Decision 4, and the largest piece of work in the slice.</b> The
/// store is read by every flight, every clone, every push and every destination
/// adapter, and until now it has been a plaintext file per locator. What changes
/// is only how it rests; what it is for does not.
/// </para>
/// <para>
/// <b>The substring assertion is the one that can actually fail.</b> A sealing
/// layer that wrote ciphertext beside a plaintext index, or that left the old
/// file where it was, would satisfy "the credential round-trips" and still hand
/// the value to whoever copied the directory. So these read every byte under the
/// root and look for the value in all of it.
/// </para>
/// <para>
/// <b>And the key is deliberately not under that root.</b> The threat this
/// defends against is a directory that moves — a backup, a disk image, a
/// <c>docker cp</c>, a support bundle. A key sealed inside the thing being
/// copied travels with it and buys nothing at all, which is the way this feature
/// would most plausibly have been built wrong.
/// </para>
/// </remarks>
public class AStoreRestsSealedTests
{
    private const string Locator = "local:acme/widgets";
    private const string Value = "ghp-a-token-nobody-should-find";

    private static (FileCredentialStore Store, string Root, string KeyPath) AStore()
    {
        var root = Path.Combine(Path.GetTempPath(), "gg-sealed-" + Guid.NewGuid().ToString("N"));
        var keyPath = Path.Combine(
            Path.GetTempPath(), "gg-key-" + Guid.NewGuid().ToString("N"), "machine-key");

        return (new FileCredentialStore(root, MachineKey.LoadOrCreate(keyPath)), root, keyPath);
    }

    /// <summary>Every byte under the store root, as one string.</summary>
    private static string EverythingUnder(string root) =>
        Directory.Exists(root)
            ? string.Join(
                "\n",
                Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                    .Select(File.ReadAllText))
            : string.Empty;

    [Test]
    public async Task A_written_credential_reads_back()
    {
        var (store, _, _) = AStore();

        store.Write(Locator, Value);

        await Assert.That(store.Read(Locator)).IsEqualTo(Value);
    }

    [Test]
    public async Task The_value_is_in_no_file_under_the_root()
    {
        var (store, root, _) = AStore();

        store.Write(Locator, Value);

        await Assert.That(EverythingUnder(root)).DoesNotContain(Value);
    }

    [Test]
    public async Task No_substring_of_the_value_is_either()
    {
        var (store, root, _) = AStore();

        store.Write(Locator, Value);
        var everything = EverythingUnder(root);

        // EVERY RUN OF SIX CHARACTERS. A ciphertext that happened to contain the
        // whole value is the obvious failure; one that leaked a recognisable
        // PREFIX - "ghp-a-" - is the one a whole-value check walks past, and a
        // provider's token prefix is exactly the part worth not publishing.
        for (var at = 0; at + 6 <= Value.Length; at++)
        {
            await Assert.That(everything).DoesNotContain(Value.Substring(at, 6));
        }
    }

    [Test]
    public async Task Base64_of_the_value_is_not_there_either()
    {
        var (store, root, _) = AStore();

        store.Write(Locator, Value);

        // THE SHAPE A LAZY "ENCODE IT" PRODUCES, which reads as sealed to
        // anybody glancing at the file and is not.
        await Assert.That(EverythingUnder(root))
            .DoesNotContain(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(Value)));
    }

    [Test]
    public async Task The_key_is_not_under_the_store_root()
    {
        var (store, root, keyPath) = AStore();

        store.Write(Locator, Value);

        // THE WHOLE DEFENCE RESTS ON THIS. A key inside the directory being
        // copied travels with it, and sealing then protects against nothing a
        // file mode did not already cover.
        await Assert.That(Path.GetFullPath(keyPath).StartsWith(Path.GetFullPath(root), StringComparison.Ordinal))
            .IsFalse();
    }

    [Test]
    public async Task A_plaintext_credential_from_before_this_is_still_readable()
    {
        // STEP 2 MUST NOT BREAK A MACHINE. Every store in the field holds
        // plaintext today and step 3 is what reseals it; a step 2 that could
        // only read what it wrote would strand every runner at the moment it
        // shipped.
        var (store, _, _) = AStore();

        var legacy = store.PathFor(Locator);
        Directory.CreateDirectory(Path.GetDirectoryName(legacy)!);
        File.WriteAllText(legacy, Value);

        await Assert.That(store.Read(Locator)).IsEqualTo(Value);
    }

    [Test]
    public async Task Writing_over_a_plaintext_credential_leaves_no_plaintext_behind()
    {
        // THE DEFECT THE SUBSTRING CLAUSE WAS WRITTEN FOR. Sealing on write
        // while leaving the old file where it was would pass a round-trip test
        // and leave the value on disk for ever.
        var (store, root, _) = AStore();

        var legacy = store.PathFor(Locator);
        Directory.CreateDirectory(Path.GetDirectoryName(legacy)!);
        File.WriteAllText(legacy, Value);

        store.Write(Locator, "a-different-token");

        await Assert.That(File.Exists(legacy)).IsFalse();
        await Assert.That(EverythingUnder(root)).DoesNotContain(Value);
    }

    [Test]
    public async Task Removing_a_credential_removes_both_shapes_of_it()
    {
        var (store, root, _) = AStore();

        var legacy = store.PathFor(Locator);
        Directory.CreateDirectory(Path.GetDirectoryName(legacy)!);
        File.WriteAllText(legacy, Value);
        store.Write(Locator, Value);

        await Assert.That(store.Remove(Locator)).IsTrue();
        await Assert.That(store.Holds(Locator)).IsFalse();
        await Assert.That(EverythingUnder(root)).DoesNotContain(Value);
    }
}
