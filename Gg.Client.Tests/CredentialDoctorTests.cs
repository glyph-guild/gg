using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// <c>gg doctor</c> and credential resolution.
/// </summary>
/// <remarks>
/// <para>
/// ADR-0004 named this failure before it existed: <i>secret-reference
/// indirection fails opaquely. A runner that cannot read a vault produces a
/// stalled flight that looks like a broken product. Diagnostics for this are a
/// feature, not logging.</i>
/// </para>
/// <para>
/// So there are two checks and they answer different questions. One says where
/// the secret lives and how it is protected - always, in every state, because
/// a person cannot reason about a store they cannot find. The other says
/// whether every reference the control plane holds resolves on this machine,
/// and that one is blocking, fixable, and names its remedy.
/// </para>
/// </remarks>
public class CredentialDoctorTests
{
    private static StoredSession ASession() => new()
    {
        SessionToken = StubControlPlane.IssuedSessionToken,
        ExpiresAt = DateTimeOffset.UtcNow.AddHours(12),
        TenantId = "019fe062-d000-730c-a37d-7247342cd810",
        PrincipalDisplay = "stub-principal",
    };

    private static Doctor Build(StubControlPlane stub, ICredentialStore store, StoredSession? session) =>
        new(new ControlPlaneClient(new HttpClient { BaseAddress = new Uri(stub.BaseAddress) }),
            new HeldSessionStore(session),
            store,
            new Uri(stub.BaseAddress));

    private static async Task<DoctorCheck> CheckAsync(
        StubControlPlane stub, ICredentialStore store, StoredSession? session, string name) =>
        (await Build(stub, store, session).RunAsync()).Checks.Single(c => c.Name == name);

    [Test]
    public async Task Doctor_says_where_the_secret_is_and_how_it_is_protected()
    {
        await using var stub = new StubControlPlane();
        using var temporary = new TemporaryStore();

        var check = await CheckAsync(stub, temporary.Store, ASession(), DoctorChecks.CredentialStore);

        await Assert.That(check.Detail).Contains(temporary.Store.Root)
            .Because("a person cannot reason about a store they cannot find.");
        await Assert.That(check.Detail).Contains("0600");
    }

    [Test]
    public async Task Doctor_does_not_imply_protection_the_store_does_not_have()
    {
        // The honest half. This is a file with restrictive permissions on a
        // developer's laptop; anything running as that uid can read it. The
        // property this slice delivers is that the secret never reaches the
        // control plane, and overstating it here would undo that honesty.
        await using var stub = new StubControlPlane();
        using var temporary = new TemporaryStore();

        var detail = (await CheckAsync(stub, temporary.Store, ASession(), DoctorChecks.CredentialStore))
            .Detail.ToLowerInvariant();

        await Assert.That(detail).DoesNotContain("keychain");
        await Assert.That(detail).DoesNotContain("encrypted");
    }

    [Test]
    public async Task The_store_check_reports_and_never_blocks()
    {
        // It is a statement of fact, not a finding. A check that went red on
        // "here is where your secrets live" would train somebody to ignore the
        // line above the one that matters.
        await using var stub = new StubControlPlane();
        using var temporary = new TemporaryStore();

        var check = await CheckAsync(stub, temporary.Store, ASession(), DoctorChecks.CredentialStore);

        await Assert.That(check.Blocking).IsFalse();
        await Assert.That(check.Passed).IsTrue();
    }

    [Test]
    public async Task A_reference_with_no_secret_on_this_machine_is_blocking_and_fixable()
    {
        // The failure ADR-0004 named. Without this the flight simply stalls,
        // and a stalled flight looks like a broken product.
        await using var stub = new StubControlPlane();
        using var temporary = new TemporaryStore();
        stub.Credentials.Add(new CredentialSummary
        {
            CredentialId = "019fe815-6136-7518-bb57-b06d6d3f411a",
            Repo = "acme/widgets",
            Reference = new CredentialReference
            {
                Kind = CredentialKinds.Local,
                Locator = "local:acme/widgets",
                Identity = "acme-bot",
                Scopes = [CredentialScopes.Read],
            },
            AddedAt = DateTimeOffset.UtcNow,
        });

        var check = await CheckAsync(stub, temporary.Store, ASession(), DoctorChecks.Credentials);

        await Assert.That(check.Passed).IsFalse();
        await Assert.That(check.Blocking).IsTrue();
        await Assert.That(check.Fixable).IsTrue();
        await Assert.That(check.Fix).IsNotNull();
        await Assert.That(check.Fix!).Contains("gg credential add")
            .Because("nothing claims fixable without naming what would fix it.");
        await Assert.That(check.Detail).Contains("local:acme/widgets")
            .Because("which reference failed is the whole diagnosis.");
    }

