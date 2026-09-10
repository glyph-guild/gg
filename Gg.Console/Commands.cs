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
    /// Everything known about the flight under the cursor.
    /// </summary>
    /// <remarks>
    /// Reads nothing: the flight and its log are what the boot already fetched,
    /// which is the same reason an arrow key is free. A modal because it is a
    /// question with an answer and a way out.
    /// </remarks>
    ShowFlight,

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

    ToggleEvidence,

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
    ToggleChecklist,

    /// <summary>The rules in force. The shell's, because showing it is a read.</summary>
    ToggleEnvelope,

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
        // NOT ToggleBrowse, AND THAT IS NOT AN OVERSIGHT. Browsing is not a
        // read: an IntentReader is a Command, its Arguments, the environment
        // variable "the only place a secret may go", and a credential locator -
        // it is a CHILD PROCESS HOLDING A CREDENTIAL, and a session may do
        // neither. AutoRefresh's exception is for a read and does not stretch
        // to a spawn. Four guards said so before this was tried, each with the
        // reason written out, and they were right.
        Command.ToggleChecklist,
        Command.ToggleEnvelope,
        Command.ToggleRepositories,
    };

    /// <summary>The commands whose effect lives in <c>ConsoleLoop</c>.</summary>
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

        // The three the parity guard used to exempt. Writes, so the shell does them.
        Command.OpenFlight,

        // THE MODAL'S TWO ANSWERS, and they are here because each of them
        // OPENS A FLIGHT - the same spawn-and-request OpenFlight above is here
        // for, differing only in which child composes the intent. An earlier
        // version had them as pure reductions, which meant they never ended the
        // session and nothing was ever opened.
        Command.ComposeInEditor,
        Command.ComposeWithAgent,
        Command.AddCredential,
        Command.Invite,

        // BROWSING STAYS, AND THE THREE BESIDE IT DID NOT. The reason written
        // here was "showing the browser starts a reader, and a session may read
        // a local file and nothing else" - and it is exactly right about
        // browsing, which launches an executable with a credential in its
        // environment. It was over-broad about the checklist, the envelope and
        // the repositories, which are control-plane reads like any other and
        // are in `Reads` now. Ending the whole session was one way to honour
        // the rule; for a read it costs a screen taken away and given back.
        Command.ToggleBrowse,
        Command.ForgetCredential,

        // It opens a child and then writes a file, which is two things a
        // session may not do.
        Command.EditConfiguration,

        // It asks the control plane and then writes a file. The second half is
        // the one that puts it here; the first is why it cannot be a read the
        // session does either.
        Command.TakeOfferedConfiguration,

        // It writes, so it is the loop's like every other write.
        Command.FlyPicked,

        // SPAWNS A CHILD, so both halves of what this set means apply.
        Command.StartRunner,

        // Ends a flight, and asks for a sentence before it does.
        Command.GroundFlight,

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
