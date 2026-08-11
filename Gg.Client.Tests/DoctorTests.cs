namespace Gg.Client.Tests;

/// <summary>
/// <c>gg doctor</c> covers only what exists, and classifies each check
/// blocking and fixable INDEPENDENTLY.
/// </summary>
/// <remarks>
/// <para>
/// The pairing is the point. Collapsing them into one severity loses the two
/// cases that matter most: a blocking problem the person cannot fix themselves
/// - which is a support call, and should say so - and a non-blocking one they
/// can, which is the entire value of a doctor command.
/// </para>
/// <para>
/// Connectivity, session validity, protocol floor, runner reachability. Nothing
/// about credentials: that is step 5, and a check that always passed because
/// the feature does not exist would be worse than no check.
/// </para>
/// </remarks>
public class DoctorTests
{
    /// <summary>
    /// A doctor pointed at a scratch credential store.
    /// </summary>
    /// <remarks>
    /// The store is real but empty and lives under the temp directory: these
    /// tests are about the other checks, and a doctor reading the developer's
    /// own store would answer differently on every machine.
    /// </remarks>
    private static Doctor Build(StubControlPlane stub, ISessionStore sessions) =>
        new(new ControlPlaneClient(new HttpClient { BaseAddress = new Uri(stub.BaseAddress) }),
            sessions,
            ScratchStore(),
            new Uri(stub.BaseAddress));

    private static FileCredentialStore ScratchStore() =>
        new(Path.Combine(Path.GetTempPath(), "gg-doctor-tests", Guid.NewGuid().ToString("n")));

    private sealed class HeldSession(StoredSession? session) : ISessionStore
    {
        public StoredSession? Read() => session;
        public void Write(StoredSession value) { }
        public void Clear() { }
    }

    private static StoredSession AValidSession() => new()
    {
        SessionToken = StubControlPlane.IssuedSessionToken,
        ExpiresAt = DateTimeOffset.UtcNow.AddHours(12),
        TenantId = "019fe062-d000-730c-a37d-7247342cd810",
        PrincipalDisplay = "stub-principal",
    };

    [Test]
    public async Task Blocking_and_fixable_are_answered_separately_for_every_check()
    {
        await using var stub = new StubControlPlane();
        var report = await Build(stub, new HeldSession(AValidSession())).RunAsync();

        await Assert.That(report.Checks).IsNotEmpty();

        // Both combinations must be REACHABLE, or the two fields are one field
        // wearing two names.
        var seen = report.Checks.Select(c => (c.Blocking, c.Fixable)).Distinct().ToList();
        await Assert.That(seen.Count).IsGreaterThan(1)
            .Because("if every check agreed on both, nothing here would be classifying anything.");
    }

    [Test]
    public async Task A_control_plane_that_cannot_be_reached_is_blocking()
    {
        // Nothing else gg does works without it, so this is not a warning.
        await using var stub = new StubControlPlane();
        var unreachable = new Doctor(
            new ControlPlaneClient(new HttpClient { BaseAddress = new Uri("http://127.0.0.1:1/") }),
            new HeldSession(AValidSession()),
            ScratchStore(),
            new Uri("http://127.0.0.1:1/"));

        var check = (await unreachable.RunAsync()).Checks.Single(c => c.Name == DoctorChecks.ControlPlane);

        await Assert.That(check.Passed).IsFalse();
        await Assert.That(check.Blocking).IsTrue();
        await Assert.That(check.Fixable).IsFalse()
            .Because("a person cannot fix an unreachable control plane from their laptop, "
                   + "and telling them to try is how a support call starts badly.");
    }

    [Test]
    public async Task No_session_is_blocking_and_the_person_can_fix_it()
    {
        // The pairing that justifies two fields: it stops everything, and the
        // remedy is one command they already have.
        await using var stub = new StubControlPlane();
        var check = (await Build(stub, new HeldSession(null)).RunAsync())
            .Checks.Single(c => c.Name == DoctorChecks.Session);

        await Assert.That(check.Passed).IsFalse();
        await Assert.That(check.Blocking).IsTrue();
        await Assert.That(check.Fixable).IsTrue();
        await Assert.That(check.Fix).IsNotNull()
            .Because("'fixable' with no fix named is a claim, not help.");
    }

    [Test]
    public async Task A_session_the_control_plane_no_longer_honours_is_reported_as_such()
    {
        // Held locally and dead server-side is the case a local expiry check
        // alone would call healthy.
        await using var stub = new StubControlPlane();
        stub.RevokedTokens.Add(StubControlPlane.IssuedSessionToken);

        var check = (await Build(stub, new HeldSession(AValidSession())).RunAsync())
            .Checks.Single(c => c.Name == DoctorChecks.Session);

        await Assert.That(check.Passed).IsFalse();
        await Assert.That(check.Fixable).IsTrue();
    }

    [Test]
    public async Task A_healthy_session_passes()
    {
        // Without this, every assertion above would also hold for a doctor
        // that failed everything unconditionally.
        await using var stub = new StubControlPlane();
        var report = await Build(stub, new HeldSession(AValidSession())).RunAsync();

        await Assert.That(report.Checks.Single(c => c.Name == DoctorChecks.Session).Passed).IsTrue();
        await Assert.That(report.Checks.Single(c => c.Name == DoctorChecks.ControlPlane).Passed).IsTrue();
    }

