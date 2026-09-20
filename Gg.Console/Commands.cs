namespace Gg.Console;

/// <summary>
/// Everything the keymap can produce. Bindings live in the keymap; meanings
/// live here; effects live in the reducer.
/// </summary>
public enum Command
{
    Quit,
    ToggleHelp,

    /// <summary>
    /// Opens the key group under the cursor, or closes an open one.
    /// </summary>
    /// <remarks>
    /// <b>A command rather than something the tree does by itself.</b> A fold a
    /// widget opened would be invisible to the model, and the console rebuilds
    /// its views from the model every time it hands the terminal to an editor —
    /// so the fold would spring shut on the way back.
    /// </remarks>
    ToggleFold,

    /// <summary>
    /// The next value of the Look setting under the cursor.
    /// </summary>
    /// <remarks>
    /// <b>One command for seven settings, because the cursor says which.</b>
    /// A command per setting would be seven keys in a modal that has about
    /// four to spare, and the page would have to advertise all of them.
    /// </remarks>
    NextLookValue,

    /// <summary>The value before it, for walking back a step.</summary>
    /// <remarks>
    /// <b>Both directions, even though everything wraps.</b> Wrapping means one
    /// key can REACH every value; it does not make eleven steps forward a
    /// reasonable way to undo one.
    /// </remarks>
    PreviousLookValue,

    /// <summary>Put every Look setting back to what the console ships as.</summary>
    /// <remarks>
    /// <b>A spike needs a way out more than a finished feature does.</b>
    /// Somebody trying looks on will make the console unreadable at least once,
    /// and the way back must not be a walk through eleven line styles in a
    /// colour scheme they cannot see.
    /// </remarks>
    ResetLook,

    /// <summary>Turn the compose modal between its kind and its repositories.</summary>
    NextWorkKindTab,

    /// <summary>
    /// Name the repository under the cursor on the flight being composed, or
    /// stop naming it.
    /// </summary>
    /// <remarks>
    /// <b>Scoped to one flight, unlike the registry's own toggle.</b> That one
    /// says what every new flight STARTS with; this one says what this
    /// particular flight will actually fly against.
    /// </remarks>
    ToggleFlightRepository,

    /// <summary>
    /// Hold the help modal out of the way, or bring it back.
    /// </summary>
    /// <remarks>
    /// <b>The page changes what is behind the page.</b> Most of what the Look
    /// page sets is panes, borders and tabs the modal is sitting on top of, so
    /// the one thing it could not do was let somebody see their own change.
    /// Hidden rather than closed: the mode does not move, so the keyboard still
    /// belongs to the page and the same key brings it back.
    /// </remarks>
    PeekBehindTheModal,

    /// <summary>
    /// Everything known about the flight under the cursor.
    /// </summary>
    /// <remarks>
    /// Reads nothing: the flight and its log are what the boot already fetched,
    /// which is the same reason an arrow key is free. A modal because it is a
    /// question with an answer and a way out.
    /// </remarks>
    ShowFlight,

    /// <summary>What the selected flight RECORDED, rather than what it said.</summary>
    /// <remarks>
    /// <b>Reads, unlike <see cref="ShowFlight"/> beside it.</b> The flight and
    /// its log are what the boot already fetched; facts are not, because they
    /// are large and rarely wanted, so this is the press that asks. It folds in
    /// beside the console rather than ending the session - the pane is already
    /// open and the flight already on it.
    /// </remarks>
    ShowFlightFacts,

    /// <summary>What can be done to the selected flight.</summary>
    ToggleFlightActions,

    /// <summary>The one escape hatch out of whichever modal is open.</summary>
    CloseModal,

    /// <summary>Open the gate on the selected row.</summary>
    OpenGate,

    /// <summary>Answer the open gate yes. Posts; decides nothing locally.</summary>
    ApproveGate,

    /// <summary>Answer it no, with a reason. Posts; decides nothing locally.</summary>
    RejectGate,

    /// <summary>Ask what to do about the nomination under the board's cursor.</summary>
    /// <remarks>
    /// <b>The question, never one of its answers.</b> Called
    /// <c>OpenNomination</c> this would be the name of the ask AND the name of
    /// one of the two things it can produce, which is a command that means two
    /// things depending on where it is read. <see cref="OpenGate"/> has the
    /// same shape and the same reason.
    /// </remarks>
    AskToAnswerNomination,

    /// <summary>Open the row under the cursor on the board.</summary>
    /// <remarks>
    /// <b>Any row, not only one that can be answered.</b> A watch's row is
    /// machinery and has nothing to decide, and a person still needs to read
    /// how it is doing - which is the half the old key could not reach.
    /// </remarks>
    ShowBoardRow,

    /// <summary>Open the nominated work. Posts; decides nothing locally.</summary>
    /// <remarks>
    /// <b>The word the CONTRACT uses for this ending</b>, so the console is not
    /// inventing a third vocabulary for a transition the board and the verb
    /// already name. What the row becomes is an admission pass that may still
    /// refuse.
    /// </remarks>
    OpenNomination,

    /// <summary>Decline it, with a reason. Posts; decides nothing locally.</summary>
    DeclineNomination,

    /// <summary>Re-read everything the boot read.</summary>
    /// <remarks>
    /// <b>A shell command, because a read is not a session's business.</b> Rule
    /// 3: no I/O inside a UI session. The session ends, the loop reloads, and
    /// the next session renders the new model - the same terminal-release shape
    /// every write in this console already has.
    /// </remarks>
    Refresh,

    FocusNextPane,
    SelectNext,
    SelectPrevious,


    /// <summary>
    /// Moves the flight modal to its other tab.
    /// </summary>
    /// <remarks>
    /// <b>Cycles rather than naming a tab</b>, for the reason the console's own
    /// bar does: one key that always works beats two that are each wrong half
    /// the time, and a person who overshoots in a modal with no way back is
    /// stuck in it.
    /// </remarks>
    NextFlightTab,

    /// <summary>
    /// Moves the work item modal to its other tab.
    /// </summary>
    /// <remarks>
    /// <b>Its own command rather than a shared one</b>, because the two modals
    /// are never up at once and a command that meant "whichever modal is
    /// showing" would be a dispatch decided somewhere other than the reducer.
    /// The KEY is shared, which is the part a person experiences.
    /// </remarks>
    NextWorkItemTab,

