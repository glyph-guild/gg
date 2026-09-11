namespace Gg.Contracts;

/// <summary>
/// The windows an allowance is measured over.
/// </summary>
/// <remarks>
/// <para>
/// <b>Closed, because the only safe answer to an unknown window is to halt.</b>
/// A control plane that met one it did not recognise and carried on would
/// simply not count what that window held — and under-counting is the
/// direction that spends somebody's allowance for them. That is the ordinary
/// member-versus-value rule with a consequence attached.
/// </para>
/// <para>
/// <b>Two, and a third is a real decision rather than a line.</b> A provider
/// may meter a model separately from the rest, and adding that window means
/// every reader learns what it is for; the day it earns its keep, the argument
/// goes here.
/// </para>
/// </remarks>
[VocabularyOf(VocabularyFingerprints.Contract)]
public static class AllowanceWindows
{
    /// <summary>The short rolling one a provider resets most often.</summary>
    public const string Session = "session";

    /// <summary>The long one.</summary>
    public const string Week = "week";

    /// <summary>Every window a reading may name.</summary>
    public static IReadOnlyList<string> All { get; } = [Session, Week];
}

/// <summary>
/// What one window of an allowance holds.
/// </summary>
/// <remarks>
/// <para>
/// <b>Tokens and a ceiling, never a percentage.</b> A fraction computed on the
/// machine would arrive as a number nobody could check; both halves crossing
/// means a reader can see that a ceiling was configured, what it was, and what
/// was counted against it.
/// </para>
/// <para>
/// <b>The ceiling is absent when nobody configured one.</b> Nothing on a
/// machine records a subscription's limits, so the denominator is typed by a
/// person and may simply not be there. Zero would read as plenty left.
/// </para>
/// </remarks>
[PinnedId("96bca7e0-0c37-43d0-8e8a-12accac76640")]
public sealed record AllowanceWindow
{
    /// <summary>One of <see cref="AllowanceWindows"/>.</summary>
    public required string Kind { get; init; }

    /// <summary>Fresh input.</summary>
    public required long InputTokens { get; init; }

    /// <summary>What the model produced, thinking included.</summary>
    public required long OutputTokens { get; init; }

    /// <summary>
    /// Input served from cache. Counted, and deliberately NOT in
    /// <see cref="Tokens"/>.
    /// </summary>
    /// <remarks>
    /// <b>The excluded one crosses because it is the one that most needs to be
    /// visible.</b> A weighting nobody can see is one nobody can correct, and
    /// the provider's real one is not published — so this member is what lets
    /// a reader who learns better recompute without a new contract.
    /// </remarks>
    public required long CacheReadTokens { get; init; }

    /// <summary>Input written to cache.</summary>
    public required long CacheWriteTokens { get; init; }

    /// <summary>
    /// What this window spent, in the same arithmetic a ceiling is set in.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Input, output and cache writes — deliberately not cache reads, and
    /// the exclusion was measured rather than reasoned.</b> Over one week of a
    /// real machine's transcripts, 37,209 assistant messages: cache reads
    /// 99.0% of the raw sum, cache writes 0.8%, output 0.2%, input 0.0%. An
    /// unweighted total therefore measures how much cached context got
    /// re-read, which is the cheapest component a provider bills; the first
    /// version of this reported 757,457% of a plan's ceiling.
    /// </para>
    /// <para>
    /// <b>A stated approximation, and it exists at all because of
    /// <c>AttemptLedger</c>'s rule: one derivation, however many readers.</b> A
    /// window carrying only components would let a console and a scheduler
    /// each compute the number and disagree about who has headroom — and the
    /// disagreement would decide who gets work.
    /// </para>
    /// </remarks>
    public long Tokens => InputTokens + OutputTokens + CacheWriteTokens;

    /// <summary>When the window opened.</summary>
    public required DateTimeOffset Since { get; init; }

    /// <summary>The ceiling somebody configured, or absent when nobody did.</summary>
    public long? Limit { get; init; }
}

