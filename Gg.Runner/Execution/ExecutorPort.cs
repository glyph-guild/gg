using Gg.Contracts;

namespace Gg.Runner.Execution;

/// <summary>Something this executor cannot do, and what that costs.</summary>
/// <remarks>
/// A gap named without a consequence is a footnote. These are read by somebody
/// deciding whether this executor can do their job, so each one says what
/// happens because of it.
/// </remarks>
/// <summary>
/// What an executor can report, what it cannot, and what degrades.
/// </summary>
/// <remarks>
/// <para>
/// <b>One adapter is not an abstraction</b> - learned twice here, on VCS and
/// on identity. The mitigation is not a second adapter; it is declaring
/// capabilities from the first, which is what the provider adapter
/// established.
/// </para>
/// <para>
/// Every value below was measured against the real binary before it was
/// written down. A declaration assembled from what the interface made
/// convenient would describe an executor nobody has run.
/// </para>
/// </remarks>
/// <summary>
/// How completely an executor's declared moves bound it.
/// </summary>
/// <remarks>
/// Measured per tool rather than assumed, because it is not uniform: on the one
/// executor this product has, <c>Edit</c> and <c>Write</c> are refused at the
/// call, <c>Grep</c> is removed from the tool list entirely, and <c>Read</c> and
/// <c>Bash</c> are not bound at all.
/// </remarks>
public enum MoveEnforcement
{
    /// <summary>Nothing declared is withheld. A move is an observation only.</summary>
    None,

    /// <summary>
    /// Some tools are withheld and some are not, and which is which is the
    /// executor's business rather than the envelope's.
    /// </summary>
    /// <remarks>
    /// The honest state for anything whose bound is per tool: a flight declaring
    /// <c>read</c> alone is genuinely stopped from editing and genuinely able to
    /// run shell commands, so neither <c>None</c> nor <c>Full</c> is true of it.
    /// </remarks>
    PerTool,

    /// <summary>Every declared move bounds what may happen. Nothing declares this yet.</summary>
    Full,
}

/// <summary>How an enforcement level is spelled on the wire.</summary>
/// <remarks>
/// Two spellings of one idea would drift, and the drift would be invisible: the
/// enum is what this assembly reasons with and <see cref="MoveEnforcements"/> is
/// what crosses, so the translation is in one place and asserted.
/// </remarks>
public static class MoveEnforcementNames
{
    public static string Of(MoveEnforcement enforcement) => enforcement switch
    {
        MoveEnforcement.None => MoveEnforcements.None,
        MoveEnforcement.PerTool => MoveEnforcements.PerTool,
        MoveEnforcement.Full => MoveEnforcements.Full,
        _ => throw new ArgumentOutOfRangeException(nameof(enforcement)),
    };
}

/// <summary>
/// Which rung this executor is, from the envelope's vocabulary.
/// </summary>
/// <remarks>
/// <b>Seven members were deleted at slice twenty, and the reason is worth
/// keeping.</b> They declared what this executor reports and what it cannot
/// account for — attempts, duration, moves used, tokens, move enforcement,
/// tool attribution, and a list of named gaps — and <b>nothing ever degraded
/// against any of them</b>. <c>IExecutorPort.Capabilities</c> was never called
/// by production at all; the only readers were the tests that asserted the
/// declarations had not drifted.
///
/// <para>
/// Three guard assertions went with them: an exact member list, the declared
/// move enforcement, and the gap naming the flag the move bound rests on.
/// Those guarded a claim rather than a behaviour — and the behaviour they
/// described is measured per session by <see cref="MoveBoundProbe"/>, which
/// reads none of this. A declaration nothing consults is documentation with a
/// test on it, and documentation belongs where somebody reads it.
/// </para>
/// </remarks>
public sealed record ExecutorCapabilities
{
    /// <summary>Which rung this is, from the envelope's vocabulary.</summary>
    public required string Rung { get; init; }
}

/// <summary>What a loop is asked to do.</summary>
public sealed record ExecutorRequest
{
    /// <summary>The materialized tree. The agent works here and nowhere else.</summary>
    public required string WorkingDirectory { get; init; }

