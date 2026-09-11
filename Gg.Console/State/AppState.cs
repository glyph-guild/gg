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
    /// Everything known about one flight: what it is, and what happened to it.
    /// </summary>
    /// <remarks>
    /// A modal rather than a tab of its own, because looking into one flight is
    /// a question with an answer and a way out - where a tab would be a
    /// permanent thing on the bar that is only ever about whatever was last
    /// selected.
    /// </remarks>
    FlightDetail,

    /// <summary>
    /// What the runner on this machine is doing, what it has said, and the two
    /// things that can be done to it.
    /// </summary>
    /// <remarks>
    /// <b>A modal because the answer does not fit on the activity line.</b> The
    /// starting sentence carried a path and lost it off the right edge, and
    /// watching a runner come up is a thing somebody does for a few seconds
    /// rather than a receipt they glance at.
    /// </remarks>
    Runner,

    /// <summary>
    /// Why a flight was not flown by hand, and what to do about it.
    /// </summary>
    /// <remarks>
    /// <b>A modal because the answer is three sentences and the activity line is
    /// one.</b> The refusal names a requirement and then a remedy, and the
    /// remedy - the half somebody can act on - ran off the right edge of the
    /// screen. Only for the outcomes where nothing was created: a flight that
    /// was flown by hand was watched by the person who flew it.
    /// </remarks>
    HandFlight,

    /// <summary>
    /// A work item that already has a flight, and whether to open a second.
    /// </summary>
    /// <remarks>
    /// A modal because the answer must be given before anything is opened, and
    /// because the question has to name which item it is about - a person who
    /// scrolled while reading it would otherwise answer about a different row.
    /// </remarks>
    ConfirmFlight,

    /// <summary>Asking whether to ground the flight on the screen.</summary>
    /// <remarks>
    /// <b>Because `x` used to take the terminal away on one keypress.</b> It
    /// ended the session and handed the screen to <c>$EDITOR</c> to ask for a
    /// reason, so the only way back out of a mistyped one was to write nothing
    /// and read the refusal. A prompt asks WHAT; this asks WHETHER.
    /// </remarks>
    ConfirmGround,

    /// <summary>Asking whether to apply the working copy.</summary>
    /// <remarks>
    /// <b>The only act on the Envelope tab that asks.</b> Pull writes files and
    /// git guards it; apply submits one amendment flight per changed document,
    /// each taking a number and an attribution, and there is no key that
    /// unopens one. So the question is asked for the same reason
    /// <see cref="ConfirmGround"/>'s is, about a heavier act - and the body
    /// names the changeset in the order it will land, because what a person
    /// agrees to is a sequence.
    /// </remarks>
    ConfirmApply,

    /// <summary>Typing where the airspace is.</summary>
    /// <remarks>
    /// <b>A mode, because a field that accepts keystrokes owns the
    /// keyboard.</b> This console measured that once already - TableView's
    /// type-to-search ate all twenty-one keys the moment rows arrived, which
    /// is why QuietTable exists - so an always-focused field on the airspace
    /// tab would eat p, s, m, j and k. Here the letters are meant for the
    /// field: `p` inside a path is a character rather than a pull, and the
    /// only two keys the keymap answers are the two a person needs.
    /// </remarks>
    AirspacePath,

    /// <summary>
    /// The rules in force, read as a document.
    /// </summary>
    /// <remarks>
    /// <b>A MODAL RATHER THAN THE TAB IT USED TO FILL.</b> The composed
    /// envelope is a REPORT on the documents, and it sat on the pane those
    /// documents needed — so the tab answered "what governs this tenant" and
    /// had no room to answer "what is in my working copy". It is a document
    /// and it scrolls, which a label cannot: <c>UiMode.Help</c> is declared a
    /// document and its content is clipped, and nothing in this console
    /// scrolls a label.
    /// </remarks>
    ReadingEnvelope,

    /// <summary>
    /// What the working copy would change, in the order apply will run it.
    /// </summary>
    /// <remarks>
    /// <b>THE SECOND VIEW OF ONE MODAL, and a mode rather than a flag because
    /// <c>AppState.Mode</c> is what describes the screen.</b> It costs no
    /// Normal-mode letter: <c>d</c> is <c>decide</c> out there and a
    /// tab-scoped one would never fire, since <c>Keymap.Resolve</c> answers
    /// the first match — so the switch lives inside the modal, where the
    /// letters are free.
    /// <para>
    /// Reachable from <see cref="ReadingEnvelope"/> and back, because
    /// comparing what governs against what you are about to change is why
    /// both are here — <c>HostedBar</c>'s own argument for <c>e</c> and
    /// <c>i</c>.
    /// </para>
    /// </remarks>
    ReadingChangeset,

    /// <summary>
    /// What the last apply came to, one line per document.
    /// </summary>
    /// <remarks>
    /// <b>A MODAL BECAUSE THE ROW LOST THE REMEDY.</b> The apply's outcome went
    /// to the activity slot, which is one row: a refused apply read "Nothing
    /// was applied:" and the reason - the whole of what somebody does next -
    /// ran off the right edge. Measured in the world twice over, on an apply
    /// refused for an undeclared name.
    /// <para>
    /// The third view of the reading modal, and it opens ITSELF, for
    /// <see cref="HandFlight"/>'s reason: somebody who just pressed `y` is owed
    /// the answer without discovering a second keystroke.
    /// </para>
    /// </remarks>
    ReadingOutcome,

    /// <summary>Asking whether to open a new flight on this one's intent.</summary>
    /// <remarks>
    /// One flight opened by accident is a record somebody has to explain and a
    /// number that is now taken — <c>ConsoleHandFlight</c>'s reason, and the
    /// same reason <see cref="ConfirmFlight"/> exists.
    /// </remarks>
    ConfirmFlyAgain,

    /// <summary>
    /// Answering a gate: what is being decided, the evidence, and both answers.
    /// </summary>
    /// <remarks>
    /// A modal of its own rather than an item on the actions menu, because what is being
    /// decided has to be stated, the evidence has to be in front of the person, and both
    /// answers have to be offered together - none of which a menu item can do.
    /// </remarks>
    GateDecision,

    /// <summary>
    /// Nobody is signed in on this machine, and what to do about it.
    /// </summary>
    /// <remarks>
    /// <b>The only modal the LOADER opens rather than a key.</b> Every other one
    /// answers a question somebody asked; this one states the reason the console
    /// behind it is empty, which is not a thing a person can press for. It is
    /// also the reason the console is worth drawing at all - without a session
    /// every pane is a blank with a sentence under it.
    /// <para>
    /// It owns the keyboard like the rest, and escaping is a real answer: a
    /// person who wants to look at an empty console is allowed to.
    /// </para>
    /// </remarks>
    SignIn,

    /// <summary>
    /// Which way to compose the flight somebody just asked for: their editor,
    /// or an agent.
    /// </summary>
    /// <remarks>
    /// <b>A modal because there is no honest default.</b> An editor is what
    /// somebody who already knows what they want wants; an agent is worth
    /// hosting because composing an intent from nothing is the hard part. Which
    /// one a person needs depends on the flight rather than on the person, so a
    /// remembered default would be wrong about half the time and say nothing
    /// about it.
    /// <para>
    /// It holds no I/O at all, which is why it can be open inside a UI session
    /// while both things it leads to happen outside one.
    /// </para>
    /// </remarks>
    ComposeChoice,

    /// <summary>
    /// How much of your own allowance to keep back.
    /// </summary>
    /// <remarks>
    /// <b>A few shares and a key each, because nothing here is written by
    /// typing.</b> <c>ComposeChoice</c>'s shape applied to a number: an
    /// arbitrary percentage is <c>gg allowances floor</c>'s job, and what a
    /// console is for is the decision somebody makes while looking at a fleet.
    /// </remarks>
    FloorChoice,
}

