using Gg.Client;

namespace Gg.Client.Tests;

/// <summary>
/// The store says how each credential rests, and its one-line summary does not
/// claim more than the directory holds.
/// </summary>
/// <remarks>
/// <para>
/// <b>Rule 6, and the reason step 3 exists at all.</b>
/// <c>ICredentialStore.Protection</c> is printed verbatim by <c>gg doctor</c>
/// and its own remark calls implying more than the store has *"the one lie this
/// slice cannot afford"*. After step 2 a machine can hold both shapes at once,
/// so one sentence for the whole directory can no longer be true of everything
/// in it.
/// </para>
/// <para>
/// <b>Per credential rather than an atomic migration, decided 2026-10-04.</b>
/// The alternative was a store that reseals everything on first touch or
/// refuses to start, which keeps one sentence honest and turns a locked file or
/// a full disk into a machine that will not run. A cosmetic honesty problem
/// must not become a fleet outage, and <c>gg doctor</c> is most useful during a
/// migration rather than after it.
/// </para>
/// <para>
/// <b>Neither answer opens anything.</b> The shape a credential rests in is the
/// extension, so this is the same question <c>Holds</c> asks and carries the
/// same guarantee — a sentence about a credential must not be a reason to
/// decrypt one.
/// </para>
/// </remarks>
public class ProtectionAnswersPerCredentialTests
{
    private const string Sealed = "local:acme/sealed";
    private const string Plain = "local:acme/plain";

    private static FileCredentialStore AStore(out string root)
    {
        root = Path.Combine(Path.GetTempPath(), "gg-protection-" + Guid.NewGuid().ToString("N"));

        return new FileCredentialStore(
            root,
            MachineKey.LoadOrCreate(
                Path.Combine(Path.GetTempPath(), "gg-p-" + Guid.NewGuid().ToString("N"), "k")));
    }

    private static void PlaceAPlaintext(FileCredentialStore store, string locator)
    {
        var path = store.PathFor(locator);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "ghp-from-before-sealing");
    }

    [Test]
    public async Task A_sealed_credential_says_it_is_sealed()
    {
        var store = AStore(out _);
        store.Write(Sealed, "a-token");

        await Assert.That(store.ProtectionFor(Sealed)).Contains("sealed");
    }

    [Test]
    public async Task A_plaintext_credential_says_so_rather_than_claiming_the_seal()
    {
        var store = AStore(out _);
        PlaceAPlaintext(store, Plain);

        var said = store.ProtectionFor(Plain);

        await Assert.That(said).Contains("plaintext")
            .Because("this is the one lie the store cannot afford.");
    }

    [Test]
    public async Task The_two_answers_differ()
    {
        var store = AStore(out _);
        store.Write(Sealed, "a-token");
        PlaceAPlaintext(store, Plain);

        await Assert.That(store.ProtectionFor(Sealed)).IsNotEqualTo(store.ProtectionFor(Plain))
            .Because("a per-credential answer that is the same for both answers nothing.");
    }

    [Test]
    public async Task A_credential_that_is_not_here_says_that()
    {
        var store = AStore(out _);

        await Assert.That(store.ProtectionFor("local:acme/absent")).Contains("nothing");
    }

    [Test]
    public async Task The_summary_of_a_half_migrated_store_says_it_is_half_migrated()
    {
        var store = AStore(out _);
        store.Write(Sealed, "a-token");
        PlaceAPlaintext(store, Plain);

        await Assert.That(store.Protection).Contains("plaintext")
            .Because("a directory holding one of each must not read as sealed.");
    }

    [Test]
    public async Task The_summary_of_a_fully_sealed_store_does_not_mention_plaintext()
    {
        var store = AStore(out _);
        store.Write(Sealed, "a-token");

        await Assert.That(store.Protection).DoesNotContain("plaintext")
            .Because("and a store with nothing left to migrate must not imply it has.");
    }

    [Test]
    public async Task The_summary_still_refuses_to_claim_what_sealing_does_not_buy()
    {
        // THE HONESTY CLAUSE, which survives the whole of this slice. The key is
        // a file this machine can read, so sealing defends against a directory
        // that MOVES and against nothing already running as this user - and
        // ADR-0037 says that must keep being said in as many words.
        var store = AStore(out _);
        store.Write(Sealed, "a-token");

        await Assert.That(store.Protection).Contains("this user");
    }

    [Test]
    public async Task Asking_how_a_credential_rests_does_not_open_it()
    {
        // The same guarantee Holds carries: a store sealed to another machine
        // answers the question without being able to decrypt anything.
        var root = Path.Combine(Path.GetTempPath(), "gg-protection-" + Guid.NewGuid().ToString("N"));

        new FileCredentialStore(
            root,
            MachineKey.LoadOrCreate(
                Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "one")))
            .Write(Sealed, "a-token");

        var stranger = new FileCredentialStore(
            root,
            MachineKey.LoadOrCreate(
                Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "another")));

        await Assert.That(stranger.ProtectionFor(Sealed)).Contains("sealed");
    }
}