/// <summary>
/// A machine saying what the allowance it spends from has left.
/// </summary>
/// <remarks>
/// <para>
/// <b>Its own record and its own prefix, which is <c>PoolAttestation</c>'s
/// argument unchanged.</b> The fact plumbing is lease-welded at four points —
/// the batch carries a generation, the endpoint is lease-scoped, the ship call
/// takes a lease id, and the idempotency key needs a flight id — and a reading
/// of what a machine has spent has no flight.
/// </para>
/// <para>
/// <b>And not on the heartbeat.</b> <see cref="RunnerHeartbeat"/> is liveness
/// only, deliberately, because a runner able to report something about itself
/// can report it while dead. The hazard is real here rather than theoretical:
/// a stale reading still saying <i>plenty left</i> would spend somebody's
/// allowance for them. So <see cref="MeasuredAt"/> crosses with the numbers and
/// the control plane can see how old they are instead of believing them.
/// </para>
/// <para>
/// <b><see cref="Allowance"/> is a NAME and never an account.</b> It is the
/// string a person put in their own configuration file — not an account id, an
/// email, an organisation uuid or a token. Two machines signed in to one
/// subscription say the same name and their spending adds up; nothing checks
/// the claim, and nothing could, because a control plane able to tell two
/// subscriptions apart would be holding something about them.
/// </para>
/// </remarks>
[PinnedId("2ca54ca4-2b1d-4869-9f35-3c37a3105330")]
public sealed record AllowanceReading
{
    /// <summary>Which allowance, by the name its owner chose.</summary>
    public required string Allowance { get; init; }

    /// <summary>When the machine looked. The control plane's staleness check.</summary>
    public required DateTimeOffset MeasuredAt { get; init; }

    /// <summary>One per window.</summary>
    public required IReadOnlyList<AllowanceWindow> Windows { get; init; }

    /// <summary>The diagnosis, or null when there is nothing wrong.</summary>
    public static string? Validate(AllowanceReading reading)
    {
        ArgumentNullException.ThrowIfNull(reading);

        if (string.IsNullOrWhiteSpace(reading.Allowance))
        {
            return "A reading names the allowance it is about. Blank is not unset: a "
                 + "machine with no allowance configured reports nothing at all.";
        }

        if (reading.Windows.Count is 0)
        {
            return "A reading with no windows says a machine looked and found nothing to "
                 + "say, which is not the same as a machine that spent nothing.";
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var window in reading.Windows)
        {
            if (!AllowanceWindows.All.Contains(window.Kind))
            {
                return $"'{window.Kind}' is not a window this contract declares. The only "
                     + "safe answer to an unknown one is to halt: counting it as nothing "
                     + "under-reports what was spent, which is the direction that costs "
                     + "somebody their own allowance.";
            }

            if (!seen.Add(window.Kind))
            {
                return $"'{window.Kind}' appears twice, and a reader adding them would "
                     + "double-count while a reader taking the first would not.";
            }

            if (window.InputTokens < 0 || window.OutputTokens < 0
                || window.CacheReadTokens < 0 || window.CacheWriteTokens < 0)
            {
                return $"'{window.Kind}' reports a negative count, and a window cannot "
                     + "hold fewer tokens than none.";
            }

            if (window.Limit is { } limit && limit <= 0)
            {
                return $"'{window.Kind}' declares a ceiling of {limit}. A ceiling nobody "
                     + "configured is absent; one of zero is a machine that may never "
                     + "work, said by accident.";
            }
        }

        return null;
    }
}

/// <summary>
/// What one allowance has left, and which machines spend from it.
/// </summary>
/// <remarks>
/// <b>Runners, because the console asks the question the other way round.</b>
/// A person looking at a fleet is looking at machines and wants to know what
/// each has left; the reading arrives per allowance, and two machines may share
/// one. Carrying the ids here lets the console join without a second read, and
/// keeps the allowance the thing that is counted.
/// </remarks>
[PinnedId("30a7da45-5771-4adb-96ab-8c055f288dfa")]
public sealed record AllowanceSummary
{
    /// <summary>The name its owner chose.</summary>
    public required string Name { get; init; }

    /// <summary>When the most recent machine to report it looked.</summary>
    public required DateTimeOffset MeasuredAt { get; init; }

    /// <summary>One per window.</summary>
    public required IReadOnlyList<AllowanceWindow> Windows { get; init; }

    /// <summary>The runners that say they spend from it.</summary>
    public required IReadOnlyList<string> Runners { get; init; }
}

/// <summary>Every allowance this tenant's machines have reported.</summary>
[PinnedId("33374987-8ca8-49ac-96f7-aa9b473e2bbb")]
public sealed record AllowanceList
{
    /// <summary>One per allowance, in no promised order.</summary>
    public required IReadOnlyList<AllowanceSummary> Allowances { get; init; }
}