/// <summary>Which page of help a person is reading.</summary>
/// <remarks>
/// Two pages rather than one longer one: "what can I press" and "what is this
/// machine configured to do" are different questions, and a person asking the
/// second is usually debugging something the first cannot explain.
/// </remarks>
/// <summary>What the open compose question is about.</summary>
/// <remarks>
/// <para>
/// <b>The QUESTION is state; the ANSWER is not.</b> Which way somebody chose
/// travels as a command and is gone the moment it is handled, which is what
/// stops a choice made for one flight deciding the next. Which question is being
/// asked has to survive until it is answered, because it is on the screen — and
/// the two answers are the same two keys on both paths, so this is the only
/// thing that tells the loop what to do with them.
/// </para>
/// <para>
/// <see cref="Nothing"/> is the state with no question open, and it is what the
/// escape hatch restores: a question left open behind a closed modal is one the
/// next keypress could answer by accident.
/// </para>
/// </remarks>
public enum ComposingFor
{
    /// <summary>No question is open.</summary>
    Nothing,

    /// <summary>A flight opened from nothing — the <c>n</c> key.</summary>
    NewFlight,

    /// <summary>A flight somebody is about to fly themselves — the <c>y</c> key.</summary>
    HandFlight,

    /// <summary>
    /// A work item picked in the browser — the <c>f</c> key, which never asks.
    /// </summary>
    /// <remarks>
    /// <b>Here so the reason can be said out loud, not because a question is
    /// asked.</b> Flying a work item sends a provider and an id: the item IS the
    /// intent, and there is no text for either composer to write. Offering the
    /// choice would mean replacing the ticket with prose, which throws away the
    /// link back to the item that flying from the browser exists for.
    /// </remarks>
    WorkItem,
}