    [Test]
    public async Task A_binary_below_the_protocol_floor_is_blocking_and_fixable()
    {
        // Upgrading is something the person can do, which is exactly why the
        // refusal has to reach them as a diagnosis rather than as a 426.
        await using var stub = new StubControlPlane { ProtocolFloorMessage = "This control plane speaks 2..4." };

        var check = (await Build(stub, new HeldSession(AValidSession())).RunAsync())
            .Checks.Single(c => c.Name == DoctorChecks.Protocol);

        await Assert.That(check.Passed).IsFalse();
        await Assert.That(check.Blocking).IsTrue();
        await Assert.That(check.Fixable).IsTrue();
        await Assert.That(check.Detail).Contains("2..4")
            .Because("the range the control plane named is the actionable part.");
    }

    [Test]
    public async Task A_check_that_could_not_run_offers_no_fix()
    {
        // Found by running the binary: with the control plane down, doctor was
        // reporting the protocol as failed AND fixable by installing a newer
        // gg - advice that costs somebody time and changes nothing, over a
        // network problem. A check that did not run has no remedy to offer.
        var unreachable = new Doctor(
            new ControlPlaneClient(new HttpClient { BaseAddress = new Uri("http://127.0.0.1:1/") }),
            new HeldSession(AValidSession()),
            ScratchStore(),
            new Uri("http://127.0.0.1:1/"));

        var report = await unreachable.RunAsync();

        foreach (var check in report.Checks.Where(c => c.Detail.StartsWith("not checked", StringComparison.Ordinal)))
        {
            await Assert.That(check.Fixable).IsFalse()
                .Because($"'{check.Name}' never ran, so it cannot claim the person can fix it.");
            await Assert.That(check.Fix).IsNull();
        }

        await Assert.That(report.Checks.Any(c => c.Detail.StartsWith("not checked", StringComparison.Ordinal)))
            .IsTrue()
            .Because("with nothing skipped this test would assert over an empty set.");
    }

    [Test]
    public async Task Nothing_claims_to_be_fixable_without_naming_a_fix()
    {
        // The general form. 'fixable' with no remedy named is a claim rather
        // than help, and it is the shape a copy-pasted check arrives in.
        await using var stub = new StubControlPlane();

        foreach (var report in (DoctorReport[])
                 [await Build(stub, new HeldSession(AValidSession())).RunAsync(),
                  await Build(stub, new HeldSession(null)).RunAsync()])
        {
            foreach (var check in report.Checks.Where(c => c.Fixable))
            {
                await Assert.That(string.IsNullOrWhiteSpace(check.Fix)).IsFalse()
                    .Because($"'{check.Name}' says it is fixable and does not say how.");
            }
        }
    }

    [Test]
    public async Task Doctor_reports_where_the_control_plane_sends_telemetry()
    {
        // A customer runs the control plane in their own account, and "is this
        // thing transmitting to anybody" is a question they must be able to
        // ask it. Ambient environment once chose a destination nothing in
        // either repository had configured, and nobody could have found out.
        await using var stub = new StubControlPlane { TelemetryDestination = "https://collector.example.invalid" };

        var check = (await Build(stub, new HeldSession(AValidSession())).RunAsync())
            .Checks.Single(c => c.Name == DoctorChecks.Telemetry);

        await Assert.That(check.Detail).Contains("collector.example.invalid");
        await Assert.That(check.Blocking).IsFalse()
            .Because("a destination the customer chose is the system working, not failing.");
    }

    [Test]
    public async Task Doctor_says_so_when_the_control_plane_sends_nothing()
    {
        // The other state, said out loud. Silence and "exports nothing" must
        // not be the same line, or the report is worthless in exactly the case
        // somebody is checking.
        await using var stub = new StubControlPlane();

        var check = (await Build(stub, new HeldSession(AValidSession())).RunAsync())
            .Checks.Single(c => c.Name == DoctorChecks.Telemetry);

        await Assert.That(check.Detail).Contains("nothing");
        await Assert.That(check.Passed).IsTrue();
    }

    [Test]
    public async Task An_unreachable_runner_is_not_blocking()
    {
        // A person can read their flights, open one, and look at a log with no
        // runner at all. Calling it blocking would train them to ignore the
        // word.
        await using var stub = new StubControlPlane();
        var check = (await Build(stub, new HeldSession(AValidSession())).RunAsync())
            .Checks.Single(c => c.Name == DoctorChecks.Runner);

        await Assert.That(check.Blocking).IsFalse();
    }

    [Test]
    public async Task Doctor_checks_nothing_that_does_not_exist_yet()
    {
        // Credentials came off this list at step 5, which is what a list like
        // this is for. The rest stay: a check that passed because the feature
        // is absent is the same lie as a stub verb.
        await using var stub = new StubControlPlane();
        var report = await Build(stub, new HeldSession(AValidSession())).RunAsync();

        foreach (var absent in (string[])["bundle", "envelope", "fact", "digest"])
        {
            await Assert.That(report.Checks.Any(c => c.Name.Contains(absent, StringComparison.OrdinalIgnoreCase)))
                .IsFalse()
                .Because($"nothing produces {absent}s yet, so a check on them could only ever pass.");
        }
    }

    [Test]
    public async Task The_report_exits_non_zero_only_when_something_blocking_failed()
    {
        // A doctor that always exits zero is decoration in a script, and one
        // that exits non-zero on a warning makes people stop running it.
        await using var stub = new StubControlPlane();

        var healthy = await Build(stub, new HeldSession(AValidSession())).RunAsync();
        await Assert.That(healthy.ExitCode).IsEqualTo(0);

        var noSession = await Build(stub, new HeldSession(null)).RunAsync();
        await Assert.That(noSession.ExitCode).IsNotEqualTo(0);
    }
}
