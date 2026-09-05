using Gg.Client;
using Gg.Contracts;

namespace Gg.Console;

/// <summary>What owns the keyboard.</summary>
/// <remarks>
/// Every value other than <see cref="Normal"/> is a MODAL: it takes the
/// keyboard while it is open and has exactly one key that gives it back. That
/// property is proven over generated key sequences rather than by example,
/// because "no modal can trap the terminal" is quantified over any and cannot
/// be established by a list.
/// </remarks>
public enum UiMode
{
    Normal,

    Help,

    /// <summary>What can be done to the selected flight.</summary>
    FlightActions,

    /// <summary>
    /// A work item that already has a flight, and whether to open a second.
    /// </summary>
    /// <remarks>
    /// A modal because the answer must be given before anything is opened, and
    /// because the question has to name which item it is about - a person who
    /// scrolled while reading it would otherwise answer about a different row.
    /// </remarks>
    ConfirmFlight,

    /// <summary>
    /// Answering a gate: what is being decided, the evidence, and both answers.
    /// </summary>
    /// <remarks>
    /// A modal of its own rather than an item on the actions menu, because what is being
    /// decided has to be stated, the evidence has to be in front of the person, and both
    /// answers have to be offered together - none of which a menu item can do.
    /// </remarks>
    GateDecision,
}

/// <summary>The four panes.</summary>
public enum PaneId
{
    /// <summary>Flights needing me. Not a flight list.</summary>
    Queue,

    /// <summary>One flight: state, pinned refs, credential identity, facts.</summary>
    Flight,

    /// <summary>The digest, rendered. On demand.</summary>
    Evidence,

    /// <summary>The runner's normalised output. Off by default.</summary>
    Live,
}

/// <summary>
/// What kind of line the runner produced.
/// </summary>
/// <remarks>
/// Typed from the start so verbosity is a DATA MODEL rather than a regex
/// applied to a screen later. Nothing produces these until the executor
/// exists; the type and its rendering exist now because they cost nothing now
/// and are a rewrite once there is output to classify.
/// </remarks>
public enum StreamLineKind
{
    /// <summary>What the agent said.</summary>
    Text,

    /// <summary>A tool call and its result.</summary>
    Tool,

    /// <summary>Unclassified output, passed through.</summary>
    Raw,

    /// <summary>Our own narration about the run.</summary>
    Meta,

    /// <summary>Environment preparation, before any work.</summary>
    Setup,
}

/// <summary>
/// Why a flight is in the queue.
/// </summary>
/// <remarks>
/// The queue's rows are FLIGHTS NEEDING ME - a queue that happens to be short,
/// not a list that will later be filtered. Every value here is a condition
/// step 3 actually produces; nothing is here in anticipation. Credential
/// resolution joins at step 5 and is deliberately absent rather than stubbed.
/// </remarks>
public enum QueueReason
{
    /// <summary>
    /// A person has to answer something before this flight can go on.
    /// </summary>
    /// <remarks>
    /// The reason this pane is a queue of DECISIONS rather than a list of flights. A
    /// flight nobody needs anything from is countable, not readable.
    /// </remarks>
    AwaitingDecision,

    /// <summary>Two expiries is a pattern, not an incident.</summary>
    LeaseExpiredTwice,

    /// <summary>A runner stopped heartbeating while holding work.</summary>
    RunnerOffline,
}

/// <summary>One row of the queue.</summary>
public sealed record QueueRow
{
    public required string FlightId { get; init; }

    /// <summary>Rendered, e.g. GG-42. What a person types.</summary>
    public required string FlightNumber { get; init; }

    public required string Name { get; init; }

    public required QueueReason Reason { get; init; }

    /// <summary>When this became true. What the default sort orders on.</summary>
    public required DateTimeOffset Since { get; init; }

    /// <summary>
    /// Decisions that arrived while this row was not selected.
    /// </summary>
    /// <remarks>
    /// A count, and a mark on the row. Never a reason to move the cursor.
    /// </remarks>
    public int UnreadArrivals { get; init; }
}

/// <summary>One line of the runner's normalised output.</summary>
/// <summary>
/// Which silence the live pane is showing.
/// </summary>
/// <remarks>
/// <b>An empty box cannot say why it is empty</b>, and the three reasons want
/// three different sentences: the pane is off, the flight has written nothing
/// because nothing is writing, and the flight is writing but the agent has not
/// spoken. A person reading the second and the third the same way concludes the
/// feature is broken.
/// </remarks>
public enum LiveSilence
{
    /// <summary>The pane is off, or nothing is selected.</summary>
    NotAttached,

    /// <summary>No live view exists for this flight.</summary>
    NotStarted,

    /// <summary>There is a view and it holds nothing yet.</summary>
    NothingYet,