public enum HelpPage
{
    /// <summary>The keys. What help has always been for, so it opens here.</summary>
    Keys,

    /// <summary>The environment variables, and what each decides.</summary>
    Environment,
}


/// <summary>
/// One view that can have the screen.
/// </summary>
/// <remarks>
/// <para>
/// <b>WAS <c>PaneId</c>, AND WAS FOUR OF THE SEVEN.</b> Six views shared one
/// region of the right-hand side and kept out of each other's way by turning
/// each other off, so the model needed six flags and a rule about which of
/// them could be on. A view takes the whole screen now, exactly one is drawn,
/// and this is the field that says which - so the flags go back to meaning
/// what they say: this view is open.
/// </para>
/// <para>
/// <b>The declaration order is the order of the bar.</b> Queue first because
/// it is what a console opens on and the one tab that cannot be closed;
/// evidence next because it is the one about the flight the queue selected.
/// </para>
/// </remarks>
/// <summary>
/// The runner process this console started, as far as this console knows.
/// </summary>
/// <remarks>
/// <para>
/// <b>Numbers and lines, because the model is written to disk.</b> A
/// <c>Process</c> handle is unserializable and is a live resource in a
/// document; what crosses is a pid, an exit code and what the log said. The
/// handle itself is the composition root's, like the reader sessions'.
/// </para>
/// <para>
/// <b>Absent means this console did not start one.</b> A runner started by
/// hand in another terminal is a runner the fleet knows about and this does
/// not, which is exactly right: the two things it offers are stopping and
/// restarting the child it holds.
/// </para>
/// </remarks>
public sealed record RunnerHere
{
    /// <summary>The child, while it is running.</summary>
    public int? Pid { get; init; }

    /// <summary>What it exited with, once it has.</summary>
    public int? Exit { get; init; }

    /// <summary>Where its output goes, so the modal can say where to look.</summary>
    public string LogPath { get; init; } = "";

    /// <summary>The tail of that log, newest last.</summary>
    public IReadOnlyList<string> Log
    {
        get => field ?? [];
        init;
    } = [];

    /// <summary>Whether the child this console started is still up.</summary>
    public bool Up => Pid is not null && Exit is null;
}

/// <summary>
/// Where the automatic refresh has got to.
/// </summary>
/// <remarks>
/// <b>Three small facts rather than a clock.</b> What the hint line needs is
/// whether one is happening and how long until the next; what the tick needs is
/// whether somebody asked for one. A time in here would be a time the model
/// carries into a dump and compares against a different now.
/// </remarks>
public sealed record RefreshState
{
    /// <summary>Whether a read is in the air.</summary>
    public bool Busy { get; init; }

    /// <summary>Seconds until the next one, when none is.</summary>
    public int NextIn { get; init; }

    /// <summary>Whether somebody pressed the key and it has not been done yet.</summary>
    public bool Wanted { get; init; }
}

/// <summary>Which half of the flight modal is showing.</summary>
/// <remarks>
/// <b>Two, and the second is why there is an enum at all.</b> A bool would say
/// "evidence is showing" and read as a visibility flag like the pane ones this
/// console has been unpicking; this says which of a set has the body, which is
/// what TabId says one level up and what a third tab would need.
/// </remarks>
public enum FlightTab
{
    /// <summary>The intent, the scalars and the log - what the key was pressed for.</summary>
    Details,

    /// <summary>
    /// The decision waiting on this flight, and the case made for it.
    /// </summary>
    /// <remarks>
    /// Named for the GATE rather than for the evidence, because the gate is
    /// the thing that is either there or not: an obligation declares what its
    /// decision requires and may declare nothing, so a gate can open with no
    /// evidence at all. Naming the tab after the optional half made its empty
    /// sentence read as "no exhibits were filed" instead of "nothing is
    /// waiting on you".
    /// </remarks>
    Gate,
}

public enum TabId
{
    /// <summary>Flights needing me, and the detail of the selected one.</summary>
    /// <remarks>
    /// Two panes and one tab, deliberately: the flight detail is what the
    /// selected row MEANS, and a person moving the cursor is reading both.
    /// </remarks>
    Queue,

