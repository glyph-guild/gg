namespace Gg.Console;

/// <summary>
/// Every change to the model, as a pure function.
/// </summary>
/// <remarks>
/// Nothing here touches a terminal, a clock or a socket, which is why the
/// interaction disciplines can be tested at all. "Arrivals do not move the
/// cursor" is a claim about this file and nowhere else.
/// </remarks>
public static class Reducer
{
    public static AppState Reduce(AppState state, Command command)
    {
        ArgumentNullException.ThrowIfNull(state);

        return command switch
        {
            Command.ToggleHelp => Modal(state, UiMode.Help),

            // NOTHING TO OPEN IS NOT A MODAL. Article XI: a key that appears to
            // work is worse than one that is not offered, and a modal whose
            // only content is the way out is exactly that. The key stays bound
            // because whether a row exists is not the keymap's question.
            // OPENED NOW, FILLED WHEN THE READ LANDS. This was the shell's and
            // changed nothing here: the session ended, one request was made,
            // and a new session was built over the answer - a whole screen
            // taken away and given back to fetch a log. The summary is already
            // in hand from the boot, so the number, the name and the intent
            // draw at once; ReadInFlight is what lets the log say it is still
            // coming rather than that there is none. See Reducer.FlightShown
            // for what happens when it arrives.
            Command.ShowFlight => Modal(state, UiMode.FlightDetail) with
            {
                ReadInFlight = true,

                // ON THE DETAILS, because this is a flight being opened rather
                // than a modal being re-shown. See AppState.FlightTab.
                FlightTab = FlightTab.Details,
            },

            // TWO TABS AND ONE KEY, so it has to come back round.
            // THE MODE IS THIS SIDE'S AND THE CONTENT IS THE READ'S. The
            // modal opens on the press so something happens when a key is
            // pressed - the blink used to be that - and what the reader says
            // arrives afterwards as a patch.
            //
            // AND IT CLEARS WHAT IT IS REPLACING, unless the answer it already
            // holds is about this very row. Stale prose under a new title is
            // worse than an empty pane, because it cannot be told from an
            // answer.
            Command.ShowWorkItem => Holding(state, Under(state)) ? state with
            {
                Mode = UiMode.WorkItemDetail,
            } : state with
            {
                Mode = UiMode.WorkItemDetail,
                WorkItemId = null,
                WorkItemSaid = null,
                WorkItemChanges = [],
                WorkItemHistorySaid = null,
                WorkItemFields = [],
                WorkItemFieldsSaid = null,
                WorkItemRow = null,
                WorkItemSelected = 0,
                WorkItemTab = WorkItemTab.Details,
            },

            // THE FLIGHT'S OWN ITEM, HELD BESIDE THE LISTING. The row is
            // synthesised from the intent because the item is usually not on
            // the page anybody browsed - and replacing the listing to make room
            // for it would throw away a filtered page somebody assembled. What
            // the reader answers arrives afterwards, as every other read does.
            Command.OpenTheTicket => FlightDetails.TicketAReaderHere(state) is var ticket
                                  && ticket is not null
                ? state with
                {
                    Mode = UiMode.WorkItemDetail,
                    WorkItemId = ticket.Value.Id,
                    WorkItemRow = new BrowseRow
                    {
                        Id = ticket.Value.Id,
                        Title = "",
                        State = "",
                    },
                    WorkItemSaid = null,
                    WorkItemChanges = [],
                    WorkItemHistorySaid = null,
                    WorkItemFields = [],
                    WorkItemFieldsSaid = null,
                    WorkItemSelected = 0,
                    WorkItemTab = WorkItemTab.Details,
                }
                : state,

            // THE SAME, one modal over. FilterOffered fills it when the reader
            // answers; this is what makes the key do something before then.
            Command.FilterBrowse => state with { Mode = UiMode.BrowseFilter },

            // THE WORK ITEM MODAL'S OWN, and it cycles for the flight
            // modal's reason: one key that always works beats two that are
            // each wrong half the time.
            // THREE NOW, AND IT COMES BACK ROUND - the flight modal's own
            // sentence, one modal over: a cycle that stopped at the last tab
            // would make the third one a place a person reaches and cannot
            // leave by the key that got them there.
            Command.NextWorkItemTab => state with
            {
                WorkItemTab = state.WorkItemTab switch
                {
                    WorkItemTab.Details => WorkItemTab.History,
                    WorkItemTab.History => WorkItemTab.Fields,
                    _ => WorkItemTab.Details,
                },
            },

            // THREE NOW, AND IT COMES BACK ROUND. A cycle that stopped at the
            // last tab would make the third one a place a person reaches and
            // cannot leave by the key that got them there.
            Command.NextFlightTab => state with
            {
                FlightTab = state.FlightTab switch
                {
                    FlightTab.Details => FlightTab.Gate,
                    FlightTab.Gate => FlightTab.Log,
                    FlightTab.Log => FlightTab.Facts,
                    _ => FlightTab.Details,
                },
            },
            // THE READ ITS OWN COMMAND, so the tab can be reached by the cycle
            // AND asked for directly - and so ShellCommands can name it a read
            // without naming the cycle one. Landing on the tab is not what
            // fetches; pressing for it is.
            Command.ShowFlightFacts => state with { FlightTab = FlightTab.Facts },

            Command.ToggleFlightActions => Modal(state, UiMode.FlightActions),
            Command.ToggleAirspaceActions => Modal(state, UiMode.AirspaceActions),

            // THE THREE THAT ALSO BLINKED. Each was the shell's so the pane
            // could be filled before it was shown, and the cost was the whole
            // screen going away and coming back once per keypress. The toggle
            // is a mode change and belongs here; the read runs beside the
            // console and is folded when it lands.
            //
            // BROWSE IS NOT AMONG THEM. It launches an executable with a
            // credential in its environment, and a session may do neither -
            // AutoRefresh's exception is for a read and does not stretch to a
            // spawn.
            //
            // CLOSING READS NOTHING, which the read function decides rather
            // than this: a toggle that shut a pane and then fetched what to put
            // in it is a request nobody asked for.
            Command.ToggleEnvelope => EnvelopeToggled(state) with { ReadInFlight = true },
            Command.ToggleRepositories => RepositoriesToggled(state) with { ReadInFlight = true },

            // NO READ IN FLIGHT, because the allowances are already in the
            // model: the runners tab's refresh fetches them, and the boot
            // does too. Opening this pane is a rendering rather than a round
            // trip - which is also why it is the one read-backed tab that can
            // be right on the first frame.
            Command.ToggleAllowances => Toggled(state, TabId.Allowances),

            // ASKS RATHER THAN OPENING. The key used to hand the terminal
            // straight to $EDITOR; there are two ways to compose now and neither
            // is the obvious one. This sets a field and nothing else - the loop
            // reads what it recorded, with the terminal released, and starts
            // whichever child was chosen.
            // A DECISION, NOT A FORM. The modal owns the keyboard and offers a
            // few shares; the write itself happens when the session ends,
            // which is why the keys are shell commands rather than reductions.
            Command.AskToKeepAShare => Modal(state, UiMode.FloorChoice),

            Command.AskHowToCompose => Asked(state, ComposingFor.NewFlight),

            // THE SAME QUESTION ABOUT A DIFFERENT FLIGHT. Both hand a child an
            // empty buffer and take back what comes out, so the keys are the
            // same and only the subject differs.
            Command.AskHowToFlyByHand => Asked(state, ComposingFor.HandFlight),
            Command.OpenGate => Modal(state, UiMode.GateDecision),

            // THE SAME MOVE OVER THE OTHER DECISION. Asking is a mode change
            // and nothing else - which is this function's whole job, and the
            // reason both of its answers below leave the state alone.
            Command.AskToAnswerNomination => Modal(state, UiMode.NominationDecision),

            // ASKING IS A MODE CHANGE AND NOTHING ELSE, which is the reducer's
            // whole job. Both of these were written in the loop, where they
            // reached nobody: the screen hands a command to the shell only when
            // the shell declares it, and everything else comes here. `f` did
            // nothing and `x` stopped grounding - a confirmation added to
            // grounding that broke grounding.
            Command.AskToGround => Modal(state, UiMode.ConfirmGround),
            Command.AskToFlyAgain => Modal(state, UiMode.ConfirmFlyAgain),
            Command.AskToApplyEstate => Modal(state, UiMode.ConfirmApply),
            Command.ReadEnvelope => Modal(state, UiMode.ReadingEnvelope),
            Command.ReadChangeset => Modal(state, UiMode.ReadingChangeset),
            Command.ReadOutcome => Modal(state, UiMode.ReadingOutcome),
            Command.ReadSaid => Modal(state, UiMode.ReadingSaid),

            // TURNING A PAGE OVER WHAT IS ALREADY HELD. AirspaceViews answers
            // which views this row has, so a view that stopped being offered -
            // the on-disk one, once its document is applied - cannot be landed
            // on by a cursor that was already there.
            Command.NextRunnerView =>
                state with { RunnerView = RunnerViews.Next(state.RunnerView) },
            Command.NextAirspaceView => state with
            {
                AirspaceView = AirspaceViews.Next(state, state.AirspaceView),
            },

            // WHICH HALF THE ARROW KEYS DRIVE. Both are on screen either way;
            // this is only about the keyboard, which is why it is a flag and
            // not a mode.
            Command.NextAirspacePane => state with
            {
                AirspaceReading = !state.AirspaceReading,
            },
            Command.AskToRetire => Modal(state, UiMode.ConfirmRetire),

            // SET RATHER THAN TOGGLED, unlike the modals beside it: enter is
            // also the key that APPLIES inside the field, so a toggle would
            // make the second press mean two things depending on which side
            // of the mode it landed.
            Command.FocusAirspacePath => state with { Mode = UiMode.AirspacePath },

            // ANSWERING POSTS; IT DOES NOT DECIDE. Both answers leave the state exactly as
            // it is: the loop sends the decision, the control plane records it, the Engine
            // re-evaluates, and what comes back is what closes this modal. A reducer that
            // closed it here would be the console deciding - Article IX in its softest
            // clothing, which is the dangerous kind, because the demo works.
            Command.ApproveGate => state,
            Command.RejectGate => state,

            // AND THE BOARD'S TWO ANSWERS, for that paragraph's reason with one
            // more on top: opening a nomination starts an ADMISSION PASS, which
            // composes the envelope again and may refuse. A reducer that ended
            // the row here would be reporting an outcome nobody has computed
            // yet - and the row would go back to standing on the next refresh,
            // which is worse than never having moved.
            Command.OpenNomination => state,
            Command.DeclineNomination => state,

            // THE SHELL DOES IT, so the reducer does nothing - and it must do
            // nothing, because a local effect here would land whether or not
            // the remote half did.
            Command.LogAgentIn => state,
            // CLOSING A CONFIRMATION IS AN ANSWER, not a dismissal. Leaving
            // PendingFlight behind would let the next 'y' - aimed at something
            // else entirely - open the flight this person just declined.
            Command.CloseModal => state.Mode == UiMode.ConfirmFlight
                ? FlightDeclined(state)
                // AND THE QUESTION CLOSES WITH THE MODAL. One left open behind a
                // closed one is a question the next keypress could answer by
                // accident.
                // AND WHAT WAS TYPED GOES WITH IT, for the same reason: a path
                // half-entered and abandoned would be applied by whichever
                // question opened next.
                : state with
                {
                    Mode = UiMode.Normal,
                    ComposingFor = ComposingFor.Nothing,
                    AskingKindFor = ComposingFor.Nothing,
                    KindSelected = 0,
                    AirspacePathTyped = null,

                    // AND THE ITEM HELD BESIDE THE LISTING GOES WITH THE MODAL
                    // THAT WAS ABOUT IT. Left behind, it would make the next
                    // browse modal open about a flight's ticket instead of the
                    // row under the cursor.
                    WorkItemRow = null,
                },

            // ANSWERING OPENS; IT DOES NOT DECIDE, which is the shape the two
            // gate answers above already have. Both end the session and the loop
            // does the work, because opening a flight spawns a child and makes a
            // request - and the loop is where the terminal is free. A reducer
            // that closed the modal here would be closing it before the thing it
            // asked about had happened.
            // ANSWERED THE SAME WAY, and for the same reason: it opens a
            // flight, which is the loop's to do with the terminal free.
            Command.FlyForKind => state,

            // ASKED HERE, ANSWERED HERE, SENT OUT THERE. The question needs the
            // registry and the runner under the cursor, both of which are in
            // this model; the answer is folded into it so the loop can send
            // once the terminal is free. Only the secret has to wait for that.
            Command.ChooseCredentialRepository =>
                CredentialRepositoryAsked(state) with { ReadInFlight = true },

            Command.ComposeInEditor => state,
            Command.ComposeWithAgent => state,

            // TAB TURNS THE HELP PAGE WHILE HELP OWNS THE KEYBOARD, and moves
            // the focused pane everywhere else. A modal holds the keys for one
            // question, so the key means what the question needs - and it is
            // borrowed rather than taken: Normal mode is unchanged.
            // THE LOOK PAGE'S THREE, and they are only meaningful on it - the
            // keymap binds them there and nowhere else, so a guard here would
            // be a second answer to a question the keymap has already asked.
            Command.NextLookValue => state with { Look = Looks.Turned(state.Look, +1) },
            Command.PreviousLookValue => state with { Look = Looks.Turned(state.Look, -1) },

            // THE CURSOR SURVIVES THE RESET, because it is not a setting - it
            // is where the person is standing, and moving them somewhere else
            // while they undo is a second surprise on top of the one they asked
            // for.
            // THE PEEK SURVIVES THE RESET TOO, for the cursor's reason: it is
            // where a person is standing rather than something they set, and
            // putting the modal back while they are looking behind it would
            // undo the wrong thing.
            // THE COMPOSE MODAL'S TWO TABS, and the repository under the cursor.
            Command.NextWorkKindTab => state with
            {
                WorkKindTab = state.WorkKindTab is WorkKindTab.Kind
                    ? WorkKindTab.Repositories
                    : WorkKindTab.Kind,
            },

            Command.ToggleFlightRepository => FlyingWithToggled(state),

            Command.ResetLook => state with
            {
                Look = new Look { Selected = state.Look.Selected, Peeking = state.Look.Peeking },
            },

            Command.PeekBehindTheModal => state with
            {
                Look = state.Look with { Peeking = !state.Look.Peeking },
            },

            Command.FocusNextPane when state.Mode == UiMode.Help => state with
            {
                // THROUGH THE LIST THE BAR IS DRAWN FROM. Naming the two pages
                // here is how a third gets added to the enum, drawn on the bar
                // and never reached by the key - Tabs.Offered's argument, one
                // modal down.
                HelpPage = HelpPages.Next(state.HelpPage),
            },

            // NOTHING WHERE THERE IS NOTHING TO FOLD. The cursor is on a key
            // most of the time, and a key that silently changed something else
            // is worse than one that does nothing.
            Command.ToggleFold => state.HelpFold is { } fold
                ? HelpTree.Toggle(state, fold)
                : state,

            // TAB WALKS THE OPEN TABS. It used to move focus between the panes
            // that happened to be visible, which under one shared region was
            // the same question; a view takes the whole screen now, so "the
            // next thing" is the next tab and focus follows it.
            Command.FocusNextPane => Arrived(state with { ActiveTab = Tabs.Next(state) }),

            // STRAIGHT THERE, AND NOT THROUGH Showing. That helper closes a
            // view by clearing its visibility flag, and these two have none to
            // clear - they are always there, which is what makes them the
            // things a close lands on. Nothing else about the screen moves:
            // whatever was open stays open behind the tab a person asked for.
            Command.ShowQueueTab => Arrived(state with { ActiveTab = TabId.Queue }),
            Command.ShowFlightsTab => Arrived(state with { ActiveTab = TabId.Flights }),
            Command.ShowBoardTab => Arrived(state with { ActiveTab = TabId.Board }),

            // WHICHEVER LIST HAS THE SCREEN. j and k are one pair of keys over
            // two lists, and moving the queue underneath a person reading work
            // items would change what the flight pane shows for a keystroke
            // they aimed somewhere else.
            Command.PickFilterValue => FilterPicked(state),
            Command.NextFilterView => state with
            {
                FilterView = FilterViews.Next(state.FilterView),
            },
            Command.ClearFilter => FilterCleared(state),

            Command.SelectNext => Moved(state, +1),
            Command.SelectPrevious => Moved(state, -1),

            // ONLY THAT ONE IS WANTED. The reducer is pure and a refresh is a
            // read; the tick starts it and folds what comes back, which is what
            // stops the console tearing the terminal down to do it.
            // AND IT DROPS WHAT A READER ALREADY ANSWERED, which is what makes
            // `g` a way past the cache. A tracker moves, and a person who
            // suspects it has needs one gesture that always costs a request -
            // a cache with no way past it is a stale screen nobody can fix.
            Command.Refresh => state with
            {
                Refresh = state.Refresh with { Wanted = true },
                WorkItemId = null,
                Facets = null,
            },
            // THE LOCAL HALF OF A READ, and it was missing. This arm did not
            // exist while the command was the shell's, and BrowseToggled's own
            // remark gave the reason: a shell command with a reducer arm too
            // has two effects, the local one happening whether or not the
            // remote one did. The command is a read now - only the FIRST press
            // of a console lifetime falls to the shell, because only that one
            // has to start the reader - so every press after it arrived here
            // and found nothing. The pane stayed open and the tracker was
            // asked again each time, which is worse than a dead key: it spends
            // a request to do nothing.
            //
            // BOTH HALVES STILL HAPPEN ONCE, because the two paths do not
            // overlap. ConsoleScreen exits BEFORE reducing when a command is
            // the shell's, and ConsoleLoop switches on the exit command and
            // calls BrowseToggled by name rather than going through here.
            //
            // AND THE READ RUNS AFTER THIS, on the state this returns - so a
            // press that shuts the pane leaves ConsoleBrowsing.Patch with
            // nothing to fetch, which is the sentence that file already keeps.
            Command.ToggleBrowse => BrowseToggled(state),

            // WHOLLY HERE, because showing the fleet reads nothing - it is in
            // the model from the boot. Its four neighbours are the shell's
            // because opening them fetches something.
            Command.ToggleRunners => Toggled(state, TabId.Runners),
            Command.ShowRunner => RunnerShown(state),
            Command.ToggleLive => ToggleLive(state),
            Command.ToggleFreeze => ToggleFreeze(state),

            // Quit and OpenEditor end the UI session; the shell handles them.
            Command.Quit => state,
            _ => state,
        };
    }

