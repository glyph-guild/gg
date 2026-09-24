namespace Gg.Contracts;

/// <summary>How an exposure arranges for a served port to be reachable.</summary>
/// <remarks>
/// <b>Closed, and an unknown kind is refused by name.</b> A provider a tenant
/// names but gg cannot drive must fail where the document is written, not on the
/// machine that was going to serve the preview. Opened one member at a time, on
/// <see cref="StrategyKinds"/>' precedent.
/// </remarks>
[VocabularyOf(VocabularyFingerprints.Contract)]
public static class ExposureKinds
{
    /// <summary>A Cloudflare named tunnel, one per slot, dialled out by the runner.</summary>
    public const string CloudflareTunnel = "cloudflare-tunnel";

    public static IReadOnlyList<string> All { get; } = [CloudflareTunnel];
}

/// <summary>
/// Where a flight's served port may appear: an airspace document,
/// <c>airspace/exposures/&lt;name&gt;.yaml</c>, applied and governed like a
/// strategy (ADR-0027).
/// </summary>
/// <remarks>
/// <para>
/// <b>The tenant owns the address.</b> The hostnames are the tenant's domain and
/// the credentials are the tenant's to register, because a preview carries the
/// tenant's unreleased interface and whatever data is on its screen. There is no
/// default exposure and no fallback: a tenant that has declared none cannot
/// publish a preview and is told which document to write. A built-in default
/// pointing at a domain we own would be a hosted service arriving by the back
/// door, and the path everybody takes is the only path that stays correct.
/// </para>
/// <para>
/// <b>It names, and the machine resolves</b>, the same rule a
/// <see cref="FleetProfile"/> follows. A <see cref="Credentials"/> entry is a
/// reference; the secret stays where the tenant put it and never reaches the
/// control plane.
/// </para>
/// <para>
/// <b>The inventory is finite on purpose, and that is not a limitation.</b> An
/// identity provider will not accept a wildcard redirect URI for the audiences a
/// customer-facing application signs in, so an address that can be registered at
/// all has to be one somebody can write down in advance. Declaring eight names
/// once is what makes a preview able to sign a person in; a scheme that invented
/// names per flight could never do so.
/// </para>
/// <para>
/// <b>A slot is granted exclusively, and the failure to do so is silent.</b> A
/// provider may accept a second connector on one slot as a replica and route
/// each request to whichever is nearer, with no error — two flights would serve
/// each other's previews, intermittently. Exclusivity is the control plane's to
/// enforce.
/// </para>
/// </remarks>
[PinnedId("3f6a1c08-9d52-4b71-ae44-0c58d2e7b193")]
public sealed record Exposure
{
    /// <summary>One of <see cref="ExposureKinds"/>.</summary>
    public required string Kind { get; init; }

    /// <summary>How many slots there are, and what each one is called.</summary>
    public required ExposureInventory Inventory { get; init; }

    /// <summary>The name a slot's hostname and credential are built from.</summary>
    /// <remarks>
    /// Substituted into <see cref="ExposureInventory.Hostnames"/> and
    /// <see cref="ExposureInventory.Credentials"/>. Two digits, so that eight
    /// slots sort the way a person reads them and a later ninth does not
    /// reorder the first eight.
    /// </remarks>
    public const string SlotToken = "{slot}";

    /// <summary>
    /// What slot <paramref name="ordinal"/> is called, one-based.
    /// </summary>
    public static string Slot(int ordinal) => ordinal.ToString("00");
}

/// <summary>How many addresses an exposure has, and how each one is spelled.</summary>
/// <remarks>
/// <b>Both patterns must carry <see cref="Exposure.SlotToken"/></b>, because a
/// pattern without it names one address for every slot — which is the replica
/// collision above, written into the document rather than reached by accident.
/// </remarks>
[PinnedId("b70d4e29-1a83-45cc-9f16-6e2a83d0c574")]
public sealed record ExposureInventory
{
    /// <summary>How many slots. At least one.</summary>
    public required int Size { get; init; }

    /// <summary>The hostname pattern, e.g. <c>jdapp-{slot}.example.dev</c>.</summary>
    public required string Hostnames { get; init; }

    /// <summary>The credential reference pattern, e.g. <c>local:exposure/jdapp-{slot}</c>.</summary>
    public required string Credentials { get; init; }
}
