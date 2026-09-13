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

    /// <summary>
    /// Place a credential on this runner, for this runner's own use.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The third value, and the "for now" above is what it spends.</b> It
    /// arrives the way that paragraph says it must - through a lease and an
    /// envelope - rather than quietly: a fingerprint moved and a contract
    /// version was spent to add it.
    /// </para>
    /// <para>
    /// <b>It is not <c>RunCommand</c>, and the difference is worth stating
    /// rather than leaving to be inferred.</b> ADR-0013 names that type as the
    /// thing this vocabulary exists to refuse, and <c>RunnerAskClosureTests</c>
    /// plants it by name. This performs nothing, returns no data, and writes one
    /// file the runner already writes for itself - what it widens is what a
    /// runner may be TOLD, not what it may be made to DO.
    /// </para>
    /// <para>
    /// <b>And it is the only way in.</b> A strategy document cannot hold a
    /// credential, an offered configuration cannot carry one, and a pool member
    /// has no file an operator can reach. What was left was rebuilding the image
    /// for every rotation.
    /// </para>
    /// </remarks>
    public const string ConfigureCredential = "configure-credential";

    public static IReadOnlyList<string> All { get; } =
        [TailLog, Status, ConfigureCredential];
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
/// A credential for this runner to keep, and the only thing on this contract
/// that carries one.
/// </summary>
/// <remarks>
/// <para>
/// <b>Article VIII is not bent by this, and the reason is WHERE it travels.</b>
/// The control plane stores <i>references and facts, never secrets</i> - and it
/// never sees this. It crosses a channel sealed end to end between a console
/// and a runner, brokered by a relay that <i>says where and whether, never
/// what</i>. A secret the control plane cannot read is the aligned answer to
/// "how does a machine with no filesystem anybody can reach get a token", not a
/// transgression against the article.
/// </para>
/// <para>
/// <b>That safety is structural and asserted.</b> No endpoint in
/// <see cref="Description.ProtocolSurface"/> names this type, transitively - so
/// there is no request body it can enter - and the channel has its own
/// serializer context, kept apart from the one a runner uses to speak to its
/// control plane, so <i>a type added to one is not silently serialisable over
/// the other</i>.
/// </para>
/// <para>
/// <b>Two members, and the shortness is the design.</b> The runner writes a
/// file keyed by a locator; identity and scopes are the control plane's record
/// of the reference and it already holds them. A member here that the write
/// does not need is a member a secret-carrying type did not have to have.
/// </para>
/// </remarks>
[PinnedId("0f4e6a21-9c73-4b58-8d10-72a5e3b6c94f")]
[RunnerAskKind(RunnerAskKinds.ConfigureCredential)]
public sealed record ConfigureCredentialAsk
{
    /// <summary>
    /// Which credential this is, in the form <c>gg credential add</c> stores.
    /// </summary>
    /// <remarks>
    /// <b>Validated by the contract's own rule before it becomes a path</b>,
    /// which <see cref="CredentialStore"/> already does and already says why:
    /// <i>a path it could steer is a path it could steer anywhere</i>. It said
    /// that about a locator arriving from a control plane. This one arrives
    /// from a console over a channel <c>CLAUDE.md</c> calls hostile - the same
    /// guard, and a better reason for it.
    /// </remarks>
    public required string Locator { get; init; }

    /// <summary>The value. It goes to a 0600 file and nowhere else.</summary>
    /// <remarks>
    /// <b>Named for what it is.</b> <c>CredentialContainmentTests</c> refuses a
    /// member called this on every type it scans, which is exactly the guard
    /// that should fire if anybody ever adds this one to that list.
    /// </remarks>
    public required string Secret { get; init; }
}

/// <summary>What the runner did with it. Never what it was given.</summary>
/// <remarks>
/// <b>An acknowledgement rather than an echo.</b> A runner that answered with
/// what it had been handed would put the secret back on a channel a person is
/// watching and into whatever renders it - which is the one failure this whole
/// path has to not have. The locator is a reference and names nothing;
/// <see cref="Written"/> is the fact.
/// </remarks>
[PinnedId("6c21d90b-473e-4a85-b1f6-2d089e7a3c15")]
public sealed record ConfiguredCredential
{
    /// <summary>Which credential, by the name it was asked about.</summary>
    public required string Locator { get; init; }

    /// <summary>Whether the secret is now on this machine.</summary>
    /// <remarks>
    /// <b>A fact, not an apology</b>, for <see cref="LogTail.Truncated"/>'s
    /// reason. A write that failed and a write that happened must not read
    /// identically, because the flight after this one depends on which.
    /// </remarks>
    public required bool Written { get; init; }
}

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

    public ConfigureCredentialAsk? ConfigureCredential { get; init; }
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

    /// <summary>
    /// The flight it is on, as a person reads it, or null when it is on none.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Because a watch outlives the flight it started on.</b> A channel used
    /// to exist only while one flight did, so what the tail carried could not
    /// change subject under a reader. A watcher attached to an idle machine
    /// follows whatever it claims next, and a pane that silently begins drawing
    /// a different flight's output is lying by omission.
    /// </para>
    /// <para>
    /// <b>The NUMBER and not the id.</b> <c>GG-84</c> is what a person reads and
    /// what <c>RunnerSummary.CurrentFlightNumber</c> already carries; the id is
    /// what the runner builds a live-view path out of, which it needs on the
    /// machine and nowhere else. Sending both would put an identifier on the
    /// wire that nothing off the machine has a use for.
    /// </para>
    /// <para>
    /// <b>Null is idle, not unknown.</b> Requiring it would force a sentinel,
    /// and every sentinel would then have to be told apart from a flight number
    /// by whoever read it.
    /// </para>
    /// </remarks>
    public string? FlightNumber { get; init; }

    /// <summary>
    /// When this runner last beat the control plane, or null if it has not.
    /// </summary>
    /// <remarks>
    /// <b>A SECOND SILENCE, and telling the two apart is the point.</b>
    /// <see cref="At"/> says this channel answered; this says the machine is
    /// still talking to the control plane. A runner reachable over a peer
    /// connection while partitioned from the control plane reads as healthy on
    /// the first and stopped on the second - and only one of those is going to
    /// be given work, which is the thing a person watching wants to know before
    /// they wait any longer.
    /// </remarks>
    public DateTimeOffset? BeatAt { get; init; }
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

    public ConfiguredCredential? Configured { get; init; }

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

            // STRIPPED LIKE EVERY OTHER STRING OFF A MACHINE. A flight number
            // is the control plane's own, but it reaches here by way of a
            // runner, and the rule this method exists for is that nothing
            // crosses unstripped rather than that some things are trusted.
            FlightNumber = Status.FlightNumber is null
                ? null
                : ControlText.Strip(Status.FlightNumber),
            BeatAt = Status.BeatAt,
        },
        // THE LOCATOR IS A LINE, so it is stripped like one. It is a value a
        // console sent and a runner sent back, and an arm added to the envelope
        // and forgotten here is an escape sequence riding home through the one
        // artifact everybody was told is safe.
        Configured = Configured is null ? null : new ConfiguredCredential
        {
            Locator = ControlText.Strip(Configured.Locator, allowLineBreaks: false),
            Written = Configured.Written,
        },
    };
}