    /// <summary>Attach or detach the live view. Recorded as a fact.</summary>
    ToggleLive,

    /// <summary>Hold the live view still so text can be selected.</summary>
    ToggleFreeze,

    /// <summary>
    /// Show or hide the work a tracker offers to pick from.
    /// </summary>
    /// <remarks>
    /// <b>The shell's, unlike the other view toggles.</b> Evidence and Live
    /// draw what the console already holds; this one has to ASK a reader, which
    /// means starting a child process - the one thing a UI session may not do.
    /// So the session ends, the loop reads, and the next session is rebuilt
    /// from the model. TakeFlight's shape, for a much smaller reason.
    /// </remarks>
    ToggleBrowse,

    /// <summary>
    /// What must hold before the selected flight can start.
    /// </summary>
    /// <remarks>
    /// The shell's, because showing it is a read. Same reason as
    /// <see cref="ToggleBrowse"/>, for a much smaller request.
    /// </remarks>

    /// <summary>The rules in force. The shell's, because showing it is a read.</summary>
    ToggleEnvelope,



    /// <summary>
    /// Render the estate into the working copy.
    /// </summary>
    /// <remarks>
    /// <b>A write, so the shell handles it.</b> Pull overwrites files with
    /// canonical renderings, and the exception that lets a read run on a task
    /// outside the UI lifetime is for a read. It is also the one act without
    /// which the estate pane has no working copy to describe.
    /// </remarks>
    PullEstate,

    /// <summary>Ask whether to apply the working copy.</summary>
    /// <remarks>
    /// Holds no I/O at all, which is why it can happen inside a session while
    /// the thing it asks about happens outside one - the same property
    /// <c>ComposeChoice</c> relies on.
    /// </remarks>
    AskToApplyEstate,

    /// <summary>
    /// Submit every changed document, one amendment flight each.
    /// </summary>
    /// <remarks>
    /// <b>The pen, reached from a third surface.</b> One flight, one gate, one
    /// minted version and one attribution per document, in the safe order -
    /// tightenings before widenings, so no intermediate state is looser than
    /// either endpoint.
    /// </remarks>
    ApplyEstate,

    /// <summary>
    /// Hand the terminal to an agent, in the estate's working copy.
    /// </summary>
    /// <remarks>
    /// <b>The same shape as composing a flight's intent, one document over.</b>
    /// A pty gg owns, a bar gg keeps, and a value back only by tool call. It
    /// applies nothing: a draft in a working copy is exactly as authoritative
    /// as a person typing into it, which is to say not at all.
    /// </remarks>
    DraftEstate,

    /// <summary>
    /// Say where this machine's airspace working copy is.
    /// </summary>
    /// <remarks>
    /// <b>The other half of refusing an unset airspace.</b> The read, pull
    /// and apply keys all need a path and none of them could ask for one, so
    /// the console fell back to whatever directory it was launched from -
    /// which is how `p` came to write a tree into somebody's home directory
    /// with no git to refuse it. A pane that reports a state and offers no
    /// way out of it is a dead end; this is the way out.
    /// </remarks>
    /// <summary>Read the rules in force, as a document.</summary>
    /// <remarks>
    /// <b>Holds no I/O, so a reducer may open it</b> — the envelope is already
    /// on the model by the time this can be pressed, read on the same key that
    /// opens the tab. What it opens is a view of what is already in hand,
    /// which is why it is neither the shell's nor a read.
    /// </remarks>
    ReadEnvelope,

    /// <summary>Read what the working copy would change.</summary>
    /// <remarks>
    /// <b>Holds no I/O either.</b> The diff is already on the model — the
    /// estate read fetches it on the same key that opens the tab — so this
    /// opens a view of what is in hand. Reached from inside the reading modal
    /// rather than from a key of its own, which is what keeps it off the four
    /// Normal-mode letters that are left.
    /// </remarks>
    ReadChangeset,

    /// <summary>Read the whole of what the activity line is showing part of.</summary>
    /// <remarks>
    /// <b>No I/O: the message is already on the model</b>, which is what the
    /// line is drawn from — this opens a view of the same string with room for
    /// all of it. Bound only while the line is actually clipped, because a key
    /// offered over a sentence a person can already read in full is a keypress
    /// that opens a modal saying what the screen already says.
    /// </remarks>
    ReadSaid,

    /// <summary>Read what the last apply came to.</summary>
    /// <remarks>
    /// <b>No I/O either, and no key in Normal mode.</b> The outcome is already
    /// on the model - the loop put it there when the apply returned - so this
    /// opens a view of what is in hand. It is reached from inside the reading
    /// modal, because the modal opens itself on this view when an apply
    /// finishes and the letter is only for coming back to it.
    /// </remarks>
    ReadOutcome,

    /// <summary>Put what the open modal is showing on the clipboard.</summary>
    /// <remarks>
    /// <b>The shell's, because a clipboard is a child process.</b>
    /// LiveStreamingTests grants one clipboard exception, scoped to a READ in
    /// one field by one key, and says a copy stays the shell's. The modal
    /// comes back by itself afterwards - the screen is rebuilt from AppState
    /// and Mode is part of it - so the round trip costs a redraw rather than a
    /// person's place.
    /// </remarks>
    CopyModal,

    /// <summary>
    /// Turn the airspace pane to the next view of the selected document.
    /// </summary>
    /// <remarks>
    /// <b>NO I/O AT ALL, which is the point of holding the documents.</b> The
    /// estate read already fetched every document and the walk kept every
    /// file's text, so turning the page is arithmetic over what is in hand -
    /// and moving the cursor is too. A pane that fetched per row would be a
    /// request on an arrow key, which this console does not make.
    /// </remarks>
    NextAirspaceView,

    /// <summary>Turn the runner modal to its next view.</summary>
    /// <remarks>
    /// <b>`v' here too, and deliberately the same letter.</b> It turns the pane
    /// beside a tree on the airspace tab and the pane under the fields here;
    /// one letter for one act is what a single keymap is for, and the two are
    /// in different modes so neither shadows the other.
    /// </remarks>
    NextRunnerView,

