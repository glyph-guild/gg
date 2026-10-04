using Gg.Client;

namespace Gg.Client.Tests;

/// <summary>
/// <c>gg doctor</c> reports a half-migrated store as half-migrated.
/// </summary>
/// <remarks>
/// <para>
/// <b>The wiring, not the sentence.</b>
/// <c>ProtectionAnswersPerCredentialTests</c> holds what the store SAYS; this
/// holds that the thing a person actually runs says it too. Those are different
/// claims and the gap between them is where a correct sentence goes unprinted —
/// the credential-store check could have kept a cached string, or a summary
/// computed once at construction, and nothing about the store's own tests would
/// have noticed.
/// </para>
/// <para>
/// <b>Still not blocking, and that is unchanged.</b> The check's own remark
/// says a red on *"here is where your secrets live"* would train somebody to
/// skip the line above the one that matters. A machine mid-migration is working
/// correctly; the doctor's job here is to say what is true, not to judge it.
/// </para>
/// </remarks>
public class DoctorSaysWhatRestsSealedTests
{
    private static FileCredentialStore AStore() =>
        new(
            Path.Combine(Path.GetTempPath(), "gg-doctor-sealed-" + Guid.NewGuid().ToString("N")),
            MachineKey.LoadOrCreate(
                Path.Combine(Path.GetTempPath(), "gg-ds-" + Guid.NewGuid().ToString("N"), "k")));

    private static async Task<DoctorCheck> TheStoreCheckOf(FileCredentialStore store)
    {
        await using var stub = new StubControlPlane();

        var report = await new Doctor(
            new ControlPlaneClient(new HttpClient { BaseAddress = new Uri(stub.BaseAddress) }),
            new HeldSessionForThisTest(DoctorTests.AValidSession()),
            store,
            new Uri(stub.BaseAddress)).RunAsync();

        return report.Checks.Single(c => c.Name == DoctorChecks.CredentialStore);
    }

    private sealed class HeldSessionForThisTest(StoredSession? session) : ISessionStore
    {
        public StoredSession? Read() => session;
        public void Write(StoredSession value) { }
        public void Clear() { }
    }

    [Test]
    public async Task A_half_migrated_store_is_reported_as_half_migrated()
    {
        var store = AStore();
        store.Write("local:acme/sealed", "a-token");

        var plain = store.PathFor("local:acme/plain");
        Directory.CreateDirectory(Path.GetDirectoryName(plain)!);
        File.WriteAllText(plain, "ghp-from-before-sealing");

        var check = await TheStoreCheckOf(store);

        await Assert.That(check.Detail).Contains("plaintext")
            .Because("the one moment somebody is looking at this is while it is half done.");
    }

    [Test]
    public async Task A_fully_sealed_store_is_not()
    {
        var store = AStore();
        store.Write("local:acme/sealed", "a-token");

        var check = await TheStoreCheckOf(store);

        await Assert.That(check.Detail).DoesNotContain("plaintext");
        await Assert.That(check.Detail).Contains("sealed");
    }

    [Test]
    public async Task It_still_says_what_sealing_does_not_buy()
    {
        var store = AStore();
        store.Write("local:acme/sealed", "a-token");

        var check = await TheStoreCheckOf(store);

        await Assert.That(check.Detail).Contains("this user")
            .Because("ADR-0037: the key is a file this machine can read, and the line a person "
                   + "reads must keep saying so.");
    }

    [Test]
    public async Task And_it_never_blocks()
    {
        var store = AStore();

        var plain = store.PathFor("local:acme/plain");
        Directory.CreateDirectory(Path.GetDirectoryName(plain)!);
        File.WriteAllText(plain, "ghp-from-before-sealing");

        var check = await TheStoreCheckOf(store);

        await Assert.That(check.Blocking).IsFalse();
        await Assert.That(check.Passed).IsTrue()
            .Because("a machine mid-migration is working correctly, and a red here trains "
                   + "somebody to skip the line above the one that matters.");
    }
}