    /// <summary>
    /// Every flight this tenant has recently, needed or not.
    /// </summary>
    /// <remarks>
    /// <b>OPEN BEFORE ANYBODY ASKS, like the queue.</b> A flight whose loop
    /// asked a question the envelope never turned into a gate lands, needs
    /// nobody, and was invisible - the queue was telling the truth and a person
    /// still could not find what they had just started. A view you have to
    /// learn a key to reach is one somebody in that position does not reach.
    /// </remarks>
    Flights,

    /// <summary>
    /// The fleet, with this machine's runner first.
    /// </summary>
    /// <remarks>
    /// <b>OPEN BEFORE ANYBODY ASKS, like the queue and the flights.</b> The boot
    /// already fetches the runner list for the queue's stranded-runner reason,
    /// so this tab costs nothing and is never waiting on a read. "Is my runner
    /// up, and is it doing anything" is the question that comes before "why has
    /// my flight not moved".
    /// </remarks>
    Runners,


    /// <summary>The runner's normalised output. Off by default.</summary>
    Live,

    /// <summary>The tracker's work items, to fly one.</summary>
    Browse,

    /// <summary>What this tenant may fly against.</summary>
    Repositories,

    /// <summary>The envelope in force.</summary>
    Envelope,

    /// <summary>
    /// Every allowance in the fleet, and what each has left.
    /// </summary>
    /// <remarks>
    /// <b>The only tab that is not always on the bar</b>, and both gates are
    /// needed: the control plane answers with everybody's allowances only for
    /// an administrator, and the local file decides whether this console draws
    /// a pane for that answer. See <see cref="Tabs.Offered"/>.
    /// </remarks>
    Allowances,
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

    /// <summary>
    /// Which view has the screen. The queue is what a person is here for.
    /// </summary>
    /// <remarks>
    /// <b>Always a tab that is open</b>, which the reducer maintains: closing
    /// the one showing falls back to the queue, and the queue cannot be closed.
    /// A value naming a view whose flag is false would render an empty pane
    /// under a tab nobody chose, so <c>Tabs.Showing</c> answers from this and
    /// the flags together rather than from this alone.
    /// </remarks>
    public TabId ActiveTab { get; init; } = TabId.Queue;

    /// <summary>Flights needing me.</summary>
    public IReadOnlyList<QueueRow> Queue { get; init; } = [];

    /// <summary>
    /// Where the cursor is in the flights list, newest first.
    /// </summary>
    /// <remarks>
    /// Its own cursor, like the browser's and the repositories': one pair of
    /// keys over several lists, and the list that has the screen is the one
    /// they move. Indexes the list AS SHOWN - newest first - because what a
    /// person is pointing at is a row on a screen rather than a position in
    /// whatever order a request came back in.
    /// </remarks>
    public int FlightSelected { get; init; }

    /// <summary>
    /// Which entry of the open flight's log the cursor is on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>An ENTRY, never a row.</b> The entry under the cursor unwraps into as
    /// many rows as its detail needs, so a cursor kept as a row number would
    /// point at a different thing the moment anything expanded. <c>LogRow.Entry</c>
    /// is what the view maps through, in both directions.
    /// </para>
    /// <para>
    /// <b>Kept, though it belongs to a modal.</b> Everything else a modal knows
    /// is discarded when it closes, because a modal is a question with an answer
    /// and a way out. This one is not the modal's: it is where a person is in a
    /// history, it decides which entry is readable in full, and a state that
    /// could not answer "which one" is a state the view would have to answer
    /// for. <see cref="Reducer.FlightShown"/> puts it back to the top when the
    /// modal opens, so it never carries one flight's place into another's log.
    /// </para>
    /// </remarks>
    public int LogSelected { get; init; }

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
    /// Which tab the flight modal is showing.
    /// </summary>
    /// <remarks>
    /// <b>Here rather than on the widget</b>, because a UI lifetime is not
    /// where this console keeps anything: the screen is torn down and rebuilt
    /// from this record whenever the terminal has to be handed over, and a tab
    /// the widget alone remembered would go back to the first one every time.
    /// <para>
    /// Reset when a flight is OPENED rather than when the modal closes - the
    /// two differ for somebody who escapes and comes back, and what makes this
    /// right is that it is about the flight being read rather than a
    /// preference. Left alone, the next flight opens on the previous one's
    /// evidence.
    /// </para>
    /// </remarks>
    public FlightTab FlightTab { get; init; }

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

    /// <summary>
    /// Its story, exactly as `gg show` returned it.
    /// </summary>
    /// <remarks>
    /// <b>Beside the log rather than instead of it.</b> The queue still derives
    /// its rows from the logs the boot fetches, and the story answers the four
    /// questions the summary could not: which stage the flight reached, what
    /// became of it, what it waits on, and who has it right now.
    /// </remarks>
    public FlightStory? Story { get; init; }