    /// <summary>
    /// What a person said when they sent the previous attempt back, if they did.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Advice, never authority.</b> It reaches the agent's context and changes nothing
    /// the flight is permitted to do: the moves, the tree, the budget and the scope all
    /// come from the envelope, and this record carries none of them. A reason able to
    /// widen any of it would be unreviewable configuration arriving one sentence at a
    /// time.
    /// </para>
    /// <para>
    /// <b>Null on a first attempt</b>, and null again on the attempt after next: a
    /// rejection is in the record permanently and in the context once.
    /// </para>
    /// </remarks>
    public LeaseFeedback? Feedback { get; init; }

    /// <summary>
    /// The rendered handoff record of the attempt this loop resumes, when there
    /// was one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Advice, never authority</b> - the same disposition as
    /// <see cref="Feedback"/>. It reaches the agent's context and changes nothing
    /// the flight is permitted to do: the moves, the tree, the budget and the
    /// scope all come from the envelope, and this record carries none of them.
    /// </para>
    /// <para>
    /// <b>Two kinds of claim in one document.</b> Its measured sections are the
    /// platform's own account of the prior run, counted from that run's event
    /// stream; its agent's-own-account section is the prior agent's words about
    /// itself. The prompt marks which is which, because the account must not
    /// borrow the measurement's authority.
    /// </para>
    /// <para>
    /// <b>Null on a first attempt</b>, which is the ordinary case. Already
    /// rendered: the contract renders the seed once, control-plane-side, and a
    /// runner that re-rendered it would be a second implementation of the same
    /// document.
    /// </para>
    /// </remarks>
    public string? ResumesFrom { get; init; }

    /// <summary>
    /// Where to append the live view. The runner always sets one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This said the opposite until slice 31, and the revision is the point
    /// rather than a detail.</b> It read: <i>"Null is the ordinary state: the
    /// pane is off by default and a run nobody is watching writes nothing."</i>
    /// That assumed the runner knows whether anyone is watching. <b>It cannot.</b>
    /// The console and the runner are unrelated processes started by different
    /// invocations, with no channel between them but the filesystem - so
    /// "nobody is watching" is not a fact this side can hold.
    /// </para>
    /// <para>
    /// <b>So the runner always writes, and the cost is paid deliberately.</b>
    /// The alternatives were an environment variable, which cannot start
    /// watching a flight already in progress, and a marker file the runner
    /// polls, which buys the same thing for a stat on a hot path. Peeking at a
    /// run that is already going is the whole feature, and only always-writing
    /// gives it.
    /// </para>
    /// <para>
    /// Measured before it was decided: a 51-second flight wrote 37 lines and
    /// 5,331 bytes. <see cref="LiveStream"/> puts these in their own directory
    /// precisely because they are deletable and transcripts are not, and a
    /// sweep and a cap are slice 31 step 5.
    /// </para>
    /// <para>
    /// Null remains legal and means write nothing - <see cref="MoveBoundProbe"/>
    /// passes nothing, because a probe is not a flight anybody watches.
    /// Still not evidence: ephemeral, local, and it crosses nothing.
    /// </para>
    /// </remarks>
    public LiveStream? Live { get; init; }

    /// <summary>Which loop, by its id in the envelope.</summary>
    public required string LoopId { get; init; }

    /// <summary>
    /// What the flight is for, as an addressable reference.
    /// </summary>
    /// <remarks>
    /// The URI. The body is resolved here, on this machine, with the
    /// customer's own credential - and it never travels back.
    /// </remarks>
    public string? IntentUri { get; init; }

    /// <summary>
    /// Whether this session has anybody to ask. True for a flight; false for
    /// work this runner set itself.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The move-bound probe is why this exists, and the failure it prevents
    /// is the probe measuring nothing.</b> The probe denies the moves that
    /// write, asks an agent to modify a file and create one, and looks at the
    /// disk. An agent handed a tool for asking a person could ask instead of
    /// attempting the write - and the probe would then report a bound that
    /// held, having tested nothing at all.
    /// </para>
    /// <para>
    /// <b>A member rather than a check on the loop id.</b> The executor
    /// deciding by recognising the probe's own name would be the launcher
    /// knowing about one caller, and the next caller with nobody to ask would
    /// have to be recognised too. The caller says.
    /// </para>
    /// <para>
    /// Defaulted true, because every request that is a flight has somebody to
    /// ask and there are far more of those.
    /// </para>
    /// </remarks>
    public bool CanAskAPerson { get; init; } = true;