    /// <summary>
    /// Move the keyboard between the airspace tree and the document beside it.
    /// </summary>
    /// <remarks>
    /// <b>NO I/O, and no new pane either</b> - both halves are already on
    /// screen. This says which one the arrow keys drive: the tree, where they
    /// walk documents, or the document, where they scroll it. Without it the
    /// right-hand pane was a one-way door, reachable by clicking and with
    /// nothing to bring the keyboard back.
    /// </remarks>
    NextAirspacePane,

    /// <summary>Open what can be done to the airspace as a whole.</summary>
    /// <remarks>
    /// <b>A MODE CHANGE AND NOTHING ELSE</b>, like the flight actions it is
    /// modelled on. What the four keys inside it do is what they always did;
    /// this only decides whether they are on the status line or one keystroke
    /// behind it.
    /// </remarks>
    ToggleAirspaceActions,

    /// <summary>Ask whether to retire the names the tree no longer holds.</summary>
    /// <remarks>
    /// Reduced in session - it opens a question and nothing else. `x` inside
    /// the changeset view, where the names are listed: a deleted document has
    /// no tree row, so there is nowhere else to put it, and inside a modal the
    /// letter is free.
    /// </remarks>
    AskToRetire,

    /// <summary>Retire them.</summary>
    /// <remarks>
    /// <b>A request per name, so it is the loop's.</b> A session may read a
    /// local file and nothing else.
    /// </remarks>
    RetireNames,

    /// <summary>Open the field that says where the airspace is.</summary>
    /// <remarks>
    /// Holds no I/O at all, which is what lets it happen inside a session
    /// while the write it leads to happens outside one - ComposeChoice's
    /// property, and the reason a reducer may open a mode.
    /// </remarks>
    FocusAirspacePath,

    SetAirspacePath,

    /// <summary>Fill the airspace field with the directory gg was launched from.</summary>
    /// <remarks>
    /// <b>Performed on the widget rather than reduced.</b> The field holds
    /// the in-progress text - Command is a parameterless enum, so the model
    /// cannot carry a keystroke's worth of it - and the keymap still owns
    /// the binding, which is the split Dispatch already describes: the
    /// keymap says what a key MEANS, and this is what happens once
    /// something means it.
    /// </remarks>
    AirspacePathFromCwd,

    /// <summary>Fill the airspace field from the clipboard.</summary>
    /// <remarks>
    /// <b>A stated exception to the session rule.</b> A UI session may read
    /// a local file and nothing else, and every clipboard on the platforms
    /// this ships to is a child process - which is why <c>ConsoleLink</c>'s
    /// copy is the shell's. Pasting is granted anyway, because a path is
    /// most often already on the clipboard and a terminal-release round
    /// trip in the middle of editing one field would lose what is typed.
    /// Recorded in <c>LiveStreamingTests</c>, where the rule is enforced.
    /// </remarks>
    AirspacePathFromClipboard,

    /// <summary>Pick the airspace directory with a file dialog.</summary>
    /// <remarks>
    /// <b>A directory picker, not a file one.</b> An airspace is a
    /// directory, and a dialog left on its default would let somebody choose
    /// a file - the path would be written and pull would render a tree
    /// BESIDE it rather than in it.
    /// <para>
    /// A second, narrower exception to the session rule: it reads directory
    /// listings a person walks, which is more than a file whose path the
    /// console already holds. It spawns nothing, unlike the clipboard.
    /// </para>
    /// </remarks>
    AirspacePathFromDialog,

    /// <summary>Hand the configuration file to $EDITOR.</summary>
    /// <remarks>
    /// <b>A handoff, because nothing in this console is written by typing.</b>
    /// The session ends, the loop opens an editor with the terminal free, and
    /// what comes back is validated before it lands.
    /// </remarks>
    EditConfiguration,

    /// <summary>
    /// Takes the configuration this tenant's control plane is offering.
    /// </summary>
    /// <remarks>
    /// <b>A handoff for the same reason the edit above it is</b>: it writes
    /// this machine's configuration file, and a session may not write. It takes
    /// the version the Environment page SHOWED, so a control plane that changed
    /// its offer between somebody reading it and pressing the key cannot have
    /// the replacement applied by a person who never saw it.
    /// </remarks>
    TakeOfferedConfiguration,

    /// <summary>
    /// Forgets a credential this tenant holds a reference to.
    /// </summary>
    /// <remarks>
    /// The shell's, like the other writes. A store you cannot clean is a store
    /// people work around, and it is the half of credential management that
    /// matters when one leaks.
    /// </remarks>
    ForgetCredential,

    /// <summary>Open a flight for the work item the browser has selected.</summary>
    /// <remarks>
    /// The shell's, because it writes. What crosses is a provider and an id,
    /// declared - never the title a person happened to read.
    /// </remarks>
    FlyPicked,

    /// <summary>Show or hide what this tenant can fly against.</summary>
    /// <remarks>The shell's: showing them is a read, and a session may not make one.</remarks>
    ToggleRepositories,

    /// <summary>
    /// Start a runner on this machine.
    /// </summary>
    /// <remarks>
    /// <b>The shell's, because it spawns a child.</b> And offered only when
    /// there is none running here: a second runner registered from one machine
    /// is litter in the fleet, and Article XI says a key that appears to work is
    /// worse than one that is not offered.
    /// </remarks>
    StartRunner,

    /// <summary>
    /// Stop the flight this modal is about.
    /// </summary>
    /// <remarks>
    /// <b>Grounding, not withdrawing.</b> Withdrawing says the work no longer
    /// has a question to answer; this says the question is still real and a
    /// person is stopping the attempt. A flight nobody can serve is the second
    /// one, and offering only the first is how a console teaches somebody to
    /// say something untrue about why a flight ended.
    /// <para>
    /// The shell's twice over: it writes, and it asks for a reason first.
    /// </para>
    /// </remarks>
    GroundFlight,

    /// <summary>
    /// Open the verification link in a browser.
    /// </summary>
    /// <remarks>
    /// The shell's: it spawns a browser. The modal stays open behind it,
    /// because opening the page is a step on the way through this modal rather
    /// than a way out of it.
    /// </remarks>
    OpenSignInUri,

    /// <summary>
    /// Put the verification link on the clipboard.
    /// </summary>
    /// <remarks>
    /// The shell's too: the OS clipboard is reached through a child process on
    /// every platform this runs on. For the machine where a browser cannot be
    /// opened - over ssh, or a pool host - which is exactly where reading a URL
    /// across by hand is worst.
    /// </remarks>
    CopySignInUri,
    CopySignInCode,

