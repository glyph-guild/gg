namespace Gg.Local;

/// <summary>
/// Whether the meter's cache is worth asking the executor to refresh.
/// </summary>
/// <remarks>
/// <para>
/// <b>Measured rather than reasoned about.</b> <c>claude -p "/usage"</c>
/// refreshes <c>cachedUsageUtilization</c> — on the machine this was written
/// on it moved the reading forward eleven hours and the weekly share from 40%
/// to 48%, and on an agent host that had no such key at all it created one.
/// </para>
/// <para>
/// <b>No credential, which is the whole reason this is allowed.</b> gg asks
/// the tool that already holds the subscription's credential and reads the
/// file it writes. That is the guest rule <see cref="AllowanceMeter"/> and
/// <see cref="AllowanceLedger"/> already follow; reading another tool's
/// <c>.credentials.json</c> and calling an API with it would be a different
/// thing entirely, and is what every other route to this number required.
/// </para>
/// <para>
/// <b>ONCE PER ACCOUNT, not once per machine.</b> The share is a property of
/// the PLAN, so one fresh reading answers for every machine spending from it.
/// This type answers only whether a refresh is warranted HERE. Which machine
/// does it for an allowance several of them share is a claim, and a claim is
/// the control plane's to hold — and an allowance with no runner has nobody to
/// make one, which is why a person's own verb refreshes by the same path.
/// </para>
/// <para>
/// <b>Spawning is the caller's, deliberately.</b> This type takes the ask as a
/// delegate so the decision can be tested without a process and so the one
/// place that runs another program is named where the rule about it lives —
/// a UI session may not spawn, and the runner and the command line may.
/// </para>
/// </remarks>
public static class MeterRefresh
{
    /// <summary>
    /// Whether this reading is behind a window it describes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Derived from the meter's own instants, never a threshold somebody
    /// picked.</b> A reading taken before one of its windows reset cannot
    /// account for anything spent since — so the number is behind by an amount
    /// nothing here can estimate, and that is true of the WEEK even when only
    /// the five-hour window rolled.
    /// </para>
    /// <para>
    /// <b>No meter at all is behind too.</b> That is the agent host's case, and
    /// it is the one a refresh most obviously exists for: absent is not
    /// "nothing to refresh".
    /// </para>
    /// </remarks>
    public static bool IsBehind(MeteredShare metered, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(metered);

        if (metered.FetchedAt is not { } fetched)
        {
            return true;
        }

        return metered.Resets.Values.Any(resets => fetched < resets && resets <= now);
    }

    /// <summary>
    /// Refreshes the meter if it is behind, and answers with what to use.
    /// </summary>
    /// <param name="ask">
    /// Runs the executor and answers whether it wrote a fresh cache. Called at
    /// most once, and only when the reading would otherwise be wrong.
    /// </param>
    /// <remarks>
    /// <b>Every failure is silent and keeps the old reading.</b> A machine with
    /// no executor, or one nobody has signed in, still has token counts that
    /// are right — and another tool's absence is never this one's failure.
    /// </remarks>
    public static async Task<MeteredShare> EnsureCurrentAsync(
        MeteredShare metered,
        DateTimeOffset now,
        Func<CancellationToken, Task<bool>> ask,
        Func<MeteredShare>? reread = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(metered);
        ArgumentNullException.ThrowIfNull(ask);

        if (!IsBehind(metered, now))
        {
            return metered;
        }

        try
        {
            if (await ask(cancellationToken) && reread is not null)
            {
                return reread();
            }
        }
        catch (Exception whatever) when (whatever is not OperationCanceledException)
        {
            // DELIBERATELY EVERYTHING. The ask runs another program: it can be
            // missing, unauthenticated, wedged, or a version that does not know
            // the command. None of those is a reason to lose a reading that is
            // otherwise true, and the shapes are too many to name.
        }

        return metered;
    }
}
