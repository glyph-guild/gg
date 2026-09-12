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

    /// <summary>
    /// The long one — the last seven days, which is probably not the
    /// provider's week.
    /// </summary>
    /// <remarks>
    /// <b>A known difference, recorded rather than hidden.</b> A subscription's
    /// weekly limit resets at a fixed time, and nothing on a machine can
    /// discover when that is; this window is simply the last seven days. So it
    /// keeps counting spending that a real reset would have cleared, which
    /// means it reports LESS headroom than there is. That is the safe
    /// direction: a rule built on it stops fleet work slightly early rather
    /// than slightly late, and stopping early costs a flight where stopping
    /// late costs somebody their own allowance.
    /// </remarks>
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

    /// <summary>
    /// What the provider's own meter says this window has spent, 0 to 1.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Read from the provider, never divided here — which is why it is not
    /// a fraction of <see cref="Limit"/>.</b> Nothing on a machine can
    /// discover a plan's token ceiling, so <see cref="Limit"/> is a number
    /// somebody typed. This one is the meter's own statement, it needs no
    /// ceiling, and it is right on a machine where nobody configured one.
    /// </para>
    /// <para>
    /// <b>It describes a DIFFERENT SPAN from the counts beside it, and that is
    /// the reason it is read rather than back-computed.</b> The meter's window
    /// is fixed and ends at <see cref="ResetsAt"/>; the counts are summed over
    /// a rolling window opening at <see cref="Since"/>. Dividing measured
    /// tokens by a reported percentage would put those two spans over each
    /// other and produce a ceiling that looks plausible and is not.
    /// </para>
    /// <para>
    /// <b>Absent means the meter said nothing</b>, which is every machine not
    /// running an executor that keeps one — not nought percent, for the reason
    /// <see cref="Limit"/> is nullable: a zero reads as a plan nobody has
    /// touched.
    /// </para>
    /// </remarks>
    public double? Reported { get; init; }

    /// <summary>
    /// When the meter's own window resets. Absent when it said nothing.
    /// </summary>
    /// <remarks>
    /// <b>Carried so a reader can tell the two windows apart.</b> A share and
    /// a token count on one line look like one measurement; this is what says
    /// they are two, and it is the only thing that does.
    /// </remarks>
    public DateTimeOffset? ResetsAt { get; init; }

    /// <summary>
    /// When the meter was last asked. Absent when it said nothing.
    /// </summary>
    /// <remarks>
    /// <b>A share is only as current as the last time the executor asked</b>,
    /// and nothing else on this record says so. <see cref="ResetsAt"/> catches
    /// the unambiguous case — a window that has already ended — but a weekly
    /// number can be hours old against a reset three days out, and an
    /// hours-old share presented bare cannot be told from a live one.
    /// </remarks>
    public DateTimeOffset? ReportedAt { get; init; }

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

    /// <summary>
    /// The principals whose machines report it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Derived, never declared.</b> A machine is registered by a person, so
    /// the people behind an allowance are the registrants of the machines
    /// reporting it. Nobody claims an allowance and nothing has to be
    /// transferred when a machine changes hands.
    /// </para>
    /// <para>
    /// <b>A set, because lending is a thing several people can do to one
    /// subscription</b> — and because any of them may set the floor. A single
    /// owner would make the second person to plug a machine in unable to
    /// protect the thing they are lending.
    /// </para>
    /// <para>
    /// <b>Absent on a control plane that predates it</b>, which reads as
    /// nobody's: a console showing "mine" would show none rather than
    /// everybody's.
    /// </para>
    /// </remarks>
    public IReadOnlyList<string> Owners
    {
        get => field ?? [];
        init;
    } = [];

    /// <summary>What its owners keep back, or absent when nobody set one.</summary>
    public AllowanceFloor? Floor { get; init; }

    /// <summary>An administrator spending that floor, or absent.</summary>
    public AllowanceOverride? Override { get; init; }
}

/// <summary>Every allowance this tenant's machines have reported.</summary>
[PinnedId("33374987-8ca8-49ac-96f7-aa9b473e2bbb")]
public sealed record AllowanceList
{
    /// <summary>One per allowance, in no promised order.</summary>
    public required IReadOnlyList<AllowanceSummary> Allowances { get; init; }
}

/// <summary>
/// How an allowance decides which machine gets work.
/// </summary>
/// <remarks>
/// <b>Closed, because an unknown strategy falling back to
/// <see cref="Any"/> is a knob somebody believes they turned.</b> Two values,
/// and a third is a real decision rather than a line.
/// </remarks>
[VocabularyOf(VocabularyFingerprints.Contract)]
public static class AllowanceTargeting
{
    /// <summary>
    /// Whichever machine asks first. The behaviour before this existed.
    /// </summary>
    /// <remarks>
    /// <b>And the honest name for "random".</b> Dispatch is a pull: a runner
    /// asks and the matcher hands over whatever is ready, so the machine that
    /// gets the work is whichever transaction wins a <c>SKIP LOCKED</c> race.
    /// Declaring a <c>random</c> strategy beside this one would be two names
    /// for one behaviour.
    /// </remarks>
    public const string Any = "any";

    /// <summary>
    /// The machine whose allowance has the most left gets first refusal.
    /// </summary>
    /// <remarks>
    /// <b>First refusal, not a veto, and the bound is the load-bearing
    /// part.</b> A pull cannot select, so a higher-spent machine is held back
    /// for a bounded window and then allowed to claim anyway. Without the
    /// bound a flight starves whenever the least-spent machine simply is not
    /// asking - which is most of the time, because a machine only asks when it
    /// is free.
    /// </remarks>
    public const string LeastSpent = "least-spent";

