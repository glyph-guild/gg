namespace Gg.Console;

/// <summary>
/// Everything the keymap can produce. Bindings live in the keymap; meanings
/// live here; effects live in the reducer.
/// </summary>
public enum Command
{
    Quit,
    ToggleHelp,

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
    /// Open a flight: take the intent, submit it, come back to the same state.
    /// </summary>
    /// <remarks>
    /// A write, so the shell does it with the terminal free. The intent is taken the
    /// way this console has always taken text - a prompt, with $EDITOR for more than
    /// a line - because a modal that read text would be a new keyboard path needing
    /// its own escape hatch.
    /// </remarks>
    OpenFlight,

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

        // A READ, and the first one here. Everything else in this set is a
        // write; a refresh is in it for the same reason - the effect lives in
        // ConsoleLoop, because a UI session may not make a request.
        Command.Refresh,

        // Bound and inert until this declaration existed.
        Command.TakeFlight,
        Command.HandBack,
        Command.ApproveGate,
        Command.RejectGate,

        // The three the parity guard used to exempt. Writes, so the shell does them.
        Command.OpenFlight,
        Command.AddCredential,
        Command.Invite,

        // NOT A PREFERENCE. Showing the browser starts a reader, and a session
        // may read a local file and nothing else. The loop owns the reader for
        // the same reason it owns the editor and the take.
        Command.ToggleBrowse,
        Command.ToggleChecklist,
        Command.ToggleEnvelope,
        Command.ForgetCredential,

        // It writes, so it is the loop's like every other write.
        Command.FlyPicked,
    };
}
