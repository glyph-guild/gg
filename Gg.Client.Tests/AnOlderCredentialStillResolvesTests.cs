using Gg.Client;

namespace Gg.Client.Tests;

/// <summary>
/// Everything already on a machine keeps resolving. This step has no flag day.
/// </summary>
/// <remarks>
/// <para>
/// <b>S64.2-05, and the row most likely to be the one that matters in the
/// field.</b> There are credentials sealed to machine keys on this Mac, on
/// vmlinux001 and on vmlinux002 right now, and pool members hold ones pushed to
/// them. A step that changed what a credential is sealed to and stopped the old
/// ones opening would take a fleet down for a property nobody had asked for yet.
/// </para>
/// <para>
/// <b>The trap this file exists to catch, found by reading rather than by a
/// failure.</b> <c>Read</c> calls <c>Reseal</c> on any pre-sealing plaintext it
/// finds, and <c>Reseal</c> calls <c>Write</c>. If <c>Write</c> had been changed to
/// seal to the person — which is the obvious way to implement step 2 — then every
/// runner reading a plaintext credential would have resealed it to a person whose
/// key that machine does not have, and bricked it. Silently, on a migration path,
/// on machines nobody is watching.
/// </para>
/// <para>
/// <b>So registering is a SEPARATE act and <c>Write</c> keeps its meaning:</b>
/// this machine seals one for itself. Its two callers are that migration and
/// <c>LocalCredentialKeeper.KeepLocallyMinted</c>, where a runner mints its own
/// agent token and <i>"the store's own key is the right holder"</i> in the file's
/// own words.
/// </para>
/// <para>
/// <b>And nothing re-seals what already exists.</b> A credential sealed to a
/// machine key stays sealed to it until somebody rewraps it deliberately. ADR-0037
/// already put re-sealing on rotation out of scope as <i>"a rewrap-everything
/// pass and its own argument"</i>; this is the same argument arriving from the
/// other side.
/// </para>
/// </remarks>
public class AnOlderCredentialStillResolvesTests
{
    private const string Older = "local:acme/older";
    private const string Plain = "local:acme/pre-sealing";
    private const string Minted = "agent:anthropic/claude-code";
    private const string Secret = "ghp-not-a-real-token";
    private const string Passphrase = "correct horse battery staple";

    private static string ATempDir(string tag) =>
        Path.Combine(Path.GetTempPath(), $"gg-{tag}-" + Guid.NewGuid().ToString("N"));

    private static FileCredentialStore AStore(out MachineKey machine)
    {
        machine = MachineKey.LoadOrCreate(Path.Combine(ATempDir("mk"), "machine-key"));
        return new FileCredentialStore(ATempDir("store"), machine);
    }

    [Test]
    public async Task A_credential_sealed_to_a_machine_key_still_reads()
    {
        var store = AStore(out _);

        store.Write(Older, Secret);

        await Assert.That(store.Read(Older)).IsEqualTo(Secret)
            .Because("this is what every machine in the fleet holds today, and step 2 must not be "
                   + "something an operator has to be told about.");
    }

    [Test]
    public async Task And_is_not_quietly_resealed_to_anybody()
    {
        var store = AStore(out var machine);

        store.Write(Older, Secret);
        _ = store.Read(Older);

        await Assert.That(store.HoldersOf(Older)).IsEquivalentTo(new[] { machine.PublicKey })
            .Because("re-sealing what exists is a rewrap-everything pass with its own argument, and "
                   + "a read is not the place to perform one nobody asked for.");
    }

    [Test]
    public async Task A_plaintext_credential_reseals_to_the_MACHINE_and_not_to_a_person()
    {
        // THE TRAP. Reseal goes through Write, so a Write that sealed to a person
        // would brick this path on any machine without a person's key - which is
        // every runner. Found by reading the call graph; this is what keeps it
        // found.
        var store = AStore(out var machine);

        var path = store.PathFor(Plain);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, Secret);

        await Assert.That(store.Read(Plain)).IsEqualTo(Secret)
            .Because("the migration reads the value out before it reseals, so the caller is served "
                   + "whatever happens next.");

        await Assert.That(store.HoldersOf(Plain)).IsEquivalentTo(new[] { machine.PublicKey })
            .Because("the machine that performed the migration is the holder. Sealing it to a "
                   + "person here would make it unopenable on the machine that just migrated it.");

        await Assert.That(store.Read(Plain)).IsEqualTo(Secret)
            .Because("and it still opens on the next read, which is the whole point of resealing.");
    }

    [Test]
    public async Task A_locally_minted_token_is_still_sealed_to_the_machine_that_minted_it()
    {
        // KeepLocallyMinted's path, asserted at the store rather than through the
        // keeper: a runner logs its agent in and keeps the result, with no person
        // anywhere near it. There is no person's key on a pool member at all.
        var store = AStore(out var machine);

        store.Write(Minted, "sk-ant-not-real");

        await Assert.That(store.HoldersOf(Minted)).IsEquivalentTo(new[] { machine.PublicKey })
            .Because("nobody sealed one to send it, so the store's own key is the right holder - "
                   + "LocalCredentialKeeper says exactly that in its own remark.");

        await Assert.That(store.Read(Minted)).IsEqualTo("sk-ant-not-real")
            .Because("and the runner reads it back at three in the morning with nobody present.");
    }

    [Test]
    public async Task A_pushed_credential_is_untouched_by_any_of_this()
    {
        // WriteSealed is the arriving-push path and it already carries whatever
        // holders the sender wrapped for. Step 2 must not add one.
        var store = AStore(out var machine);
        var sender = AStore(out var senderKey);

        sender.Write(Older, Secret);
        var forThem = Gg.Contracts.CredentialSeal.Rewrap(
            sender.SealedFor(Older), senderKey, machine.PublicKey);

        store.WriteSealed(Older, forThem);

        await Assert.That(store.Read(Older)).IsEqualTo(Secret)
            .Because("a credential that arrives by a push is sealed to the receiving machine's own "
                   + "key, which is Decision 4 and is unchanged.");

        await Assert.That(store.HoldersOf(Older)).Contains(senderKey.PublicKey)
            .Because("and the sender is still a holder, because a push adds rather than moves.");
    }

    [Test]
    public async Task Both_kinds_live_in_one_directory_without_interfering()
    {
        var store = AStore(out var machine);

        var keyPath = Path.Combine(ATempDir("pk"), "person-key");
        PersonKey.Create(keyPath, Passphrase);

        store.Write(Older, Secret);
        store.Register("local:acme/new", "ghp-registered", PersonKey.PublicHalfOf(keyPath));

        await Assert.That(store.Read(Older)).IsEqualTo(Secret)
            .Because("one credential's holders say nothing about another's - they are separate "
                   + "files with separate envelopes.");

        await Assert.That(store.HoldersOf("local:acme/new"))
            .IsEquivalentTo(new[] { PersonKey.PublicHalfOf(keyPath) })
            .Because("and the new one did not pick up the machine key from its neighbour.");

        _ = machine;
    }
}
