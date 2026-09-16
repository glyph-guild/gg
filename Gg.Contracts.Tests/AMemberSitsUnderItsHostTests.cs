

namespace Gg.Contracts.Tests;

/// <summary>
/// A member says which machine runs it, so a fleet can be read as the shape it
/// actually is rather than as one flat list.
/// </summary>
/// <remarks>
/// <para>
/// <b>A pool member is a runner on somebody's host.</b> It beats, it holds a
/// credential, it can be sent one, and a maintenance ask can name it - so it
/// belongs in the fleet. But it is not a peer of the machines beside it: a
/// maintainer on one of them created it and it dies with the pool. Listed flat,
/// fifteen members bury the three machines a person actually operates.
/// </para>
/// <para>
/// <b>Absence is silence, as everywhere else in this contract.</b> Null means
/// "not a member", which is what every runner registered before this field
/// existed says, and what a laptop always says.
/// </para>
/// </remarks>
public class AMemberSitsUnderItsHostTests
{
    [Test]
    public async Task A_member_names_the_runner_that_hosts_it()
    {
        var member = new RunnerSummary
        {
            RunnerId = "01a0a8d4-ae13-74e9-af76-7aae5b144764",
            Label = "gg-pool-ui-1",
            State = "idle",
            HostRunnerId = "01a0a856-eac3-7671-9f0e-000000000000",
        };

        await Assert.That(member.HostRunnerId)
            .IsEqualTo("01a0a856-eac3-7671-9f0e-000000000000")
            .Because("the console groups a member under this, and a label cannot carry it: "
                   + "`gg-pool-ui-1` says nothing about which machine warmed it.");
    }

    [Test]
    public async Task An_ordinary_runner_says_nothing_and_that_is_how_it_is_known()
    {
        var laptop = new RunnerSummary
        {
            RunnerId = "01a0a81c-2d70-7393-a355-fa3d134ba06c",
            Label = "Kevins-MBP",
            State = "offline",
        };

        await Assert.That(laptop.HostRunnerId).IsNull()
            .Because("absence is silence: a laptop has no host, and neither has any runner "
                   + "described by a control plane too old to send this.");
    }

    // THE WIRE HALF IS IN Gg.Client.Tests, beside the serializer. This project
    // holds the contract's shape and takes no dependency that could reach a
    // JsonSerializerContext, so the round trip and the omitted-when-absent
    // property are asserted where the context lives.
}