    /// <summary>
    /// The choices a tracker offered, with the modal over them.
    /// </summary>
    /// <remarks>
    /// <b>The loop calls this, not <see cref="Reduce"/>.</b> Asking is a spawn,
    /// so the command is the shell's; a reducer arm for it as well would open
    /// the modal whether or not the reader ever answered, over a list from the
    /// last time somebody asked.
    /// </remarks>
    /// <summary>The row the cursor is on, or nothing.</summary>
    private static BrowseRow? Under(AppState state) =>
        state.Browse is { Items.Count: > 0 } listing
        && state.BrowseSelected >= 0
        && state.BrowseSelected < listing.Items.Count
            ? listing.Items[state.BrowseSelected]
            : null;

    /// <summary>Whether what is held is about this very row.</summary>
    /// <remarks>
    /// Both halves, because an id with no prose behind it is a read that was
    /// started and never landed - reopening on that would show an empty modal
    /// and ask nobody to fill it.
    /// </remarks>
    private static bool Holding(AppState state, BrowseRow? row) =>
        row is not null
        && string.Equals(state.WorkItemId, row.Id, StringComparison.Ordinal)
        && state.WorkItemSaid is { Length: > 0 };

    /// <summary>
    /// A chosen value the tracker still offers, or null.
    /// </summary>
    /// <remarks>
    /// <b>AN EMPTY LIST IS NOT AN ANSWER ABOUT THE FILTER.</b> A reader that
    /// answered no sprints at all - this project files nothing by sprint - has
    /// said nothing about whether the one somebody chose still exists. Reading
    /// that as "none of them do" would clear a filter every time a dimension
    /// came back unpopulated, which is a person losing their narrowing to a
    /// tracker having a quiet day.
    /// </remarks>
    private static string? StillOffered(string? chosen, IReadOnlyList<string> offered) =>
        chosen is not { Length: > 0 } || offered.Count == 0
            || offered.Contains(chosen, StringComparer.Ordinal)
            ? chosen
            : null;

