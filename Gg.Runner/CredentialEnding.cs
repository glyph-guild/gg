namespace Gg.Runner;

/// <summary>
/// How a runner's credential ended, once a 401 says one has.
/// </summary>
/// <param name="Said">The sentence, for whoever reads the log.</param>
/// <param name="Exit">
/// Zero only for an ordinary ending, so a restart policy and a pool can tell
/// one from a machine that broke.
/// </param>
/// <remarks>
/// <b>Shared by both loops, and that is the point of the file.</b> The flight
/// loop learned this first and the pull point died of the same 401 for another
/// slice — which is the shape <c>MaintainLoop</c>'s own remark had already
/// named once: <i>"the classification and the backoff are TransientFailure's,
/// the same ones RunnerLoop reads. A copy here is what left this loop behind
/// when its twin was fixed."</i>
/// </remarks>
public sealed record CredentialEnding(string Said, int Exit)
{
    /// <summary>The exit code for an ending somebody caused, or one nobody can explain.</summary>
    /// <remarks>
    /// Not 1: a distinct code lets a restart policy and a reader tell "this
    /// machine's credential is gone" from any other failure, without parsing a
    /// sentence.
    /// </remarks>
    public const int Refused = 75;

    /// <summary>
    /// Which of the three things a 401 is.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>PAST ITS OWN EXPIRY IS THE DESIGN WORKING.</b> A member's credential
    /// is deliberately short — <i>"reset is only a boundary if the credential
    /// dies with the container"</i> — so a member reaching the end of one did
    /// exactly what it was built to do. Exiting non-zero there would make a
    /// restart policy fight the design.
    /// </para>
    /// <para>
    /// <b>BEFORE IT, SOMEBODY ACTED.</b> A revocation or a retirement is a
    /// person removing this machine, and that is the case the original rule was
    /// written for. It still leaves loudly, because a tidy silent exit would
    /// retire a runner nobody meant to retire.
    /// </para>
    /// <para>
    /// <b>AND UNKNOWN IS NEITHER</b>, which is <c>ScopeProbe</c>'s rule one
    /// concern over: <i>"unknown is not false."</i> A runner carrying no
    /// recorded expiry — every runner registered before a member existed — must
    /// not have the end of its credential inferred, because inferring "expired"
    /// would make a revocation exit 0 and disappear.
    /// </para>
    /// </remarks>
    public static CredentialEnding For(
        DateTimeOffset? expiresAt, DateTimeOffset now, HttpRequestException refused)
    {
        ArgumentNullException.ThrowIfNull(refused);

        return expiresAt switch
        {
            { } ends when ends <= now => new CredentialEnding(
                "this runner's credential expired at "
              + $"{ends:yyyy-MM-dd HH:mm}Z and a runner token cannot be renewed in flight. "
              + "Nothing is wrong: a machine whose credential is sized to its life has "
              + "reached the end of one.", 0),

            { } ends => new CredentialEnding(
                "this runner's credential was refused although it does not expire until "
              + $"{ends:yyyy-MM-dd HH:mm}Z, so it was revoked or this runner was retired. "
              + "No waiting fixes that.", Refused),

            // NOT GUESSED. Said as what it is - a 401 this runner cannot
            // explain - so a reader is told the platform does not know rather
            // than told a guess.
            null => new CredentialEnding(
                "the control plane refused this runner's credential (401) and this runner "
              + "has no recorded expiry, so whether it ended or was taken away is not "
              + $"known here: {refused.Message}", Refused),
        };
    }
}
