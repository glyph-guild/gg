namespace Gg.Local;

/// <summary>
/// What this machine's runner last said it was holding.
/// </summary>
/// <remarks>
/// <b>A fact about the runner, not about updating.</b> It is named and shaped
/// that way on purpose: the runner writes it, and nothing the runner role
/// reaches may name the update path - <c>UpdateBoundaryTests</c> holds that,
/// and it is the boundary keeping a process that runs customer code away from
/// its own executable. What the updater does with this is the updater's
/// business.
/// </remarks>
/// <param name="Present">Whether the marker was there to read at all.</param>
/// <param name="Holding">
/// What it says the runner has, or null for a runner holding nothing.
/// </param>
/// <param name="WrittenAt">When the runner last wrote it.</param>
public sealed record RunnerHolding(bool Present, string? Holding, DateTimeOffset? WrittenAt)
{
    /// <summary>Nothing was there.</summary>
    public static RunnerHolding Absent => new(false, null, null);
}

/// <summary>
/// Whether this is a moment at which a machine may be updated.
/// </summary>
/// <remarks>
/// <para>
/// <b>Fail closed, because the cost is asymmetric.</b> Updating a machine that
/// turns out to be busy abandons a flight somebody is waiting on and loses
/// whatever it had done; declining to update one that was actually idle costs
/// a wait until the next ask. So every uncertain case is a refusal.
/// </para>
/// <para>
/// <b>But no runner is not an uncertain case.</b> A machine with nothing
/// running has no flight to abandon - a laptop, or a host whose unit is
/// stopped - and refusing there would make the safe case the one that cannot
/// be updated. Failing closed applies to not KNOWING, not to nothing being
/// there.
/// </para>
/// <para>
/// <b>A stale marker is a refusal and not an absence.</b> The runner rewrites
/// it as it beats, so one that has stopped moving means the runner stopped
/// moving - which is exactly the state where it may still hold a lease the
/// control plane has not yet taken back.
/// </para>
/// <para>
/// <b>Pure.</b> Reading the file and asking whether a unit is up are the
/// caller's; what they mean is here, where it can be tested.
/// </para>
/// </remarks>
public static class UpdateWindow
{
    /// <summary>
    /// How old a marker may be before it stops being evidence.
    /// </summary>
    /// <remarks>
    /// A runner beats in seconds and rewrites this as it goes, so a minute is
    /// many missed beats rather than one late one - long enough that an
    /// ordinary hiccup does not block an update, short enough that a wedged
    /// runner is not mistaken for an idle one.
    /// </remarks>
    public static readonly TimeSpan MarkerGoesStaleAfter = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Why this machine must not be updated now, or null when it may be.
    /// </summary>
    /// <param name="runnerRunning">Whether a runner unit is up on this machine.</param>
    /// <param name="marker">What the runner last wrote about itself.</param>
    /// <param name="now">The clock, handed in.</param>
    public static string? WhyNot(bool runnerRunning, RunnerHolding marker, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(marker);

        // NOTHING RUNNING IS NOTHING TO ABANDON. Checked before the marker,
        // because a stopped runner leaves a stale one behind and that must not
        // read as "busy" for ever.
        if (!runnerRunning)
        {
            return null;
        }

        if (!marker.Present)
        {
            return "a runner is up on this machine and has not said what it is holding, so "
                 + "whether it is busy cannot be established. Updating would restart it.";
        }

        if (marker.WrittenAt is not { } when)
        {
            return "a runner is up and its marker carries no time, so how old it is cannot "
                 + "be established.";
        }

        var age = now - when;

        if (age > MarkerGoesStaleAfter || age < TimeSpan.Zero)
        {
            return $"a runner is up but last said what it was holding {Said(age)}, which is "
                 + "too long ago to act on. A runner that has stopped writing may still hold "
                 + "a lease nobody has taken back.";
        }

        if (marker.Holding is { Length: > 0 } flight)
        {
            return $"the runner on this machine is working on {flight}. Updating restarts it "
                 + "and abandons what it holds.";
        }

        return null;
    }

    /// <summary>An age, including the one a clock that went backwards gives.</summary>
    private static string Said(TimeSpan age) =>
        age < TimeSpan.Zero
            ? "at a time in the future, which is a clock disagreeing rather than an age"
            : $"{(int)age.TotalSeconds} seconds ago";
}
