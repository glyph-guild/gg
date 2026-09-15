using Gg.Client;

namespace Gg.Client.Tests;

/// <summary>
/// The doctor finishes its report on a machine that is broken, which is the
/// only kind of machine it is ever run on.
/// </summary>
/// <remarks>
/// <para>
/// <b>REPORTED FROM USE: `gg doctor` crashes on the un-authed telemetry
/// check.</b> A session that is held locally and no longer honoured
/// server-side is the ordinary way to be signed out - a token expires, or
/// somebody revokes it - and it is exactly the state a person runs this verb
/// in. The session check diagnosed it correctly, said <i>"the control plane no
/// longer honours this session"</i>, and then the very next check asked the
/// control plane a question with that same dead token and let the 401 out as
/// an unhandled exception.
/// </para>
/// <para>
/// <b>So the verb died on the condition it had just diagnosed</b>, printing a
/// stack trace over its own output and never reaching the twelve checks below
/// - the runner, the channel, the executor, the airspace, the forge. Those are
/// the ones somebody is looking for when the machine is misbehaving, and the
/// crash guaranteed they never saw them.
/// </para>
/// <para>
/// <b>Two things were wrong, and both are worth fixing.</b> The narrow one:
/// three checks guarded on whether a session EXISTS when the question is
/// whether it WORKS, which the session check above them had already paid to
/// find out. The wide one: any check that threw ended the report. A doctor is
/// the wrong program to be brittle in - the exception is a fact about the
/// machine like any other, and belongs on a line rather than in a stack trace.
/// </para>
/// </remarks>
public class ADoctorThatOutlivesItsDiagnosisTests
{
    private sealed class Held(StoredSession? session) : ISessionStore
    {
        public StoredSession? Read() => session;
        public void Write(StoredSession value) { }
        public void Clear() { }
    }

    private static Doctor Against(StubControlPlane stub) => new(
        new ControlPlaneClient(new HttpClient { BaseAddress = new Uri(stub.BaseAddress) }),
        new Held(DoctorTests.AValidSession()),
        DoctorTests.ScratchStore(),
        new Uri(stub.BaseAddress));

    /// <summary>A control plane that no longer honours the session gg holds.</summary>
    private static StubControlPlane Forgotten()
    {
        var stub = new StubControlPlane();
        stub.RevokedTokens.Add(StubControlPlane.IssuedSessionToken);
        return stub;
    }

    [Test]
    public async Task A_session_it_no_longer_honours_does_not_end_the_report()
    {
        await using var stub = Forgotten();

        var report = await Against(stub).RunAsync();

        await Assert.That(report.Checks).IsNotEmpty()
            .Because("this threw NotSignedInException out of RunAsync, so there was no report "
                   + "at all - just a stack trace where the diagnosis should have been.");
    }

    [Test]
    public async Task And_the_checks_below_it_are_still_run()
    {
        // THE POINT. The session is dead and that is one line; whether an agent
        // binary is configured, whether a data channel opens, whether this
        // machine is reachable are all still true or false and all still worth
        // knowing. A person signs in again and finds the next problem, instead
        // of finding them one crash at a time.
        var report = await Against(Forgotten()).RunAsync();

        var names = report.Checks.Select(c => c.Name).ToList();

        await Assert.That(names).Contains(DoctorChecks.Telemetry);
        await Assert.That(names).Contains(DoctorChecks.Runner)
            .Because("everything after the check that threw was missing from the report.");
        await Assert.That(names).Contains(DoctorChecks.Channel);
    }

    [Test]
    public async Task The_telemetry_line_says_it_could_not_be_asked()
    {
        // NOT "the control plane exports nothing", which is what a check that
        // swallowed the refusal and carried on would say - and it is a claim
        // about somebody's deployment made from an answer nobody got.
        var report = await Against(Forgotten()).RunAsync();
        var telemetry = report.Checks.Single(c => c.Name == DoctorChecks.Telemetry);

        await Assert.That(telemetry.Detail).Contains("not checked");
        await Assert.That(telemetry.Passed).IsFalse();
    }

    [Test]
    public async Task The_session_line_still_names_the_real_problem()
    {
        // ASK WHY IT PASSES: a doctor that skipped every authed check would
        // also satisfy the assertions above while saying nothing. The session
        // check is the one that must still speak, because it is the one with
        // the fix on it.
        var report = await Against(Forgotten()).RunAsync();
        var session = report.Checks.Single(c => c.Name == DoctorChecks.Session);

        await Assert.That(session.Passed).IsFalse();
        await Assert.That(session.Detail).Contains("no longer honours");
        await Assert.That(session.Fix).IsEqualTo("gg login");
    }

    [Test]
    public async Task A_check_that_throws_becomes_a_line_rather_than_a_stack_trace()
    {
        // THE WIDER HALF, and a real state rather than a contrived one: a
        // control plane upgraded between two calls of one doctor run answers
        // the protocol check and then refuses the door below it. Whatever the
        // cause, a throwing check is a check that could not be run - which is
        // something to report, not something to die of.
        await using var stub = new StubControlPlane();
        stub.UpgradeRequiredPaths.Add("/v1/telemetry");

        var report = await Against(stub).RunAsync();

        var telemetry = report.Checks.Single(c => c.Name == DoctorChecks.Telemetry);

        await Assert.That(telemetry.Passed).IsFalse();
        await Assert.That(telemetry.Blocking).IsFalse()
            .Because("a check that could not be run has not found anything wrong, and an exit "
                   + "code that said otherwise would be reporting the doctor's own trouble as "
                   + "the machine's.");
        await Assert.That(report.Checks.Select(c => c.Name)).Contains(DoctorChecks.Channel)
            .Because("and the rest of the report survives it.");
    }

    [Test]
    public async Task What_it_could_not_do_is_said_in_the_line()
    {
        // A check reported as failed with nothing about why is worse than the
        // crash it replaced: the crash at least named the exception.
        await using var stub = new StubControlPlane();
        stub.UpgradeRequiredPaths.Add("/v1/telemetry");

        var telemetry = (await Against(stub).RunAsync()).Checks
            .Single(c => c.Name == DoctorChecks.Telemetry);

        await Assert.That(telemetry.Detail).Contains("protocol")
            .Because("the refusal already says what is wrong in words somebody can act on, "
                   + "and this check is the only place it would ever be seen.");
    }
}