    /// <summary>Show what the runner on this machine is doing.</summary>
    /// <remarks>
    /// Pure: what it is doing is already in the model, folded in between
    /// sessions and on the tick by the thing that reads its log.
    /// </remarks>
    ShowRunner,

    /// <summary>Stop it and start it again.</summary>
    /// <remarks>
    /// The shell's, and it is a stop followed by a start rather than a port of
    /// its own - a third way to spawn the same child is a third place for the
    /// two of them to disagree.
    /// </remarks>
    RestartRunner,

    /// <summary>Go and watch what the runner under the cursor is flying.</summary>
    /// <remarks>
    /// <b>The modal has named <c>gg runner watch</c> since slice thirty-four
    /// and offered no way to run it.</b> Reference-and-fetch is right about the
    /// ssh lines beside it - those are about somebody else's machine and gg is
    /// guessing - but this is gg's own verb against gg's own runner.
    /// <para>
    /// <b>A child, so the session ends first.</b> Reaching a runner is three
    /// calls to the control plane and a WebRTC socket, and a UI session may
    /// make none of them. This takes the slot the editor takes instead.
    /// </para>
    /// </remarks>
    WatchRunner,

    /// <summary>Put a credential on the runner under the cursor.</summary>
    /// <remarks>
    /// <para>
    /// <b>The one act this console could diagnose and not perform.</b> The queue
    /// says a flight is blocked on a credential and names the machine; the
    /// runner modal is where that machine is on the screen; and until this, the
    /// way to act on it was to quit, find the runner id again, and type a
    /// command. A console that can see something and not fix it is a console a
    /// person leaves at the moment it was useful.
    /// </para>
    /// <para>
    /// <b>The session ends first, like the editor and like watching.</b> It asks
    /// for a repository, reads a secret with the echo off, and opens a sealed
    /// channel - a prompt, a credential and three network calls, none of which a
    /// UI session may do. All of it happens with the terminal provably free.
    /// </para>
    /// <para>
    /// <b>And it is the same sender the command line uses.</b> A second
    /// implementation would be a second answer to where the secret comes from,
    /// and the two would drift on the question that matters most.
    /// </para>
    /// </remarks>
    SendCredential,

    /// <summary>Claims the machine this modal is about, or gives it up.</summary>
    ClaimRunner,

    /// <summary>Keeps it to its owner's flights, or lets it take the tenant's again.</summary>
    ReserveRunner,

    /// <summary>Shows the tenant's enrollment tokens, and reads them if nobody has.</summary>
    ShowFleetTokens,

    /// <summary>Asks whether to revoke the token under the cursor.</summary>
    AskToRevokeToken,

    /// <summary>Revokes it.</summary>
    RevokeToken,

    /// <summary>Asks whether to say, as an admin, who may claim this machine.</summary>
    AskWhoMayClaim,

    /// <summary>Says it: the tenant's if anybody had claimed it, open if nobody may.</summary>
    SetOwnership,

    /// <summary>
    /// Logs the agent in on the runner an agent-login gate names.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The act that CLEARS this gate, rather than an answer to it.</b> A
    /// runner whose agent cannot start holds, reports it, and the control
    /// plane mints a maintenance flight whose gate asks a person to sign the
    /// agent in. Approving that gate answers nothing: the machine is still
    /// unable to fly. So the modal offers this first, and the gate goes away
    /// when the runner next reports ready.
    /// </para>
    /// <para>
    /// <b>It takes the terminal for the same reason <see cref="SendCredential"/>
    /// does, and one more.</b> It reads a code with the echo off - which a
    /// Terminal.Gui session cannot arrange - and it prints a URL a person has
    /// to select and open. Both need the terminal back.
    /// </para>
    /// </remarks>
    LogAgentIn,

    /// <summary>Asks whether to ground the flight the modal is about.</summary>
    /// <remarks>
    /// Separate from <see cref="GroundFlight"/>, which is now the ANSWER. The
    /// two used to be one key, and a mistyped `x` was a session ended and an
    /// editor open before anybody had agreed to anything.
    /// </remarks>
    AskToGround,

    /// <summary>Asks whether to open a new flight on this one's intent.</summary>
    AskToFlyAgain,

    /// <summary>
    /// Opens the editor on the flight's own intent, and then opens a flight.
    /// </summary>
    /// <remarks>
    /// <b>It does not fly the summary.</b> A summary carries the intent and no
    /// repository, so flying from it would reproduce most flights as flights
    /// about nothing. Seeding the editor hands the ordinary open path
    /// everything it already knows.
    /// </remarks>
    FlyAgain,

    /// <summary>Shut the runner on this machine down.</summary>
    /// <remarks>The shell's: it signals a child, which a UI session may not.</remarks>
    StopRunner,

    /// <summary>Show or hide the fleet, with this machine's runner first.</summary>
    /// <remarks>
    /// <b>NOT the shell's, unlike its four neighbours.</b> The boot already
    /// fetches the runner list for the queue's stranded-runner reason, so
    /// showing this reads nothing and the reducer can do the whole of it.
    /// </remarks>
    ToggleRunners,


    /// <summary>
    /// Take the selected flight over: unmount, hand a person the terminal, come
    /// back to the same state.
    /// </summary>
    /// <remarks>
    /// Only for a flight whose loop has ended. Interrupting a running one is a
    /// handoff rather than a steering wheel, and it is a different feature.
    /// </remarks>
    TakeFlight,

    /// <summary>
    /// Hand a taken flight back: the agent proposes an account of what you did,
    /// and you confirm it.
    /// </summary>
    /// <remarks>
    /// Only for a flight somebody has taken over. Nothing resumes the loop - the
    /// account is recorded and the flight ends - so what this buys is a record
    /// the next reader finds, which is the next takeover.
    /// </remarks>
    HandBack,

    /// <summary>
    /// Open a flight and fly it yourself, on this machine, at a Claude Code
    /// prompt.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Its own key rather than a modifier on flying.</b> Flying by hand and
    /// flying on the fleet are the same act with different consequences for
    /// where the work happens, and a person choosing between them is choosing
    /// before they press rather than after.
    /// </para>
    /// <para>
    /// <b>It opens a NEW flight, and this used to say `the selected flight'.</b>
    /// The machinery never could do that: <c>ConsoleHandFlight.StartInfoFor</c>
    /// spawns <c>gg fly &lt;intent&gt; --hand</c>, which takes an intent and
    /// mints a number. So this key is <see cref="OpenFlight"/>'s prompt with the
    /// terminal handed over, and the sentence that said otherwise was prose
    /// nothing checked.
    /// </para>
    /// </remarks>
    FlyByHand,

