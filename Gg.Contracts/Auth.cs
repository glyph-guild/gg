namespace Gg.Contracts;

/// <summary>
/// Asks the control plane to begin a device authorization. The client sends
/// nothing but its versions, which travel in headers.
/// </summary>
/// <remarks>
/// Deliberately provider-neutral. The client learns a code and a URI to show a
/// human; which identity provider stands behind them is the control plane's
/// business, and changing it must not change this contract.
/// </remarks>
[PinnedId("b0163513-2638-43c6-90d6-41e93d7a73f7")]
public sealed record DeviceAuthorizationRequest
{
    /// <summary>Human-readable label for the device being authorized.</summary>
    public required string DeviceLabel { get; init; }
}

/// <summary>
/// What the human must do, and how the client should wait.
/// </summary>
[PinnedId("a401b483-7060-4a44-9580-18ba234759ea")]
public sealed record DeviceAuthorizationStarted
{
    /// <summary>Opaque handle the client polls with. Not a credential.</summary>
    public required string DeviceCode { get; init; }

    /// <summary>Short code the human types. Displayed, never stored.</summary>
    public required string UserCode { get; init; }

    /// <summary>Where the human goes to enter the code.</summary>
    public required string VerificationUri { get; init; }

    /// <summary>
    /// Seconds the client must wait between polls. Server-supplied: the client
    /// respects it rather than inventing a cadence.
    /// </summary>
    public required int PollIntervalSeconds { get; init; }

    /// <summary>When this authorization attempt stops being pollable.</summary>
    public required DateTimeOffset ExpiresAt { get; init; }
}

/// <summary>Polls for completion of a device authorization.</summary>
[PinnedId("91de01c2-aea3-49b4-b217-8f08b7792b10")]
public sealed record DeviceTokenRequest
{
    /// <summary>The handle returned when the authorization started.</summary>
    public required string DeviceCode { get; init; }
}

/// <summary>
/// A session the client may act with.
/// </summary>
/// <remarks>
/// This is OUR session, not any provider's credential. Nothing a provider
/// issued reaches the client, and nothing the client holds can be replayed
/// against a provider.
/// </remarks>
[PinnedId("4d822aec-0692-4a16-8fed-3c7419359cb3")]
public sealed record SessionIssued
{
    public required string SessionToken { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }

    /// <summary>Display label of the principal this session acts as.</summary>
    public required string PrincipalDisplay { get; init; }

    /// <summary>Tenant the principal belongs to.</summary>
    public required string TenantId { get; init; }
}

/// <summary>Who the caller currently is.</summary>
[PinnedId("7783ba8d-38e7-4009-bd05-a2cf9c0d8224")]
public sealed record WhoAmI
{
    public required string PrincipalId { get; init; }

    public required string PrincipalDisplay { get; init; }

    public required string TenantId { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }
}
