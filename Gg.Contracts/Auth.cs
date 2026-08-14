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

/// <summary>The codes a notice can carry. Neutral about who the provider is.</summary>
/// <remarks>
/// A code named for a forge would put a forge's name in <c>gg</c>, which is
/// the one thing this tool does not contain. The code says WHICH CAPABILITY
/// is degraded; the sentence saying whose and what to do about it is composed
/// by the control plane, which is allowed to know.
/// </remarks>
[VocabularyOf(VocabularyFingerprints.Contract)]
public static class TenantNoticeCodes
{
    /// <summary>
    /// Reporting a flight's result back to where it came from is not working.
    /// </summary>
    /// <remarks>
    /// The degradation nobody on this machine can detect. Observation is
    /// unaffected - a runner clones with the customer's own credential and
    /// never needed the control plane's - so flights keep running, keep
    /// recording facts and keep landing. Only the last mile is gone, and
    /// nothing about a pull request with no check on it says why.
    /// </remarks>
    public const string Egress = "egress";

    public static IReadOnlyList<string> All { get; } = [Egress];
}

/// <summary>
/// Something degraded that the tenant should be told about.
/// </summary>
/// <remarks>
/// One shape, three renderers: <c>gg doctor</c> turns it into a check, the
/// console puts it on the queue row, and the tenant page shows it as a banner.
/// Written once on the control plane, so the three cannot drift into saying
/// different things about the same fault.
/// </remarks>
[PinnedId("bd41f0c6-9e73-4a58-b2d1-6c0e57a839f4")]
public sealed record TenantNotice
{
    /// <summary>One of <see cref="TenantNoticeCodes"/>.</summary>
    public required string Code { get; init; }

    /// <summary>What is wrong, as a sentence somebody can read.</summary>
    public required string Detail { get; init; }

    /// <summary>
    /// What to do about it, or null when there is nothing the reader can do.
    /// </summary>
    /// <remarks>
    /// Null rather than a placeholder. "Contact support" is worse than
    /// silence: following it costs somebody an hour and changes nothing.
    /// </remarks>
    public string? Remedy { get; init; }

    /// <summary>
    /// Whether this should stop a build rather than warn.
    /// </summary>
    /// <remarks>
    /// Decided by the control plane, never upgraded by a reader. A tool that
    /// promoted advisories to failures would make every notice a broken build
    /// and teach people to pass <c>--ignore</c>.
    /// </remarks>
    public required bool Blocking { get; init; }
}

/// <summary>Who the caller currently is.</summary>
[PinnedId("7783ba8d-38e7-4009-bd05-a2cf9c0d8224")]
public sealed record WhoAmI
{
    public required string PrincipalId { get; init; }

    public required string PrincipalDisplay { get; init; }

    public required string TenantId { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }

    /// <summary>
    /// Degradations this tenant should see. Empty when there are none.
    /// </summary>
    /// <remarks>
    /// Empty rather than null, and not <c>required</c>: a control plane too
    /// old to send this omits the member, and the default has to mean "nothing
    /// to report" rather than throw on a response that was valid when it was
    /// written.
    /// </remarks>
    public IReadOnlyList<TenantNotice> Notices { get; init; } = [];
}