    /// <summary>
    /// Open a flight: take the intent, submit it, come back to the same state.
    /// </summary>
    /// <remarks>
    /// A write, so the shell does it with the terminal free. The intent is taken the
    /// way this console has always taken text - a prompt, with $EDITOR for more than
    /// a line - because a modal that read text would be a new keyboard path needing
    /// its own escape hatch.
    /// </remarks>
    OpenFlight,

    /// <summary>Ask which way to compose a flight somebody will fly by hand.</summary>
    /// <remarks>
    /// <b><see cref="FlyByHand"/>'s question, and it is a different one from
    /// <see cref="AskHowToCompose"/> only in what it is about.</b> The two
    /// answers are the same two keys; what the loop does with them comes from
    /// the question, which the model carries while the modal is up.
    /// </remarks>
    AskHowToFlyByHand,

    /// <summary>Ask which way to compose a new flight.</summary>
    /// <remarks>
    /// <b>A command of its own rather than <see cref="OpenFlight"/> gaining a
    /// mode.</b> Opening a flight spawns a child and makes a request, so it is
    /// the shell's; asking which way sets a field and does nothing else, so it
    /// is the session's. A command that did both would have two effects, and
    /// the local one would happen whether or not the remote one did - which is
    /// the rule <c>ShellHandledTests</c> holds, and it is right.
    /// </remarks>
    AskHowToCompose,

    /// <summary>Open a flight, composing its intent in <c>$EDITOR</c>.</summary>
    /// <remarks>
    /// <b>The shell's, and the answer IS the command.</b> A first version had
    /// these set a field the session handled, which meant they never ended the
    /// session - and opening a flight spawns a child, which may only happen
    /// between sessions with the terminal free. So the modal recorded a choice
    /// and nothing ever acted on it: `n` was a key that opened a modal and did
    /// nothing.
    /// <para>
    /// Carrying the choice in the command rather than in the model is also what
    /// makes "the choice is not remembered" structural instead of a rule
    /// somebody has to maintain: there is nothing to remember.
    /// </para>
    /// </remarks>
    ComposeInEditor,

    /// <summary>Open a flight, composing its intent with an agent.</summary>
    /// <remarks><see cref="ComposeInEditor"/>'s, one composer over.</remarks>
    ComposeWithAgent,

    /// <summary>
    /// Open the flight this question was asked for, with the kind under the cursor.
    /// </summary>
    /// <remarks>
    /// <b>The shell's, because it opens a flight.</b> The compose modal was
    /// found with a reducer arm and no shell command, so it recorded a choice,
    /// returned to Normal, and nothing ever acted on it - an arm no key reaches.
    /// Answering this question has to END the session, because that is the only
    /// place a flight can be opened from.
    /// <para>
    /// <b>One command for three doors.</b> Which flight it opens is
    /// <see cref="AppState.AskingKindFor"/>'s answer, not this name's.
    /// </para>
    /// </remarks>
    FlyForKind,

    /// <summary>
    /// Ask which repository the credential about to be sent is for.
    /// </summary>
    /// <remarks>
    /// <b>In session, unlike the send it leads to.</b> The question needs the
    /// registry and the runner under the cursor, both of which are already in
    /// the model; the SEND needs the terminal, because a secret is read with
    /// the echo off. So the asking half stays and only the answering half
    /// leaves.
    /// </remarks>
    ChooseCredentialRepository,

    /// <summary>
    /// Show what the work item under the cursor actually says.
    /// </summary>
    /// <remarks>
    /// <b>A READ, because asking a reader starts a child holding a
    /// credential.</b> A UI session may do neither - the same sentence that
    /// makes browsing a shell command rather than a toggle - so this happens
    /// between sessions with the terminal free.
    /// </remarks>
    ShowWorkItem,

    /// <summary>
    /// Ask the tracker what there is to narrow the listing by, and offer it.
    /// </summary>
    /// <remarks>
    /// <b>The shell's, for the sentence one command up.</b> The choices come
    /// from a child process holding a credential, which is a spawn and not a
    /// read, and a UI session may do neither. It opens the modal with what came
    /// back - including the sentence that came back instead.
    /// </remarks>
    FilterBrowse,

    /// <summary>Show everybody's board rows, or go back to mine and the tenant's.</summary>
    ShowEverybodysRows,

    /// <summary>Open the browse tab's field, to go to an item or find one.</summary>
    /// <remarks>
    /// <b>Opens a field and reads nothing.</b> What is asked for is decided by
    /// what somebody types, so the request is <see cref="GoToOrFind"/>'s.
    /// </remarks>
    FindInBrowse,

    /// <summary>Go to the item that was typed, or find the words that were.</summary>
    /// <remarks>
    /// <b>The shell's, because both arms read.</b> One asks the reader for a
    /// single item and the other for a listing; which it is comes from
    /// <see cref="BrowseFind.Wanted"/> rather than from two keys.
    /// </remarks>
    GoToOrFind,

    /// <summary>
    /// Pick, or un-pick, the choice the filter cursor is on.
    /// </summary>
    /// <remarks>
    /// <b>The reducer's, because it changes nothing outside the model.</b>
    /// Choosing is not applying: a browse tears the session down and starts a
    /// reader, so re-querying per toggle would spawn a child per cursor move.
    /// </remarks>
    PickFilterValue,

    /// <summary>
    /// Turn the filter modal's bar to the next criterion.
    /// </summary>
    /// <remarks>
    /// The reducer's: it changes which of three lists is showing and nothing
    /// else. The runner modal's bar turns the same way, with the same key.
    /// </remarks>
    NextFilterView,

    /// <summary>Take the whole filter off, in one key.</summary>
    /// <remarks>
    /// Also the reducer's. Clearing one dimension is a row in the list; this is
    /// the key for a person who narrowed three ways and wants the backlog back.
    /// </remarks>
    ClearFilter,

