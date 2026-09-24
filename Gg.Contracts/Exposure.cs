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

    /// <summary>
    /// A pattern spelled for one slot: <paramref name="pattern"/> with
    /// <see cref="SlotToken"/> replaced by slot <paramref name="ordinal"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The one place a slot's names are spelled.</b> Three places touch them
    /// and they are not in the same process, the same repository, or the same
    /// week: the control plane grants a hostname and a credential locator, gg
    /// writes a secret under that locator on another machine, and the runner
    /// dials with it. <c>CredentialLocator</c> already says why that has to be
    /// one method - two derivations that agree today is how a runner ends up
    /// hunting for a file the CLI never wrote.
    /// </para>
    /// <para>
    /// <b>The pattern is the tenant's, and nothing here invents one.</b> It
    /// comes off the exposure document, which the tenant may edit; all this
    /// does is put the slot where the document said to.
    /// </para>
    /// <para>
    /// <b>A pattern with no slot in it throws rather than returning.</b> One
    /// name for every slot is the replica collision, and it is silent: the
    /// provider takes a second connector on one tunnel and serves each request
    /// from whichever is nearer, with no error anywhere. <see cref="Validate"/>
    /// refuses such a document at apply, so arriving here without a token is
    /// already impossible — and a method that quietly returned the pattern
    /// would make "impossible" the only thing standing between two flights and
    /// each other's screens.
    /// </para>
    /// </remarks>
    public static string Spelled(string pattern, int ordinal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        ArgumentOutOfRangeException.ThrowIfLessThan(ordinal, 1);

        if (!pattern.Contains(SlotToken, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"'{pattern}' has no {SlotToken} in it, so it is one name for every slot - and "
              + "two slots that spell the same name are two flights serving each other's work.",
                nameof(pattern));
        }

        return pattern.Replace(SlotToken, Slot(ordinal), StringComparison.Ordinal);
    }

    /// <summary>
    /// What is wrong with this exposure, or null when nothing is.
    /// </summary>
    /// <remarks>
    /// <b>A refusal names what IS available</b>, because one that does not leaves
    /// somebody guessing at a vocabulary they cannot see.
    /// </remarks>
    public static string? Validate(Exposure exposure)
    {
        ArgumentNullException.ThrowIfNull(exposure);

        if (!ExposureKinds.All.Contains(exposure.Kind, StringComparer.Ordinal))
        {
            return $"'{exposure.Kind}' is not a way gg can arrange an exposure. It can arrange "
                 + $"{string.Join(" and ", ExposureKinds.All)}.";
        }

        if (exposure.Inventory.Size < 1)
        {
            return "This exposure has no slots, so it declares a place where nothing can "
                 + "appear - which is the same as declaring none, and harder to notice.";
        }

        // ONE ADDRESS FOR EVERY SLOT IS THE REPLICA COLLISION, WRITTEN DOWN. A
        // provider accepts a second connector on one tunnel and routes each
        // request to whichever is nearer, with no error - so two flights holding
        // two slots that spell the same name serve each other's previews.
        foreach (var (pattern, key) in (( string Pattern, string Key )[])
                 [
                     (exposure.Inventory.Hostnames, "inventory.hostnames"),
                     (exposure.Inventory.Credentials, "inventory.credentials"),
                 ])
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                return $"{key} says nothing, so no slot has one.";
            }

            if (!pattern.Contains(SlotToken, StringComparison.Ordinal))
            {
                return $"{key} is '{pattern}', which is one value for every slot. Put "
                     + $"{SlotToken} in it so each slot has its own - two slots that spell the "
                     + "same name are two flights serving each other's work.";
            }
        }

        return null;
    }

    /// <summary>
    /// Which field widens, and why, or null when the change only ever removes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Almost every edit here widens, and that is the honest answer rather
    /// than a cautious one.</b> Growing the inventory adds addresses. Moving the
    /// hostname pattern moves every address at once, to names nobody has
    /// registered with any identity provider. Moving the credential pattern
    /// points every slot at secrets nobody has placed. Changing the kind changes
    /// what is dialled. None can be shown to reduce anything.
    /// </para>
    /// <para>
    /// <b>Shrinking the inventory is the one edit that only removes</b>, so it
    /// tightens - and it has to, because a tightening a tenant must ask
    /// permission for is a tenant who stops tightening.
    /// </para>
    /// </remarks>
    public static (string Field, string Because)? Widening(Exposure prior, Exposure proposed)
    {
        ArgumentNullException.ThrowIfNull(prior);
        ArgumentNullException.ThrowIfNull(proposed);

        if (!string.Equals(prior.Kind, proposed.Kind, StringComparison.Ordinal))
        {
            return ("kind",
                $"it stops arranging '{prior.Kind}' and starts arranging '{proposed.Kind}', "
              + "which is a different thing dialled from every runner that serves a preview.");
        }

        if (proposed.Inventory.Size > prior.Inventory.Size)
        {
            return ("inventory.size",
                $"it goes from {prior.Inventory.Size} addresses to {proposed.Inventory.Size}, "
              + "and the new ones are reachable the moment a flight is granted one.");
        }

        if (!string.Equals(
                prior.Inventory.Hostnames, proposed.Inventory.Hostnames, StringComparison.Ordinal))
        {
            return ("inventory.hostnames",
                $"it moves every address from '{prior.Inventory.Hostnames}' to "
              + $"'{proposed.Inventory.Hostnames}' at once, and no identity provider has been "
              + "told to expect the new ones.");
        }

        if (!string.Equals(
                prior.Inventory.Credentials,
                proposed.Inventory.Credentials,
                StringComparison.Ordinal))
        {
            return ("inventory.credentials",
                $"it points every slot at '{proposed.Inventory.Credentials}' instead of "
              + $"'{prior.Inventory.Credentials}', which is a set of secrets somebody has to "
              + "have placed for any preview to appear at all.");
        }

        return null;
    }
}

