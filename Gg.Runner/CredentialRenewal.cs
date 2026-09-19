namespace Gg.Runner;

/// <summary>
/// Asks for more time when this runner's own credential is close to ending, and
/// writes the answer down.
/// </summary>
/// <remarks>
/// <para>
/// <b>One rule for every runner that asks.</b> The maintainer learned it first
/// and the runner that flies never did, so a resident stopped every thirty days
/// with a 401 while the pool host beside it carried on. Both loops hold one of
/// these now, and neither can drift from the other.
/// </para>
/// <para>
/// <b>What stays a person's act is unchanged.</b> The credential that asks is
/// the one being renewed, so a revoked runner cannot, and one that was down
/// past its expiry cannot either - its ask is a 401, which is not a renewal
/// question and is left to whoever called this.
/// </para>
/// </remarks>
internal sealed class CredentialRenewal(
    IRunnerCredential credential,
    IClock clock,
    DateTimeOffset? expiresAt,
    Func<DateTimeOffset, Task>? renewed,
    Action<DateTimeOffset> notExtended)
{
    /// <summary>
    /// How close to its end a credential has to be before this asks.
    /// </summary>
    /// <remarks>
    /// <b>Days rather than minutes, because the ask has to survive a bad
    /// week.</b> A runner that first asked an hour before its expiry would get
    /// one attempt against a control plane that might be mid-deploy; three days
    /// is room for the transient arm to do its work without the window being so
    /// wide that a renewal is really a monthly poll.
    /// </remarks>
    public static readonly TimeSpan Within = TimeSpan.FromDays(3);

    /// <summary>When the credential ends, as last known here - null when never recorded.</summary>
    public DateTimeOffset? ExpiresAt { get; private set; } = expiresAt;

    /// <summary>Whether the control plane has settled that this one may not be renewed.</summary>
    private bool _notRenewable;

    /// <summary>Asks, when due. Unknown is never due.</summary>
    /// <remarks>
    /// <b>A transient failure throws, and the caller already has a backoff for
    /// it</b> - the next turn asks again. A settled refusal is said once and not
    /// asked again, because writing to the control plane on every turn for the
    /// rest of a credential's life is its own outage.
    /// </remarks>
    public async Task RenewIfDueAsync(CancellationToken cancellationToken)
    {
        if (_notRenewable
            || ExpiresAt is not { } ends
            || ends - clock.UtcNow > Within)
        {
            return;
        }

        if (await credential.RenewCredentialAsync(cancellationToken) is not { } answer)
        {
            _notRenewable = true;
            notExtended(ends);
            return;
        }

        ExpiresAt = answer.ExpiresAt;

        if (renewed is not null)
        {
            await renewed(answer.ExpiresAt);
        }
    }
}