    /// <summary>
    /// List the work again, narrowed by whatever is picked.
    /// </summary>
    /// <remarks>
    /// <b>The shell's, because it is a browse.</b> It is the same spawn
    /// <see cref="ToggleBrowse"/> performs and it earns its own name because it
    /// closes the modal first: a filter applied under a dialog that stays up is
    /// a person looking at choices instead of at what they chose.
    /// </remarks>
    BrowseFiltered,

    /// <summary>
    /// Open the work item where it lives.
    /// </summary>
    /// <remarks>
    /// <b>A tracker holds more than a reader renders.</b> Attachments, links,
    /// the people on it, the thing somebody dragged into a comment - a console
    /// that showed a rendering and offered no way out would be asking a person
    /// to believe the rendering is all of it. It spawns a browser, so it is the
    /// shell's, exactly as the sign-in link beside it is.
    /// </remarks>
    OpenWorkItem,

    /// <summary>Open the page this flight's intent names, in a browser.</summary>
    /// <remarks>
    /// <b><see cref="OpenWorkItem"/>'s act, reached from the flight rather than
    /// from the listing.</b> A sweep nominates a work item by its url, so the
    /// flight it opens carries a link and no ticket - and the ticket key needs
    /// a provider, an id and a reader here. This is the same port and the same
    /// exception to the session rule: a browser takes the display and stays the
    /// shell's.
    /// </remarks>
    OpenTheLink,

    /// <summary>
    /// Shows the work item a flight was opened against.
    /// </summary>
    /// <remarks>
    /// <b>The modal next door, about the item this flight names.</b> The flight
    /// modal already shows <c>provider#id</c>; reaching what that item actually
    /// says meant leaving the modal, opening Browse and finding the row by eye
    /// - and the row is usually not on the page anybody last browsed.
    /// <para>
    /// <b>Offered only where it leads somewhere.</b> The intent has to be a
    /// ticket and a reader has to be declared for its provider, because a key
    /// that opens a modal to say it could not ask is the dead key Article XI
    /// names.
    /// </para>
    /// </remarks>
    OpenTheTicket,

    /// <summary>Shows or hides every allowance in the fleet.</summary>
    ToggleAllowances,

    /// <summary>
    /// Goes to the queue.
    /// </summary>
    /// <remarks>
    /// <b>Shows, where the other tab keys toggle.</b> The six that can be
    /// closed land on the queue when they are, which only works because the
    /// queue is the thing they land on - so there is nothing for a second
    /// press of this to do but leave somebody where they already are.
    /// <para>
    /// <b>Reads nothing.</b> The queue is derived from flights the boot already
    /// fetched, so this is the reducer's alone.
    /// </para>
    /// </remarks>
    ShowQueueTab,

    /// <summary>Goes to the list of every recent flight.</summary>
    /// <remarks>
    /// <see cref="ShowQueueTab"/>'s shape and its reasons: it shows rather than
    /// toggles, because this tab cannot be closed either, and it reads nothing.
    /// </remarks>
    ShowFlightsTab,

    /// <summary>
    /// Goes to the board: every nomination, and the watches whose sweeps make
    /// them.
    /// </summary>
    /// <remarks>
    /// <b>Shows rather than toggles, like the two lists beside it</b> - this
    /// tab cannot be closed either. Unlike them it READS: the board and the
    /// watch standings are two requests nothing else in the console makes, so
    /// the shell fetches them when this is shown and on every refresh while it
    /// is.
    /// </remarks>
    ShowBoardTab,

    /// <summary>Opens the question of how much of this allowance to keep.</summary>
    AskToKeepAShare,

    /// <summary>Keep a tenth of each window back.</summary>
    /// <remarks>
    /// <b>Both windows, because a console offers a decision rather than a
    /// form.</b> Keeping a third of the week and nothing of the session is
    /// coherent and is what the command line is for; what somebody wants while
    /// looking at a fleet is "hold some of this back".
    /// </remarks>
    KeepATenth,

    /// <summary>Keep a quarter of each window back.</summary>
    KeepAQuarter,

    /// <summary>Keep half of each window back.</summary>
    KeepAHalf,

    /// <summary>
    /// Keep nothing back, clearing the floor.
    /// </summary>
    /// <remarks>
    /// <b>Its own key rather than escape.</b> Escaping means "I did not mean to
    /// open this"; keeping nothing is a decision, and collapsing the two would
    /// let a mistaken keypress clear a reserve.
    /// </remarks>
    KeepNothing,

    /// <summary>
    /// Register a credential for a repository. The value is prompted for and never
    /// held here.
    /// </summary>
    /// <remarks>
    /// The console used to refuse this on the grounds that a prompt inside a modal
    /// has its own escape-hatch rules. It does; this is not one. The prompt runs in
    /// the shell, and the value is read by <c>CredentialCommands</c> rather than by
    /// anything in this project - which matters, because <c>AppState</c> serializes
    /// itself to disk.
    /// </remarks>
    AddCredential,

    /// <summary>
    /// Issue an invitation, and put the link where a person can get at it.
    /// </summary>
    /// <remarks>
    /// Whoever holds the link becomes a principal in this tenant, so it is a
    /// capability: it goes to the clipboard or to a named file through
    /// <c>SeedPlacer</c>, and the model records WHERE rather than WHAT.
    /// </remarks>
    Invite,

    /// <summary>
    /// Sign this machine in, one step of a device authorization at a time.
    /// </summary>
    /// <remarks>
    /// <b>One command, two meanings, decided by whether a code is already
    /// waiting</b> — <see cref="FlyPicked"/>'s shape. Pressed with nothing
    /// started it asks the control plane for a code; pressed with one on the
    /// screen it says the person has approved it. The keymap binds a different
    /// key to each, so nothing downstream depends on how fast somebody types.
    /// </remarks>
    SignIn,
}