    /// <summary>The tracker a work-item flight names, or null.</summary>
    /// <remarks>
    /// <b>Beside the uri, because they are two ways of naming external work and
    /// a runner has to tell them apart.</b> Composing a URL out of a provider
    /// and an id is the derivation slice nine retired, and this repository names
    /// no forge to do it with.
    /// </remarks>
    public string? IntentProvider { get; init; }

    /// <summary>That work item's identifier. Declared, never parsed.</summary>
    public string? IntentId { get; init; }

    /// <summary>
    /// Whether a flight names external work an agent could go and resolve.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The question the invocation gate was actually asking</b>, and asking
    /// wrong: it required a uri, so every work-item flight was claimed, cloned
    /// and returned without invoking anything.
    /// </para>
    /// <para>
    /// <b>Half a work item is not a work item.</b> A provider with no id names a
    /// tracker rather than an item in it; an id with no provider does not say
    /// which tracker it is in. The contract refuses that pair at intake, and a
    /// runner treating it as workable would be a second, laxer copy of the rule.
    /// </para>
    /// </remarks>
    public static bool NamesWork(string? uri, string? provider, string? id) =>
        uri is { Length: > 0 }
        || (provider is { Length: > 0 } && id is { Length: > 0 });

    /// <summary>What the envelope permits. Passed through, and not enforced.</summary>
    public required IReadOnlyList<string> Moves { get; init; }

    /// <summary>The one budget this slice enforces.</summary>
    public required TimeSpan WallClock { get; init; }

    /// <summary>
    /// Where the transcript is written: durable, and OUTSIDE the ephemeral
    /// tree.
    /// </summary>
    /// <remarks>
    /// Outside deliberately. The tree is deleted when the flight ends - that is
    /// the whole point of the observational tier - and a transcript inside it
    /// would be a reference to something that no longer exists by the time
    /// anybody follows it.
    /// </remarks>
    public required string TranscriptPath { get; init; }
}

/// <summary>What a loop did.</summary>
public sealed record ExecutorRun
{
    public required string LoopId { get; init; }

    /// <summary>One of <see cref="LoopOutcomes"/>.</summary>
    public required string Outcome { get; init; }

    /// <summary>Short, decidable, stripped, and the first thing a person reads.</summary>
    public required string Reason { get; init; }

    public required int Attempts { get; init; }

    public required long DurationMs { get; init; }

    /// <summary>
    /// What the agent REACHED FOR, distinct and ordered.
    /// </summary>
    /// <remarks>
    /// Attempts rather than grants. A real run reaches for tools the envelope
    /// did not name and is refused, and recording the attempt is the more
    /// useful signal: it says what the loop wanted, which is what a bound
    /// would have to be set against. Ordered so two identical flights digest
    /// identically.
    /// </remarks>
    public required IReadOnlyList<string> MovesUsed { get; init; }

    /// <summary>Where the transcript is, when one was written.</summary>
    public ArtifactReference? Transcript { get; init; }

    /// <summary>
    /// The work kind this loop nominated, or null where it nominated none.
    /// </summary>
    /// <remarks>
    /// <b>Extracted HERE, from the stream, while it is still on this
    /// machine</b> - the same boundary the digest is taken at, and for the same
    /// reason. Null is the ordinary state: only a classifying loop nominates,
    /// and a classifier that could not decide nominates nothing, which is a
    /// real answer rather than a missing one.
    /// </remarks>
    public Gg.Contracts.FlightNomination? Nomination { get; init; }

    /// <summary>
    /// What this run asked a person, when it asked anything.
    /// </summary>
    /// <remarks>
    /// Beside the outcome rather than inside it: asking and finishing are two
    /// facts, not one state, so a run that asked and then finished carries both
    /// a question and <c>completed</c>.
    /// </remarks>
    public Gg.Contracts.LoopQuestion? Question { get; init; }

    /// <summary>
    /// What the stream said, extracted so it can cross without the transcript.
    /// </summary>
    /// <remarks>
    /// The transcript is a machine-local reference and does not cross, so this
    /// is what a person gets instead. Extracted mechanically from the same
    /// stream the transcript holds - never summarised, because a summary would
    /// be a claim rather than a fact and would carry whatever the transcript
    /// told it to.
    /// </remarks>
    public LoopDigest? Digest { get; init; }

