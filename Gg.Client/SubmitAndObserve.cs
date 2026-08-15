namespace Gg.Client;

/// <summary>
/// What observing a submission came to.
/// </summary>
/// <remarks>
/// <b>Three, and the third one is why this vocabulary exists.</b> A submission
/// can be answered no, or it can be unanswered, and collapsing those into one
/// non-zero turns a slow worker into a recorded rejection. A closed list because
/// a fourth state must break a reader rather than be treated as one of these.
/// </remarks>
public static class ObservationStates
{
    /// <summary>It happened, and this is what it came to.</summary>
    public const string Decided = "decided";

    /// <summary>It was answered, and the answer was no.</summary>
    public const string Refused = "refused";

    /// <summary>
    /// Nothing is known yet, and that is not the same as nothing happening.
    /// </summary>
    /// <remarks>
    /// A bound that expired says nothing about whether the work was recorded.
    /// Reporting it as a refusal would be a claim nobody made.
    /// </remarks>
    public const string NotYetVisible = "not-yet-visible";

    public static IReadOnlyList<string> All { get; } = [Decided, Refused, NotYetVisible];
}

/// <summary>How long to look, and how often.</summary>
/// <remarks>
/// <b>Carried rather than hard-coded, and reported rather than hidden.</b> "We do
/// not know yet" is only actionable next to how long somebody looked - otherwise
/// nobody can tell a bound that is too short from a control plane that is stuck.
/// </remarks>
public sealed record ObservationBound
{
    /// <summary>The whole budget. Never overrun.</summary>
    public required TimeSpan Wait { get; init; }

    /// <summary>How long to wait before looking a second time.</summary>
    public required TimeSpan FirstDelay { get; init; }

    /// <summary>The ceiling the doubling stops at.</summary>
    /// <remarks>
    /// Without it a long bound ends in one long sleep, and ctrl-c has nothing to
    /// interrupt for most of the wait.
    /// </remarks>
    public required TimeSpan MaxDelay { get; init; }

    /// <summary>What a person at a terminal gets when nobody chose.</summary>
    public static ObservationBound Default { get; } = new()
    {
        Wait = TimeSpan.FromSeconds(30),
        FirstDelay = TimeSpan.FromMilliseconds(200),
        MaxDelay = TimeSpan.FromSeconds(2),
    };
}

/// <summary>What the loop saw.</summary>
public sealed record Observation
{
    /// <summary>One of <see cref="ObservationStates"/>.</summary>
    public required string State { get; init; }

    /// <summary>Why, in a sentence somebody can act on.</summary>
    public required string Because { get; init; }

    /// <summary>What was observed, when anything was.</summary>
    public string? Outcome { get; init; }

    /// <summary>How long it actually looked for.</summary>
    public required double WaitedSeconds { get; init; }

    /// <summary>How long it was willing to look for.</summary>
    public required double BoundSeconds { get; init; }

    /// <summary>How many times it looked. One means it never had to wait.</summary>
    public required int Polls { get; init; }
}

/// <summary>
/// Submit something, then watch for it to become true.
/// </summary>
/// <remarks>
/// <para>
/// <b>A component rather than a verb, because two more transports will need
/// it.</b> A web surface and a chat surface each submit and then wait, and each
/// renders the waiting differently. What they share is this loop and the three
/// answers it can give; what differs is only the renderer. Writing it inside
/// <c>gg decide</c> would mean the second caller reimplements the bound, the
/// backoff, and - the part that matters - the distinction between <i>no</i> and
/// <i>not yet</i>.
/// </para>
/// <para>
/// <b>It looks before it sleeps.</b> The control plane still writes
/// synchronously, so the first observation almost always succeeds; a loop that
/// slept first would add latency to every decision for no reason. When that write
/// becomes asynchronous nothing here changes, which is the point of building the
/// waiting before removing the wait.
/// </para>
/// <para>
/// <b>Time is a parameter.</b> The bound is the subject rather than the setting,
/// so a real clock would make its tests either slow or flaky - and it would make
/// the boundary that matters the one part nobody can assert.
/// </para>
/// </remarks>
public sealed class SubmitAndObserve(
    Func<TimeSpan, CancellationToken, Task> delay, Func<DateTimeOffset> now)
{
    private readonly Func<TimeSpan, CancellationToken, Task> _delay = delay;
    private readonly Func<DateTimeOffset> _now = now;

    /// <summary>
    /// Submits, then observes until an outcome or the bound.
    /// </summary>
    /// <param name="submit">
    /// Does the thing. Returns null when it was accepted, or the reason it was
    /// refused - a refusal is an ANSWER, and the loop stops rather than waiting
    /// for something nobody wrote.
    /// </param>
    /// <param name="observe">
    /// Looks. Returns null when nothing is visible yet, or what was observed.
    /// Null must mean "not yet", never "no" - the caller's mapping is what keeps
    /// those apart, and getting it wrong here is the failure this whole component
    /// is about.
    /// </param>
    public async Task<Observation> RunAsync(
        Func<CancellationToken, Task<string?>> submit,
        Func<CancellationToken, Task<string?>> observe,
        ObservationBound bound,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(submit);
        ArgumentNullException.ThrowIfNull(observe);
        ArgumentNullException.ThrowIfNull(bound);

        cancellationToken.ThrowIfCancellationRequested();

        var started = _now();

        if (await submit(cancellationToken) is { Length: > 0 } refused)
        {
            return new Observation
            {
                State = ObservationStates.Refused,
                Because = refused,
                WaitedSeconds = 0,
                BoundSeconds = bound.Wait.TotalSeconds,
                Polls = 0,
            };
        }

        var polls = 0;
        var wait = bound.FirstDelay;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            polls++;

            if (await observe(cancellationToken) is { Length: > 0 } outcome)
            {
                return new Observation
                {
                    State = ObservationStates.Decided,
                    Because = "Observed on the read surface, which is what the record says.",
                    Outcome = outcome,
                    WaitedSeconds = (_now() - started).TotalSeconds,
                    BoundSeconds = bound.Wait.TotalSeconds,
                    Polls = polls,
                };
            }

            var spent = _now() - started;
            var left = bound.Wait - spent;

            // THE BOUND IS NEVER OVERRUN, including by the last sleep. A bound
            // somebody was told about and then exceeded is worse than no bound.
            if (left <= TimeSpan.Zero || wait >= left)
            {
                return new Observation
                {
                    State = ObservationStates.NotYetVisible,
                    Because = $"Submitted, and not visible on the read surface within "
                            + $"{bound.Wait.TotalSeconds:0.#}s. This does NOT mean it was "
                            + "refused - nothing has said no, and it may land after this. Look "
                            + "again rather than submitting it a second time.",
                    WaitedSeconds = spent.TotalSeconds,
                    BoundSeconds = bound.Wait.TotalSeconds,
                    Polls = polls,
                };
            }

            await _delay(wait, cancellationToken);

            // Doubling, capped. A busy loop against a control plane is a denial
            // of service with good intentions.
            wait = wait + wait > bound.MaxDelay ? bound.MaxDelay : wait + wait;
        }
    }
}