/// <summary>
/// Which commands the shell performs, rather than the reducer.
/// </summary>
/// <remarks>
/// <para>
/// <b>One declaration, because two lists drifted.</b> <c>ConsoleScreen</c> ended
/// the UI session for a literal <c>Quit or OpenEditor</c> while <c>ConsoleLoop</c>
/// had arms for <c>TakeFlight</c> and <c>HandBack</c>. Each was right about its own
/// half and neither knew about the other, so four bound, advertised keys resolved
/// to a command, reached the reducer, and returned the state unchanged.
/// </para>
/// <para>
/// <b>What being here MEANS.</b> The UI session ends, the terminal is provably
/// free, the shell does the work, and the next session is rebuilt from the model
/// alone. That is the console's whole architecture - the same lifetime
/// <c>$EDITOR</c> has always used - and it is why an effect that talks to the
/// control plane or spawns a child belongs here rather than in a pure reducer.
/// </para>
/// <para>
/// <b>Every command in here needs an arm in <c>ConsoleLoop</c></b>, which throws on
/// one it does not recognise. <c>ShellHandledTests</c> checks that, and checks that
/// the screen and the generated key walk both read this rather than restating it.
/// </para>
/// </remarks>
public static class ShellCommands
{
    /// <summary>
    /// Commands whose answer is fetched beside the console rather than by
    /// ending it.
    /// </summary>
    /// <remarks>
    /// <b>The set the flicker came from.</b> Every one of these used to be
    /// declared as the shell's, so a keypress disposed Terminal.Gui, left the
    /// alternate screen, made one request and built a new session over the
    /// answer. That teardown is right for handing the terminal to a child and
    /// it is a blink for a read — and <c>AutoRefresh</c> had already won the
    /// argument for not doing it: the session does not read, it folds a result
    /// that has arrived.
    /// <para>
    /// Read by <c>ConsoleScreen</c> to know which keypresses want one, and by
    /// the composition root to know which reads to supply. One declaration, so
    /// the two cannot disagree about which is which.
    /// </para>
    /// </remarks>
    public static readonly IReadOnlySet<Command> Reads = new HashSet<Command>
    {
        Command.ShowFlight,

        // THE FOUR TOGGLES, for the same reason and with the same shape: a
        // pane that is opened wants filling, and filling it used to mean the
        // console going away and coming back. Closing one reads nothing, which
        // the read function decides rather than the screen - a toggle that shut
        // a pane and then fetched what to put in it is a request nobody asked
        // for.
        Command.ToggleEnvelope,
        Command.ToggleRepositories,

        // THE FACTS TAB, for the same reason one press over: the pane is
        // already open and the flight already on it, so ending the session to
        // fetch what goes in it would blink a terminal for a question about
        // something a person is looking at.
        Command.ShowFlightFacts,

        // THE SAME REGISTRY, WANTED BY A DIFFERENT SCREEN. The credential
        // chooser lists what has no credential yet, which it cannot do without
        // the registry - and it used to compensate with a row that asked a
        // person to type a slug the console could have read. A read that folds
        // in without the session ending is what makes that row unnecessary.
        Command.ChooseCredentialRepository,

        // AND THE COMPOSE MODAL, which lists the same registry on its second
        // tab. Without this it was empty unless somebody had happened to visit
        // the Repositories tab first - measured by driving it, and the
        // difference between a feature and a feature that works on the second
        // try.
        Command.AskHowToCompose,
        Command.AskHowToFlyByHand,

        // BROWSING, WHICH THIS SET REFUSED TWICE AND NOW DOES NOT AT ALL. The
        // reason it refused is worth keeping because it was half right. "An
        // IntentReader is a Command, its Arguments, the environment variable
        // 'the only place a secret may go', and a credential locator - it is a
        // CHILD PROCESS HOLDING A CREDENTIAL, and a session may do neither.
        // AutoRefresh's exception is for a read and does not stretch to a
        // spawn."
        //
        // EVERY WORD OF THAT IS ABOUT STARTING ONE, which first bought the
        // asking: ReaderSessions caches what it starts - "a reader asked for
        // twice is the same reader" - so the spawn is one act and every press
        // after it is a pipe.
        //
        // AND THEN THE SPAWN WAS MEASURED. SpawnedReader reads neither the
        // environment variable nor the locator; it places no secret, redirects
        // all three streams so the child cannot touch the terminal, and runs on
        // the read task, so it blocks nothing. The sentence was true of what an
        // IntentReader DECLARES and false of what this console does with one.
        // The exception is granted and scoped in LiveStreamingTests, beside the
        // clipboard's - which is where it has to be, because the scan there
        // cannot see a spawn reached through a Func composed in the root.
        Command.ToggleBrowse,
        Command.ShowWorkItem,

        // THE SAME READ, REACHED FROM THE OTHER MODAL. It asks the same reader
        // the same three questions about one item; the only difference is that
        // the id came off a flight rather than off a row.
        Command.OpenTheTicket,
        Command.FilterBrowse,

        // THE FIELD'S ANSWER, and it reads whichever arm it takes: one item by
        // the id somebody typed, or a listing for the words they typed.
        Command.GoToOrFind,
        Command.BrowseFiltered,
    };

    /// <summary>The commands whose effect lives in <c>ConsoleLoop</c>.</summary>
    /// <summary>
    /// Commands the SCREEN performs on a widget, rather than the shell or the
    /// reducer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A third category, and it had to be declared rather than exempted.</b>
    /// Every command a key can resolve was either the shell's or the
    /// reducer's, because one that is neither is a key advertised on the hint
    /// line that does nothing when pressed - and that ratchet caught the first
    /// of these, correctly.
    /// </para>
    /// <para>
    /// <b>What they change is a widget's in-progress text, which the model
    /// cannot hold</b>: <see cref="Command"/> is a parameterless enum, so a
    /// keystroke's worth of a half-typed path has nowhere to go through the
    /// reducer. The keymap still owns the binding, which is the split
    /// <c>Dispatch</c> already describes - the keymap says what a key MEANS,
    /// and the screen is what happens once something means it.
    /// </para>
    /// <para>
    /// Kept as data with the reason attached, so a fourth has to be argued
    /// rather than quietly appended.
    /// </para>
    /// </remarks>
    public static IReadOnlyDictionary<Command, string> OnTheWidget { get; } =
        new Dictionary<Command, string>
        {
            [Command.AirspacePathFromCwd] =
                "puts the directory gg was launched from into the airspace field. The value "
              + "is in the model; where it lands is the widget's own text.",

            [Command.AirspacePathFromClipboard] =
                "puts the clipboard into the airspace field. A stated exception to the "
              + "session rule, recorded in LiveStreamingTests where that rule is enforced.",

            [Command.AirspacePathFromDialog] =
                "runs a directory picker and puts its answer in the airspace field. A "
              + "second, narrower exception - a filesystem read a person walks, with no "
              + "spawn - recorded in the same place.",
        };