    /// <summary>Lines have arrived; there is no silence to explain.</summary>
    Speaking,

    /// <summary>
    /// The tail stopped. Something went wrong reading, and it is said out loud.
    /// </summary>
    /// <remarks>
    /// A reader that died quietly looks exactly like a flight that went quiet,
    /// and those want opposite reactions from a person.
    /// </remarks>
    Stopped,
}

public sealed record StreamLine
{
    public required StreamLineKind Kind { get; init; }

    public required string Text { get; init; }

    public required DateTimeOffset At { get; init; }
}

/// <summary>
/// Whether a person watched a flight run, and how often.
/// </summary>
/// <remarks>
/// <para>
/// The live view is a trust artifact that should decay with familiarity, so
/// attach rate is a number we want to FALL. Slice one is the only honest
/// moment to baseline it - measured after we have been impressed by the live
/// view, it measures the wrong thing.
/// </para>
/// <para>
/// Recorded as a fact on the flight, on the paths that already exist. NOT as
/// telemetry: this is the first thing that wanted a metric and it arrives
/// immediately after the control plane was found exporting logs to a third
/// party through an ambient variable. It is exported nowhere.
/// </para>
/// </remarks>
public sealed record LiveAttachFact
{
    public required string FlightId { get; init; }

    /// <summary>Whether the live view is attached right now.</summary>
    public required bool Attached { get; init; }

    /// <summary>How many times it has been attached to this flight.</summary>
    public required int AttachCount { get; init; }
}

/// <summary>
/// The model. Plain data only.
/// </summary>
/// <remarks>
/// <para>
/// It must round-trip through JSON unchanged, because the UI is torn down and
/// rebuilt FROM this state and views are never the source of truth. Every
/// non-serializable handle lives on a controller outside this record - a
/// handle in here is a session that cannot be torn down, which is the one
/// thing terminal release cannot survive.
/// </para>
/// <para>
/// The flight-shaped fields are the CONTRACT types the verbs return, not
/// shapes invented here. That is what makes "every verb has a console
/// equivalent and both render the same structured result" true by
/// construction: a pane cannot render anything a verb does not return,
/// because there is nowhere for it to have come from.
/// </para>
/// </remarks>
public sealed record AppState
{
    public UiMode Mode { get; init; } = UiMode.Normal;

    /// <summary>The queue is what a person is here for.</summary>
    public PaneId FocusedPane { get; init; } = PaneId.Queue;

    /// <summary>Flights needing me.</summary>
    public IReadOnlyList<QueueRow> Queue { get; init; } = [];

    /// <summary>
    /// What the control plane says is degraded, exactly as it said it.
    /// </summary>
    /// <remarks>
    /// The failure the queue hides by construction: when check runs stop being
    /// written, every flight still runs, still records its facts and still
    /// leaves the queue. Nothing needs anybody, and a pull request somewhere
    /// quietly has no check on it - so the queue is at its most reassuring
    /// exactly when this is worst.
    /// </remarks>
    public IReadOnlyList<TenantNotice> Notices { get; init; } = [];

    public int SelectedRow { get; init; }

    /// <summary>
    /// The case a gate is putting to this person, exactly as `gg gates` returned it.
    /// </summary>
    /// <remarks>
    /// <b>Fetched, never assembled here.</b> A console that built its own case would be
    /// deciding what a person is shown, and the envelope already decided that. Null when
    /// nothing is waiting - said in the pane rather than rendered as a blank.
    /// </remarks>
    public GateEvidencePayload? Payload { get; init; }

    /// <summary>
    /// What is waiting on a person, exactly as `gg gates` returned it.
    /// </summary>
    /// <remarks>
    /// <b>The list, and the selected row picks one out of it.</b> Storing the single
    /// gate instead would make the model depend on which row the cursor was on when
    /// it was fetched, and moving the cursor would then need a round trip.
    /// <para>
    /// Without this the gate modal had the evidence and not the QUESTION: no
    /// obligation id, so nothing could be answered even once the keys reached the
    /// shell. It is what made ApproveGate a dead key rather than an unwired one.
    /// </para>
    /// </remarks>
    public GateList? Gates { get; init; }

    /// <summary>
    /// Every flight this tenant has, exactly as `gg flights` returned them.
    /// </summary>
    /// <remarks>
    /// <b>What makes an arrow key free.</b> The detail under the selected row
    /// comes from here rather than from a request, so moving the selection is a
    /// reducer step and nothing else - rule 3, no I/O inside a UI session. The
    /// boot already fetched this list to derive the queue and then held only the
    /// queue; keeping it costs no request at all.
    /// </remarks>
    public FlightList? Flights { get; init; }

