using Gg.Runner.Exposures;

namespace Gg.Runner.Tests;

/// <summary>
/// A machine serving a preview holds its lease until the gate is answered, and
/// takes no other work while it does.
/// </summary>
/// <remarks>
/// <para>
/// <b>The owner's decision, 2026-09-25: a flight gated on a preview stays
/// alive, and the runner cannot accept work.</b> The two are one requirement.
/// The address points at a server inside that flight's tree, on that flight's
/// port — so a second flight on the same machine would want the same port and
/// the same tree, and the first person's preview would go out from under them.
/// </para>
/// <para>
/// <b>Bounded by the lease, not by a timer.</b> The hold already ends when a
/// renewal comes back fenced or gone, which is what happens once the gate is
/// answered and the flight lands. So this needs no new signal and no polling —
/// the machine is released by the same event that makes it free.
/// </para>
/// <para>
/// <b>An address is the test, not the grant.</b> A machine holding a slot whose
/// connector never started has nothing serving, so it holds its ten seconds and
/// goes back to work like any other.
/// </para>
/// </remarks>
public class APreviewHoldsItsMachineTests
{
    private static ExposureServed Serving() => new()
    {
        Url = "https://jdapp-01.goodgrief.dev",
        Exposure = "jdapp",
        Slot = "01",
    };

    private static ExposureServed Unserved() => new()
    {
        Exposure = "jdapp",
        Slot = "01",
        Diagnosis = "cloudflared is not on this machine",
    };

    [Test]
    public async Task A_machine_serving_an_address_holds_until_the_lease_ends()
    {
        await Assert.That(TreeRetention.HoldsItsMachine(Serving())).IsTrue()
            .Because("the address points at a server in this flight's tree on this flight's "
                   + "port, so taking a second flight would pull both out from under the "
                   + "person who was asked to look at it.");
    }

    [Test]
    public async Task A_machine_serving_nothing_holds_for_its_ordinary_window()
    {
        // EVERY FLIGHT TODAY. A runner that held after every flight would take
        // one piece of work and never another.
        await Assert.That(TreeRetention.HoldsItsMachine(null)).IsFalse();
    }

    [Test]
    public async Task A_preview_that_could_not_be_served_holds_nothing()
    {
        // NO ADDRESS MEANS NO SERVER, and nobody was told to go and look at
        // anything - so there is no reason to keep the machine out of service.
        await Assert.That(TreeRetention.HoldsItsMachine(Unserved())).IsFalse();
    }

    [Test]
    public async Task It_is_the_same_fact_that_keeps_the_tree()
    {
        // ONE CONDITION, TWO CONSEQUENCES. The tree must outlive the flight and
        // so must the machine, for the same reason and at the same moment - two
        // rules that could disagree would leave a held machine serving from a
        // released tree, which is the state GG-303 was found in.
        await Assert.That(TreeRetention.HoldsItsMachine(Serving()))
            .IsEqualTo(TreeRetention.MustKeep(landed: true, Serving()));
    }
}
