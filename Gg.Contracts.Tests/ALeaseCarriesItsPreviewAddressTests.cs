using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// A lease carries the address this flight's preview was granted, so the runner
/// serves at a name it was told rather than one it chose.
/// </summary>
/// <remarks>
/// <para>
/// <b>ADR-0027 § 3.</b> The control plane allocates the hostname and the runner
/// never picks one. That is what makes one domain safe across many flights, and
/// it is worth having even for a tenant serving only itself: a runner that could
/// name its own address could name another flight's.
/// </para>
/// <para>
/// <b>It carries no secret, and the type cannot.</b> The slot's credential
/// travels the way every other credential travels — as a
/// <see cref="CredentialReference"/> in the lease's existing list, resolved on
/// the machine that holds it. What is here is the reference's LOCATOR, which is
/// a name; the value never crosses in either direction. The lease's own remark
/// on credentials states the rule, and this member is held to it rather than
/// excused from it.
/// </para>
/// <para>
/// <b>And it is optional, because most flights have no preview.</b> Absent is a
/// flight that was granted none — every flight today — and a required member
/// would refuse every lease a control plane wrote before this existed.
/// </para>
/// </remarks>
public class ALeaseCarriesItsPreviewAddressTests
{
    private static LeasePreview Granted() => new()
    {
        Exposure = "jdapp",
        Slot = 3,
        Hostname = "jdapp-03.goodgrief.dev",
        Credential = "local:exposure/jdapp-03",
    };

    [Test]
    public async Task A_granted_preview_says_where_and_which_slot()
    {
        var preview = Granted();

        await Assert.That(preview.Hostname).IsEqualTo("jdapp-03.goodgrief.dev");
        await Assert.That(preview.Slot).IsEqualTo(3)
            .Because("the slot is what the fact reconciles against the inventory, so an address "
                   + "without it cannot be told from a leak.");
    }

    [Test]
    public async Task An_address_that_is_not_a_host_is_refused()
    {
        await Assert.That(LeasePreview.Validate(Granted() with { Hostname = "" })).IsNotNull();

        await Assert.That(LeasePreview.Validate(Granted() with { Hostname = "https://x.dev/" }))
            .IsNotNull()
            .Because("a hostname is a name, not a URL. Carrying a scheme here would leave two "
                   + "places that decide what a preview's address looks like, and they would "
                   + "disagree the first time either moved.");
    }

    [Test]
    public async Task A_slot_outside_an_inventory_is_refused()
    {
        await Assert.That(LeasePreview.Validate(Granted() with { Slot = 0 })).IsNotNull()
            .Because("slots are one-based ordinals into a declared inventory, so a zero is a "
                   + "grant nothing could have made.");
    }

    [Test]
    public async Task It_names_a_credential_rather_than_carrying_one()
    {
        await Assert.That(LeasePreview.Validate(Granted() with { Credential = "" })).IsNotNull();

        // THE BOUNDARY, ASSERTED OVER THE SHAPE rather than intended. The lease
        // says of its credentials that the type is "incapable of carrying one",
        // and this member is held to the same standard: a locator is a name.
        var members = typeof(LeasePreview).GetProperties().Select(p => p.Name).ToList();

        await Assert.That(members).DoesNotContain("Secret");
        await Assert.That(members).DoesNotContain("Token");
        await Assert.That(members).DoesNotContain("Value")
            .Because("the control plane holds no secret to send, and a member that could carry "
                   + "one is a member somebody will eventually fill.");
    }

    [Test]
    public async Task A_lease_without_one_is_a_flight_that_was_granted_no_preview()
    {
        var members = typeof(LeaseGranted).GetProperties()
            .Single(p => string.Equals(p.Name, "Preview", StringComparison.Ordinal));

        await Assert.That(Nullable.GetUnderlyingType(members.PropertyType) is not null
                       || !members.PropertyType.IsValueType).IsTrue()
            .Because("every flight in the field today was granted none, and a required member "
                   + "would refuse every lease a control plane wrote before this existed.");
    }

    [Test]
    public async Task A_granted_preview_is_not_refused()
    {
        // LIVENESS. Every assertion above asks for a refusal, and a validator
        // that refused everything would satisfy all of them.
        await Assert.That(LeasePreview.Validate(Granted())).IsNull();
    }
}
