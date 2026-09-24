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
    /// <b>No port, and that is a property of how the tunnel is managed.</b> Which
    /// local service the slot reaches is part of the tunnel's own configuration
    /// at the provider, set by the tenant when they bound the hostname. So the
    /// runner supplies a credential and nothing else - it cannot point a slot at
    /// a different service any more than it can rename one.
    /// </remarks>
    Task<string?> RunAsync(string token, CancellationToken cancellationToken);
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

        var refusal = await _connector.RunAsync(request.Secret, cancellationToken);

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
                Exposure = request.Preview.Exposure,
                Slot = slot,
            };
    }
}
