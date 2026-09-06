namespace Gg.Console;

/// <summary>
/// A device authorization somebody has been asked to approve.
/// </summary>
/// <remarks>
/// <para>
/// <b>Three members, and the fourth one is deliberately missing.</b> The device
/// code — the opaque handle the client polls with — is not here and must not
/// be. Whoever holds it polls once the authorization is approved and is handed
/// the session token, which makes it a bearer capability exactly like an
/// invitation link. <see cref="AppState"/> is source-generated JSON written to
/// disk under <c>GG_STATE_DUMP</c> and fed to the diagnostics bundle, so a
/// handle in here is a handle in both.
/// </para>
/// <para>
/// It lives on the <see cref="ISignInSession"/> instead, which the composition
/// root owns outside every UI lifetime — the same place the live tails and the
/// process handles already are, and for the same reason: the model holds what
/// a person reads.
/// </para>
/// <para>
/// <b>What IS here is what a person types and where they type it.</b> The code
/// is shown on a screen by design; it authorizes this machine and nothing else,
/// and it is worthless to anybody who cannot also sign in as the person
/// approving it.
/// </para>
/// </remarks>
public sealed record PendingSignIn
{
    /// <summary>The short code a person types where they are sent.</summary>
    public required string UserCode { get; init; }

    /// <summary>Where they go to type it.</summary>
    public required string VerificationUri { get; init; }

    /// <summary>
    /// When this stops being approvable.
    /// </summary>
    /// <remarks>
    /// Drawn beside the code rather than kept for a timer. A code with no
    /// expiry on the screen is one somebody comes back to after lunch and
    /// concludes is broken.
    /// </remarks>
    public required DateTimeOffset ExpiresAt { get; init; }
}
