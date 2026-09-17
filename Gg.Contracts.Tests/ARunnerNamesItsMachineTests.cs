namespace Gg.Contracts.Tests;

/// <summary>
/// A runner says which machine it runs on, so a fleet can be read as machines
/// rather than as a list of processes.
/// </summary>
/// <remarks>
/// <para>
/// <b>A machine runs several runners.</b> On vmlinux001 there is the resident
/// that takes flights, a maintainer per pool, and the members those maintainers
/// warm. Each is its own registration, and nothing on the wire said they share
/// a host - so a person reading the fleet saw members nested under
/// <c>vmlinux001:maintain</c>, a registration that never beats and reads as
/// offline, rather than under the machine they think of.
/// </para>
/// <para>
/// <b>The label only implied it.</b> Every gg registration derives its label
/// from the host name - <c>vmlinux001</c>, <c>vmlinux001:maintain</c>,
/// <c>vmlinux001:hand</c> - so the machine was always known at registration
/// and never sent. Parsing it back out of the label would make a string
/// convention into an identity.
/// </para>
/// <para>
/// <b>Absence is silence.</b> Null means "not said", which is what every
/// runner registered before this field existed says until it is backfilled.
/// </para>
/// </remarks>
public class ARunnerNamesItsMachineTests
{
    [Test]
    public async Task A_registration_says_which_machine_it_runs_on()
    {
        var request = new RunnerRegistrationRequest
        {
            Label = "vmlinux001:maintain",
            ProtocolVersion = 1,
            Machine = "vmlinux001",
        };

        await Assert.That(request.Machine).IsEqualTo("vmlinux001")
            .Because("the label says how a runner was started; this says where, and only "
                   + "this can group a maintainer with the machine it maintains.");
    }

    [Test]
    public async Task A_fleet_row_carries_the_machine()
    {
        var member = new RunnerSummary
        {
            RunnerId = "01a0ac99-6b6b-7000-8000-000000000000",
            Label = "gg-pool-ui-1",
            State = "idle",
            Machine = "vmlinux001",
        };

        await Assert.That(member.Machine).IsEqualTo("vmlinux001")
            .Because("a member's own label names its pool slot, not its host, so the fleet "
                   + "row is the only place its machine can travel.");
    }

    [Test]
    public async Task A_runner_that_said_nothing_is_on_no_named_machine()
    {
        var old = new RunnerSummary
        {
            RunnerId = "01a082c9-5e33-7000-8000-000000000000",
            Label = "Kevins-MBP",
            State = "offline",
        };
        var request = new RunnerRegistrationRequest { Label = "Kevins-MBP", ProtocolVersion = 1 };

        await Assert.That(old.Machine).IsNull();
        await Assert.That(request.Machine).IsNull()
            .Because("an older client sends no machine, and the control plane must accept "
                   + "that rather than refuse every registration it has always taken.");
    }
}
