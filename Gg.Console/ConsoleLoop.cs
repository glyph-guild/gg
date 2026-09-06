using Gg.Client;
namespace Gg.Console;

/// <summary>
/// The terminal-release loop. UI sessions are complete lifetimes: between
/// them the terminal belongs to whoever we spawn, and the model is the only
/// thing that survives.
/// </summary>
public sealed class ConsoleLoop(
    IUiSession ui,
    IEditorSession editor,
    ITakeSession? take = null,
    IHandSession? hand = null,
    IConsoleActions? actions = null,
    LiveTails? tails = null,
    IWorkBrowser? browser = null,
    Func<AppState, AppState>? reload = null,
    Func<AppState, AppState>? checklist = null,
    Func<AppState, AppState>? envelope = null,
    Func<AppState, AppState>? repositories = null,

    /// <summary>
    /// One flight's log, for the flight somebody just opened.
    /// </summary>
    /// <remarks>
    /// The boot reads a log only for a flight still in the air, because those
    /// are the only ones whose log can put a row in the queue. The detail modal
    /// is usually opened on a flight that landed, so it reads its own - one
    /// request, on the keypress, with the terminal released.
    /// </remarks>
    Func<AppState, AppState>? flightLog = null,

    /// <summary>
    /// Asks what a flight would need, and either refuses or takes an intent and
    /// hands over the terminal.
    /// </summary>
    /// <remarks>
    /// <b>A delegate, and named for the act rather than the port</b> - `hand` is
    /// already the hand-BACK session, and two things called hand in one
    /// constructor is a swap waiting to happen.
    /// <para>
    /// It is the composition root's, like its neighbours: the read it makes and
    /// the child it starts both need things this assembly may not name - the
    /// plan comes off the control plane and the child is this binary re-execed -
    /// and a console that could name a runner type would be a console that can
    /// act as one.
    /// </para>
    /// </remarks>
    Func<AppState, Func<string>, AppState>? flyByHand = null,

    /// <summary>
    /// Starts a runner on this machine, and says what became of it.
    /// </summary>
    /// <remarks>
    /// The composition root's, like its neighbours: it spawns this binary again
    /// as a separate process, and the console may not name a runner type.
    /// </remarks>
    Func<AppState, AppState>? startRunner = null,

    /// <summary>
    /// Signs this machine in, one step at a time, with the terminal free.
    /// </summary>
    /// <remarks>
    /// A port like the takeover's rather than a delegate, because it has two
    /// halves that share something the model may not hold — the device code is
    /// a credential, so it lives here and never crosses back.
    /// </remarks>
    ISignInSession? signIn = null)
{
    /// <summary>
    /// Re-reads everything the boot read, keeping what the person was looking
    /// at.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A delegate rather than a data port</b>, which is the same shape the
    /// takeover already uses: the loop is handed something it can call, and
    /// what that call does is the composition root's business. A ConsoleData
    /// here would put a read surface inside the loop's type, one step from
    /// putting one inside a session.
    /// </para>
    /// <para>
    /// <b>The view is not data.</b> A reload answers with fresh flights, gates
    /// and logs; which row somebody had highlighted and which panes they had
    /// open are theirs, and losing them on every refresh makes the key not
    /// worth pressing.
    /// </para>
    /// <para>
    /// <b>So the delegate is GIVEN the model and answers with it</b>, the read
    /// plane replaced and nothing else touched. That is why it takes an
    /// <see cref="AppState"/> rather than nothing: a reload that ignores its
    /// argument is a boot, and a boot standing in for a refresh is what emptied
    /// the browse pane, the receipts and - when the network was down - the whole
    /// console.
    /// </para>
    /// <para>
    /// <b>And a failure keeps the last good model.</b> Emptying the screen is
    /// the worst answer: the person loses what they had and cannot tell whether
    /// the work went away. What was there is still true until something better
    /// is known - said, with the diagnosis, rather than shown as an absence.
    /// </para>
    /// </remarks>
    /// <param name="asked">
    /// Whether a person pressed the key, as against a write re-reading what it
    /// invalidated.
    /// </param>
    /// <remarks>
    /// <b>An unconfigured refresh is only worth saying when somebody asked for
    /// one.</b> A console built without a reload still opens flights and answers
    /// gates; telling it "this console is not configured to refresh" after every
    /// write would be answering a question nobody put, and it would attach a
    /// diagnosis to a write that succeeded. The round-trip suite caught exactly
    /// that.
    /// </remarks>
    private static AppState Reloaded(
        AppState state, Func<AppState, AppState>? reload, bool asked = true)
    {
        if (reload is null)
        {
            // SAID, not silent - but only to the person who asked. A bound key
            // that resolves, reaches its arm and returns the state unchanged is
            // the dead-key shape this console has hit four times.
            return asked
                ? state with { Diagnosis = "This console is not configured to refresh." }
                : state;
        }

        try
        {
            // THE ANSWER, WHOLE. This used to be `fresh with { six fields }` -
            // a list of what must survive a refresh, kept in the loop, next to
            // a model with far more than six fields on it. Every one it did not
            // name reset to a default: the browse pane closed under the person
            // using it, and the sentence saying what their keypress did was
            // discarded by the re-read that keypress triggered.
            //
            // A list like that grows whenever anybody adds a field, and
            // forgetting one is silent. The reload is given the model and
            // answers with it instead, which is a promise the composition root
            // can keep without this method knowing any field names at all.
            return reload(state);
        }
        catch (Exception failure) when (failure is Gg.Client.NotSignedInException
                                            or Gg.Client.ProtocolTooOldException
                                            or Gg.Client.FlightNotFoundException
                                            or HttpRequestException)
        {
            // FOR A RELOAD THAT IS NOT THE LOADER. ConsoleStart.LoadAsync
            // catches these four itself and answers with the last good model
            // and a diagnosis, so the production path returns normally and
            // never arrives here. It is kept because the parameter is a
            // delegate: this is the loop's own answer to one that throws.
            return state with
            {
                Diagnosis = "Refresh failed, so this is what was last known: " + failure.Message,
            };
        }
    }

    public AppState Run(AppState initial)
    {
        var state = initial;
        while (true)
        {
            state = tails?.Advance(state) ?? state;

            var outcome = ui.Run(state);
            state = outcome.State;

            var before = state;

            switch (outcome.Exit)
            {
                case Command.Quit:
                    return state;

                case Command.Refresh:
                    state = Reloaded(state, reload);
                    break;

                case Command.HandBack:
                    // The same shape again: the session ends, an agent reads the
                    // tree, a person answers, and the model is the only thing
                    // that crosses back.
                    state = HandedBack(state, hand);
                    break;

                case Command.OpenFlight:
                    // AND THEN RE-READ. Rule 4: a flight opened is a flight the
                    // queue does not have yet, and a Last* sentence is a receipt
                    // rather than a substitute for the state changing.
                    state = Reloaded(Opened(state, actions, editor), reload, asked: false);
                    break;

                case Command.AddCredential:
                    // The value is read by CredentialCommands, inside the action.
                    // Nothing here holds it, which is the point: this record is
                    // serialized to disk under GG_STATE_DUMP.
                    //
                    // AND IT RE-READS, which it did not. Rule 4: registering a
                    // credential changes the credential list the flight pane
                    // draws, and this arm was the one write in the loop that
                    // changed something a pane shows and did not refresh it.
                    state = Reloaded(
                        state with
                        {
                            LastCredential = actions is null
                                ? "This console is not configured to register credentials."
                                : actions.AddCredential(),
                        },
                        reload,
                        asked: false);
                    break;

                case Command.ToggleBrowse:
                    // THE READING HAPPENS HERE BECAUSE IT CANNOT HAPPEN THERE.
                    // A UI session may read a local file and nothing else; a
                    // reader is a child process. So the session ended, the loop
                    // asks, and the next session is rebuilt from the model -
                    // the same shape the editor and the take already use, for a
                    // much smaller reason.
                    //
                    // Only on the way IN. Hiding costs nothing, and a read
                    // costs a whole session rebuild on this path.
                    state = Reducer.BrowseToggled(state);

                    if (state.BrowseVisible && browser is not null)
                    {
                        state = Browsed(state, browser);
                    }

                    break;

                case Command.ShowFlight:
                    // READ FIRST, THEN OPEN. The modal renders one flight's log
                    // and a UI session may not fetch it, so the session ends,
                    // the loop asks, and the next session opens over an answer.
                    // Opening it before the read would show "no log fetched" and
                    // then never come back to correct itself.
                    //
                    // THE REDUCER STILL DECIDES WHETHER IT OPENS. It refuses
                    // when there is no flight under the cursor, and that is not
                    // this switch's question.
                    if (flightLog is not null)
                    {
                        state = flightLog(state);
                    }

                    state = Reducer.FlightShown(state);
                    break;

                case Command.StartRunner:
                    // A CHILD, so the session ends first. The runner is treated
                    // as hostile and the OS is what keeps it apart from the
                    // console; this arm only knows that something was asked for
                    // and what came back.
                    state = startRunner is null
                        ? state with
                        {
                            LastRunner = "This console is not configured to start a runner.",
                        }
                        : startRunner(state);
                    break;

                case Command.ToggleChecklist:
                    // THE SAME SHAPE AS BROWSE, for a much smaller request.
                    // Showing this pane is a read and a UI session may not do
                    // I/O, so the session ends, the loop asks, and the next
                    // session renders it.
                    //
                    // Only on the way IN, and the answer survives hiding:
                    // somebody who closes the pane and opens it again should not
                    // pay for a second read of a flight that has not moved.
                    state = Reducer.ChecklistToggled(state);

                    if (state.ChecklistVisible && checklist is not null)
                    {
                        state = checklist(state);
                    }

                    break;

                case Command.ToggleEnvelope:
                    // The checklist's reason, for the document the checklist is
                    // derived from. Read on the way in only.
                    state = Reducer.EnvelopeToggled(state);

                    if (state.EnvelopeVisible && envelope is not null)
                    {
                        state = envelope(state);
                    }

                    break;

                case Command.FlyPicked:
                    // ONE KEY, TWO MEANINGS, DECIDED BY WHETHER A QUESTION IS
                    // OPEN. Pressing it fresh asks; pressing 'y' inside the
                    // confirmation answers. The keymap binds different keys for
                    // the two, so nothing here depends on a person's timing.
                    //
                    // AND ONLY ONE OF THE THREE ENDINGS RE-READS. Nothing
                    // selected opened nothing; a duplicate warning has opened
                    // nothing YET and is asking about a row - reloading under it
                    // would rebuild the queue beneath a question, which is how a
                    // person ends up answering about something they are no
                    // longer looking at. A flight actually opened is a flight
                    // the queue does not have.
                    bool opened;
                    state = state.PendingFlight is null
                        ? FlewPicked(state, actions, out opened)
                        : ConfirmedFlight(state, actions, out opened);

                    if (opened)
                    {
                        state = Reloaded(state, reload, asked: false);
                    }

                    break;

                case Command.ForgetCredential:
                    // A WRITE, SO IT REFRESHES WHAT IT INVALIDATED. Rule 4: the
                    // credential list the flight pane reads is exactly what this
                    // changed, and a console still showing a credential somebody
                    // just forgot is the staleness this slice exists to remove.
                    state = Reloaded(
                        state with
                        {
                            LastCredential = actions is null
                                ? "This console is not configured to forget credentials."
                                : actions.ForgetCredential(),
                        },
                        reload,
                        asked: false);
                    break;

                case Command.ToggleRepositories:
                    // A READ ON THE WAY IN, browse's and the checklist's shape.
                    // Hiding asks nothing, and re-reading a list already held
                    // would spend a whole session rebuild to show a person what
                    // they were just looking at.
                    state = Reducer.RepositoriesToggled(state);

                    if (state.RepositoriesVisible
                        && state.Repositories is null
                        && repositories is not null)
                    {
                        state = repositories(state);
                    }

                    break;

                case Command.Invite:
                    state = state with
                    {
                        LastInvite = actions is null
                            ? "This console is not configured to issue invitations."
                            : actions.Invite(),
                    };
                    break;

                case Command.ApproveGate:
                case Command.RejectGate:
                    // THE WRITE THE REDUCER DELIBERATELY DOES NOT DO. It returns the
                    // state unchanged for both answers and says why: answering posts,
                    // and what closes the modal is what the control plane sends back.
                    // That was right and the loop never saw the command, so nothing
                    // posted at all - the console had no write path.
                    // AND THEN RE-READ, which is rule 4 and the staleness a
                    // person actually sees: answer a gate and it stayed in the
                    // list, because nothing reloaded. A decision changes what is
                    // waiting, so what is waiting is read again.
                    state = Reloaded(
                        Decided(state, actions, editor, outcome.Exit == Command.ApproveGate),
                        reload,
                        asked: false);
                    break;

                case Command.FlyByHand:
                    // THE SAME TERMINAL-RELEASE SHAPE as the takeover beside it,
                    // and for a longer stretch: a person holds the screen for as
                    // long as the work takes. The session is over before the
                    // child starts, so the terminal is provably free, and the
                    // next session is rebuilt from the model alone.
                    //
                    // BOTH OUTCOMES LAND IN THE MODEL, which is the half a child
                    // cannot do for itself. It inherits the terminal, so a person
                    // sees what it printed - and then this console redraws over
                    // it, and a refusal nobody can see afterwards is a
                    // hand-flight that silently did nothing.
                    //
                    // THE PROMPT IS PASSED IN, because the editor is the loop's
                    // and the order is not. Whether anybody is asked at all
                    // depends on a read the loop may not make - a machine that
                    // cannot serve the flight is told so before it asks somebody
                    // to write a paragraph - so the port decides when to call
                    // this, and the loop only says what "ask" means here.
                    state = flyByHand is null
                        ? state with
                        {
                            LastHandFlight =
                                "This console is not configured to fly flights by hand.",
                            HandFlightProblem =
                                "This console is not configured to fly flights by hand.",
                        }
                        : flyByHand(state, () => editor.Edit(""));

                    // OVER THE CONSOLE, when nothing was created. The refusal is
                    // three sentences and the activity line is one, so the
                    // remedy - the half somebody can act on - ran off the right
                    // edge of the screen. A flight that flew opens nothing: they
                    // watched it happen at a prompt the child owned.
                    state = Reducer.HandFlightAnswered(state);
                    break;

                case Command.SignIn:
                    // THE ONE WRITE A PERSON REACHES BEFORE THERE IS A SESSION,
                    // and the only reason the console is worth drawing on a
                    // machine that has none. Two requests and a credential
                    // written to disk, so it happens here with the terminal
                    // provably free.
                    //
                    // AND RULE 4 AT ITS WIDEST. A sign-in does not invalidate
                    // one pane, it invalidates every read the console makes -
                    // the queue is empty because of exactly this. So the reload
                    // is the point rather than the tidy-up, and it runs only
                    // when a session actually arrived: reloading after a code
                    // was merely fetched would spend a whole round trip to be
                    // refused again.
                    bool arrived;
                    state = SignedIn(state, signIn, out arrived);

                    if (arrived)
                    {
                        state = Reloaded(state, reload, asked: false);
                    }

                    break;

                case Command.TakeFlight:
                    // The same shape, against something much larger: a person
                    // holds the terminal for minutes rather than an editor for
                    // seconds. It works for the same reason - the session is
                    // over before the child starts, so the terminal is provably
                    // free, and the next session is rebuilt from the model
                    // alone.
                    state = Took(state, take);
                    break;

                default:
                    throw new InvalidOperationException(
                        $"UI session exited with {outcome.Exit}, which the shell does not handle");
            }

            // WHAT JUST HAPPENED, in one slot, derived rather than set six times.
            // Each arm above already records its outcome per kind; this is the line a
            // person reads, and taking it from whichever field the arm changed means a
            // new arm cannot forget to say anything.
            state = state with { LastAction = Said(before, state) };
        }
    }

    /// <summary>
    /// The outcome this pass produced, whichever field carried it.
    /// </summary>
    /// <remarks>
    /// Compared rather than assigned, so an arm that records an outcome cannot also
    /// forget to surface it. Quit and the editor change nothing a person needs told.
    /// </remarks>
    /// <summary>What changed, as the one line a person reads.</summary>
    /// <remarks>
    /// <b>Public rather than private so the derivation can be asserted</b>, the
    /// same disposition the other reducible statics here already have. This
    /// assembly is bundled and never packed - <c>PackagingTests</c> names what
    /// may be published and this is not on the list - so "public" here is a
    /// testability decision rather than an API surface.
    /// <para>
    /// Every arm records its outcome in its own field and this takes whichever
    /// moved, which is what stops a new arm forgetting to say anything. That is
    /// worth a test rather than a comment, because arms have forgotten before.
    /// </para>
    /// </remarks>
    public static string? Said(AppState before, AppState after) =>
        after.LastFlightOpened != before.LastFlightOpened ? after.LastFlightOpened
        : after.LastCredential != before.LastCredential ? after.LastCredential
        : after.LastInvite != before.LastInvite ? after.LastInvite
        : after.LastDecision != before.LastDecision ? after.LastDecision
        : after.LastTakeover != before.LastTakeover ? after.LastTakeover
        : after.LastHandBack != before.LastHandBack ? after.LastHandBack
        : after.LastHandFlight != before.LastHandFlight ? after.LastHandFlight
        : after.LastRunner != before.LastRunner ? after.LastRunner
        : after.LastSignIn != before.LastSignIn ? after.LastSignIn
        : before.LastAction;

    /// <summary>
    /// Takes the intent and opens a flight, or opens nothing and says so.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A prompt with an escape to <c>$EDITOR</c>, and it is ONE field.</b> The
    /// prompt's answer seeds the editor rather than competing with it, so there is a
    /// single value with somewhere to grow - two input paths to one field is how they
    /// drift, and an intent that came from either is the same intent.
    /// </para>
    /// <para>
    /// <b>Nothing typed opens nothing.</b> A flight opened by accident is a record
    /// somebody has to explain and a number that is now taken, so an empty answer
    /// says it changed nothing rather than falling silent.
    /// </para>
    /// </remarks>
    /// <summary>
    /// Open a flight from what a person typed.
    /// </summary>
    /// <remarks>
    /// Public so what crosses to the control plane can be asserted directly
    /// rather than through a whole session — the same reason
    /// <see cref="FlewPicked"/> is.
    /// </remarks>
    public static AppState Opened(
        AppState state, IConsoleActions? actions, IEditorSession editor)
    {
        if (actions is null)
        {
            return state with
            {
                LastFlightOpened = "This console is not configured to open flights.",
            };
        }

        // The editor IS the prompt at this cardinality: it opens on an empty buffer,
        // a person types a line or a paragraph, and saving is the answer. Adding a
        // separate one-line reader would be the second path this comment warns about.
        var intent = editor.Edit("").Trim();

        return state with
        {
            LastFlightOpened = intent.Length == 0
                ? "Nothing was opened: no intent was written."
                // THE CHOSEN REPOSITORY CROSSES ON BOTH DOORS. A setting that
                // worked depending on whether you pasted or picked would be
                // worse than no setting.
                : actions.Fly(intent, state.ChosenRepository),
        };
    }

    /// <summary>
    /// Sends the answer, and folds what was sent into the model.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The modal is not closed here.</b> What a gate BECAME is the control
    /// plane's answer - the decision is recorded, the Engine re-evaluates, and the
    /// next load carries the result. A console that closed the modal on the
    /// keystroke would be reporting its own optimism, which is Article IX in its
    /// softest clothing because the demo works.
    /// </para>
    /// <para>
    /// <b>Everything that can go wrong leaves the model intact and says so</b>, the
    /// same shape the takeover has: no actions configured, no gate on this row, or a
    /// refusal from the far side.
    /// </para>
    /// </remarks>
    private static AppState Decided(
        AppState state, IConsoleActions? actions, IEditorSession editor, bool approved)
    {
        if (actions is null || state.SelectedGate is not { } gate)
        {
            return state with
            {
                LastDecision = actions is null
                    ? "This console is not configured to answer gates."
                    : "Nothing on this flight is waiting on a decision.",
            };
        }

        // A REJECTION NEEDS A REASON, and the verb refuses one without it - the loop
        // runs again with the reason, so a rejection that says nothing sends the work
        // back to be done the same way. The console has no text field, and the answer
        // is the one it has always used for text: release the terminal to $EDITOR.
        //
        // Refused HERE as well as there, so a person who changes their mind by
        // saving an empty buffer has not answered a gate by accident.
        string? reason = null;
        if (!approved)
        {
            reason = editor.Edit("").Trim();

            if (reason.Length == 0)
            {
                return state with
                {
                    LastDecision = $"{gate.FlightNumber}: nothing was sent. Rejecting needs a "
                                 + "reason - the loop runs again with it.",
                };
            }
        }

        return state with
        {
            LastDecision = actions.Decide(gate.FlightNumber, gate.ObligationId, approved, reason),
        };
    }

    /// <summary>
    /// Takes one step of signing in, and says whether a session arrived.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Which step is decided by the model, not by counting presses.</b>
    /// Nothing waiting means ask for a code; a code waiting means the person
    /// says they have approved it. Pressing the second key early is an ordinary
    /// answer — the control plane says not yet, and they press it again — but
    /// pressing the FIRST one twice would abandon the code on the screen and
    /// fetch another, leaving the approved one with nobody polling it. The
    /// keymap binds a different key to each step so that cannot happen by
    /// timing.
    /// </para>
    /// <para>
    /// <b>Failure returns to the offer rather than to the code.</b> Expired,
    /// declined and pressed-too-early differ only in the sentence; all three
    /// leave a person somewhere they can start again, which means clearing the
    /// pending authorization. A modal still showing a dead code is a modal
    /// whose only working key does nothing.
    /// </para>
    /// <para>
    /// Public so what this console answers can be asserted directly, the same
    /// reason <see cref="Took"/> is.
    /// </para>
    /// </remarks>
    /// <param name="arrived">
    /// Whether this machine now holds a session. The caller reloads on it — and
    /// only on it, because a code merely fetched has changed nothing the
    /// control plane would answer differently.
    /// </param>
    public static AppState SignedIn(AppState state, ISignInSession? signIn, out bool arrived)
    {
        ArgumentNullException.ThrowIfNull(state);

        arrived = false;

        if (signIn is null)
        {
            // Said, rather than a key that reaches its arm and returns the
            // state unchanged. That shape has cost this console four dead keys,
            // and here it would be worse: the modal exists to be the way out.
            return state with
            {
                LastSignIn = "This console is not configured to sign in. Quit and run gg login.",
            };
        }

        var step = state.SignIn is null ? signIn.Start() : signIn.Wait();

        arrived = step.SignedIn;

        return state with
        {
            // NORMAL ONLY WHEN THERE IS A SESSION. The modal is over the console
            // it exists because of; closing it on anything less would hand a
            // person back an empty queue with no way to ask again.
            Mode = step.SignedIn ? UiMode.Normal : UiMode.SignIn,
            SignIn = step.Pending,
            LastSignIn = step.Said,
        };
    }

    /// <summary>
    /// Runs the takeover and folds what came back into the model.
    /// </summary>
    /// <remarks>
    /// <b>Everything that can go wrong ends with the model intact.</b> No
    /// takeover configured, a child that would not start, a return file that
    /// cannot be trusted - each leaves the flight exactly as it was and says so
    /// on the state the next session renders from.
    /// </remarks>
    /// <remarks>
    /// Public so what this console answers can be asserted directly, the same
    /// reason <see cref="FlewPicked"/> is.
    /// </remarks>
    public static AppState Took(AppState state, ITakeSession? take)
    {
        if (take is null || state.Selected is not { } row || state.TakeableTree is not { } tree)
        {
            return state with
            {
                // TWO DIFFERENT FACTS, AND NEITHER IS ABOUT THIS FLIGHT. The
                // first is a console wired without a take session. The second
                // is every console: ConsoleStart holds no tree by design,
                // because the branch is authoritative and a local tree is a
                // cache this machine may not have. Saying "no held tree for
                // this flight" implied a look at the flight that never
                // happened, and sent people hunting for a tree that was never
                // going to be here.
                LastTakeover = take is null
                    ? "This console is not configured to take flights over."
                    : "Taking over needs the flight's working tree, and this console never "
                    + "holds one — the branch is what is authoritative. It can be done on the "
                    + "machine that ran the flight.",
            };
        }

        var result = take.Take(new TakeRequest
        {
            FlightId = row.FlightId,
            FlightNumber = row.FlightNumber,
            TreePath = tree,
            Seed = state.TakeSeed!,
        });

        return state with
        {
            LastTakeover = result switch
            {
                { Diagnosis: { Length: > 0 } diagnosis } => diagnosis,
                { Decision: { } decision } =>
                    $"{row.FlightNumber}: {decision.Outcome}"
                  + (decision.Note is { Length: > 0 } note ? $" — {note}" : ""),
                _ => $"{row.FlightNumber}: taken over for {result.Held.TotalMinutes:F0} minute(s), "
                   + "and no decision was written.",
            },
            LastTakeoverHeld = result.Held,
        };
    }

    /// <summary>
    /// Runs the hand-back and folds what the person confirmed into the model.
    /// </summary>
    /// <remarks>
    /// <b>Nothing is recorded unless they answered.</b> A walk-away leaves no
    /// account and the state says so, because an unconfirmed proposal stored as
    /// somebody's words attributes a guess to them.
    /// </remarks>
    /// <summary>
    /// Ask the reader, and turn whatever happens into something drawable.
    /// </summary>
    /// <remarks>
    /// <b>The last line, and it catches.</b> <see cref="IWorkBrowser"/> answers
    /// with an outcome and never throws, which is the contract - but a bug in
    /// an implementation is not a failure that contract modelled, and a person
    /// should not lose their console to one. The sentence says the fault is
    /// here rather than at the tracker, because that is where to go and look.
    /// </remarks>
    private static AppState Browsed(AppState state, IWorkBrowser browser)
    {
        var key = browser.Key ?? "the reader";

        try
        {
            return Reducer.Browsed(
                state, key, browser.BrowseAsync(cursor: null, limit: 50, CancellationToken.None)
                    .GetAwaiter().GetResult());
        }
        catch (Exception problem) when (problem is not OperationCanceledException)
        {
            return Reducer.Browsed(state, key, new BrowseOutcome.Unintelligible(
                $"Browsing '{key}' failed inside this console rather than at the tracker: "
              + problem.Message));
        }
    }

    /// <summary>
    /// Open a flight for the work item the browser has selected.
    /// </summary>
    /// <remarks>
    /// <b>Internal to the loop and public for one reason:</b> what crosses to
    /// the control plane on this path is worth asserting directly rather than
    /// through a whole session. The alternative is a test that presses keys to
    /// check a string is absent, which is a worse test of the same thing.
    /// </remarks>
    /// <remarks>
    /// <b>An overload rather than a changed signature</b>, because eight tests
    /// call the three-less one and none of them is asking this question. What
    /// the loop needs and they do not is whether a flight was actually OPENED -
    /// three of this method's four endings open nothing, and only one of them
    /// is worth re-reading the queue for.
    /// </remarks>

    public static AppState FlewPicked(AppState state, IConsoleActions? actions) =>
        FlewPicked(state, actions, out _);

    /// <remarks>
    /// <b><c>opened</c> comes from the branch that ran, never from reading the
    /// sentence afterwards.</b> The first version of this decided by matching
    /// the opening words of three English sentences, one of which lives in
    /// <c>VerbConsoleActions</c> - so rewording a refusal in another file would
    /// silently have flipped a control-flow flag, with no test to go red and
    /// nothing but a console reloading after a write that did not happen. A
    /// decision keyed on user-facing prose has no way to fail loudly when the
    /// prose moves, and this repository rewords prose constantly.
    /// </remarks>
    public static AppState FlewPicked(
        AppState state, IConsoleActions? actions, out bool opened)
    {
        opened = false;
        ArgumentNullException.ThrowIfNull(state);

        if (actions is null)
        {
            return state with
            {
                LastFlightOpened = "This console is not configured to open flights.",
            };
        }

        // NOTHING PICKED IS AN ANSWER, NOT A CRASH. An empty pane with a key
        // that appears to work is worse than one without the key.
        if (state.Browse is not { Items.Count: > 0 } listing
            || state.BrowseSelected < 0
            || state.BrowseSelected >= listing.Items.Count)
        {
            return state with
            {
                LastFlightOpened = "Nothing was opened: no work item is selected.",
            };
        }

        var id = listing.Items[state.BrowseSelected].Id;

        // ASKED BEFORE ANYTHING IS OPENED. Two flights on one work item is
        // legal and usually a mistake, and it is exactly what pressing a key
        // twice produces. A console that refused would decide something the
        // control plane allows.
        if (actions.AlreadyFlown(listing.ProviderKey, id) is { Length: > 0 } why)
        {
            return state with
            {
                Mode = UiMode.ConfirmFlight,
                PendingFlight = new PendingFlight
                {
                    Provider = listing.ProviderKey,
                    Id = id,
                    Why = why,
                },
            };
        }

        // TWO VALUES, DECLARED. Not the title, which is what a person read and
        // not what a flight is called, and not the url, which is not even held.
        //
        // AND THIS IS THE ONE ENDING THAT OPENED ANYTHING, which is why the flag
        // is set here rather than inferred from what the sentence says.
        opened = true;

        return state with
        {
            LastFlightOpened = actions.FlyTicket(
                listing.ProviderKey, id, state.ChosenRepository),
        };
    }

    /// <summary>
    /// Open the second flight after all.
    /// </summary>
    /// <remarks>
    /// <b>From the question, not from the selection.</b> The answer arrives on
    /// a later keystroke and the list may have scrolled or been re-read since;
    /// resolving the selection again would open a flight for whatever is under
    /// the cursor now rather than what was asked about.
    /// </remarks>
    /// <remarks>The overload's reason is <see cref="FlewPicked"/>'s.</remarks>
    public static AppState ConfirmedFlight(
        AppState state, IConsoleActions? actions, out bool opened)
    {
        var asked = state.PendingFlight is not null;
        var after = ConfirmedFlight(state, actions);

        opened = asked && actions is not null;
        return after;
    }

    public static AppState ConfirmedFlight(AppState state, IConsoleActions? actions)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.PendingFlight is not { } pending)
        {
            // Nothing was asked, so nothing is confirmed. A key reaching here
            // otherwise would open a flight for the last thing selected.
            return state;
        }

        return state with
        {
            Mode = UiMode.Normal,
            PendingFlight = null,
            LastFlightOpened = actions is null
                ? "This console is not configured to open flights."
                : actions.FlyTicket(
                    pending.Provider, pending.Id, state.ChosenRepository),
        };
    }

    private static AppState HandedBack(AppState state, IHandSession? hand)
    {
        if (hand is null || state.Selected is not { } row || state.TakeSeed is not { } seed)
        {
            return state with
            {
                LastHandBack = hand is null
                    ? "This console is not configured to hand flights back."
                    : "There is nothing to hand back: this flight has not been taken over.",
            };
        }

        var outcome = hand.Hand(new HandRequest
        {
            FlightId = row.FlightId,
            FlightNumber = row.FlightNumber,
            TreePath = state.TakeableTree ?? "",
            By = state.Principal,
            PriorAccount = seed.Account,
            Measurements = seed.Measurements,
        });

        return state with
        {
            LastHandBack = outcome.Detail,
            // Recorded whichever way it went, INCLUDING the walk-away: a rate
            // that only counted the answers would be a rate of answers.
            HandConfirmations =
            [
                .. state.HandConfirmations.Where(f => f.FlightId != row.FlightId),
                new HandConfirmationFact { FlightId = row.FlightId, Choice = outcome.Choice },
            ],
            // The account joins the seed, so the next person to take this flight
            // over finds it where a resuming reader looks.
            TakeSeed = outcome.Account is { } account
                ? seed with { PriorHuman = account }
                : seed,
            TakenOver = false,
        };
    }
}
