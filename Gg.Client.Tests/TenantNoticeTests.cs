using Gg.Client;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// What the control plane says is wrong, said here too.
/// </summary>
/// <remarks>
/// <para>
/// A degradation the control plane knows about is useless if the only place it
/// appears is a log nobody reads. The one that prompted this: the app writing
/// check runs gets uninstalled, observation keeps working - the runner clones
/// with the customer's own credential and never needed ours - and only the
/// reporting breaks. Nothing on the developer's machine can detect that, and
/// nothing about it looks broken until somebody asks why a pull request has no
/// check on it.
/// </para>
/// <para>
/// <b>The sentence comes from the control plane, whole.</b> gg names no forge -
/// that is the neutrality rule, and it is why the remedy cannot be written
/// here. What gg contributes is placement: a notice becomes a doctor check
/// with the blocking and fixable flags the control plane set, and the remedy
/// it supplied.
/// </para>
/// </remarks>
public class TenantNoticeTests
{
    private static Doctor Build(StubControlPlane stub, ISessionStore sessions) =>
        new(new ControlPlaneClient(new HttpClient { BaseAddress = new Uri(stub.BaseAddress) }),
            sessions,
            new FileCredentialStore(Path.Combine(
                Path.GetTempPath(), "gg-notice-tests", Guid.NewGuid().ToString("n"))),
            new Uri(stub.BaseAddress));

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
        ControlPlane = "http://127.0.0.1",
    };

    private static TenantNotice ARevokedEgressNotice() => new()
    {
        Code = TenantNoticeCodes.Egress,
        Detail = "The app that writes check runs is no longer installed for installation 501.",
        Remedy = "Reinstall it from the console; the account rejoins this tenant with its history intact.",
        Blocking = true,
    };

    [Test]
    public async Task A_notice_becomes_a_check()
    {
        using var stub = new StubControlPlane { Notices = [ARevokedEgressNotice()] };
        var report = await Build(stub, new HeldSession(AValidSession())).RunAsync();

        var check = report.Checks.SingleOrDefault(c => c.Name == DoctorChecks.Egress);

        await Assert.That(check).IsNotNull();
        await Assert.That(check!.Passed).IsFalse();
    }

    [Test]
    public async Task A_tenant_with_nothing_wrong_gets_no_check()
    {
        // The twin. A doctor that always printed an egress line would train
        // somebody to read past the one that matters, and there is nothing
        // useful to say about egress that is working.
        using var stub = new StubControlPlane();
        var report = await Build(stub, new HeldSession(AValidSession())).RunAsync();

        await Assert.That(report.Checks.Any(c => c.Name == DoctorChecks.Egress)).IsFalse();
    }

    [Test]
    public async Task The_check_carries_the_remedy_the_control_plane_supplied()
    {
        // Not a remedy written here. gg cannot name the forge, so a sentence
        // composed on this side would either be useless or would break the
        // neutrality rule the whole design rests on.
        using var stub = new StubControlPlane { Notices = [ARevokedEgressNotice()] };
        var report = await Build(stub, new HeldSession(AValidSession())).RunAsync();

        var check = report.Checks.Single(c => c.Name == DoctorChecks.Egress);

        await Assert.That(check.Fixable).IsTrue();
        await Assert.That(check.Fix).IsEqualTo(ARevokedEgressNotice().Remedy);
        await Assert.That(check.Detail).IsEqualTo(ARevokedEgressNotice().Detail);
    }

    [Test]
    public async Task A_blocking_notice_makes_the_report_exit_non_zero()
    {
        // What "blocking" is for. A check that reads red and exits 0 is a
        // check that a script ignores.
        using var stub = new StubControlPlane { Notices = [ARevokedEgressNotice()] };
        var report = await Build(stub, new HeldSession(AValidSession())).RunAsync();

        await Assert.That(report.Checks.Single(c => c.Name == DoctorChecks.Egress).Blocking).IsTrue();
        await Assert.That(report.ExitCode).IsEqualTo(1);
    }

    [Test]
    public async Task A_notice_that_is_not_blocking_does_not_fail_the_report()
    {
        // The twin of the assertion above. The control plane decides which a
        // notice is; gg must not upgrade one, or every advisory becomes a
        // broken build.
        using var stub = new StubControlPlane
        {
            Notices = [ARevokedEgressNotice() with { Blocking = false, Remedy = null }],
        };
        var report = await Build(stub, new HeldSession(AValidSession())).RunAsync();

        var check = report.Checks.Single(c => c.Name == DoctorChecks.Egress);
        await Assert.That(check.Blocking).IsFalse();
        await Assert.That(check.Fixable).IsFalse()
            .Because("no remedy means nothing to offer; claiming fixable would send somebody looking.");
        await Assert.That(report.ExitCode).IsEqualTo(0);
    }

    [Test]
    public async Task A_notice_naming_a_forge_still_renders_because_gg_never_composes_one()
    {
        // The neutrality rule cuts one way only: gg contains no forge name, and
        // it renders whatever sentence it is handed. A gg that filtered
        // provider names out of a control-plane diagnosis would be a gg that
        // hides the remedy.
        using var stub = new StubControlPlane
        {
            Notices = [ARevokedEgressNotice() with
            {
                Detail = "The Good Grief app is no longer installed on github.com for glyph-guild.",
            }],
        };
        var report = await Build(stub, new HeldSession(AValidSession())).RunAsync();

        await Assert.That(report.Checks.Single(c => c.Name == DoctorChecks.Egress).Detail)
            .Contains("github.com");
    }

    [Test]
    public async Task Control_sequences_in_a_notice_are_stripped_before_they_reach_a_terminal()
    {
        // A notice is externally-sourced text arriving at a renderer, which is
        // the same shape as every other ingress in this system. The control
        // plane strips at its own edge; this is the last code between a
        // response body and somebody's terminal.
        using var stub = new StubControlPlane
        {
            Notices = [ARevokedEgressNotice() with { Detail = "clean[31mred[0m" }],
        };
        var report = await Build(stub, new HeldSession(AValidSession())).RunAsync();

        var detail = report.Checks.Single(c => c.Name == DoctorChecks.Egress).Detail;

        await Assert.That(detail).DoesNotContain("");
        await Assert.That(detail).Contains("red")
            .Because("stripped rather than dropped - if the text vanished, the absence above would "
                   + "also pass on a doctor that silently discarded the notice.");
    }

    [Test]
    public async Task Notices_are_not_read_when_there_is_no_session()
    {
        // Nothing to ask on behalf of. A doctor that reported an egress problem
        // to somebody who is not signed in would be reporting somebody else's.
        using var stub = new StubControlPlane { Notices = [ARevokedEgressNotice()] };
        var report = await Build(stub, new HeldSession(null)).RunAsync();

        await Assert.That(report.Checks.Any(c => c.Name == DoctorChecks.Egress)).IsFalse();
    }
}