    /// <summary>
    /// Each flight's log, keyed by flight id, exactly as `gg log` returned them.
    /// </summary>
    /// <remarks>
    /// <b>The N requests the boot already pays for.</b> It fetches a log per
    /// flight to find the ones whose lease expired twice, and threw every one of
    /// them away. Holding them is what makes the log pane cost nothing and the
    /// selection stay free.
    /// </remarks>
    public IReadOnlyDictionary<string, FlightLog> Logs { get; init; } =
        new Dictionary<string, FlightLog>(StringComparer.Ordinal);

    /// <summary>The selected flight, exactly as `gg show` returned it.</summary>
    public FlightSummary? Flight { get; init; }

    /// <summary>Its log, exactly as `gg log` returned it.</summary>
    public FlightLog? FlightLog { get; init; }

    /// <summary>The fleet, exactly as `gg runners` returned it.</summary>
    public RunnerList? Runners { get; init; }

    /// <summary>
    /// The credential references, exactly as `gg credential list` returned them.
    /// </summary>
    /// <remarks>
    /// Safe to hold in a serializable model precisely because it holds no
    /// secret: kind, locator, identity, scopes. The state is written to disk by
    /// the state-dump hook and survives a terminal release, and neither of
    /// those would be acceptable for anything else.
    /// </remarks>
    public CredentialList? Credentials { get; init; }

    /// <summary>Evidence is on demand.</summary>
    public bool EvidenceVisible { get; init; }

    /// <summary>
    /// The live view is OFF by default, and that is a decision rather than a
    /// convenience.
    /// </summary>
    public bool LiveVisible { get; init; }

    /// <summary>Held still so text can be selected.</summary>
    public bool Frozen { get; init; }

    /// <summary>What the live pane shows.</summary>
    public IReadOnlyList<StreamLine> Live { get; init; } = [];

    /// <summary>What arrived during a freeze, kept rather than dropped.</summary>
    public IReadOnlyList<StreamLine> Held { get; init; } = [];

    /// <summary>Whether each flight was watched. Exported nowhere.</summary>
    public IReadOnlyList<LiveAttachFact> AttachFacts { get; init; } = [];

    /// <summary>Which silence the live pane is showing, when it is showing one.</summary>
    public LiveSilence Silence { get; init; } = LiveSilence.NotAttached;

    /// <summary>
    /// The work a tracker offered to pick from, or why it offered none.
    /// </summary>
    /// <remarks>
    /// <b>Null is "no reader was ever asked"</b>, which is a different sentence
    /// from a reader that answered nothing - the same distinction
    /// <see cref="Silence"/> draws for the live view, and for the same reason:
    /// an empty box cannot say which of them it is showing.
    /// </remarks>
    /// <summary>Whether the browse pane has the region.</summary>
    /// <remarks>
    /// One region, one pane: turning this on turns <see cref="EvidenceVisible"/>
    /// and <see cref="LiveVisible"/> off, because two visible flags over one
    /// region is two panes drawn on top of each other.
    /// </remarks>
    /// <summary>Which row of the work list is picked.</summary>
    /// <remarks>
    /// <b>Not <see cref="SelectedRow"/>, which is the queue's.</b> The queue's
    /// selection is what the flight pane hangs off; somebody scrolling a work
    /// list and returning to a different flight than they left is the confusion
    /// two indices avoid.
    /// </remarks>
    /// <summary>A flight this console has asked about but not opened.</summary>
    /// <remarks>
    /// <b>Held rather than passed</b>, because the answer arrives on a later
    /// keystroke and the model is the only thing that survives a session. Null
    /// is the ordinary state: nothing is waiting on an answer.
    /// </remarks>
    public PendingFlight? PendingFlight { get; init; }

    public int BrowseSelected { get; init; }

    public bool BrowseVisible { get; init; }

    public BrowseListing? Browse { get; init; }

    /// <summary>
    /// The held tree of the selected flight, when there is one.
    /// </summary>
    /// <remarks>
    /// Only a flight that ended without landing has one, which is exactly the
    /// flight worth taking over: a landed flight's work is on a branch somebody
    /// can fetch.
    /// </remarks>
    public string? TakeableTree { get; init; }

    /// <summary>What a person reads before taking the selected flight over.</summary>
    public TakeSeed? TakeSeed { get; init; }

    /// <summary>What the last takeover ended with, for the pane to say.</summary>
    /// <remarks>
    /// Held on the model rather than printed, because the console is rebuilt
    /// from the model after the child exits and anything printed is gone.
    /// </remarks>
    public string? LastTakeover { get; init; }

    /// <summary>How long the last takeover held the terminal.</summary>
    public TimeSpan? LastTakeoverHeld { get; init; }

