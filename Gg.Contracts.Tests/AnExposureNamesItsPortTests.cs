namespace Gg.Contracts.Tests;

/// <summary>
/// An exposure may say which local port a slot reaches, and a granted slot
/// carries it.
/// </summary>
/// <remarks>
/// <para>
/// <b>This reverses ADR-0027 § 5, and the measurement is why.</b> That section
/// said the runner supplies a credential and nothing else, because which local
/// service a slot reaches is the tunnel's own configuration at the provider. It
/// was written from the documentation. Measured on a real tunnel on
/// 2026-09-24: passing <c>--url http://127.0.0.1:8080</c> alongside the token
/// OVERRIDES the remotely-managed ingress, and the address served the runner's
/// own page with a 200.
/// </para>
/// <para>
/// <b>What that buys is the removal of a coupling nobody could see.</b> Without
/// it, every hostname's origin has to be configured in the provider's dashboard
/// to the same port the work kind tells an agent to serve on — two places that
/// agree until somebody moves one, and the failure is a preview that answers
/// 502 with nothing in either document explaining why. The port said once, in
/// the document that already describes how a served port becomes reachable, is
/// the whole of the fix.
/// </para>
/// <para>
/// <b>What it costs is a sentence of ADR-0027 that was already weaker than it
/// looked.</b> A runner that can serve content can serve whatever it likes on
/// whatever port; the protection that matters — that it cannot take another
/// flight's address — is the token's, and that is untouched.
/// </para>
/// <para>
/// <b>Nullable, because every document written before this declares none.</b>
/// Absent means the provider's own ingress decides, which is exactly the
/// behaviour those documents have today.
/// </para>
/// </remarks>
public class AnExposureNamesItsPortTests
{
    private static Exposure With(int? port) => new()
    {
        Kind = ExposureKinds.CloudflareTunnel,
        Inventory = new ExposureInventory
        {
            Size = 8,
            Hostnames = "jdapp-{slot}.goodgrief.dev",
            Credentials = "keyvault://ggdev.vault.example/jdapp-{slot}",
            Port = port,
        },
    };

    [Test]
    public async Task An_exposure_may_name_the_port_its_slots_reach()
    {
        await Assert.That(Exposure.Validate(With(8080))).IsNull();
    }

    [Test]
    public async Task An_exposure_that_names_none_is_still_valid()
    {
        await Assert.That(Exposure.Validate(With(null))).IsNull()
            .Because("every document written before this declares none, and absent means the "
                   + "provider's own ingress decides - which is what those documents do today.");
    }

    [Test]
    public async Task A_port_outside_the_range_is_refused()
    {
        await Assert.That(Exposure.Validate(With(0))).IsNotNull();
        await Assert.That(Exposure.Validate(With(70000))).IsNotNull()
            .Because("a number no socket can bind is a preview that fails on the machine "
                   + "serving it, hours after the document was written.");
    }

    [Test]
    public async Task A_granted_slot_carries_the_port()
    {
        var granted = new LeasePreview
        {
            Exposure = "jdapp",
            Slot = 1,
            Hostname = "jdapp-01.goodgrief.dev",
            Credential = "keyvault://ggdev.vault.example/jdapp-01",
            Port = 8080,
        };

        await Assert.That(LeasePreview.Validate(granted)).IsNull();
        await Assert.That(granted.Port).IsEqualTo(8080)
            .Because("the runner dials with it, and a port that stopped at the document would "
                   + "leave the provider's ingress deciding after all.");
    }

    [Test]
    public async Task A_granted_slot_without_a_port_is_still_valid()
    {
        var granted = new LeasePreview
        {
            Exposure = "jdapp",
            Slot = 1,
            Hostname = "jdapp-01.goodgrief.dev",
            Credential = "local:exposure/jdapp-01",
        };

        await Assert.That(LeasePreview.Validate(granted)).IsNull();
        await Assert.That(granted.Port).IsNull();
    }
}