    [Test]
    public async Task A_reference_whose_secret_is_present_passes()
    {
        await using var stub = new StubControlPlane();
        using var temporary = new TemporaryStore();
        var store = temporary.Store;
        store.Write("local:acme/widgets", "ghp-not-a-real-token");

        stub.Credentials.Add(new CredentialSummary
        {
            CredentialId = "019fe815-6136-7518-bb57-b06d6d3f411a",
            Repo = "acme/widgets",
            Reference = new CredentialReference
            {
                Kind = CredentialKinds.Local,
                Locator = "local:acme/widgets",
                Identity = "acme-bot",
                Scopes = [CredentialScopes.Read],
            },
            AddedAt = DateTimeOffset.UtcNow,
        });

        var check = await CheckAsync(stub, store, ASession(), DoctorChecks.Credentials);

        await Assert.That(check.Passed).IsTrue();
        await Assert.That(check.Detail).DoesNotContain("ghp-")
            .Because("doctor output ends up in tickets.");
    }

    [Test]
    public async Task A_check_that_could_not_run_offers_no_remedy()
    {
        // Step 4a's rule, and the one that is easiest to get wrong here: with
        // no session the references cannot be listed, so nothing established
        // that a credential is missing. Telling somebody to add one would send
        // them to re-enter a token over a login problem.
        await using var stub = new StubControlPlane();
        using var temporary = new TemporaryStore();

        var check = await CheckAsync(stub, temporary.Store, session: null, DoctorChecks.Credentials);

        await Assert.That(check.Passed).IsFalse();
        await Assert.That(check.Detail).Contains("not checked");
        await Assert.That(check.Fixable).IsFalse();
        await Assert.That(check.Fix).IsNull();
    }

    [Test]
    public async Task No_check_claims_to_be_fixable_without_naming_the_fix()
    {
        // Over the whole report rather than over the credential checks, because
        // this is the rule the new checks are most likely to break.
        await using var stub = new StubControlPlane();
        using var temporary = new TemporaryStore();

        var report = await Build(stub, temporary.Store, ASession()).RunAsync();

        var offenders = report.Checks
            .Where(c => c.Fixable && string.IsNullOrWhiteSpace(c.Fix))
            .Select(c => c.Name)
            .ToList();

        await Assert.That(offenders).IsEmpty()
            .Because("fixable with no fix is a check that says 'you can do something' and stops. Found: "
                   + string.Join(", ", offenders));
        await Assert.That(report.Checks.Any(c => c.Fixable)).IsTrue();
    }

    [Test]
    public async Task Blocking_and_fixable_stay_independent_across_the_credential_checks()
    {
        // Asserted in the state where they differ. Both are non-blocking when
        // there is nothing wrong, which is correct and proves nothing - the
        // question is whether the two fields can ever disagree, and the missing
        // credential is where they do.
        await using var stub = new StubControlPlane();
        using var temporary = new TemporaryStore();
        stub.Credentials.Add(new CredentialSummary
        {
            CredentialId = "019fe815-6136-7518-bb57-b06d6d3f411a",
            Repo = "acme/widgets",
            Reference = new CredentialReference
            {
                Kind = CredentialKinds.Local,
                Locator = "local:acme/widgets",
                Identity = "acme-bot",
                Scopes = [CredentialScopes.Read],
            },
            AddedAt = DateTimeOffset.UtcNow,
        });

        var checks = (await Build(stub, temporary.Store, ASession()).RunAsync()).Checks
            .Where(c => c.Name.Contains("credential", StringComparison.OrdinalIgnoreCase))
            .ToList();

        await Assert.That(checks.Count).IsEqualTo(2)
            .Because("where the secret lives and whether it resolves are different questions.");
        await Assert.That(checks.Select(c => (c.Blocking, c.Fixable)).Distinct().Count()).IsEqualTo(2)
            .Because("one of these stops a flight and the person can fix it; the other is a "
                   + "statement of fact about the machine.");
    }

    [Test]
    public async Task Doctor_still_checks_nothing_that_does_not_exist_yet()
    {
        // The rule the original version of this asserted about credentials.
        // Credentials exist now; these do not, and a check that passed because
        // the feature is absent is the same lie as a stub verb.
        await using var stub = new StubControlPlane();
        using var temporary = new TemporaryStore();

        var report = await Build(stub, temporary.Store, ASession()).RunAsync();

        foreach (var absent in (string[])["bundle", "envelope", "fact", "digest"])
        {
            await Assert.That(report.Checks.Any(c => c.Name.Contains(absent, StringComparison.OrdinalIgnoreCase)))
                .IsFalse()
                .Because($"nothing produces {absent}s yet, so a check on them could only ever pass.");
        }
    }
}