    /// <summary>
    /// Why the selected flight is stopped, exactly as `gg why` returned it.
    /// </summary>
    /// <remarks>
    /// <b>Read for the selected row and no other, so it is null far more often
    /// than the summary beside it.</b> That is not a gap the pane papers over:
    /// an attribution names a HALT, and one flight's halt shown under another
    /// flight's name is the worst answer a console can give, because a person
    /// cannot see that it is wrong. <see cref="Reducer.Detail"/> drops it when
    /// the cursor moves and the refresh key reads it again.
    /// </remarks>
    public FlightAttribution? Attribution { get; init; }

    /// <summary>
    /// The envelope in force, exactly as `gg envelope show` returned it.
    /// </summary>
    /// <remarks>
    /// <b>Read when the pane is opened.</b> Every flight this console shows
    /// names the version of this document and the console could not show it, so
    /// a person reading `envelope  v3` in the flight pane had to leave to find
    /// out what v3 says.
    /// </remarks>
    public EnvelopeState? Envelope { get; init; }

    /// <summary>
    /// Which row of the airspace tree the cursor is on.
    /// </summary>
    /// <remarks>
    /// <b>Clamped against the ROWS rather than the documents</b>, for
    /// <c>PickRunner</c>'s reason one table over: the pane invents rows the
    /// wire list does not have — a folder is a row and no document — so the
    /// count the cursor lives inside is the projection's.
    /// </remarks>
    public int AirspaceSelected { get; init; }

    /// <summary>Whether the envelope is open as a tab.</summary>
    public bool EnvelopeVisible { get; init; }

    /// <summary>
    /// The documents the envelope was composed from, and what the working copy
    /// says about them.
    /// </summary>
    /// <remarks>
    /// <b>Beside the envelope rather than inside it</b>, because they answer
    /// different questions - what governs, and where to change it - and
    /// because the envelope is a contract type the control plane composed while
    /// this is two reads joined on this machine. Read on the same key, since
    /// somebody who wants one almost always wants the other.
    /// </remarks>
    public EstateOnThisMachine? Estate { get; init; }

    /// <summary>The fleet, exactly as `gg runners` returned it.</summary>
    public RunnerList? Runners { get; init; }

    /// <summary>
    /// What each allowance the fleet spends from has left, or null when
    /// nothing has answered.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The FLEET's, never this machine's.</b> <c>gg allowance</c> reads the
    /// transcripts on the disk it runs on; this is what every machine has
    /// reported, over the read surface, including machines this one has never
    /// seen. The two are different questions with one word in them, and the
    /// console only ever asks the second.
    /// </para>
    /// <para>
    /// <b>Null rather than empty when unread</b>, and a control plane one
    /// version behind answers nothing at all — so the runners pane renders
    /// without it rather than going blank over a column that is extra.
    /// </para>
    /// </remarks>
    public AllowanceList? Allowances { get; init; }

    /// <summary>
    /// Whether the control plane says this person administers the tenant.
    /// </summary>
    /// <remarks>
    /// <b>Given, never decided here.</b> It comes off <c>whoami</c> and is a
    /// hint about what a surface would be allowed to show — every route checks
    /// the principal itself. A console that treated this as authority would be
    /// checking a claim it was handed.
    /// </remarks>
    public bool IsAdmin { get; init; }

    /// <summary>
    /// Whether this machine's own file asked for the fleet pane.
    /// </summary>
    /// <remarks>
    /// <b>Not a permission, and that is the whole of why there are two
    /// gates.</b> This decides whether a pane is DRAWN; what it could contain
    /// is the control plane's answer. A local flag that granted visibility
    /// would be a client-side authorization check, which anybody could edit
    /// their way past.
    /// </remarks>
    public bool FleetAllowancesShown { get; init; }

    /// <summary>Whether the fleet's allowances pane is showing.</summary>
    /// <remarks>
    /// Off by default, like every read-backed pane: opening it is a round trip
    /// and a person who has not asked for one should not be made to wait for
    /// it at boot.
    /// </remarks>
    public bool AllowancesVisible { get; init; }

    /// <summary>
    /// What the last floor change said, or null when nobody has made one.
    /// </summary>
    /// <remarks>
    /// <b>A sentence rather than a flag</b>, like every other <c>Last…</c>
    /// here: the interesting answers are refusals - somebody else's allowance,
    /// no ceiling configured - and a bool cannot carry one.
    /// </remarks>
    public string? LastAllowance { get; init; }

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

