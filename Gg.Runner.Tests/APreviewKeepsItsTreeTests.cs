using Gg.Contracts;
using Gg.Runner.Exposures;

namespace Gg.Runner.Tests;

/// <summary>
/// A flight that published a preview keeps its tree, because the server that
/// preview points at is still running inside it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Measured on GG-303, and on GG-268 before it.</b> The ui-preview work kind
/// tells an agent to leave the server running, because the person who asked for
/// the preview opens the address after the flight ends. The runner then
/// released the tree, and the kernel reported the server's working directory as
/// <c>(deleted)</c> — alive, holding two unlinked files open, and failing every
/// lazy import with <i>"Cannot find module …/vite/dist/node/chunks/…"</i>.
/// </para>
/// <para>
/// <b>Up and gutted is worse than down.</b> A dead address answers 502 and a
/// person knows to wait; this one answered 500 with a stack trace naming paths
/// that no longer exist, which reads as a broken application rather than a
/// reclaimed directory.
/// </para>
/// <para>
/// <b>Held rather than special-cased on the ending.</b> The runner already
/// holds a tree it did not land, and says so; this is the same hold for a
/// different reason, and the reason travels with it so an operator reading the
/// line knows why disk is being used.
/// </para>
/// </remarks>
public class APreviewKeepsItsTreeTests
{
    private static LeasePreview Granted() => new()
    {
        Exposure = "jdapp",
        Slot = 1,
        Hostname = "jdapp-01.goodgrief.dev",
        Credential = "keyvault://ggdev.vault.example/jdapp-01",
        Port = 8080,
    };

    [Test]
    public async Task A_flight_serving_a_preview_is_not_released()
    {
        var serving = new ExposureServed
        {
            Url = "https://jdapp-01.goodgrief.dev",
            Exposure = "jdapp",
            Slot = "01",
        };

        await Assert.That(TreeRetention.MustKeep(landed: true, serving)).IsTrue()
            .Because("the server the address points at runs inside that tree, so releasing it "
                   + "leaves a process whose working directory the kernel reports as deleted - "
                   + "up, and failing every lazy import.");
    }

    [Test]
    public async Task A_flight_serving_nothing_is_released_as_before()
    {
        // EVERY FLIGHT TODAY. A landed flight's tree is reclaimed, and a machine
        // that kept them all would fill its own disk - which this estate has
        // already measured once.
        await Assert.That(TreeRetention.MustKeep(landed: true, serving: null)).IsFalse();
    }

    [Test]
    public async Task A_flight_that_did_not_land_is_held_for_its_own_reason()
    {
        // UNCHANGED. A tree that did not land is already kept for handoff, and
        // this must not quietly become the only reason one is.
        await Assert.That(TreeRetention.MustKeep(landed: false, serving: null)).IsTrue();
    }

    [Test]
    public async Task A_preview_that_could_not_be_served_keeps_nothing()
    {
        // NO ADDRESS MEANS NO SERVER. A connector that would not start reports a
        // diagnosis and no url, and there is nothing running to protect.
        var unserved = new ExposureServed
        {
            Exposure = "jdapp",
            Slot = "01",
            Diagnosis = "cloudflared is not on this machine",
        };

        await Assert.That(TreeRetention.MustKeep(landed: true, unserved)).IsFalse();
    }
}