    /// <summary>Every strategy a work kind may name.</summary>
    public static IReadOnlyList<string> All { get; } = [Any, LeastSpent];
}

/// <summary>
/// The share of an allowance its owner keeps for themselves.
/// </summary>
/// <remarks>
/// <para>
/// <b>A FRACTION, NOT A TOKEN COUNT.</b> The ceiling lives on the machines
/// that report, and a person setting this means <i>keep a third of it for
/// me</i> — which stays true when their plan changes and a token count does
/// not.
/// </para>
/// <para>
/// <b>Per window, and either may be absent.</b> Keeping a third of the week
/// and nothing of the session is a coherent thing to want: the session refills
/// in hours and the week does not.
/// </para>
/// <para>
/// <b>A floor with no ceiling to measure against is UNENFORCEABLE, and the
/// answer is to stop rather than to guess.</b> Nothing on a machine discovers
/// a subscription's limits, so a person can set a floor on an allowance whose
/// machines were never told a ceiling. Fleet work then stops on that
/// allowance, loudly, with the reason readable — because a floor that silently
/// does not protect is the one outcome a hard stop exists to prevent, and the
/// fix is one line of configuration.
/// </para>
/// </remarks>
[PinnedId("1ae9b291-056a-4c57-ba77-8d6b1970a5fd")]
public sealed record AllowanceFloor
{
    /// <summary>The share of the session window kept back, or absent.</summary>
    public double? SessionFraction { get; init; }

    /// <summary>The share of the week kept back, or absent.</summary>
    public double? WeekFraction { get; init; }

    /// <summary>The diagnosis, or null when there is nothing wrong.</summary>
    public static string? Validate(AllowanceFloor floor)
    {
        ArgumentNullException.ThrowIfNull(floor);

        if (floor.SessionFraction is null && floor.WeekFraction is null)
        {
            return "A floor that keeps nothing back is not a floor. DELETE clears one; an "
                 + "empty body would be a second way to say the same thing, and the two "
                 + "would drift.";
        }

        foreach (var (window, share) in
                 (ReadOnlySpan<(string, double?)>)
                 [(AllowanceWindows.Session, floor.SessionFraction),
                  (AllowanceWindows.Week, floor.WeekFraction)])
        {
            if (share is not { } kept) { continue; }

            if (kept <= 0)
            {
                return $"The {window} floor keeps {kept}, and a floor of nothing or less "
                     + "is an absent one written down. Leave it out.";
            }

            if (kept >= 1)
            {
                return $"The {window} floor keeps {kept}, which lends nothing at all. Not "
                     + "naming an allowance already says that, and it says it without "
                     + "leaving a machine that reads as broken.";
            }
        }

        return null;
    }
}

/// <summary>
/// An administrator spending a floor somebody else set.
/// </summary>
/// <remarks>
/// <para>
/// <b>It expires, always.</b> One with no end is a floor somebody deleted
/// without saying so — and the person it was taken from would have no moment
/// at which to expect it back.
/// </para>
/// <para>
/// <b>And it carries a reason, refused when blank.</b> The owner reads this
/// sentence. A blank one is worse than none, because it looks like an answer.
/// </para>
/// </remarks>
[PinnedId("2821c421-43f9-4aed-a27e-5bcbce0088da")]
public sealed record AllowanceOverrideRequest
{
    /// <summary>How long the floor is spendable for.</summary>
    public required int Minutes { get; init; }

    /// <summary>Why, for the person whose allowance this is.</summary>
    public required string Reason { get; init; }

    /// <summary>The longest an override may run.</summary>
    /// <remarks>
    /// A day. Long enough for anything urgent, short enough that an override
    /// nobody revisited stops mattering — which is the failure mode a
    /// permanent one has.
    /// </remarks>
    public const int MaxMinutes = 24 * 60;

    /// <summary>The most a reason may be.</summary>
    public const int MaxReason = 280;

    /// <summary>The diagnosis, or null when there is nothing wrong.</summary>
    public static string? Validate(AllowanceOverrideRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Minutes < 1)
        {
            return "An override runs for at least a minute. One with no end is a floor "
                 + "somebody deleted without saying so.";
        }

        if (request.Minutes > MaxMinutes)
        {
            return $"An override runs for at most {MaxMinutes} minutes. A longer one is a "
                 + "floor nobody will revisit, which is the state this exists to avoid.";
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return "An override names why. The person whose allowance it spends reads "
                 + "this, and a blank reason looks like an answer.";
        }

        if (request.Reason.Length > MaxReason)
        {
            return $"A reason is at most {MaxReason} characters. This is the line somebody "
                 + "reads first, not the place for the argument.";
        }

        return null;
    }
}

/// <summary>
/// A floor being spent, and by whom, until when.
/// </summary>
/// <remarks>
/// <b>Visible to the owner, which is the whole of the bargain.</b> An override
/// nobody can see is a floor that silently stopped protecting — the one
/// outcome the hard stop exists to prevent, arriving through the mechanism
/// meant to relieve it.
/// </remarks>
[PinnedId("d0f853c9-b4fa-4756-81dd-f8649ba191c6")]
public sealed record AllowanceOverride
{
    /// <summary>When it lapses and the floor is a floor again.</summary>
    public required DateTimeOffset Until { get; init; }

    /// <summary>Who took it, as the control plane derived it.</summary>
    public required string By { get; init; }

    /// <summary>Why they took it.</summary>
    public required string Reason { get; init; }
}