    /// <summary>
    /// The live view is OFF by default, and that is a decision rather than a
    /// convenience.
    /// </summary>
    public bool LiveVisible { get; init; }

    /// <summary>
    /// The flight the live pane is drawing, when it was chosen rather than
    /// followed from the cursor.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The pane used to have exactly one binding and it was the queue
    /// cursor.</b> That is right for the queue tab - a person moving the cursor
    /// with the pane open IS watching the flight they moved to - and wrong for
    /// everything else, because the queue is a queue of PROBLEMS: awaiting a
    /// decision, a lease expired twice, a runner gone. A flight that is simply
    /// flying is in none of them.
    /// </para>
    /// <para>
    /// <b>Which made watching a fleet runner draw nothing.</b> GG-77 was
    /// started, watched, connected, and the pane had nowhere to point - the
    /// output was arriving in a buffer nothing was reading. Named here rather
    /// than faked by adding a row to the queue, because a flying flight in a
    /// queue of decisions is a lie about what the queue is.
    /// </para>
    /// <para>
    /// Null is the ordinary state and means "follow the cursor", so every pane
    /// that worked before works unchanged.
    /// </para>
    /// </remarks>
    public string? WatchedFlightId { get; init; }

    /// <summary>
    /// Whether a read this console asked for has not come back yet.
    /// </summary>
    /// <remarks>
    /// <b>So an absence can say which absence it is.</b> Opening a flight used
    /// to end the session, make one request and build a new session over the
    /// answer — a whole screen taken away and given back to fetch a log. It
    /// opens immediately now and the answer is folded when it lands, which
    /// leaves an instant where nothing has been read; without this the modal
    /// says "No story was fetched for this flight", which is a statement of
    /// fact and false while one is being fetched.
    /// </remarks>
    public bool ReadInFlight { get; init; }

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
    /// One region, one pane: turning this on turns <see cref="LiveVisible"/>
    /// off, because two visible flags over one region is two panes drawn on
    /// top of each other.
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

    /// <summary>
    /// A device authorization waiting on a person, or null.
    /// </summary>
    /// <remarks>
    /// <b>Null is the ordinary state, and it means "nothing has been
    /// started".</b> It is also what makes the sign-in key mean two things: the
    /// loop asks the control plane for a code when this is null and waits on the
    /// one already showing when it is not. Cleared the moment either half of
    /// that resolves — a code that has been used, or has expired, is a code
    /// nobody should still be reading off a screen.
    /// <para>
    /// It holds no polling handle. See <see cref="PendingSignIn"/>: that value
    /// is a credential and this record is written to disk.
    /// </para>
    /// </remarks>
    public PendingSignIn? SignIn { get; init; }

    /// <summary>What became of the last attempt to sign in, or null.</summary>
    /// <remarks>
    /// Both outcomes, like <see cref="LastHandFlight"/>: signed in and did not
    /// are one question answered, and the modal reads one line either way.
    /// </remarks>
    public string? LastSignIn { get; init; }

    public int BrowseSelected { get; init; }

    public bool BrowseVisible { get; init; }

    public BrowseListing? Browse { get; init; }

    /// <summary>What this tenant can fly against, or null if never asked.</summary>
    /// <remarks>
    /// Null and empty are different answers, the distinction
    /// <see cref="Browse"/> already draws: never asked versus asked and told
    /// none.
    /// </remarks>
    public Gg.Contracts.RegisteredRepositories? Repositories { get; init; }

    /// <summary>Whether the repositories pane has the region.</summary>
    public bool RepositoriesVisible { get; init; }

    /// <summary>Which repository row the cursor is on.</summary>
    /// <remarks>
    /// A third cursor, because there are three lists. Sharing one would move a
    /// person's place in a list they were not looking at.
    /// </remarks>
    public int RepositorySelected { get; init; }

    /// <summary>
    /// The repository every flight this console opens will name, or null.
    /// </summary>
    /// <remarks>
    /// <b>Null is the ordinary state</b> and means the envelope resolves it,
    /// which is what every flight does today. This is an override, so it is
    /// announced in the activity line rather than living only inside a pane:
    /// invisible state that changes what a write does is the worst kind.
    /// </remarks>
    public string? ChosenRepository { get; init; }

    /// <summary>
    /// The environment variables this program reads, and what they decide.
    /// </summary>
    /// <remarks>
    /// <b>Given, never gathered.</b> The composition root reads the environment
    /// once and hands the answer over; a session that read it again could reach
    /// a different one, which is the rule <c>ExecutorConfiguration</c> already
    /// states. An empty list therefore means <i>nobody told this console</i> and
    /// not <i>nothing is set</i> — a test host builds no list.
    /// </remarks>
    public IReadOnlyList<Gg.Local.EnvironmentSetting> Settings { get; init; } = [];