    public static ExecutorRun Completed(
        string loopId, string reason, int attempts, TimeSpan took, IReadOnlyList<string> movesUsed) =>
        new()
        {
            LoopId = loopId,
            Outcome = LoopOutcomes.Completed,
            Reason = Clean(reason),
            Attempts = attempts,
            DurationMs = (long)took.TotalMilliseconds,
            MovesUsed = Distinct(movesUsed),
        };

    public static ExecutorRun Failed(
        string loopId, string reason, int attempts, TimeSpan took, IReadOnlyList<string> movesUsed) =>
        new()
        {
            LoopId = loopId,
            Outcome = LoopOutcomes.Failed,
            Reason = Clean(reason),
            Attempts = attempts,
            DurationMs = (long)took.TotalMilliseconds,
            MovesUsed = Distinct(movesUsed),
        };

    /// <summary>
    /// It asked for a decision and stopped - waiting on a person, not broken
    /// and not finished.
    /// </summary>
    /// <remarks>
    /// <b>Carries the agent's own words like a completion does</b>, because the
    /// question it asked is on its own fact and this reason is the account of
    /// the run around it. Two facts, not one state: what was asked, and what
    /// the run then did.
    /// </remarks>
    public static ExecutorRun Blocked(
        string loopId, string reason, int attempts, TimeSpan took, IReadOnlyList<string> movesUsed) =>
        new()
        {
            LoopId = loopId,
            Outcome = LoopOutcomes.Blocked,
            Reason = Clean(reason),
            Attempts = attempts,
            DurationMs = (long)took.TotalMilliseconds,
            MovesUsed = Distinct(movesUsed),
        };

    /// <summary>
    /// Out of budget - a real state rather than an error.
    /// </summary>
    /// <remarks>
    /// The reason here is the measurement and only the measurement. Who the
    /// flight waits for next is the envelope's knowledge - on-exhaustion names
    /// a person or another agent - and this factory has never seen the
    /// envelope, so the runner appends that sentence where the envelope is
    /// known. A constant here once claimed a person was waiting, written when
    /// handoff-to-human was the only value, and it survived a second value
    /// arriving.
    /// </remarks>
    public static ExecutorRun Exhausted(
        string loopId, TimeSpan after, IReadOnlyList<string> movesUsed) =>
        new()
        {
            LoopId = loopId,
            Outcome = LoopOutcomes.Exhausted,
            Reason = $"The loop used its whole wall-clock budget of {Describe(after)} and stopped.",
            Attempts = 0,
            DurationMs = (long)after.TotalMilliseconds,
            MovesUsed = Distinct(movesUsed),
        };

    /// <summary>The run, as the fact that crosses.</summary>
    public LoopOutcome ToFact(string executor) => new()
    {
        LoopId = LoopId,
        Outcome = Outcome,
        Reason = Reason,
        Executor = executor,
        Attempts = Attempts,
        DurationMs = DurationMs,
        MovesUsed = MovesUsed,
    };

    private static string Describe(TimeSpan span) =>
        span.TotalMinutes >= 1
            ? $"{(int)span.TotalMinutes}m"
            : $"{(int)span.TotalSeconds}s";

    /// <summary>The most a reason may be, before it stops being one.</summary>
    /// <remarks>
    /// This value is carried INLINE - it is the row somebody reads first, and
    /// the whole point of that disposition is that it is short and decidable.
    /// A real agent's closing summary runs to paragraphs with code blocks in
    /// it; that is the transcript's job, and putting it here would make the
    /// cheapest thing to read the most expensive.
    /// </remarks>
    public const int MaxReasonLength = 280;

