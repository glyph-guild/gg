using Gg.Contracts;

namespace Gg.Runner.Exposures;

/// <summary>What serving one flight's preview needs.</summary>
public sealed record ExposureRequest
{
    /// <summary>The slot the control plane granted, off the lease.</summary>
    public required LeasePreview Preview { get; init; }

    /// <summary>
    /// That slot's credential, resolved on this machine.
    /// </summary>
    /// <remarks>
    /// <b>Resolved here and nowhere else.</b> The lease carries a locator; the
    /// value is found by the machine that holds it and never crosses in either
    /// direction, which is the same boundary every other credential observes.
    /// </remarks>
    public required string Secret { get; init; }

}

/// <summary>What came of trying to serve one.</summary>
/// <remarks>
/// <b>A refusal is reported rather than thrown.</b> A preview that cannot be
/// served is a flight that still did its work; the gate should say the preview
/// is gone rather than the flight failing, which is the same rule as a member
/// that reset under one.
/// </remarks>
public sealed record ExposureServed
{
    /// <summary>The address, or null when it could not be served.</summary>
    public string? Url { get; init; }

    /// <summary>The exposure whose inventory the slot came from.</summary>
    public required string Exposure { get; init; }

    /// <summary>The slot, spelled as the inventory spells it.</summary>
    public required string Slot { get; init; }

    /// <summary>
    /// The origin the connector dials, or null when nothing was served.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The one address a person reaches through <see cref="Url"/>, and the
    /// only one safe to measure.</b> Nothing in gg declares a port for a flight:
    /// <c>ExposureInventory.Port</c>, <c>LeasePreview.Port</c> and the
    /// <c>PREVIEW_PORT</c> variable are three unconnected answers, and an
    /// envelope settles that a flight may serve a port but cannot be FOR one.
    /// Probing a declared port measured traefik on ADR-0033's topology, where
    /// the kind said 8080 and the app bound 4200.
    /// </para>
    /// <para>
    /// <b>Built from the value handed to the connector</b>, which is the same
    /// number <c>TunnelFiles</c> writes as <c>service: http://localhost:{port}</c>.
    /// Two spellings of one address is a preview measured where nobody is
    /// serving.
    /// </para>
    /// </remarks>
    public string? Origin { get; init; }

    /// <summary>What went wrong, or null when nothing did.</summary>
    public string? Diagnosis { get; init; }
}

/// <summary>Runs a provider's connector. The one thing that touches it.</summary>
/// <remarks>
/// <b>Separated from the adapter so the adapter is testable without a
/// provider.</b> Everything interesting about serving a preview — which address
/// is reported, which credential is used, what a refusal does — is decided by
/// the adapter, and none of it should need a tunnel to assert.
/// </remarks>
public interface IExposureConnector
{
    /// <summary>
    /// Runs the connector for one slot, and returns a diagnosis or null when it
    /// started.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The port the tenant's document named, or null to let the provider's
    /// own ingress decide.</b> ADR-0027 § 5 said there could be no port here,
    /// because which local service a slot reaches is the tunnel's configuration
    /// at the provider. Measured on a real tunnel: <c>--url</c> alongside the
    /// token overrides that, so the document can say it once instead of the
    /// same number living in a dashboard for every hostname.
    /// </para>
    /// <para>
    /// <b>It still cannot take another flight's address.</b> That is the
    /// token's to prevent, and naming a local port does not touch it.
    /// </para>
    /// </remarks>
    Task<string?> RunAsync(
        string secret, string hostname, int? port, CancellationToken cancellationToken);
}

/// <summary>Serves a flight's preview at the slot it was granted.</summary>
/// <remarks>
/// <para>
/// <b>One port, on <c>IDestinationAdapter</c>'s precedent</b>, which the runner
/// rule already states for writing: it lives behind one interface and nowhere
/// else. Serving is the same shape — an outward act, arranged on this machine,
/// with the credential resolved here.
/// </para>
/// <para>
/// <b>It reports the address it was GIVEN.</b> A granted slot already has a
/// name. Reading one back out of a connector's own output would be the runner
/// choosing again, and the two could differ with nothing noticing — which is
/// exactly how GG-268 came to serve a preview at an address no identity
/// provider had ever been told about.
/// </para>
/// <para>
/// <b>It holds no provider API access.</b> The whole interaction is running a
/// connector with one slot's credential: it cannot create a hostname, move one,
/// or see another slot. The most a stolen slot credential can do is serve
/// content at the one address it was minted for, which the tenant revokes by
/// rotating that one credential.
/// </para>
/// </remarks>
public sealed class CloudflareExposureAdapter(IExposureConnector connector)
{
    private readonly IExposureConnector _connector = connector;

    /// <summary>The exposure kind this adapter answers for.</summary>
    public static string Kind => ExposureKinds.CloudflareTunnel;

    /// <summary>Dials the slot and says where it is answering.</summary>
    public async Task<ExposureServed> ServeAsync(
        ExposureRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var slot = Exposure.Slot(request.Preview.Slot);

        if (LeasePreview.Validate(request.Preview) is { } invalid)
        {
            return new ExposureServed
            {
                Exposure = request.Preview.Exposure,
                Slot = slot,
                Diagnosis = invalid,
            };
        }

        var refusal = await _connector.RunAsync(
            request.Secret, request.Preview.Hostname, request.Preview.Port, cancellationToken);

        return refusal is { Length: > 0 }
            ? new ExposureServed
            {
                Exposure = request.Preview.Exposure,
                Slot = slot,
                Diagnosis = refusal,
            }
            : new ExposureServed
            {
                // BUILT FROM THE GRANT, never read back. https because every way
                // gg can arrange an exposure serves one, and because a scheme
                // decided in two places is a scheme that disagrees with itself.
                Url = $"https://{request.Preview.Hostname}",

                // AND WHERE THAT ADDRESS SENDS TRAFFIC, from the same port the
                // connector was just dialled with. Carried because it is
                // knowable here and nowhere afterwards, and because the only
                // honest way to ask "is the preview serving" is to ask the
                // address the preview actually forwards to.
                Origin = $"http://localhost:{request.Preview.Port}",
                Exposure = request.Preview.Exposure,
                Slot = slot,
            };
    }
}
