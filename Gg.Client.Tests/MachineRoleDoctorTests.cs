using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// <c>gg doctor</c> answering "can this machine do the job it was installed
/// for", not only "can it reach the control plane".
/// </summary>
/// <remarks>
/// <para>
/// <b>Every existing check answers the same question</b> — control plane,
/// protocol, session, credential store — and all of them can pass on a host that
/// will claim flights and never run an agent. That is the question a person
/// stands up a pool host to settle, and it was the one thing doctor did not
/// answer.
/// </para>
/// <para>
/// <b>The address being a default is a fact about this machine, not about the
/// server.</b> With <c>GG_CONTROL_PLANE</c> unset the CLI falls back to
/// localhost, every verb fails with a connection refused, and doctor reported it
/// as <c>not something this machine can fix</c>. It is exactly what this machine
/// can fix. That diagnosis sent two people looking at a healthy control plane.
/// </para>
/// <para>
/// <b>The facts are passed in rather than read here</b>, like
/// <c>accountsMissing</c> already is: <c>Gg.Client</c> references only
/// <c>Gg.Contracts</c>, and the environment belongs to whoever composed the
/// process. It also means these tests state a machine's shape instead of mutating
/// the environment of a test run that is parallel by default.
/// </para>
/// </remarks>
public class MachineRoleDoctorTests
{
    private static StoredSession ASession() => new()
    {
        SessionToken = StubControlPlane.IssuedSessionToken,
        ExpiresAt = DateTimeOffset.UtcNow.AddHours(12),
        TenantId = "019fe062-d000-730c-a37d-7247342cd810",
        PrincipalDisplay = "stub-principal",
    };

    private static async Task<DoctorReport> ReportAsync(
        StubControlPlane stub, MachineRole role, bool addressConfigured = true)
    {
        using var temporary = new TemporaryStore();

        return await new Doctor(
                new ControlPlaneClient(new HttpClient { BaseAddress = new Uri(stub.BaseAddress) }),
                new HeldSessionStore(ASession()),
                temporary.Store,
                new Uri(stub.BaseAddress),
                addressConfigured)
            .RunAsync(role: role);
    }

    private static DoctorCheck Of(DoctorReport report, string name) =>
        report.Checks.Single(c => c.Name == name);

    [Test]
    public async Task A_runner_with_no_executor_is_told_it_will_never_invoke_an_agent()
    {
        // The consequence, not the variable. "GG_EXECUTOR_BINARY is not set" is
        // true and means nothing to somebody who does not already know what it
        // does; what they need is that this host will claim work and do none.
        await using var stub = new StubControlPlane();

        var check = Of(await ReportAsync(stub, MachineRole.None), DoctorChecks.Executor);

        await Assert.That(check.Passed).IsFalse();
        await Assert.That(check.Detail).Contains("never invoke")
            .Because("a host with no executor passes every other check, registers, claims "
                   + "flights, and runs nothing.");
        await Assert.That(check.Fixable).IsTrue()
            .Because("it is a variable on this machine, which is the definition of fixable.");
    }

    [Test]
    public async Task An_executor_whose_binary_is_missing_is_not_the_same_as_none()
    {
        // Two different mistakes with two different fixes: nobody configured
        // one, and somebody configured one that is not there.
        await using var stub = new StubControlPlane();

        var check = Of(
            await ReportAsync(stub, MachineRole.WithExecutor("/nowhere/claude", present: false)),
            DoctorChecks.Executor);

        await Assert.That(check.Passed).IsFalse();
        await Assert.That(check.Detail).Contains("/nowhere/claude")
            .Because("the path it looked for is the whole diagnosis - a typo in a unit file "
                   + "reads identically to a missing install without it.");
    }

    [Test]
    public async Task A_configured_executor_passes_and_says_where_it_is()
    {
        await using var stub = new StubControlPlane();

        var check = Of(
            await ReportAsync(stub, MachineRole.WithExecutor("/usr/local/bin/claude", present: true)),
            DoctorChecks.Executor);

        await Assert.That(check.Passed).IsTrue();
        await Assert.That(check.Detail).Contains("/usr/local/bin/claude");
    }

    [Test]
    public async Task A_defaulted_control_plane_address_says_so_and_is_fixable()
    {
        // THE ONE THAT COST TWO PEOPLE AN AFTERNOON. Unreachable and defaulted
        // is a different sentence from unreachable and configured, and only one
        // of them is about the server.
        await using var stub = new StubControlPlane();
        await stub.DisposeAsync();

        var report = await ReportAsync(stub, MachineRole.None, addressConfigured: false);
        var check = Of(report, DoctorChecks.ControlPlane);

        await Assert.That(check.Passed).IsFalse();
        await Assert.That(check.Detail).Contains("GG_CONTROL_PLANE")
            .Because("the variable is not set, and saying 'not something this machine can fix' "
                   + "about a missing local value points at a healthy server.");
        await Assert.That(check.Fixable).IsTrue();
    }

    [Test]
    public async Task A_configured_address_that_is_down_is_still_not_this_machines_fault()
    {
        // THE TWIN. The existing sentence is right whenever somebody DID
        // configure an address, and this change must not turn every outage into
        // advice to check a variable that is already correct.
        await using var stub = new StubControlPlane();
        await stub.DisposeAsync();

        var report = await ReportAsync(stub, MachineRole.None, addressConfigured: true);
        var check = Of(report, DoctorChecks.ControlPlane);

        await Assert.That(check.Passed).IsFalse();
        await Assert.That(check.Fixable).IsFalse()
            .Because("nothing on this machine changes whether a remote service is up, and "
                   + "telling somebody to try is how a support call starts badly.");
        await Assert.That(check.Detail).DoesNotContain("GG_CONTROL_PLANE");
    }

    [Test]
    public async Task A_host_with_no_forge_configured_is_warned_rather_than_stopped()
    {
        // Not blocking: plenty of hosts take flights that never clone. It is
        // still worth saying, because a pool host missing it looks healthy right
        // up until a flight needs a repository.
        await using var stub = new StubControlPlane();

        var check = Of(await ReportAsync(stub, MachineRole.None), DoctorChecks.Forge);

        await Assert.That(check.Passed).IsFalse();
        await Assert.That(check.Blocking).IsFalse();
    }

    [Test]
    public async Task A_configured_forge_is_reported_without_disclosing_a_secret()
    {
        await using var stub = new StubControlPlane();

        var check = Of(
            await ReportAsync(stub, MachineRole.None with { ForgeHosts = "ado=forge.example.invalid" }),
            DoctorChecks.Forge);

        await Assert.That(check.Passed).IsTrue();
        await Assert.That(check.Detail).Contains("forge.example.invalid");
    }

    [Test]
    public async Task Nothing_about_the_machines_role_is_blocking_on_its_own()
    {
        // A person running doctor on a laptop is not running a pool host, and
        // an exit code that failed there would make the verb useless where it
        // is used most.
        await using var stub = new StubControlPlane();

        var report = await ReportAsync(stub, MachineRole.None);

        foreach (var name in (string[])[DoctorChecks.Executor, DoctorChecks.Forge, DoctorChecks.Pool])
        {
            await Assert.That(Of(report, name).Blocking).IsFalse()
                .Because($"'{name}' describes a role this machine may simply not have.");
        }
    }
}