    /// <summary>Which page of the help modal is showing.</summary>
    public HelpPage HelpPage { get; init; } = HelpPage.Keys;

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

    /// <summary>
    /// The id of the principal this console is signed in as, or empty when
    /// nobody is.
    /// </summary>
    /// <remarks>
    /// <b>Fetched already.</b> The boot asks the control plane who it is for
    /// the notices row; the id has been arriving in that same answer since the
    /// verb existed and being dropped on the floor. Nothing here costs a
    /// request.
    /// <para>
    /// <b>The id, not <see cref="Principal"/>.</b> That is a display name -
    /// text a person chose, which two can share and either can change - and it
    /// is for reading. This is for comparing, and the only thing it is compared
    /// against is what a runner says about who registered it.
    /// </para>
    /// </remarks>
    public string PrincipalId { get; init; } = "";

    /// <summary>
    /// Which runner in the fleet is this machine's, or null when none is
    /// registered here.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The id, and only the id.</b> <c>StoredRunner</c> beside it on disk
    /// holds a runner token; this model is serialized under
    /// <c>GG_STATE_DUMP</c> and handed to the diagnostics bundle, so a secret
    /// here is a secret in a bug report. The id is all that is needed to say
    /// which row in the fleet is the one a person sitting here can do something
    /// about.
    /// </para>
    /// <para>
    /// <b>Passed rather than loaded, for <see cref="Principal"/>'s reason.</b>
    /// Every public read on <c>ConsoleData</c> returns a <c>VerbResult</c>, so
    /// what a pane shows is what <c>--json</c> would print. This is not a read:
    /// it is a file this machine already wrote, and routing it through the data
    /// layer would make it the one value the console could show and a verb
    /// could not.
    /// </para>
    /// </remarks>
    public string? LocalRunnerId { get; init; }

    /// <summary>
    /// What this machine is called, or null when nobody said.
    /// </summary>
    /// <remarks>
    /// <b>Passed rather than read, for <see cref="LocalRunnerId"/>'s reason and
    /// one more.</b> <c>Rows</c> is pure, and a call to
    /// <c>Environment.MachineName</c> inside it would make the fleet's order
    /// depend on the machine a test happens to run on. It is also the value a
    /// runner registers as its label, so this is the join between the fleet the
    /// control plane returns and the person sitting in front of it.
    /// </remarks>
    public string? Machine { get; init; }

    /// <summary>
    /// The runner process this console started, or null if it started none.
    /// </summary>
    public RunnerHere? Here { get; init; }

    /// <summary>Where the automatic refresh has got to.</summary>
    public RefreshState Refresh { get; init; } = new();

    /// <summary>Which row of the runners table the cursor is on.</summary>
    /// <remarks>
    /// <b>The model owns it, like the other three tables.</b> The widget will
    /// happily keep a cursor of its own, and a render that assigns one from a
    /// constant puts it back at the top under the person using it.
    /// </remarks>
    public int RunnerSelected { get; init; }

    /// <summary>What the last hand-back ended with, for the pane to say.</summary>
    public string? LastHandBack { get; init; }

    /// <summary>
    /// What became of the last attempt to fly a flight by hand, or null.
    /// </summary>
    /// <remarks>
    /// <b>Both outcomes land here, and that is deliberate.</b> A refusal and a
    /// flight that flew are the same question answered, and a person reads one
    /// line either way. Two fields would mean a pane deciding which to show and
    /// getting it wrong when both are set from different sessions.
    /// <para>
    /// <b>A string, because the model is written to disk.</b> <c>AppState</c> is
    /// serialized under <c>GG_STATE_DUMP</c> and handed to the diagnostics
    /// bundle, so nothing on it may be a process handle or anything else that
    /// does not survive a round trip.
    /// </para>
    /// </remarks>
    public string? LastHandFlight { get; init; }

    /// <summary>
    /// Why the last hand-flight created nothing, or null when it flew.
    /// </summary>
    /// <remarks>
    /// <b>Carried rather than read back out of the sentence.</b> The loop opens
    /// a modal over a refusal and not over a success, and deciding which by
    /// looking for a phrase in <see cref="LastHandFlight"/> would make the
    /// wording load-bearing - and the wording is the part most likely to be
    /// improved by somebody who does not know that.
    /// </remarks>
    public string? HandFlightProblem { get; init; }