    public static AppState FilterOffered(AppState state, BrowseFacets offered)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(offered);

        return state with
        {
            Facets = offered,
            Mode = UiMode.BrowseFilter,

            // WHAT THE TRACKER STILL HAS, and only what it still has. A sprint
            // ends and a team is renamed; a remembered value naming one of
            // those narrows every listing to nothing, which reads exactly like
            // a backlog with no work in it. This is the one place both the
            // choices and what is on offer are known.
            //
            // PER DIMENSION, because losing a team because a sprint ended would
            // be the same defect one column over - and the states are a set, so
            // only the ones that went come off.
            ChosenAreaPath = StillOffered(state.ChosenAreaPath, offered.AreaPaths),
            ChosenIteration = StillOffered(state.ChosenIteration, offered.Iterations),
            ChosenStates = offered.States.Count == 0
                ? state.ChosenStates
                : [.. state.ChosenStates.Where(
                       s => offered.States.Contains(s, StringComparer.Ordinal))],

            // EVERY LIST STARTS AT THE TOP, for Browsed's reason: a cursor left
            // pointing at row nine of a list that now has two picks the wrong
            // thing, silently. All three, because all three were just replaced.
            AreaSelected = 0,
            IterationSelected = 0,
            StateSelected = 0,
        };
    }

    /// <summary>
    /// Pick, or un-pick, whatever the filter cursor is on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>An area path and a sprint REPLACE; a state accumulates.</b> That is
    /// not a preference - a query takes one path and a set of states, so a
    /// modal that let somebody collect two paths would be offering to build a
    /// filter this console could not send.
    /// </para>
    /// <para>
    /// <b>The same key takes a state back.</b> A second key for un-picking is a
    /// key a person has to be taught, and the modal already shows which rows
    /// are picked.
    /// </para>
    /// </remarks>
    private static AppState FilterPicked(AppState state)
    {
        if (BrowseFilters.Under(state) is not { } choice)
        {
            return state;
        }

        // THE SAME KEY BOTH WAYS. Picking what is already picked takes it
        // back: the alternative is a second key to learn, or an invented row to
        // choose instead - and a person who narrowed to the wrong team would
        // otherwise clear all three to fix one.
        return choice.Facet switch
        {
            BrowseFacet.AreaPath => state with
            {
                ChosenAreaPath = choice.Chosen ? null : choice.Value,
            },

            BrowseFacet.Iteration => state with
            {
                ChosenIteration = choice.Chosen ? null : choice.Value,
            },

            BrowseFacet.State => state with { ChosenStates = Toggled(state, choice.Value) },
            _ => state,
        };
    }

    /// <summary>This state added, or removed where it was already picked.</summary>
    private static IReadOnlyList<string> Toggled(AppState state, string? value)
    {
        if (value is not { Length: > 0 })
        {
            return state.ChosenStates;
        }

        return state.ChosenStates.Contains(value, StringComparer.Ordinal)
            ? [.. state.ChosenStates.Where(
                chosen => !string.Equals(chosen, value, StringComparison.Ordinal))]
            : [.. state.ChosenStates, value];
    }

    /// <summary>The whole filter off, in one key.</summary>
    /// <summary>
    /// Every pick off, and every cursor back to the top.
    /// </summary>
    /// <remarks>
    /// <b>The cursors go with the picks, or the reset is not one.</b> A cursor
    /// left on row nine of a list somebody is about to walk again picks the
    /// wrong thing on the next press - which is the argument
    /// <see cref="FilterOffered"/> already makes about replacing the lists, and
    /// it is the same argument here because clearing is the other way the lists
    /// stop meaning what the cursor was pointing at.
    /// </remarks>
    private static AppState FilterCleared(AppState state) => state with
    {
        ChosenAreaPath = null,
        ChosenIteration = null,
        ChosenStates = [],
        AreaSelected = 0,
        IterationSelected = 0,
        StateSelected = 0,
    };

    /// <summary>
    /// Move the cursor of whichever list is showing, and only that one.
    /// </summary>
    /// <remarks>
    /// The three lists have nothing to do with each other: row nine of the
    /// sprints is not row nine of anything, so a shared index would move
    /// somebody's place in a list they were not looking at.
    /// </remarks>
    /// <summary>
    /// Move the cursor in the history, and not in the list behind the modal.
    /// </summary>
    /// <remarks>
    /// The tab under this modal is Browse, so without an arm here the work list
    /// would scroll behind a dialog about one of its rows - and which row that
    /// is is what the modal is about.
    /// </remarks>
    private static AppState PickWorkItemChange(AppState state, int row) =>
        state.WorkItemChanges.Count == 0
            ? state
            : state with
            {
                WorkItemSelected = Math.Clamp(row, 0, state.WorkItemChanges.Count - 1),
            };

    private static AppState PickFilterRow(AppState state, int row)
    {
        var rows = BrowseFilters.Offered(state, state.FilterView);

        if (rows.Count == 0)
        {
            return state;
        }

        var at = Math.Clamp(row, 0, rows.Count - 1);

        return state.FilterView switch
        {
            BrowseFacet.Iteration => state with { IterationSelected = at },
            BrowseFacet.State => state with { StateSelected = at },
            _ => state with { AreaSelected = at },
        };
    }

    /// <summary>
    /// A modal is entered, or left by pressing the same key again.
    /// </summary>
    /// <remarks>
    /// Opening a second modal over the first would create a stack, and a stack
    /// needs as many escapes as it has depth. One at a time keeps "exactly one
    /// escape hatch" true rather than aspirational.
    /// </remarks>
    private static AppState Modal(AppState state, UiMode mode) =>
        state with
        {
            Mode = state.Mode == mode ? UiMode.Normal : mode,

            // HELP ALWAYS OPENS ON THE KEYS. A modal that remembered a page
            // from ten minutes ago would answer a question nobody just asked,
            // and "what can I press" is what somebody pressing ? is asking.
            HelpPage = HelpPage.Keys,
        };

    /// <summary>
    /// The next pane that is actually on screen.
    /// </summary>
    /// <remarks>
    /// Focus never lands on a hidden pane. Somewhere invisible holding the
    /// focus ring reads exactly like a frozen keyboard, and the person's next
    /// move is to kill the terminal.
    /// </remarks>
    /// <summary>
    /// Shows a view, or closes it when it is already the one showing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>THE KEY MEANS "SHOW ME THIS".</b> It only means "close it" when it is
    /// already what you are looking at - so pressing <c>v</c> while reading the
    /// live view brings the evidence tab forward rather than silently
    /// discarding it. A key that closed an open-but-not-showing tab would throw
    /// away what somebody was comparing against, which is the thing this whole
    /// change exists to stop.
    /// </para>
    /// <para>
    /// <b>Closing lands on the queue.</b> It is the one tab that cannot be
    /// closed, which is what makes closing the tab you are on a move with
    /// somewhere to go.
    /// </para>
    /// </remarks>
    private static AppState Showing(AppState state, TabId tab, bool open) => Arrived(state with
    {
        ActiveTab = open ? tab : TabId.Queue,
        LiveVisible = tab == TabId.Live ? open : state.LiveVisible,
        BrowseVisible = tab == TabId.Browse ? open : state.BrowseVisible,
        RepositoriesVisible = tab == TabId.Repositories ? open : state.RepositoriesVisible,
        EnvelopeVisible = tab == TabId.Envelope ? open : state.EnvelopeVisible,
        AllowancesVisible = tab == TabId.Allowances ? open : state.AllowancesVisible,
    });

    /// <summary>
    /// Arriving at a tab nobody has read asks for it now.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The boot fetches what the queue needs, and a tab with reads of its
    /// own is not one of them.</b> The board is two routes - the nominations
    /// and the watch standings - so pressing its key on a fresh console drew a
    /// sentence until <c>AutoRefresh</c>'s next tick, up to thirty seconds
    /// later. What the pane said in the meantime was that the read had failed,
    /// which is a different thing from nobody having made one.
    /// </para>
    /// <para>
    /// <b>HERE, BECAUSE THIS IS THE EDGE.</b> Asking is level-triggered
    /// anywhere else - "read while this tab is unread" - and a control plane
    /// that is refusing would then be asked once per frame, for as long as
    /// somebody left the console open on that tab. A tab CHANGE happens once.
    /// </para>
    /// <para>
    /// <b>And it asks for nothing when the answer is already on the screen</b>,
    /// which is what stops the six tab keys becoming six requests. `g` is still
    /// the gesture that always costs one.
    /// </para>
    /// </remarks>
    private static AppState Arrived(AppState state) =>
        Tabs.HasRead(state, state.ActiveTab)
            ? state
            : state with { Refresh = state.Refresh with { Wanted = true } };

    /// <summary>What a view's own key does to it.</summary>
    /// <remarks>
    /// One function so the six keys cannot disagree about what a second press
    /// means. Returns whether the view is open afterwards, because the shell
    /// reads that to decide whether to fetch anything.
    /// </remarks>
    internal static AppState Toggled(AppState state, TabId tab) =>
        Showing(state, tab, open: !(state.ActiveTab == tab && Tabs.HasRead(state, tab)));

    /// <summary>
    /// Moves the cursor, and marks what it lands on as read.
    /// </summary>
    /// <summary>
    /// The detail under the selected row, out of what was already fetched.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Rule 3, and the reason the boot loads what it loads.</b> No I/O inside
    /// a UI session, so an arrow key is this and nothing else: the summary comes
    /// out of the flight list the boot fetched and the log out of the logs it
    /// fetched in the loop it was already running.
    /// </para>
    /// <para>
    /// <b>NULL WHEN NOTHING WAS LOADED FOR THIS ROW</b>, rather than leaving the
    /// previous flight in place. One flight's detail under another flight's name
    /// is the worst of the three answers, because it is the one a person cannot
    /// see is wrong.
    /// </para>
    /// <para>
    /// <b>Shared with the loader, which is why it is not private.</b>
    /// <c>ConsoleStart.LoadAsync</c> had its own copy of this rule that read
    /// <c>queue[0]</c> - correct at boot, where the cursor is at the top, and
    /// wrong from the moment step 3 made that method the refresh as well.
    /// </para>
    /// </remarks>
    public static AppState Detail(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        // A ROW THAT IS NOT A FLIGHT TAKES THE SAME PATH AS NO ROW AT ALL, and
        // that is the whole of this method's answer to the queue's other kind
        // of row. Every field below is about a flight - the summary, the log,
        // the attribution, the story - and a standing nomination has none of
        // them, so the honest state is the cleared one. Leaving the previous
        // row's four fields standing is exactly the defect the story taught
        // this method: a pane going on describing something the cursor had
        // already left.
        if (state.Selected is not { FlightId: { } flightId } row)
        {
            // AND THE STORY, which was the field this forgot. Approving the
            // last waiting decision empties the queue, and the pane beside it
            // went on describing the flight somebody had just dealt with -
            // because PaneText.Flight asks whether story AND flight are null,
            // so one surviving answered for both.
            return state with
            {
                Flight = null,
                FlightLog = null,
                Attribution = null,
                Story = null,
            };
        }

        return state with
        {
            Flight = state.Flights?.Flights.FirstOrDefault(f =>
                string.Equals(f.FlightId, flightId, StringComparison.Ordinal)),
            FlightLog = state.Logs.TryGetValue(flightId, out var log) ? log : null,

            // KEPT ONLY WHILE IT IS ABOUT THIS ROW. Unlike the two above, an
            // attribution is not held per flight anywhere - it is read for the
            // selected row alone - so there is nothing to look up and the honest
            // answer for any other row is that it has not been read. It names a
            // HALT, which is the worst thing to leave under the wrong name.
            Attribution = string.Equals(
                state.Attribution?.FlightNumber, row.FlightNumber, StringComparison.Ordinal)
                    ? state.Attribution
                    : null,

            // AND THE SAME RULE FOR THE SAME REASON. A story is read for the
            // selected row alone - the line below the attribution's in the
            // boot - so it is not held per flight either, and the honest
            // answer for any other row is that it has not been read. It is an
            // ACCOUNT OF WHAT HAPPENED, which under the wrong name is the
            // worst kind of wrong: every word of it is true about a flight
            // nobody is looking at.
            Story = string.Equals(
                state.Story?.FlightNumber, row.FlightNumber, StringComparison.Ordinal)
                    ? state.Story
                    : null,
        };
    }

    /// <remarks>
    /// Clamped rather than wrapped: running off the end of a short queue and
    /// arriving at the top is a way to act on the wrong flight while believing
    /// you moved one row.
    /// </remarks>
    private static AppState Select(AppState state, int index)
    {
        if (state.Queue.Count == 0)
        {
            return state with { SelectedRow = 0 };
        }

        var landed = Math.Clamp(index, 0, state.Queue.Count - 1);

        var moved = Detail(state with
        {
            SelectedRow = landed,
            Queue = [.. state.Queue.Select((r, i) => i == landed ? r with { UnreadArrivals = 0 } : r)],
        });

        // Moving the cursor while the live view is open IS watching the flight
        // you moved to. Counting only the keypress that opened the pane would
        // measure how often somebody presses `l`, which is not the number we
        // want to fall.
        return moved.LiveVisible ? RecordAttach(moved, attached: true) : moved;
    }

    /// <summary>
    /// Shows or hides the live view, and records that it happened.
    /// </summary>
    /// <remarks>
    /// The fact is written on ATTACH only. Counting detaches too would double
    /// every number and make a rate that should fall look like it doubled.
    /// </remarks>
    private static AppState ToggleLive(AppState state)
    {
        var next = Toggled(state, TabId.Live);

        // THE FACT IS ABOUT ATTACHING, not about which tab is showing. Bringing
        // an already-open live tab forward is not a second attach, and counting
        // it as one would double every number on a rate that should fall.
        return RecordAttach(next, next.LiveVisible && !state.LiveVisible);
    }

    /// <summary>
    /// Writes the fact that somebody watched this flight.
    /// </summary>
    /// <remarks>
    /// The count goes up on ATTACH only. Counting detaches would double every
    /// number and make a rate that should fall look like it doubled.
    /// </remarks>
    private static AppState RecordAttach(AppState state, bool attached)
    {
        // A NOMINATION CANNOT BE WATCHED, so there is no fact to write. The
        // live pane tails a flight's output and a standing nomination has no
        // flight - so a fact keyed on a blank id would be one row that every
        // unwatchable row shared, and the count on it would be a count of
        // nothing anybody watched.
        if (state.Selected is not { FlightId: { } flightId })
        {
            return state;
        }

        var existing = state.AttachFacts.FirstOrDefault(f => f.FlightId == flightId);
        var updated = new LiveAttachFact
        {
            FlightId = flightId,
            Attached = attached,
            AttachCount = (existing?.AttachCount ?? 0) + (attached ? 1 : 0),
        };

        return state with
        {
            AttachFacts = existing is null
                ? [.. state.AttachFacts, updated]
                : [.. state.AttachFacts.Select(f => f.FlightId == flightId ? updated : f)],
        };
    }

    /// <summary>
    /// Holds the screen still, and lets it go again.
    /// </summary>
    /// <remarks>
    /// Thawing flushes what arrived rather than discarding it. A copy that
    /// works, over output with a hole in it that nobody can see, is worse than
    /// a screen that moved while they were selecting.
    /// </remarks>
    private static AppState ToggleFreeze(AppState state) =>
        state.Frozen
            ? state with { Frozen = false, Live = [.. state.Live, .. state.Held], Held = [] }
            : state with { Frozen = true };

    /// <summary>
    /// A line of runner output arrived.
    /// </summary>
    /// <remarks>
    /// While frozen it is held, not shown and not dropped.
    /// </remarks>
    /// <summary>
    /// How many lines either buffer keeps.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>An unbounded list is also an unbounded <c>GG_STATE_DUMP</c>.</b>
    /// AppState is serialized when the terminal is released, so a pane left
    /// attached to a long flight would write every line it ever saw to disk on
    /// the way out.
    /// </para>
    /// <para>
    /// Five hundred is more scrollback than a terminal shows and, at the walk's
    /// measured mean of 69 characters a line, about 35 KB of state. <c>Held</c>
    /// gets the same cap for a different reason: a freeze somebody forgot is a
    /// buffer with no ceiling.
    /// </para>
    /// </remarks>
    private const int Keep = 500;

    public static AppState StreamArrived(AppState state, StreamLine line)
    {
        ArgumentNullException.ThrowIfNull(state);

        return state.Frozen
            ? state with { Held = Capped(state.Held, line) }
            : state with { Live = Capped(state.Live, line) };
    }

    /// <summary>Appends, and drops the oldest first when full.</summary>
    /// <remarks>
    /// Oldest first because the newest is what a person is reading. Dropping
    /// the newest to protect the cap would be a pane that stops updating at
    /// exactly the moment it matters most.
    /// </remarks>
    private static IReadOnlyList<StreamLine> Capped(IReadOnlyList<StreamLine> lines, StreamLine line)
    {
        if (lines.Count < Keep)
        {
            return [.. lines, line];
        }

        return [.. lines.Skip(lines.Count - Keep + 1), line];
    }

    /// <summary>
    /// A flight started needing attention.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Arrivals queue; they do not preempt.</b> The row appears, or its
    /// unread count goes up, and the cursor stays where the person left it.
    /// </para>
    /// <para>
    /// The cursor follows the FLIGHT rather than the index, which matters more
    /// than the obvious half: an arrival that sorts above the selection would
    /// otherwise move somebody to a different flight while the cursor appeared
    /// not to move at all. That is worse than moving it, because nothing on
    /// screen says it happened.
    /// </para>
    /// <para>
    /// One exception: a flight you just started or took. Going there is the
    /// answer to what you just did.
    /// </para>
    /// </remarks>
    /// <summary>Show the repositories, or close them, keeping what was read.</summary>
    /// <remarks>
    /// Through <see cref="Toggled"/> like the other five, so what a second
    /// press means is decided in one place. It used to clear three other flags,
    /// because all four drew into one region.
    /// </remarks>
    /// <summary>
    /// Open the detail modal over the flight under the cursor.
    /// </summary>
    /// <remarks>
    /// <b>Named, like every other effect the shell owns.</b> The loop reads the
    /// flight's log with the terminal released and then calls this; a
    /// <c>Reduce</c> arm that opened the modal itself would be the second effect
    /// <c>ShellHandledTests</c> forbids - the modal would open whether or not the
    /// read happened, over a pane that says nothing was fetched.
    /// </remarks>
    /// <remarks>
    /// It still refuses when there is no flight under the cursor. Whether a row
    /// exists is not the keymap's question and it is not the loop's either.
    /// </remarks>
    public static AppState FlightShown(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        // AT THE TOP OF THE LOG, EVERY TIME. One story is held at a time and
        // the cursor into it is not; a place left where the last flight's
        // history ended is a place pointing into a history it was never about.
        return PaneText.Detailed(state) is null
            ? state
            : Modal(state, UiMode.FlightDetail) with
            {
                LogSelected = 0,

                // THE OTHER DOOR INTO THIS MODAL, and it resets for the reason
                // the one above does. Two openings that disagreed about which
                // tab you land on would be the worse kind of inconsistency:
                // invisible until somebody uses both.
                FlightTab = FlightTab.Details,
            };
    }

    /// <summary>
    /// Open the modal over a hand-flight that created nothing.
    /// </summary>
    /// <remarks>
    /// Named, like <see cref="FlightShown"/> and for the same reason: the effect
    /// belongs to a shell command, and a <c>Reduce</c> arm that opened a modal
    /// would open it whether or not the attempt had been made.
    /// </remarks>
    /// <summary>
    /// Open the modal over the runner on this machine.
    /// </summary>
    /// <remarks>
    /// <b>Pure, unlike its neighbours.</b> What the runner is doing is already
    /// in the model - the thing that owns the child folds it in between sessions
    /// and on the tick - so opening this reads nothing and the reducer can do
    /// the whole of it.
    /// </remarks>
    public static AppState RunnerShown(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return Modal(state, UiMode.Runner);
    }

    /// <summary>
    /// Opens what the apply came to, when it came to anything.
    /// </summary>
    /// <remarks>
    /// <b>HandFlightAnswered's shape and its reason.</b> The loop calls this
    /// after the apply returns, with the terminal already back: a person who
    /// pressed `y` is owed the answer, and the row it used to go in is one row.
    /// An apply with nothing to say opens nothing, or every pass would put a
    /// box over the console.
    /// </remarks>
    public static AppState ApplyAnswered(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return state.ApplyOutcome is { Count: > 0 }
            ? Modal(state, UiMode.ReadingOutcome)
            : state;
    }

    public static AppState HandFlightAnswered(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return state.HandFlightProblem is null ? state : Modal(state, UiMode.HandFlight);
    }

    public static AppState RepositoriesToggled(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return Toggled(state, TabId.Repositories);
    }

    /// <summary>
    /// Choose the repository under the cursor, or unchoose it.
    /// </summary>
    /// <remarks>
    /// <b>Choosing the one already chosen clears it</b>, which is the only way
    /// back to the ordinary state: a flight naming no repository is one the
    /// envelope resolves, and that is what every flight does today. Without
    /// this, a person who chose by mistake could never undo it.
    /// </remarks>
    public static AppState RepositoryChosen(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.Repositories is not { Repositories.Count: > 0 } listed
            || state.RepositorySelected < 0
            || state.RepositorySelected >= listed.Repositories.Count)
        {
            return state;
        }

        var path = listed.Repositories[state.RepositorySelected].Path;

        // ADDS OR REMOVES, rather than replacing. A flight may name several
        // and the registry is where the usual set is said; the compose modal's
        // own tab is where one flight departs from it.
        return state with
        {
            ChosenRepositories = state.ChosenRepositories.Contains(path, StringComparer.Ordinal)
                ? [.. state.ChosenRepositories.Where(
                    r => !string.Equals(r, path, StringComparison.Ordinal))]
                : [.. state.ChosenRepositories, path],
        };
    }

    /// <summary>
    /// Add the repository under the cursor to this flight, or take it off.
    /// </summary>
    /// <remarks>
    /// <b>The registry's toggle, scoped to one flight.</b> That one changes
    /// what every NEW flight starts with; this one changes the flight being
    /// composed and nothing after it. They are deliberately the same gesture on
    /// the same list, because they are the same question asked at two ranges.
    /// </remarks>
    private static AppState FlyingWithToggled(AppState state)
    {
        if (state.Repositories is not { Repositories.Count: > 0 } listed
            || state.RepositorySelected < 0
            || state.RepositorySelected >= listed.Repositories.Count)
        {
            return state;
        }

        var path = listed.Repositories[state.RepositorySelected].Path;

        return state with
        {
            FlyingWith = state.Against.Contains(path, StringComparer.Ordinal)
                ? [.. state.Against.Where(
                    r => !string.Equals(r, path, StringComparison.Ordinal))]
                : [.. state.Against, path],
        };
    }

    /// <summary>
    /// What a reader answered, as the pane will draw it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Not a <c>Command</c>, for <c>StreamArrived</c>'s reason.</b> No
    /// keystroke caused this: a browse came back. Data that arrives from
    /// outside enters the model through its own door.
    /// </para>
    /// <para>
    /// <b>THE ONE PLACE FIVE ENDINGS BECOME A LISTING.</b> Every
    /// <see cref="BrowseOutcome"/> maps, and the switch is exhaustive on
    /// purpose - an outcome added without a mapping would arrive as a null
    /// <c>Browse</c>, which the pane renders as "no tracker is configured",
    /// the single most misleading sentence available. So the fallback is the
    /// outcome's own type name rather than silence: wrong, but loudly.
    /// </para>
    /// <para>
    /// <b>Replaces rather than appends.</b> A page is a page, not a log:
    /// appending would grow the state without bound while a person paged, and
    /// show them rows they had already scrolled past.
    /// </para>
    /// <para>
    /// <b>Nothing else moves.</b> A background answer that reset the selection
    /// or the focused pane would lose a person their place for a fetch they did
    /// not ask about.
    /// </para>
    /// </remarks>
    /// <summary>
    /// Show or hide the browser, keeping what it found.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>ONE REGION, ONE PANE.</b> Evidence and Live already share the screen's
    /// one detail region by never both being on; a third joins the same rule
    /// rather than being drawn over them.
    /// </para>
    /// <para>
    /// <b>REACHED BOTH WAYS, and it used to be reached one.</b> While
    /// <c>ToggleBrowse</c> was the shell's, this was deliberately absent from
    /// <see cref="Reduce"/>: a shell command with a reducer arm too has two
    /// effects, the local one happening whether or not the remote one did. The
    /// command is a read now and only the FIRST press of a console lifetime
    /// falls to the shell, so a <c>Reduce</c> with no arm meant the key worked
    /// once and then stopped - reported from use, in those words. The two paths
    /// still do not overlap: <c>ConsoleScreen</c> exits before reducing when a
    /// command is the shell's, and <c>ConsoleLoop</c> calls this by name.
    /// </para>
    /// <para>
    /// <b>The listing survives hiding.</b> Somebody who closes the pane and
    /// opens it again should not pay for a second read of the tracker to see
    /// what they were just looking at - and the shell path means that read
    /// costs a whole session rebuild.
    /// </para>
    /// </remarks>
    /// <summary>
    /// Move whichever list has the screen.
    /// </summary>
    /// <remarks>
    /// <b>Three lists, three cursors, one pair of keys.</b> The queue's
    /// selection is what the flight pane hangs off, so moving it under somebody
    /// reading work items or repositories would change what they return to for
    /// a keystroke aimed elsewhere. Ordered so the pane actually on screen
    /// wins; the visibility flags are mutually exclusive by construction, and
    /// this does not rely on that.
    /// </remarks>
    /// <remarks>
    /// <b>ON THE TAB THAT HAS THE SCREEN, and it used to ask the flags.</b> That
    /// was the same question while six views shared one region; under tabs a
    /// flag means the view is OPEN, so j and k moved the repository cursor
    /// while a person was looking at the queue. The tab showing is the only
    /// thing that can answer "which list is the person pointing at".
    /// </remarks>
    /// <summary>
    /// Which work kind the question is sitting on.
    /// </summary>
    /// <remarks>
    /// <b>Clamped to the list with `no kind' at the top.</b> One more row than
    /// the tenant declared, because inheriting the floor is an answer and not an
    /// absence of one.
    /// </remarks>
    /// <summary>
    /// What is it for, then how will it be written - in that order.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The kind first, because the other question sends somebody away.</b>
    /// Both compose answers hand the terminal to something else, so a kind
    /// asked afterwards would be asked of a person who has just come back from
    /// writing a paragraph and is holding a different thought.
    /// </para>
    /// <para>
    /// <b>And not asked at all when the tenant declared none.</b> One possible
    /// answer is not a question; for those tenants this is exactly the modal it
    /// always was.
    /// </para>
    /// </remarks>
    private static AppState Asked(AppState state, ComposingFor door) =>
        WorkKinds.Declared(state).Count > 0
            ? Modal(state, UiMode.WorkKindChoice) with
            {
                AskingKindFor = door,
                KindSelected = 0,

                // SEEDED FROM THE REGISTRY'S MARKS, which is what makes them a
                // default rather than a command. The common case is a console
                // set once; the flight that differs differs here, and nobody
                // has to go back and change a console-wide switch to fly it.
                FlyingWith = state.ChosenRepositories,
                WorkKindTab = WorkKindTab.Kind,
                RepositorySelected = 0,

                // AND THE REGISTRY IS BEING FETCHED, which the second tab says
                // rather than drawing an empty table - the credential
                // chooser's arm one modal over, for its reason.
                ReadInFlight = true,
            }
            : Modal(state, UiMode.ComposeChoice) with
            {
                ComposingFor = door,

                // THE OTHER DOOR SEEDS IT TOO. A tenant that declares no work
                // kinds never sees the modal above, and a flight opened through
                // this one must still name what the registry says.
                FlyingWith = state.ChosenRepositories,
            };

    private static AppState PickWorkKind(AppState state, int row) =>
        state with
        {
            KindSelected = Math.Clamp(row, 0, WorkKinds.Declared(state).Count),
        };

    /// <summary>
    /// Ask which repository a credential is for, here rather than at a prompt.
    /// </summary>
    /// <remarks>
    /// <b>Asked ALWAYS, unlike the work-kind question.</b> That one is skipped
    /// when a tenant declared no kinds, because one possible answer is not a
    /// question. Here the last row is a question in its own right - it is the
    /// prompt this replaced - so there is never only one answer, and a console
    /// holding no registry still has something to say rather than an empty list
    /// that would read as "there are no repositories".
    /// </remarks>
    public static AppState CredentialRepositoryAsked(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return Modal(state, UiMode.CredentialRepositoryChoice) with
        {
            CredentialRepoSelected = 0,
        };
    }

    /// <summary>
    /// Moves the chooser's cursor, clamped to the rows it actually draws.
    /// </summary>
    /// <remarks>
    /// Clamped to <c>Count - 1</c> rather than to <c>Count</c>, unlike
    /// <see cref="PickWorkKind"/>: that list has a synthetic row zero and this
    /// one has a synthetic row LAST, so the end that needs room is the other
    /// one.
    /// </remarks>
    private static AppState PickCredentialRepository(AppState state, int row)
    {
        // AN EMPTY LIST IS REACHABLE NOW, and it was not while the chooser
        // carried a row that asked at the prompt. Clamping to Count - 1 threw
        // ArgumentException on a console whose registry is empty or has not
        // landed - a crash on an arrow key, found by the escape-hatch walk
        // rather than by anything aimed at this.
        var rows = CredentialRepositories.Rows(state).Count;

        return rows == 0
            ? state with { CredentialRepoSelected = 0 }
            : state with { CredentialRepoSelected = Math.Clamp(row, 0, rows - 1) };
    }

    private static AppState Moved(AppState state, int by) =>
        // A MODAL WITH A LIST IN IT OWNS THE CURSOR, because it owns the
        // keyboard. The tab is what answers this the rest of the time, and it
        // is still there UNDER the modal - so without this arm the flights list
        // would move behind a modal that is about one particular flight.
        state.Mode is UiMode.FlightDetail
            ? PickLogEntry(state, state.LogSelected + by)
            : state.Mode is UiMode.WorkKindChoice
            ? PickWorkKind(state, state.KindSelected + by)
            : state.Mode is UiMode.CredentialRepositoryChoice
            ? PickCredentialRepository(state, state.CredentialRepoSelected + by)
            : state.Mode is UiMode.BrowseFilter
            ? PickFilterRow(state, BrowseFilters.Cursor(state) + by)
            : state.Mode is UiMode.WorkItemDetail
            ? PickWorkItemChange(state, state.WorkItemSelected + by)
            : state.ActiveTab switch
            {
                TabId.Repositories => PickRepository(state, state.RepositorySelected + by),
                TabId.Browse => PickWork(state, state.BrowseSelected + by),
                TabId.Flights => PickFlight(state, state.FlightSelected + by),
                TabId.Board => PickBoardRow(state, state.BoardSelected + by),
                TabId.Runners => PickRunner(state, state.RunnerSelected + by),
                TabId.Envelope => PickAirspaceRow(state, state.AirspaceSelected + by),
                _ => Select(state, state.SelectedRow + by),
            };

    /// <summary>
    /// The person is pointing at this row of whichever list has the screen.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>WHAT A CLICK NEEDS AND THE KEYS NEVER HAD.</b>
    /// <c>QueueSelection.Wanted</c> can answer only <c>SelectNext</c> or
    /// <c>SelectPrevious</c>, so a jump of five rows moved the cursor one -
    /// invisible while the only way to move was a key that steps, and obvious
    /// the moment a table hands over a row number.
    /// </para>
    /// <para>
    /// <b>Through the same movers the keys use</b>, so pointing and stepping
    /// cannot come to mean different things: the queue's own mover marks the
    /// row read and reloads the detail beside it, and a second path that set
    /// the index alone would look right and quietly stop doing both.
    /// </para>
    /// </remarks>
    /// <summary>
    /// Records how wide the activity line is, as the layout found it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The same instance when nothing moved</b>, because the screen renders
    /// on every model it is handed and a terminal raises a layout pass for
    /// reasons that have nothing to do with its width. A new model per pass
    /// would be a repaint per pass.
    /// </para>
    /// <para>
    /// <b>Measured rather than assumed</b>: the line is <c>Dim.Fill()</c>, so
    /// its width is the terminal's, and the one place that knows it is the
    /// view. What it is FOR is the keymap, which cannot see a view — see
    /// <see cref="AppState.SaidColumns"/>.
    /// </para>
    /// </remarks>
    public static AppState SaidMeasured(AppState state, int columns)
    {
        ArgumentNullException.ThrowIfNull(state);

        return state.SaidColumns == columns ? state : state with { SaidColumns = columns };
    }

    public static AppState Pointed(AppState state, int row)
    {
        ArgumentNullException.ThrowIfNull(state);

        // THE LOOK PAGE'S TABLE, before the modes below it: the help modal is a
        // mode like any other, and the row somebody clicked is which SETTING
        // they are about to change. Falling through would move whatever list is
        // behind the modal instead.
        if (state.Mode is UiMode.Help && state.HelpPage is HelpPage.Look)
        {
            return state with { Look = state.Look with { Selected = row } };
        }

        // THE SAME ARM AS Moved, for the same reason: a person clicking inside
        // a modal is pointing at the modal's list, and the row they hand over
        // is an ENTRY - the view has already mapped it back through
        // LogRow.Entry, because a continuation row is not one.
        if (state.Mode is UiMode.FlightDetail)
        {
            return PickLogEntry(state, row);
        }

        // AND THE RUNNER MODAL'S TWO TABLES, before the tab switch below for
        // the reason that switch is the hazard: the tab behind this modal is
        // Runners, so falling through would move the FLEET's cursor and change
        // which runner the modal is about, under somebody reading it.
        if (state.Mode is UiMode.Runner)
        {
            return state.RunnerView switch
            {
                RunnerView.Environments => PickRunnerEnvironment(state, row),
                RunnerView.Members => PickRunnerMember(state, row),

                // THE LOG IS A LIST RATHER THAN A TABLE, so nothing points at a
                // row in it and there is no cursor to move.
                _ => state,
            };
        }

        // AND THE FILTER MODAL'S THREE, for the same reason one arm up: the tab
        // behind it is Browse, so falling through would move the WORK list's
        // cursor and change which item a person flies, under a modal that is
        // not about that at all.
        if (state.Mode is UiMode.BrowseFilter)
        {
            return PickFilterRow(state, row);
        }

        if (state.Mode is UiMode.WorkItemDetail)
        {
            return PickWorkItemChange(state, row);
        }

        // AND THE KINDS. Every modal above draws a list of its own and every
        // one of them had to be added here as it did - which is what a list of
        // special cases does when it is a list rather than a rule. Missing it
        // for this one made the modal unusable: the arrows moved the work list
        // behind the dialog, KindSelected never changed, and the render that
        // followed put the highlight back on row zero.
        if (state.Mode is UiMode.WorkKindChoice)
        {
            // WHICHEVER TAB IS SHOWING, because the modal has two lists now and
            // a click lands in the one on screen. Sending every row to the
            // kinds table would move the kind under somebody picking a
            // repository - the defect this list of special cases exists for,
            // one tab in.
            return state.WorkKindTab is WorkKindTab.Repositories
                ? state with { RepositorySelected = row }
                : PickWorkKind(state, row);
        }

        // AND THE REGISTRY A CREDENTIAL IS SENT FOR, which shipped without this
        // arm one slice after the kinds did - so the list of special cases
        // above caught the same defect twice in a row. The tab behind this
        // dialog is Runners, so falling through moved the FLEET's cursor and
        // changed which machine the secret was going to, under somebody
        // choosing a repository.
        if (state.Mode is UiMode.CredentialRepositoryChoice)
        {
            return PickCredentialRepository(state, row);
        }

        return state.ActiveTab switch
        {
            TabId.Repositories => PickRepository(state, row),
            TabId.Browse => PickWork(state, row),
            TabId.Flights => PickFlight(state, row),
            TabId.Board => PickBoardRow(state, row),
            TabId.Runners => PickRunner(state, row),
            TabId.Envelope => PickAirspaceRow(state, row),
            _ => Select(state, row),
        };
    }

    /// <summary>Move the cursor inside the open flight's log.</summary>
    /// <remarks>
    /// Clamped to the entries there are, like every other list's - and to
    /// ENTRIES rather than rows, because the one under the cursor is several
    /// rows tall.
    /// </remarks>
    private static AppState PickLogEntry(AppState state, int to) => state with
    {
        LogSelected = Rows.Log(state) is { Count: > 0 } entries
            ? Math.Clamp(to, 0, entries.Count - 1)
            : 0,
    };

    /// <summary>Move the flights list's own cursor, inside the flights list.</summary>
    /// <remarks>
    /// Clamped to what is there, like the work list's: a cursor past the end
    /// points at no flight, and the key that opens one would then have to
    /// decide what to do about that.
    /// </remarks>
    private static AppState PickFlight(AppState state, int to) => state with
    {
        FlightSelected = state.Flights is { Flights.Count: > 0 } listed
            ? Math.Clamp(to, 0, listed.Flights.Count - 1)
            : 0,
    };

    /// <summary>
    /// Move the board cursor, over the rows a person is looking at.
    /// </summary>
    /// <remarks>
    /// <b>Over the ROWS rather than over the nominations</b>, for the reason
    /// the fleet's cursor is: the board's rows are nominations AND watches, so
    /// clamping to either list alone would stop the cursor short of the table
    /// on the screen.
    /// </remarks>
    private static AppState PickBoardRow(AppState state, int to) => state with
    {
        BoardSelected = Rows.Board(state) is { Count: > 0 } rows
            ? Math.Clamp(to, 0, rows.Count - 1)
            : 0,
    };

    /// <summary>
    /// Move the runner cursor, inside the fleet.
    /// </summary>
    /// <remarks>
    /// <b>Over the ROWS rather than the control plane's list.</b> A machine
    /// registered here that the fleet has never seen is a row this console adds,
    /// so clamping to the answer's length would put the cursor one short of the
    /// table a person is looking at.
    /// </remarks>
    /// <summary>The airspace row the cursor is on, clamped to what is drawn.</summary>
    /// <remarks>
    /// <b>Against the projection's count, not the documents'.</b> The tree
    /// invents a row per folder, so a cursor clamped to the document count
    /// would stop short of the last file in a nested directory - which is
    /// PickRunner's reason below, where the console invents a row the fleet
    /// does not have.
    /// </remarks>
    private static AppState PickAirspaceRow(AppState state, int row)
    {
        var rows = AirspaceRows.Tree(state).Count;

        if (rows == 0)
        {
            return state;
        }

        var moved = state with { AirspaceSelected = Math.Clamp(row, 0, rows - 1) };

        // AND THE PANE'S VIEW WITH IT. Which views a row has is the row's own
        // answer - a file matching what is applied has nothing to compare, a
        // strategy has no composition - so an arrow key can take away the view
        // the pane is showing, and leave it pointing at a tab that is not on
        // the bar. That disagreement between a model and a widget is what
        // crashed the window's tabs.
        //
        // A REPAIR RATHER THAN A RESET, and only when there is an answer: a
        // folder row offers nothing, and nothing is not evidence that the view
        // somebody chose is wrong.
        var offered = AirspaceViews.Offered(moved);

        return offered.Count > 0 && !offered.Contains(moved.AirspaceView)
            ? moved with { AirspaceView = offered[0] }
            : moved;
    }

    private static AppState PickRunner(AppState state, int to) => state with
    {
        RunnerSelected = Rows.Runners(state) is { Count: > 0 } rows
            ? Math.Clamp(to, 0, rows.Count - 1)
            : 0,
    };



    /// <summary>Move the cursor inside the runner modal's environments view.</summary>
    private static AppState PickRunnerEnvironment(AppState state, int to) => state with
    {
        RunnerEnvironmentSelected = EnvironmentRows.Environments(state) is { Count: > 0 } rows
            ? Math.Clamp(to, 0, rows.Count - 1)
            : 0,
    };

    /// <summary>Move the cursor inside the runner modal's members view.</summary>
    private static AppState PickRunnerMember(AppState state, int to) => state with
    {
        RunnerMemberSelected = EnvironmentRows.Members(state) is { Count: > 0 } rows
            ? Math.Clamp(to, 0, rows.Count - 1)
            : 0,
    };

    /// <summary>Move the repository cursor, inside the repository list.</summary>
    private static AppState PickRepository(AppState state, int to) => state with
    {
        RepositorySelected = state.Repositories is { Repositories.Count: > 0 } listed
            ? Math.Clamp(to, 0, listed.Repositories.Count - 1)
            : 0,
    };

    /// <summary>Move the work list's own cursor, inside the work list.</summary>
    /// <remarks>
    /// Clamped rather than wrapped, and clamped to what is actually there: a
    /// selection past the end selects nothing that exists, and the key that
    /// flies it would then have to decide what to do about that.
    /// </remarks>
    private static AppState PickWork(AppState state, int to) => state with
    {
        BrowseSelected = state.Browse is null || state.Browse.Items.Count == 0
            ? 0
            : Math.Clamp(to, 0, state.Browse.Items.Count - 1),
    };

    /// <summary>The second flight is not wanted after all.</summary>
    /// <remarks>
    /// A pure state change, unlike confirming: declining opens nothing, so
    /// there is nothing for the loop to do and this belongs in the reducer.
    /// </remarks>
    public static AppState FlightDeclined(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return state with { Mode = UiMode.Normal, PendingFlight = null };
    }

    /// <summary>Show the tracker's items, or close them.</summary>
    /// <remarks>
    /// It used to clear four flags: browse shared its region with evidence,
    /// live, the checklist and the envelope, so showing it meant hiding them.
    /// Only the tab showing is drawn now.
    /// </remarks>
    public static AppState BrowseToggled(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return Toggled(state, TabId.Browse);
    }

    /// <summary>Shows or hides the envelope, and gives it the region.</summary>
    /// <remarks>
    /// Not reachable through <see cref="Reduce"/>: showing this pane is a read
    /// the shell serves, so the loop calls this directly and a reducer arm too
    /// would be two effects for one keypress - the local one happening whether
    /// or not the remote one did. Browse used to be the example here and is now
    /// the counter-example: its command moved into
    /// <see cref="ShellCommands.Reads"/>, so only its FIRST press is the
    /// shell's and its arm had to arrive.
    /// </remarks>
    public static AppState EnvelopeToggled(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return Toggled(state, TabId.Envelope);
    }

    /// <param name="said">
    /// What the listing was narrowed by, or null where nobody narrowed. Recorded
    /// on the listing because it is the only moment the filter in hand and the
    /// rows on screen are the same thing.
    /// </param>
    /// <summary>
    /// The last segment of a tracker's path, or null where there is none.
    /// </summary>
    /// <remarks>
    /// Every row of one project shares the root, so the whole path is a column
    /// of one repeated word with the part that differs pushed off the edge.
    /// </remarks>
    private static string? Leaf(string? path) =>
        path is { Length: > 0 }
            ? path.Split('\\', StringSplitOptions.RemoveEmptyEntries) is { Length: > 0 } parts
                ? parts[^1]
                : path
            : null;

    public static AppState Browsed(
        AppState state, string providerKey, BrowseOutcome outcome, string? said = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(outcome);

        return state with
        {
            // A NEW LIST STARTS AT THE TOP. A cursor left pointing at row nine
            // of a list that now has two rows is a selection that flies the
            // wrong item, silently.
            BrowseSelected = 0,
            Browse = outcome switch
            {
                BrowseOutcome.Listed listed => new BrowseListing
                {
                    ProviderKey = providerKey,
                    // THE URL CROSSES NOW, AND IT DID NOT. The reason it did
                    // not was that no reader of the screen asked for it - a
                    // flight is opened from a provider and an id, never parsed
                    // out of a url. That is still true of flying; what changed
                    // is that the detail modal offers to open the item where it
                    // lives, which is a reader.
                    Items = [.. listed.Page.Items.Select(item => new BrowseRow
                    {
                        Id = item.Id,
                        Title = item.Title,
                        State = item.State,
                        Updated = item.Updated,
                        Url = item.Url is { Length: > 0 } where ? where : null,
                        Where = Leaf(item.AreaPath),
                        Sprint = item.Iteration,
                        // EVERYTHING ELSE THE TRACKER RECORDS, carried whole.
                        // The seven above are picked out because the listing's
                        // columns are built from them; these have no column and
                        // are not interpreted, so there is nothing to pick.
                        Fields = item.Fields ?? [],
                    })],
                    NextCursor = listed.Page.NextCursor,
                    FilterSaid = said,
                },

                BrowseOutcome.NotBrowsable why => Absent(providerKey, why.Why),

                // THE READER ALREADY SAID WHAT WAS WRONG. This ending arrived
                // with filtering and fell through to the catch-all below, which
                // reports a gap in this console - true of the console, false of
                // the reader, and the wrong place to send somebody to look.
                BrowseOutcome.NotFilterable why => Absent(providerKey, why.Why),

                BrowseOutcome.Refused why => Absent(providerKey, why.Why),
                BrowseOutcome.Unintelligible why => Absent(providerKey, why.Why),
                BrowseOutcome.Silent why => Absent(providerKey, why.Why),

                _ => Absent(providerKey,
                    $"The reader for '{providerKey}' answered in a way this console does not "
                  + $"have a sentence for ({outcome.GetType().Name}). That is a gap here, not "
                  + "a problem with the tracker."),
            },
        };
    }

    /// <summary>A tracker that was asked and did not answer with work.</summary>
    private static BrowseListing Absent(string providerKey, string why) =>
        new() { ProviderKey = providerKey, Absence = why };

    public static AppState Arrived(AppState state, QueueRow row, bool startedByMe)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(row);

        var selectedFlight = state.Selected?.FlightId;

        var existing = state.Queue.FirstOrDefault(r => r.FlightId == row.FlightId);
        var merged = existing is null
            ? row with { UnreadArrivals = startedByMe ? 0 : 1 }
            : existing with
            {
                Reason = row.Reason,
                Since = row.Since,
                UnreadArrivals = startedByMe ? 0 : existing.UnreadArrivals + 1,
            };

        var queue = QueueSort.Default.Order(
            existing is null
                ? [.. state.Queue, merged]
                : [.. state.Queue.Select(r => r.FlightId == row.FlightId ? merged : r)]);

        var landOn = startedByMe ? row.FlightId : selectedFlight;
        var index = queue.ToList().FindIndex(r => r.FlightId == landOn);

        return state with
        {
            Queue = queue,
            SelectedRow = index < 0 ? 0 : index,
        };
    }
}
