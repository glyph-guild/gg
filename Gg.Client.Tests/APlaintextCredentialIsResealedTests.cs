using Gg.Client;

namespace Gg.Client.Tests;

/// <summary>
/// A credential written before sealing shipped is resealed the next time it is
/// read, and the plaintext goes.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every store in the field holds plaintext, so this is the step that
/// actually removes it.</b> Step 2 made new writes sealed and left what was
/// already there alone — correct, because a store that could only read what it
/// sealed would have stranded every machine the day it shipped, and wrong to
/// leave standing, because nothing would ever have migrated.
/// </para>
/// <para>
/// <b>On read, rather than by a verb somebody has to run.</b> A migration that
/// needs a person reaches the machines whose operator reads release notes and
/// no others, which on a fleet means the pool hosts migrate and the laptops do
/// not. Reading is the one thing that certainly happens to a credential anybody
/// still uses.
/// </para>
/// <para>
/// <b>And it is best effort, which is the half that keeps a fleet up.</b> A
/// read-only mount, a full disk, a directory somebody chmodded: none of those
/// may turn a credential that resolves perfectly well into a failed flight. The
/// value is returned either way and the reseal is simply not done this time.
/// </para>
/// </remarks>
public class APlaintextCredentialIsResealedTests
{
    private const string Locator = "local:acme/widgets";
    private const string Value = "ghp-a-token-from-before-sealing";

    private static (FileCredentialStore Store, string Root) AStoreHoldingPlaintext()
    {
        var root = Path.Combine(Path.GetTempPath(), "gg-reseal-" + Guid.NewGuid().ToString("N"));
        var keyPath = Path.Combine(
            Path.GetTempPath(), "gg-reseal-key-" + Guid.NewGuid().ToString("N"), "k");

        var store = new FileCredentialStore(root, MachineKey.LoadOrCreate(keyPath));

        var plaintext = store.PathFor(Locator);
        Directory.CreateDirectory(Path.GetDirectoryName(plaintext)!);
        File.WriteAllText(plaintext, Value);

        return (store, root);
    }

    [Test]
    public async Task Reading_it_returns_the_value()
    {
        var (store, _) = AStoreHoldingPlaintext();

        await Assert.That(store.Read(Locator)).IsEqualTo(Value);
    }

    [Test]
    public async Task Reading_it_reseals_it()
    {
        var (store, _) = AStoreHoldingPlaintext();

        store.Read(Locator);

        await Assert.That(File.Exists(store.SealedPathFor(Locator))).IsTrue();
    }

    [Test]
    public async Task And_the_plaintext_is_gone()
    {
        var (store, root) = AStoreHoldingPlaintext();

        store.Read(Locator);

        await Assert.That(File.Exists(store.PathFor(Locator))).IsFalse();

        var everything = string.Join(
            "\n",
            Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Select(File.ReadAllText));

        await Assert.That(everything).DoesNotContain(Value)
            .Because("a migration that leaves the old file behind has migrated nothing.");
    }

    [Test]
    public async Task It_still_reads_the_same_value_afterwards()
    {
        var (store, _) = AStoreHoldingPlaintext();

        store.Read(Locator);

        await Assert.That(store.Read(Locator)).IsEqualTo(Value)
            .Because("the reseal must round-trip, or it has destroyed the credential.");
    }

    [Test]
    public async Task An_already_sealed_credential_is_left_alone()
    {
        var (store, _) = AStoreHoldingPlaintext();

        store.Read(Locator);
        var sealedOnce = File.ReadAllText(store.SealedPathFor(Locator));

        store.Read(Locator);

        // NOT RESEALED ON EVERY READ. Resealing mints a new content key and a
        // new nonce, so a store that did it every time would rewrite every
        // credential on every flight - burning disk writes for nothing and
        // making a backup differ from itself for no reason anybody could see.
        await Assert.That(File.ReadAllText(store.SealedPathFor(Locator))).IsEqualTo(sealedOnce);
    }

    [Test]
    public async Task A_reseal_that_cannot_be_written_still_returns_the_value()
    {
        // THE HALF THAT KEEPS A FLEET UP. A read-only mount or a full disk must
        // not turn a credential that resolves perfectly well into a failed
        // flight; the migration is an optimisation of where the value rests,
        // never a precondition of reading it.
        var (store, root) = AStoreHoldingPlaintext();

        if (OperatingSystem.IsWindows())
        {
            await Assert.That(store.Read(Locator)).IsEqualTo(Value);
            return;
        }

        var directory = Path.GetDirectoryName(store.PathFor(Locator))!;
        File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserExecute);

        try
        {
            await Assert.That(store.Read(Locator)).IsEqualTo(Value);
        }
        finally
        {
            File.SetUnixFileMode(
                directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    [Test]
    public async Task Nothing_is_resealed_that_was_not_there()
    {
        var root = Path.Combine(Path.GetTempPath(), "gg-reseal-" + Guid.NewGuid().ToString("N"));
        var store = new FileCredentialStore(
            root, MachineKey.LoadOrCreate(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "k")));

        await Assert.That(store.Read(Locator)).IsNull();
        await Assert.That(File.Exists(store.SealedPathFor(Locator))).IsFalse();
    }
}