    public static IReadOnlySet<Command> Handled { get; } = new HashSet<Command>
    {
        // Always was. Quit returns the model.
        //
        // OpenEditor sat here too - it was the ORIGINAL terminal-release effect,
        // and it is gone: the key wrote a scratchpad nothing displayed, sent or
        // kept. `new flight` hands the terminal to the same editor for a reason
        // somebody asked for, and carries the property that one demonstrated.
        Command.Quit,

        // Bound and inert until this declaration existed.
        Command.TakeFlight,
        Command.HandBack,

        // SPAWNS A CHILD AND MAKES A REQUEST, so both halves of what this set
        // means apply: a UI session may not read, and the terminal has to be
        // provably free before somebody is handed it.
        Command.FlyByHand,
        Command.ApproveGate,
        Command.RejectGate,

        // THE BOARD'S TWO ANSWERS, and each of them is here twice over: it
        // posts a decision, and it hands the terminal to $EDITOR first, because
        // the door refuses an answer that says nothing. Either half alone would
        // put it in this set.
        Command.OpenNomination,
        Command.DeclineNomination,

        // The three the parity guard used to exempt. Writes, so the shell does them.
        Command.OpenFlight,

        // THE MODAL'S TWO ANSWERS, and they are here because each of them
        // OPENS A FLIGHT - the same spawn-and-request OpenFlight above is here
        // for, differing only in which child composes the intent. An earlier
        // version had them as pure reductions, which meant they never ended the
        // session and nothing was ever opened.
        Command.ComposeInEditor,
        Command.ComposeWithAgent,
        Command.FlyForKind,

        // SETTING A FLOOR IS A WRITE, so it happens between sessions with the
        // terminal provably free - the arrangement every other write here
        // uses. As a pure reduction it would change the model and never reach
        // the control plane, which is the defect the two above recorded.
        Command.KeepATenth,
        Command.KeepAQuarter,
        Command.KeepAHalf,
        Command.KeepNothing,
        Command.AddCredential,
        Command.Invite,

        // OPENING ONE IN A BROWSER STAYS, AND THE FOUR BESIDE IT DID NOT. The
        // reason written here was "a reader is a child process holding a
        // credential, and a session may start neither" - exactly right, and
        // about STARTING one. A reader is started once per console lifetime
        // and cached; every browse after the first talks to a process that was
        // running before the session existed, which is what LiveTails already
        // does. The first press is still this set's, decided by
        // BackgroundReads.Ready rather than by membership here.
        //
        // This one does not move, because it is the spawn the others stopped
        // being: opening an item starts a BROWSER, a new process every time
        // rather than a pipe to one already running.
        Command.OpenWorkItem,

        Command.ForgetCredential,

        // It opens a child and then writes a file, which is two things a
        // session may not do.
        Command.EditConfiguration,
        Command.PullEstate,
        Command.ApplyEstate,

        // A REQUEST PER NAME, and the one act here that removes
        // governance rather than adding it.
        Command.RetireNames,
        Command.DraftEstate,
        Command.SetAirspacePath,

        // It asks the control plane and then writes a file. The second half is
        // the one that puts it here; the first is why it cannot be a read the
        // session does either.
        Command.TakeOfferedConfiguration,

        // It writes, so it is the loop's like every other write.
        Command.FlyPicked,

        // THEY WRITE AND THEY DO NOT WANT THE TERMINAL. Claiming a machine is
        // one call to the control plane and a sentence back - no child, no
        // secret read with the echo off - so unlike the four below it these
        // stay inside the session, as answering a gate does.
        Command.ClaimRunner,
        Command.ReserveRunner,

        // READ WHEN ASKED FOR, because tokens change when a person mints or
        // revokes one rather than on their own - so this is a fetch the loop
        // makes, not a refresh the console keeps making.
        Command.ShowFleetTokens,
        Command.RevokeToken,

        // THE ADMIN'S WORD, once its question has been answered. The ask
        // itself is the reducer's - it only opens a modal - and this is the
        // half that writes.
        Command.SetOwnership,

        // SPAWNS A CHILD, so both halves of what this set means apply.
        Command.StartRunner,

        // Ends a flight, and asks for a sentence before it does.
        Command.GroundFlight,

        // A SECOND CLIPBOARD USE, and the exception the paste holds does not
        // stretch to it: that one is scoped to a READ, in one field, by one
        // key. This spawns, so it is the shell's.
        Command.CopyModal,

        // A browser and the clipboard: a child process each.
        Command.OpenSignInUri,
        Command.CopySignInUri,
        Command.CopySignInCode,

        // Signals one, and spawns one again after.
        Command.StopRunner,
        Command.RestartRunner,

        // SPAWNS A CHILD THAT OWNS THE TERMINAL until a person stops it or the
        // flight ends - the editor's shape, and for the editor's reason: what
        // it does inside is a network call a session may not make.
        Command.WatchRunner,

        // TAKES THE TERMINAL TO ASK FOR A SECRET, which is the one thing on this
        // list that reads from a person rather than writing to them. The echo
        // has to be off and a Terminal.Gui session cannot turn it off, so this
        // needs the terminal back exactly as the editor does.
        Command.SendCredential,

        // TAKES THE TERMINAL TO PRINT A URL AND READ A CODE, which is the
        // send's reason with a person's browser in the middle of it. The
        // conversation stays open while they are away from the keyboard, so
        // this is the longest thing on this list.
        Command.LogAgentIn,

        // OPENS AN EDITOR AND THEN A FLIGHT, which is what `n` does; the only
        // difference is what the editor opens on.
        Command.FlyAgain,

        // OPENING A FLIGHT IS NOT HERE ANY MORE. It was, for the reason above -
        // the modal shows one flight's log and the boot only reads logs for
        // flights still in the air - and the cost was the whole screen going
        // away and coming back to make one request. The modal opens on the
        // summary already in hand and BackgroundReads folds the log when it
        // lands, which is AutoRefresh's exception and AutoRefresh's argument:
        // the session does not read, it folds a result that has arrived.

        // TWO REQUESTS AND A CREDENTIAL WRITTEN TO DISK, which is as far from
        // "a session may read a local file" as this console gets. It is also
        // the only one of these a person can reach before there is a session at
        // all - every other write here would be refused by the control plane on
        // the machine this one exists to fix.
        Command.SignIn,
    };
}
