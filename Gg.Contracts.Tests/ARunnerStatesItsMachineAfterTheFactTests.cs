using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// A runner registered before machines existed can say which machine it is on.
/// </summary>
/// <remarks>
/// <para>
/// <b><see cref="RunnerKeyOffer"/>'s reason, one fact over.</b> Registration is
/// read-or-register: a runner with a stored credential never registers again,
/// so a machine name added to registration reaches only runners registered from
/// now on. The resident and the maintainer on a host that has been up for weeks
/// would stay machineless for as long as their credentials last - and those are
/// exactly the two a person wants the members grouped under.
/// </para>
/// <para>
/// <b>No id in the path</b>, on the reason the agent reading gives: the
/// credential names the runner, and a path id is a fleet to enumerate.
/// </para>
/// <para>
/// <b>Not a substitution check.</b> A key offer refuses a DIFFERENT key, because
/// consoles pinned the first. A machine is a display grouping that nothing
/// authorizes on, so a runner moved to another host simply says so.
/// </para>
/// </remarks>
public class ARunnerStatesItsMachineAfterTheFactTests
{
    [Test]
    public async Task The_route_is_declared_for_runners_with_no_id_in_the_path()
    {
        var endpoint = ProtocolSurface.Endpoints.Single(e =>
            e.Method == "POST" && e.Path == "/v1/runner/machine");

        await Assert.That(endpoint.Audience).IsEqualTo(Audience.Runner);
        await Assert.That(endpoint.Request).IsEqualTo(typeof(RunnerMachineOffer));
        await Assert.That(endpoint.RequiredHeaders).Contains(ProtocolSurface.RunnerHeader);
        await Assert.That(endpoint.RequiredHeaders).DoesNotContain(ProtocolSurface.SessionHeader)
            .Because("a session on a runner route would let a person say where somebody "
                   + "else's machine is.");
        await Assert.That(endpoint.Path).DoesNotContain("{id}")
            .Because("the credential names the runner, and a path id is a fleet to enumerate.");
        await Assert.That(endpoint.Statuses).Contains(204)
            .Because("there is nothing to say back: the runner already knows its machine.");
        await Assert.That(endpoint.Statuses).DoesNotContain(409)
            .Because("unlike a key, a changed machine is not a substitution anybody pinned.");
    }

    [Test]
    public async Task The_offer_carries_the_machine_and_nothing_that_names_a_runner()
    {
        var offer = new RunnerMachineOffer { Machine = "vmlinux001" };

        await Assert.That(offer.Machine).IsEqualTo("vmlinux001");
        await Assert.That(typeof(RunnerMachineOffer).GetProperties().Select(p => p.Name))
            .IsEquivalentTo(["Machine"])
            .Because("the runner is the credential's to name; a body that could name one "
                   + "would let a runner speak for another.");
    }
}
