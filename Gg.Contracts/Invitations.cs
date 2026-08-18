namespace Gg.Contracts;

/// <summary>
/// Asks the control plane to invite somebody into the caller's tenant. There is
/// nothing in the body, and that is the contract.
/// </summary>
/// <remarks>
/// <para>
/// <b>No email, no name, no tenant.</b> An invitation names nobody: it is a
/// capability, and whoever holds the link becomes a principal in the tenant the
/// caller was in. Carrying an address here would imply the control plane
/// delivers it, which it does not — the person who ran the verb does that, by
/// whatever means they already trust.
/// </para>
/// <para>
/// The tenant comes from the session, never from this request. A caller may be
/// a tenant; it may not name one.
/// </para>
/// </remarks>
[PinnedId("36ca218d-2d1c-4197-aae3-e5139342e1df")]
public sealed record InvitationRequest;

/// <summary>
/// The link to hand to the person being invited, and when it stops working.
/// </summary>
/// <remarks>
/// <b>The URL is built by the control plane and echoed verbatim.</b> Where the
/// web surface lives is deployment knowledge; a client that composed this from a
/// base address would be guessing, and would guess wrong the first time somebody
/// deploys it anywhere but a laptop. Exactly the shape
/// <c>DeviceAuthorizationStarted.VerificationUri</c> already uses.
/// </remarks>
[PinnedId("121f23d1-d907-45bb-a3ec-ee6e70caf1ea")]
public sealed record InvitationIssued
{
    /// <summary>Where the invited person goes. Carries the capability.</summary>
    public required string InvitationUrl { get; init; }

    /// <summary>When the link stops working.</summary>
    public required DateTimeOffset ExpiresAt { get; init; }
}
