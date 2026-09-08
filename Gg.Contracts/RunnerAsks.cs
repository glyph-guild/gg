namespace Gg.Contracts;

/// <summary>
/// Declares which ask or answer a payload is.
/// </summary>
/// <remarks>
/// <b>The fourth registration, and it mirrors <see cref="FactKindAttribute"/>
/// deliberately.</b> A vocabulary that is closed three ways is a vocabulary
/// somebody can widen by forgetting one, and the fact surface already paid for
/// learning that. The same shape here means the same guard can be written
/// against it and a reader has one thing to understand rather than two.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class RunnerAskKindAttribute(string kind) : Attribute
{
    /// <summary>One of <see cref="RunnerAskKinds"/>.</summary>
    public string Kind { get; } = kind;
}

/// <summary>
/// Everything a person may ask a runner about itself.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two, and the shortness is the design.</b> ADR-0013: a general data channel
/// to a component <c>CLAUDE.md</c> calls hostile is a bad idea; one that can only
/// answer <c>tail-log</c> and <c>status</c> is defensible. This list is what makes
/// that a property of the build rather than a sentence in a document — widening
/// the channel means widening a closed vocabulary, which moves a fingerprint and
/// bumps a contract version.
/// </para>
/// <para>
/// <b>Read-only, for now, and the "for now" is recorded rather than implied.</b>
/// ADR-0013's amendment decides that driving a runner is a flight, so this
/// vocabulary is expected to grow — and when it does it will be through a lease
/// and an envelope, not by a third value appearing here quietly.
/// </para>
/// <para>
/// <b>Values on the wire that never reach a fact</b>, so this is the contract
/// ledger's rather than the fact ledger's.
/// </para>
/// </remarks>
[VocabularyOf(VocabularyFingerprints.Contract)]
public static class RunnerAskKinds
{
    /// <summary>The last lines the runner wrote about itself.</summary>
    public const string TailLog = "tail-log";

    /// <summary>What the runner is doing, and what it last failed at.</summary>
    public const string Status = "status";

    public static IReadOnlyList<string> All { get; } = [TailLog, Status];
}

/// <summary>
/// What a bounded answer may be, decided by the contract rather than by whatever
/// carries it.
/// </summary>
/// <remarks>
/// <b>Here rather than in a transport, because a second transport must not get
/// to disagree.</b> ADR-0013 requires the protocol to be designed before
/// anything carries it, and a limit that lived in the WebRTC path would be
/// re-invented — differently — by the dead-drop. A transport may be stricter
/// than this and may never be more generous.
/// </remarks>
public static class RunnerAskBounds
{
    /// <summary>The most lines a tail may return.</summary>
    public const int MaxLines = 200;

    /// <summary>The most bytes an answer may carry, after stripping.</summary>
    /// <remarks>
    /// A tail of a runner's log, not a log shipper. The number is small on
    /// purpose: this path exists so a person can see what a machine is doing,
    /// and anything that needs more than this wants Decision 2's command and
    /// their own credentials.
    /// </remarks>
    public const int MaxBytes = 64 * 1024;
}

/// <summary>Asks for the last lines the runner wrote.</summary>
[PinnedId("f1c8a37e-95d2-4b06-8e41-2a7c530bd94f")]
[RunnerAskKind(RunnerAskKinds.TailLog)]
public sealed record TailLogAsk
{
    /// <summary>
    /// How many lines, at most <see cref="RunnerAskBounds.MaxLines"/>.
    /// </summary>
    public required int Lines { get; init; }
}

/// <summary>Asks what the runner is doing.</summary>
/// <remarks>
/// <b>Empty, for <see cref="RunnerReservationRequest"/>'s reason.</b> The ask is
/// "how are you", and there is nothing for a caller to narrow it with. A member
/// here would be a filter, and a filter is the first step toward a query.
/// </remarks>
[PinnedId("6b09d4a2-71fe-42c3-93b8-c05e8a1d7620")]
[RunnerAskKind(RunnerAskKinds.Status)]
public sealed record StatusAsk;

/// <summary>
/// One question for one runner.
/// </summary>
/// <remarks>
/// <b>An envelope with a slot per kind, like <c>FactEnvelope</c>.</b> The kind
/// says which slot is filled, and a kind with no slot cannot be constructed —
/// which is the third of the four registrations doing its work.
/// </remarks>
[PinnedId("3e57b1c9-2d84-4f7a-a6e0-98b3c41f5d72")]
public sealed record RunnerAsk
{
    /// <summary>One of <see cref="RunnerAskKinds"/>.</summary>
    public required string Kind { get; init; }