    /// <summary>
    /// Whether the selected flight has been taken over and not yet handed back.
    /// </summary>
    /// <remarks>
    /// Handing back a flight nobody took is a key that does nothing.
    /// </remarks>
    public bool TakenOver { get; init; }

    /// <summary>
    /// Who this console is acting as. Attribution comes from here, never typed.
    /// </summary>
    /// <remarks>
    /// A human account is attributed under Article XII, and a name a person
    /// typed into a box is a name anybody can type.
    /// </remarks>
    public string Principal { get; init; } = "";

    /// <summary>What the last hand-back ended with, for the pane to say.</summary>
    public string? LastHandBack { get; init; }

    /// <summary>What came of the last gate this console answered.</summary>
    /// <remarks>
    /// The sentence a person reads after pressing the key, and the only thing the
    /// console keeps: what the gate BECAME is the control plane's answer, arriving
    /// on the next load.
    /// </remarks>
    public string? LastDecision { get; init; }

    /// <summary>What came of the last flight this console opened.</summary>
    public string? LastFlightOpened { get; init; }

    /// <summary>
    /// What came of the last credential this console registered.
    /// </summary>
    /// <remarks>
    /// The REFERENCE, never the value: kind, locator, identity and scopes are what
    /// crosses the wire anyway. This record is serialized to disk under
    /// <c>GG_STATE_DUMP</c> and handed to the diagnostics bundle, so a secret here
    /// would be a secret in both.
    /// </remarks>
    public string? LastCredential { get; init; }

    /// <summary>
    /// Where the last invitation link was put.
    /// </summary>
    /// <remarks>
    /// WHERE, not what. Whoever holds the link becomes a principal in this tenant,
    /// so it is a capability and belongs nowhere that is dumped or bundled.
    /// </remarks>
    public string? LastInvite { get; init; }

    /// <summary>
    /// What the last key a person pressed actually did.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>One slot, overwritten, because it is a timeline of length one.</b> The
    /// fields above record an outcome per KIND, which is what a bundle and a test
    /// want; a person watching the screen wants the most recent thing, and picking
    /// that out of six fields needs an ordering the model does not have.
    /// </para>
    /// <para>
    /// <b>It exists because the console used to do work and say nothing.</b>
    /// <c>LastTakeover</c> and <c>LastHandBack</c> were written, asserted in tests,
    /// and rendered by no view - so even once the keys worked, pressing one produced
    /// silence. A write a person cannot see is indistinguishable from a key that
    /// does nothing, which is the whole defect this change is about.
    /// </para>
    /// </remarks>
    public string? LastAction { get; init; }

    /// <summary>
    /// How often a proposal was kept, per flight. Exported nowhere.
    /// </summary>
    /// <remarks>
    /// Accept and edit are the design working; replace is everybody writing the
    /// summary after all, which is the failure it was built to avoid. Same
    /// treatment as the attach rate, and for the same reason - a number that says
    /// whether a premise held is only honest if it was there from the first
    /// flight.
    /// </remarks>
    public IReadOnlyList<HandConfirmationFact> HandConfirmations { get; init; } = [];

    /// <summary>
    /// What went wrong reaching the control plane, if anything.
    /// </summary>
    /// <remarks>
    /// In the model rather than on a screen somewhere, so a failure survives
    /// the UI being destroyed. A console that forgets why it is empty is a
    /// console that looks like it is working.
    /// </remarks>
    public string? Diagnosis { get; init; }

    /// <summary>The flight the cursor is on, or null when the queue is empty.</summary>
    /// <summary>
    /// The gate waiting on the selected flight, when one is.
    /// </summary>
    /// <remarks>
    /// Derived, so moving the cursor needs no round trip and the model holds one
    /// copy of the list rather than a copy per row.
    /// </remarks>
    public PendingGate? SelectedGate =>
        Selected is { } row && Gates is { } gates
            ? gates.Gates.FirstOrDefault(g => string.Equals(
                g.FlightNumber, row.FlightNumber, StringComparison.Ordinal))
            : null;

    public QueueRow? Selected =>
        Queue.Count == 0 ? null : Queue[Math.Clamp(SelectedRow, 0, Queue.Count - 1)];
}

/// <summary>
/// The number we want to fall.
/// </summary>
/// <remarks>
/// Flights watched, over flights seen. With no executor there is nothing to
/// watch, so the baseline should be near zero - and if it is not, the console
/// is being watched for its own sake and the defaults are not enough. That is
/// a finding worth having rather than a test to make pass.
/// </remarks>
public static class AttachRate
{
    public static double Of(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.Queue.Count == 0)
        {
            return 0d;
        }

        var attached = state.AttachFacts.Count(f => f.AttachCount > 0);
        return (double)attached / state.Queue.Count;
    }
}