/// <summary>One applied exposure, as the read side serves it.</summary>
[PinnedId("7c41b9d6-2e08-4a35-8f6d-51b3ca07e2f9")]
public sealed record ExposureState
{
    /// <summary>The topology name the exposure was applied to.</summary>
    public required string Name { get; init; }

    /// <summary>The per-name version in force, e.g. v2.</summary>
    public required string Version { get; init; }

    public required DateTimeOffset AppliedAt { get; init; }

    public required Exposure Exposure { get; init; }
}

/// <summary>Every exposure in force for the tenant.</summary>
[PinnedId("0d58e3a7-b16c-4927-9e4f-8a2704cd15b3")]
public sealed record ExposureList
{
    public required IReadOnlyList<ExposureState> Exposures { get; init; }
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

/// <summary>
/// Where a flight's served port was published: the address a person opens.
/// </summary>
/// <remarks>
/// <para>
/// <b>The deliverable of a preview, recorded as a fact rather than described in
/// prose.</b> A gate that asks somebody to look at a preview has to be able to
/// tell them where it is, and a summary cannot carry that: every reader
/// truncates one.
/// </para>
/// <para>
/// <b>It names the exposure and the slot as well as the address</b>, because an
/// address alone cannot be reconciled against an inventory. With them, a reader
/// can tell a grant still held from one already released — which is the question
/// asked when a preview stops answering and nobody knows whether the slot is
/// free.
/// </para>
/// <para>
/// <b>No credential, and no port.</b> The secret that dialled the tunnel stays on
/// the machine holding it, and the local port a server happened to bind is not a
/// fact about anything a person can reach.
/// </para>
/// </remarks>
[FactKind(FactKinds.PreviewUrl)]
[PinnedId("9e2c7b40-53f1-4a86-b0d9-1c48f6a2e735")]
public sealed record PreviewUrl
{
    /// <summary>The address, absolute and https.</summary>
    public required string Url { get; init; }

    /// <summary>The exposure whose inventory the address came from.</summary>
    public required string Exposure { get; init; }

    /// <summary>Which slot of that inventory was granted.</summary>
    public required string Slot { get; init; }

    /// <summary>The diagnosis, or null when there is nothing wrong.</summary>
    public static string? Validate(PreviewUrl preview)
    {
        ArgumentNullException.ThrowIfNull(preview);

        if (!Uri.TryCreate(preview.Url, UriKind.Absolute, out var address))
        {
            return "A preview's address is absolute, because a person is going to open it. "
                 + $"'{preview.Url}' is not, and a browser handed one reads it as a search.";
        }

        if (!string.Equals(address.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal))
        {
            return $"A preview is served over https and '{preview.Url}' is {address.Scheme}. It "
                 + "carries the tenant's unreleased interface and whatever data is on its "
                 + "screen, and every way gg can arrange an exposure serves https.";
        }

        if (string.IsNullOrWhiteSpace(preview.Exposure))
        {
            return "A preview names the exposure its address came from.";
        }

        return string.IsNullOrWhiteSpace(preview.Slot)
            ? "A preview names the slot it was granted. An address with no slot behind it "
            + "cannot be reconciled against an inventory, so nothing could tell a grant still "
            + "held from one already released."
            : null;
    }
}