    public TailLogAsk? TailLog { get; init; }

    public StatusAsk? Status { get; init; }
}

/// <summary>The lines a runner wrote about itself.</summary>
/// <remarks>
/// <b><c>Truncated</c> is a fact, not an apology.</b> A tail that silently
/// stopped at the bound and a tail that was genuinely that short read identically
/// otherwise, and this system's own vocabulary calls collapsing two silences its
/// most dangerous failure mode.
/// </remarks>
[PinnedId("8a2f0e63-4c15-49db-b73e-1f6a805c2e94")]
public sealed record LogTail
{
    public required IReadOnlyList<string> Lines { get; init; }

    /// <summary>Whether a bound cut this short.</summary>
    public required bool Truncated { get; init; }
}

/// <summary>What a runner is doing, and what it last failed at.</summary>
/// <remarks>
/// <b>This is ADR-0013's tier 2c, and it is the reason <c>status</c> exists
/// beside <c>tail-log</c>.</b> <c>BoundBroken</c>, <c>WorkspaceFailed</c> and
/// <c>ControlPlaneRefused</c> are three free-form diagnosis strings the runner
/// narrates into a log file and nothing else ever sees. They are unbounded text
/// that can contain anything, which is why they cannot be facts — and why what
/// carries them is stripped at ingress.
/// </remarks>
[PinnedId("c4d7920b-6e38-4a51-85fc-73b0e19a462d")]
public sealed record RunnerStatusReport
{
    /// <summary>A short phrase: what this runner is doing right now.</summary>
    public required string Doing { get; init; }

    /// <summary>The last diagnosis it recorded, or null when there is none.</summary>
    public string? Diagnosis { get; init; }

    /// <summary>When the runner measured this.</summary>
    public required DateTimeOffset At { get; init; }
}

/// <summary>
/// One answer from one runner.
/// </summary>
/// <remarks>
/// <para>
/// <b>Request and bounded response, with no streaming shape anywhere.</b> ADR-0013:
/// designing this around streaming would mean a second transport later cannot
/// carry it, and adding the fallback becomes a rewrite rather than a
/// registration. So there is no continuation token, no callback and no
/// asynchronous sequence — a store-and-forward transport has to be able to carry
/// one of these whole.
/// </para>
/// <para>
/// <b>What arrives here is hostile text.</b> A runner's log can contain anything,
/// including text addressed to a model, and it is about to be rendered in a
/// terminal. It is stripped at INGRESS by the same rule the flight log already
/// uses, so every surface inherits the property instead of re-deriving it.
/// </para>
/// </remarks>
[PinnedId("5d38c7f1-0ba9-4e26-97c4-6e2f81a3b508")]
public sealed record RunnerSaid
{
    /// <summary>One of <see cref="RunnerAskKinds"/>.</summary>
    public required string Kind { get; init; }

    public LogTail? Tail { get; init; }

    public RunnerStatusReport? Status { get; init; }

    /// <summary>
    /// The same answer with every control sequence removed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>On the CONTRACT, so both sides strip by one rule rather than two that
    /// agree today.</b> <see cref="ControlText"/> says exactly this about the
    /// flight log: applied at ingress, before storage, so the web queue, a chat
    /// card, a PR comment and a support bundle inherit the property instead of
    /// each needing its own escape hatch - and the first one written without it
    /// is the one that carries an escape sequence into somebody's terminal.
    /// </para>
    /// <para>
    /// <b>Line breaks survive a diagnosis and not a line.</b> A tail is already
    /// lines, so a newline inside one would let a runner forge extra rows in
    /// whatever renders them; a diagnosis is one string that may legitimately
    /// have them.
    /// </para>
    /// <para>
    /// <b>Nothing here summarises.</b> ADR-0006's third hazard: a transcript can
    /// contain text addressed to a model, so anything that summarises one
    /// produces output that crosses. Stripping is mechanical and total; a model
    /// on this path would be the injected instruction arriving inside the one
    /// artifact everyone was told is safe.
    /// </para>
    /// </remarks>
    public RunnerSaid Stripped() => new()
    {
        Kind = ControlText.Strip(Kind),
        Tail = Tail is null ? null : new LogTail
        {
            Lines = [.. Tail.Lines.Select(l => ControlText.Strip(l, allowLineBreaks: false))],
            Truncated = Tail.Truncated,
        },
        Status = Status is null ? null : new RunnerStatusReport
        {
            Doing = ControlText.Strip(Status.Doing),
            Diagnosis = Status.Diagnosis is null
                ? null
                : ControlText.Strip(Status.Diagnosis, allowLineBreaks: true),
            At = Status.At,
        },
    };
}