    /// <summary>
    /// Stripped at INGRESS, and cut to something a person reads first.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Stripped because it came from a process this machine started, and stdout
    /// is what a customer pastes into a ticket. Stripping is not deleting: what
    /// somebody wrote survives and the escape does not.
    /// </para>
    /// <para>
    /// Cut to the first paragraph, because a real run ends with several. The
    /// rest is in the transcript, and the cut is marked rather than silent -
    /// somebody has to be able to tell there was more.
    /// </para>
    /// </remarks>
    private static string Clean(string reason)
    {
        var stripped = (ControlText.Strip(reason, allowLineBreaks: true) ?? "").Trim();

        var firstBreak = stripped.IndexOf("\n\n", StringComparison.Ordinal);
        var head = (firstBreak > 0 ? stripped[..firstBreak] : stripped)
            .ReplaceLineEndings(" ")
            .Trim();

        // MARKED WHICHEVER CUT HAPPENED, which the paragraph one was not.
        // Dropping everything after the first paragraph silently is the worse
        // of the two: a lead-in reads like an answer, and a real flight
        // recorded "two independent blockers, neither of which I can work
        // around:" as though that were the whole of it.
        var dropped = firstBreak > 0 || head.Length > MaxReasonLength;

        var kept = head.Length <= MaxReasonLength
            ? head
            : head[..MaxReasonLength].TrimEnd();

        return dropped ? kept + "… (the rest is in the transcript)" : kept;
    }

    private static IReadOnlyList<string> Distinct(IReadOnlyList<string> moves) =>
        [.. moves.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
}

/// <summary>
/// The runner's missing verb.
/// </summary>
/// <remarks>
/// Slice one built <c>lease → resolve → materialize → ~~invoke~~ → extract →
/// digest → filter → emit</c> and omitted exactly one. This is it.
/// </remarks>
public interface IExecutorPort
{
    /// <summary>What this executor can and cannot do.</summary>
    ExecutorCapabilities Capabilities { get; }

    /// <summary>
    /// Whether running this executor can measure its own move bound.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>True for anything headless, and the default says so</b> — every
    /// executor before the attended one is probeable, and a default rather than
    /// a required member is what keeps this from being a declaration ten test
    /// doubles have to repeat.
    /// </para>
    /// <para>
    /// <b>False means the probe is skipped, not faked.</b>
    /// <see cref="MoveBoundProbe"/> measures by INVOKING the port, so an
    /// executor that hands a person the terminal cannot be probed without
    /// handing them the probe's canary task — and probing a different executor
    /// instead would measure a session other than the one it governs, which is
    /// the one claim the probe exists to make.
    /// </para>
    /// <para>
    /// <b>Read by the loop, which is the whole difference from
    /// <see cref="ExecutorCapabilities"/>.</b> That record's seven other members
    /// were deleted at slice twenty because nothing consulted them; this one
    /// changes what the runner does.
    /// </para>
    /// </remarks>
    bool BoundIsMeasurable => true;

    /// <summary>
    /// Runs the loop, and never throws for a loop that simply failed.
    /// </summary>
    /// <returns>
    /// What the loop did, or <b>null when nothing measured a loop at all</b>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <b>Null is an attended session, and it is a real state rather than a
    /// degraded one.</b> <see cref="ExecutorRun"/> requires an outcome, an
    /// attempt count and a moves list; a person at a Claude Code prompt
    /// produced none of them, because the child owned the terminal and there
    /// was no stream to read. The honest answer is that this executor measured
    /// nothing, and <c>RunnerLoop.ShipAsync</c> already ships the environment,
    /// the change manifest and the source provenance without a run.
    /// </para>
    /// <para>
    /// <b>It must stay the ONLY null.</b> A helpful <c>Attempts = 0</c> and
    /// <c>MovesUsed = []</c> are both expressible and both false — and <c>[]</c>
    /// in particular reads as "used no moves" rather than "measured no moves",
    /// which detaches every move-gate it touches. The absence is in the type
    /// rather than filtered downstream because a filter is a second place
    /// deciding what ships.
    /// </para>
    /// <para>
    /// <b>And the headless path has no ending that returns one.</b> Completed,
    /// failed, blocked, exhausted, a child that would not start and a reader
    /// that would not resolve all answer with a run;
    /// <c>ClaudeCodeExecutor.ExecuteAsync</c> is declared non-nullable and
    /// <c>AttendedExecutorTests</c> holds it there. A null arriving from two
    /// causes and read as one is how a fleet stops reporting quietly.
    /// </para>
    /// </remarks>
    Task<ExecutorRun?> ExecuteAsync(ExecutorRequest request, CancellationToken cancellationToken);
}