    /// <summary>
    /// What the last apply came to, in the lines a modal shows.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Lines rather than a string, because the join was the defect.</b>
    /// <c>ConsoleApply</c> used to compose one row; the report is per document
    /// by construction - one flight each - and a refusal names paths a line at
    /// a time.
    /// </para>
    /// <para>
    /// <b>NOT A DOCUMENT BODY.</b> This type is written to
    /// <c>GG_STATE_DUMP</c> and handed to the diagnostics bundle, so what
    /// rides here is what the control plane SAID about an apply - versions,
    /// flight numbers, approvers, refusals - and never the text of a governance
    /// document. <see cref="EstateOnThisMachine"/> holds the same line.
    /// </para>
    /// <para>
    /// <b>Nullable, and null is the meaning.</b> No apply has happened in this
    /// console's life, so there is nothing to open a modal over - which is what
    /// <c>Reducer.ApplyAnswered</c> reads. An init-only collection would
    /// deserialise to null on an absent key anyway; here that IS the state, so
    /// nothing is lost.
    /// </para>
    /// </remarks>
    public IReadOnlyList<string>? ApplyOutcome { get; init; }

    /// <summary>What became of the last flight this console grounded, or null.</summary>
    /// <remarks>
    /// Its own field rather than sharing one, for <c>Said</c>'s reason: each arm
    /// records its outcome in its own slot and the sentence a person reads is
    /// whichever changed, so a new arm cannot forget to say anything.
    /// </remarks>
    public string? LastGrounded { get; init; }

    /// <summary>What became of the last attempt to start a runner here, or null.</summary>
    /// <remarks>
    /// Its own field rather than sharing one, for <c>Said</c>'s reason: each arm
    /// records its outcome in its own slot and the sentence a person reads is
    /// whichever changed, so a new arm cannot forget to say anything.
    /// </remarks>
    public string? LastRunner { get; init; }

    /// <summary>What came of the last gate this console answered.</summary>
    /// <remarks>
    /// The sentence a person reads after pressing the key, and the only thing the
    /// console keeps: what the gate BECAME is the control plane's answer, arriving
    /// on the next load.
    /// </remarks>
    public string? LastDecision { get; init; }

    /// <summary>What came of the last flight this console opened.</summary>
    public string? LastFlightOpened { get; init; }

    /// <summary>What the open compose question is about, if one is open.</summary>
    /// <remarks>
    /// Meaningful only while <see cref="Mode"/> is
    /// <see cref="UiMode.ComposeChoice"/>. It is set when the question opens and
    /// cleared when it closes, however it closes.
    /// </remarks>
    public ComposingFor ComposingFor { get; init; }


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

    /// <summary>What the last configuration edit did, or why it did nothing.</summary>
    public string? LastConfiguration { get; init; }

    /// <summary>What the last pull came to.</summary>
    /// <remarks>
    /// <b>Its own slot rather than the configuration's.</b> The activity line
    /// is derived by comparing these fields and taking whichever moved, so a
    /// pull written into <see cref="LastConfiguration"/> would report itself as
    /// a configuration change - and the two are edited from different panes by
    /// different keys.
    /// </remarks>
    public string? LastEstate { get; init; }

    /// <summary>
    /// The path typed into the airspace field, while the question is open.
    /// </summary>
    /// <remarks>
    /// <b>Copied out of the widget once, at <c>enter</c>, and cleared when
    /// the question closes.</b> The field holds the in-progress text for the
    /// seconds somebody is typing, because <c>Command</c> is a parameterless
    /// enum and a per-keystroke reduce would need it to carry a string - a
    /// change to this console's central dispatch type for one field. What
    /// matters is upheld either way: the value is in the model before the
    /// session ends, and the write happens after it.
    /// <para>
    /// Cleared on close, or a cancelled edit would be applied by the next
    /// one.
    /// </para>
    /// </remarks>
    public string? AirspacePathTyped { get; init; }

    /// <summary>Where gg was launched from.</summary>
    /// <remarks>
    /// <b>A local fact, folded in with the machine's name.</b> It is offered
    /// while the airspace field is being edited, because the directory
    /// somebody is standing in is usually the one they mean - and it is in
    /// the model rather than read by the screen, or a state dump could not
    /// explain what was on it.
    /// </remarks>
    public string Cwd { get; init; } = "";

    /// <summary>
    /// What this tenant's control plane offers this machine, or null.
    /// </summary>
    /// <remarks>
    /// <b>Fetched where every other read is</b> — at boot and on a refresh,
    /// between sessions. An offer is not a local fact, and it is the only thing
    /// on the Environment page that is not: a person reading that page is
    /// comparing what somebody else proposes against the rows above it.
    /// </remarks>
    public OfferedOnThisMachine? Offered { get; init; }

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
