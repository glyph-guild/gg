namespace Gg.Console;

/// <summary>
/// One keystroke, structurally. No Terminal.Gui types.
/// </summary>
/// <remarks>
/// The keymap stays a pure function testable without a terminal, and this is
/// the type that makes "the set of keys that do something" a set you can
/// actually compute - which is what turns the hints check from a sampling into
/// an equality.
/// </remarks>
public readonly record struct KeyStroke(
    char? Input, bool Ctrl = false, bool Escape = false, bool Tab = false, bool Enter = false)
{
    public static KeyStroke Char(char input) => new(input);

    public static KeyStroke Control(char input) => new(input, Ctrl: true);

    public static KeyStroke Esc { get; } = new(null, Escape: true);

    public static KeyStroke TabKey { get; } = new(null, Tab: true);

    /// <summary>
    /// The key a person presses on a row without being told to.
    /// </summary>
    /// <remarks>
    /// Its own flag rather than a character, for <c>esc</c> and <c>tab</c>'s
    /// reason: what arrives from a terminal is a named key rather than a rune,
    /// and a keymap that matched it as <c>'\r'</c> would be matching one
    /// terminal's idea of it.
    /// </remarks>
    /// <remarks>
    /// <c>EnterKey</c> rather than <c>Enter</c> for <see cref="TabKey"/>'s
    /// reason: the positional parameter already has the name.
    /// </remarks>
    public static KeyStroke EnterKey { get; } = new(null, Enter: true);

    /// <summary>How this key is written where a person will read it.</summary>
    public string Name =>
        Escape ? "esc"
        : Tab ? "tab"
        : Enter ? "enter"
        : Ctrl ? $"ctrl+{Input}"
        : Input?.ToString() ?? "?";
}

/// <summary>What a key means right now.</summary>
/// <remarks>
/// Everything the keymap dispatches on. The hints are generated from the SAME
/// value, so an advertised key cannot drift from a live one - there is no
/// second input to disagree about.
/// </remarks>
public readonly record struct KeymapContext(
    UiMode Mode,

    TabId Showing = TabId.Queue,
    bool Frozen = false,
    bool Takeable = false,
    bool HandedBackable = false,

    /// <summary>Whether the airspace cursor is on a document rather than a folder.</summary>
    bool OverADocument = false,

    /// <summary>Whether the keyboard is in the document rather than the tree.</summary>
    /// <remarks>
    /// One key crosses both ways, so what it is ABOUT to do depends on where
    /// the keyboard is now - and the hint line is generated from this same
    /// value, which is what stops it advertising a direction the key does not
    /// take.
    /// </remarks>
    bool ReadingTheDocument = false,

    /// <summary>
    /// Whether the help cursor is on a key group rather than on a key.
    /// </summary>
    /// <remarks>
    /// <b>Last, because this is a positional record.</b> Adding a parameter
    /// anywhere else shifts every call site that passes by position, which the
    /// compiler catches loudly here and would not in a looser language.
    /// </remarks>
    bool OverAFold = false)
{
    /// <summary>
    /// Whether a code is already on the screen waiting to be approved.
    /// </summary>
    /// <remarks>
    /// The sign-in modal's two steps ask for opposite things - one asks the
    /// control plane for a code, the other says a person has approved it - and
    /// this is what tells them apart. It is in the CONTEXT rather than read off
    /// the model, like everything else here, so the hints and the dispatch
    /// cannot disagree about which step is showing.
    /// </remarks>
    public bool SignInStarted { get; init; }

    /// <summary>
    /// Whether the runner the cursor is on is the one this console started.
    /// </summary>
    /// <remarks>
    /// The runner modal opens over any row now, and only one row in a fleet is
    /// reachable through a pidfile this machine wrote. In the CONTEXT rather
    /// than read off the model, like everything else here, so the hint line and
    /// the dispatch cannot disagree about whether a key is live.
    /// </remarks>
    public bool RunnerIsOurs { get; init; }

    /// <summary>
    /// Whether the selected runner's allowance is one this person may reserve.
    /// </summary>
    /// <remarks>
    /// <b>Owners are the registrants, so "my machine" means "my
    /// allowance".</b> The control plane refuses anybody else, and offering a
    /// key that will be refused is worse than not offering it — a person
    /// presses it and concludes the console is broken.
    /// </remarks>
    public bool AllowanceIsMine { get; init; }

    /// <summary>
    /// Whether this console offers the fleet's allowances at all.
    /// </summary>
    /// <remarks>
    /// <b>Both gates, folded into one flag because the keymap has no business
    /// knowing why.</b> The control plane decides what the pane could hold and
    /// the local file decides whether it is drawn; what the bindings need is
    /// the conjunction.
    /// </remarks>
    public bool FleetAllowancesOffered { get; init; }

    /// <summary>
    /// Whether the runner the cursor is on is flying something.
    /// </summary>
    /// <remarks>
    /// <b>What decides whether watching is offered at all.</b> A channel to a
    /// runner exists only while a flight does - which is what stops it being a
    /// standing way in - so on an idle machine the key would be one that always
    /// fails. The modal's text still names the capability there, because a
    /// person who never sees it never learns it exists.
    /// </remarks>
    public bool RunnerIsFlying { get; init; }

    /// <summary>
    /// What the refresh key has to say for itself: a countdown, or the mark
    /// that says one is happening.
    /// </summary>
    /// <remarks>
    /// <b>In the context, like everything else the hints are made of.</b> The
    /// line comes from one place and a second renderer appending to it is the
    /// drift this whole file is arranged to prevent. Empty in the plainest
    /// shape, so the help page - a union over contexts - does not carry a
    /// stopped clock.
    /// </remarks>
    public string Refresh { get; init; } = "";

    /// <summary>
    /// The context a model puts the console in.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>One derivation, because the screen kept a literal beside the
    /// model.</b> That literal carried the mode, the tab and the freeze, and
    /// not <see cref="Takeable"/>, <see cref="HandedBackable"/> or which step
    /// the sign-in modal is on - so <c>Catalogue</c> can name a key the screen
    /// would refuse to resolve. Two of those are invisible only because nothing
    /// in production sets <c>TakeableTree</c> or <c>TakenOver</c> yet, which
    /// makes them a trap rather than a defect: the day one is set is the worst
    /// day to find out.
    /// </para>
    /// <para>
    /// The mode comes from the model too, so a caller wanting the keys of a
    /// DIFFERENT mode says so with a <c>with</c> rather than by rebuilding
    /// this.
    /// </para>
    /// </remarks>
    public static KeymapContext For(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return new KeymapContext(
            state.Mode,
            state.ActiveTab,
            state.Frozen,
            state.TakeableTree is not null,
            state.TakenOver,

            // WHICH `v' MEANS. On a document row it reads that document back;
            // anywhere else it opens the rules in force. Derived here so the
            // hint line and the dispatch cannot disagree about which.
            AirspaceRows.Pointed(state) is not null,

            // AND WHICH HALF OF THE AIRSPACE TAB HAS THE KEYBOARD, for the
            // reason above it: `w' crosses both ways and says which way it is
            // going.
            state.AirspaceReading,

            // AND WHETHER THE HELP CURSOR IS ON A GROUP. Derived here with the
            // other two for their reason: the hint line and the dispatch read
            // one answer, so a key cannot be advertised where it does nothing.
            state.HelpFold is not null)
        {
            // Which of the sign-in modal's two steps is showing. Both live in
            // one mode, so this is the only thing that tells them apart.
            SignInStarted = state.SignIn is not null,

            // Whose runner the modal is over, which decides whether the two
            // keys that need a pidfile are offered at all.
            // NO ROW IS NOT SOMEBODY ELSE'S ROW. A runner this console started
            // and is watching come up has no fleet row for a few seconds, and
            // `x` has to work on it for exactly those seconds - it is the only
            // way to stop something that is starting badly.
            RunnerIsOurs = Rows.Selected(state) is not { Mine: false },

            // WHETHER THERE IS ANYTHING TO WATCH. Derived here with the rest,
            // so the hint line and the dispatch cannot disagree about whether
            // the key is live.
            RunnerIsFlying = Rows.Selected(state) is { Work.Length: > 0 },

            // WHOSE ALLOWANCE THE SELECTED MACHINE SPENDS FROM. Yours rather
            // than Mine: an allowance belongs to the people who registered the
            // machines reporting it, which is a fact the control plane
            // recorded - where Mine is an inference from this console's own
            // file about one particular machine.
            FleetAllowancesOffered = state.IsAdmin && state.FleetAllowancesShown,

            AllowanceIsMine = Rows.Selected(state) is { Yours: true }
                              && AllowanceRows.SelectedName(state) is not null,

            // WHAT THE REFRESH KEY HAS TO SAY, derived here with everything
            // else the hints are made of, so the line has one author.
            Refresh = AutoRefresh.Says(state.Refresh),
        };
    }
}

/// <summary>One binding: a key, what it does, and how to describe it.</summary>
public readonly record struct KeyBinding(KeyStroke Key, Command Command, string Description)
{
    /// <summary>
    /// When this key applies, for one that does not always.
    /// </summary>
    /// <remarks>
    /// <b>Because the help page is a union over every context.</b> Listed
    /// without this, <c>f</c> appears twice with two meanings and nothing
    /// saying which is which - a contradiction rather than a condition. Null
    /// for a key that is always live in its own mode, and
    /// <c>HelpNamesEveryKeyTests</c> asserts the two cannot be confused: a
    /// binding that does not resolve in the plainest form of its own mode has
    /// to say when it does.
    /// </remarks>
    public string? When { get; init; }

    /// <summary>
    /// What a button for this key says, for a modal that draws them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Its own field because <see cref="Description"/> is written for a
    /// different surface.</b> The hint line explains - "write it in your
    /// editor" - and a button is a thing you point at. Using the one for the
    /// other renders two half-labels in a box sized for a question.
    /// </para>
    /// <para>
    /// <b>Null means this mode does not draw buttons yet</b>, and that is the
    /// whole rollout mechanism: a mode joins in when somebody has labelled every
    /// answer it has, and looks exactly as it does today until then. Half a set
    /// is worse than none - a modal offering `approve` and not `reject` reads as
    /// though approving is all there is.
    /// </para>
    /// </remarks>
    public string? Label { get; init; }

    /// <summary>
    /// Bound, and kept off the hint line.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>THE LINE IS ONE LINE, and every slot spent is one a key nobody knows
    /// could have had.</b> Four kinds of key are off it and each has somewhere
    /// else to be found: the six tab keys are printed on their own tabs, the
    /// two credential keys and the invite are in help and used about twice a
    /// year, j and k are what the arrows already do, and <c>tab</c> and
    /// <c>enter</c> are conventions - <c>tab</c> moves focus in every terminal
    /// program there is, and <c>enter</c> opens the row under the cursor, which
    /// nobody presses because they were told to.
    /// </para>
    /// <para>
    /// <b>AND A KIND THAT IS SCOPED RATHER THAN HIDDEN.</b> A key that acts on
    /// a flight is advertised where flights are. It is still bound everywhere
    /// it was bound - <c>a</c> opens the actions for whatever the cursor is on,
    /// wherever you press it - but naming it on the airspace tab spends a slot
    /// teaching somebody about somewhere they are not, and there were six such
    /// hints on that tab against three that were about the tab itself.
    /// </para>
    /// <para>
    /// <b>Off the LINE, not out of the program.</b> <see cref="Keymap.Resolve"/>
    /// does not look at this, and <c>KeymapTests</c> still proves advertised
    /// keys and live keys are one set. The help page shows these; see
    /// <see cref="Untaught"/> for the only two it does not.
    /// </para>
    /// </remarks>
    public bool OffTheHintLine { get; init; }

    /// <summary>
    /// Not on the help page either.
    /// </summary>
    /// <remarks>
    /// <b>ONLY j AND k, and only because the arrows do the same thing.</b> Two
    /// properties rather than one because they are two different claims, and
    /// collapsing them cost something: when the six tab keys were marked as
    /// merely hidden, the help page - which exists to name EVERY key - stopped
    /// naming them, and the test that says it names every key was iterating the
    /// same flag, so it passed while the page was wrong. A key is taken off the
    /// line because it is advertised elsewhere; it is taken out of help only
    /// when there is another way to do the thing itself.
    /// </remarks>
    public bool Untaught { get; init; }
}

/// <summary>One catalogue entry: a binding and the mode it belongs to.</summary>
public readonly record struct KeyCatalogueEntry(UiMode Mode, KeyBinding Binding);

/// <summary>
/// The keymap. Pure, total, and the only place bindings live.
/// </summary>
/// <remarks>
/// <see cref="Bindings"/> is the single source: <see cref="Resolve"/> looks up
/// in it and <see cref="Hints"/> renders it. Written as two lists that agreed
/// with each other, they would agree until somebody added a key to one - which
/// is the drift the discipline exists to prevent, so there is one list.
/// </remarks>
public static class Keymap
{
    /// <summary>Quits from anywhere, including a modal. The last resort, not the escape hatch.</summary>
    public static KeyStroke Interrupt { get; } = KeyStroke.Control('c');

    /// <summary>
    /// Every binding live in this context.
    /// </summary>
    /// <remarks>
    /// A modal returns ONLY its own bindings, which is what "modals own the
    /// keyboard" means concretely: nothing underneath is reachable while one is
    /// open, so no key can act on a flight the person cannot currently see.
    /// </remarks>
    /// <summary>The watch key, when there is anything to watch.</summary>
    /// <remarks>
    /// <b>One definition spread into both arms rather than written twice.</b>
    /// The runner modal has two shapes - ours and somebody else's - and this
    /// key is the one thing that is the same in both, which is exactly the pair
    /// a second copy would let drift.
    /// </remarks>
    /// <summary>
    /// The key that turns the runner modal's three views.
    /// </summary>
    /// <remarks>
    /// <b>Declared once, because this modal has two shapes.</b> Ours gets
    /// restart and shut-down and somebody else's does not, and a binding
    /// written into both lists is one somebody will later change in one of
    /// them - which is how a key comes to mean two things on one screen.
    /// <para>
    /// <b>`v', the same letter the airspace tab turns its pane with.</b> One
    /// letter for one act is what a single keymap is for; the two are in
    /// different modes, so neither shadows the other.
    /// </para>
    /// </remarks>
    /// <summary>
    /// The key that turns the runner modal's three views.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Declared once, because this modal has two shapes.</b> Ours gets
    /// restart and shut-down and somebody else's does not, and a binding
    /// written into both lists is one somebody will later change in one of
    /// them.
    /// </para>
    /// <para>
    /// <b>NO LABEL, LIKE EVERY OTHER BINDING IN THIS MODAL.</b> It carried one
    /// conditionally for a while, because <c>Buttons()</c> is all-or-nothing
    /// and the two shapes sat on opposite sides of that line. They sit on the
    /// same side now — keys only — so the condition went with the buttons.
    /// </para>
    /// </remarks>
    private static KeyBinding Turning { get; } =
        new(KeyStroke.Char('v'), Command.NextRunnerView, "next view");

    private static IReadOnlyList<KeyBinding> Watching(KeymapContext context) =>
        context.RunnerIsFlying
            ?
            [
                new(KeyStroke.Char('w'), Command.WatchRunner, "watch what it is flying")
                {
                    When = "while it is flying something",
                },
            ]
            : [];

    public static IReadOnlyList<KeyBinding> Bindings(KeymapContext context) => context.Mode switch
    {
        UiMode.Help =>
        [
            // TURNS THE PAGE, and it has to be BOUND rather than merely handled
            // by the reducer. The pane's own text tells a person to press it;
            // for one release it resolved to nothing, because the reducer had
            // an arm and this list did not, and every test called the reducer
            // directly. A key advertised and bound to nothing is the shape
            // ShellHandledTests exists for, one modal down.
            // WHERE THE SETTINGS ARE READ. `e` is the envelope pane in Normal
            // mode and stays it - the modal owns the keyboard while it is open,
            // which is what makes one letter safe in two places. Bound for the
            // whole modal rather than only the Environment page, because
            // knowing which page is showing would mean a new field on
            // KeymapContext and every property test's cross-product doubling
            // for a distinction a person does not feel.
            // ONLY WHERE THERE IS SOMETHING TO FOLD. The cursor is on a key
            // most of the time, and a key offered where it does nothing is the
            // dead key Article XI names - the same reason `v' and `w' on the
            // airspace tab are conditional on a document.
            .. context.OverAFold
                ? (KeyBinding[])
                [
                    new(KeyStroke.Char(' '), Command.ToggleFold, "fold or unfold this group")
                        { When = "while the cursor is on a group" },
                ]
                : [],

            new(KeyStroke.Char('e'), Command.EditConfiguration, "edit the configuration")
            {
                // LABELLED, BECAUSE THE ALL-OR-NOTHING RULE MEANS IT MUST BE.
                // An unlabelled answer here takes the help page's buttons away
                // entirely rather than adding one that does nothing - which is
                // the rule working, and it fired on this the first time.
                //
                // A click opens an editor on this machine's own configuration.
                // That tears the console down, and it is recoverable by
                // quitting without saving - the same size of consequence as
                // turning the page beside it.
                Label = "Edit file",
            },

            // THE OFFER THE ENVIRONMENT PAGE NAMES. One letter, safe here for
            // the reason `e` above it is: the modal owns the keyboard while it
            // is open, so a letter that means something else in Normal mode
            // cannot reach through.
            new(KeyStroke.Char('o'), Command.TakeOfferedConfiguration, "take what is offered")
            {
                // LABELLED, because the all-or-nothing rule means it must be:
                // an unlabelled answer takes this page's buttons away
                // entirely rather than adding one that does nothing.
                Label = "Take offer",
            },

            new(KeyStroke.TabKey, Command.FocusNextPane, "keys / environment")
            {
                // NOT "Keys" OR "Environment", EITHER OF WHICH IS WRONG HALF
                // THE TIME. The page draws its own strip - `[ Keys ]
                // Environment` - so which one is showing is already answered
                // above the button; what a person cannot see is that there is a
                // way to the other. The description names both because a hint
                // line is read once; the button says what pressing it does.
                Label = "Turn page",
            },
            new(KeyStroke.Esc, Command.CloseModal, "close help"),
        ],

        // OWNS THE KEYBOARD. Only these three resolve while it is open, so a key that
        // means something in Normal mode cannot reach through and act on the flight
        // behind the modal - and exactly one of them is a way out, which decides nothing.
        UiMode.GateDecision =>
        [
            new(KeyStroke.Char('a'), Command.ApproveGate, "approve"),
            new(KeyStroke.Char('r'), Command.RejectGate, "reject"),
            new(KeyStroke.Esc, Command.CloseModal, "close"),
        ],

        // A CONFIRMATION IS A MODAL LIKE ANY OTHER: it captures the keyboard,
        // it has exactly one escape hatch, and escaping is a real answer. The
        // confirming key is deliberately NOT the key that opened it - f twice
        // in quick succession is the accident this whole question exists to
        // catch.
        UiMode.ConfirmFlight =>
        [
            new(KeyStroke.Char('y'), Command.FlyPicked, "open a second flight"),
            new(KeyStroke.Esc, Command.CloseModal, "leave it alone"),
        ],

        // A MENU, SO ITS ITEMS RESOLVE. This bound nothing but esc while its
        // body listed two things a person could do - so somebody with two
        // gates waiting opened the modal that exists to say what can be done,
        // read both, pressed both, and got nothing. Article XI inside one
        // screen, and worse than the usual form: they did not guess the key,
        // they were told it.
        //
        // THIS IS NOT A READING MODAL. FlightDetail deliberately binds nothing
        // that acts on its flight, because a person reading a log has not
        // asked to decide anything. This one's whole subject is what CAN be
        // done, so binding what it offers is its purpose rather than a
        // violation of that rule.
        UiMode.FlightActions =>
        [
            new(KeyStroke.Char('d'), Command.OpenGate, "decide a gate on this flight"),
            new(KeyStroke.Char('v'), Command.ShowFlight, "open the flight"),
            new(KeyStroke.Esc, Command.CloseModal, "close"),
        ],

        // WHAT IS KNOWN ABOUT ONE FLIGHT, and one way out. Reading a log must
        // not be able to act on the flight it is about: `d` decides a gate in
        // Normal mode, and a person who opened a log has not asked to decide
        // anything.
        // NOTHING BUT THE WAY OUT. Pressing `y' again from inside a refusal
        // would be the second attempt nobody asked for, and every other key
        // here would act on a console the person cannot see.
        // THE FOUR ACTS ON THE AIRSPACE, each keeping the letter it had on the
        // status line. Inside a modal the letters are free, which is the whole
        // reason they could collapse without anybody relearning one.
        UiMode.AirspaceActions =>
        [
            new(KeyStroke.Char('p'), Command.PullEstate, "pull the airspace"),
            new(KeyStroke.Char('s'), Command.AskToApplyEstate, "apply the airspace"),
            new(KeyStroke.Char('m'), Command.DraftEstate, "draft with an agent"),
            new(KeyStroke.Char('o'), Command.ReadOutcome,
                "what would change, and the last apply"),
            new(KeyStroke.Esc, Command.CloseModal, "close"),
        ],

        UiMode.HandFlight => [new(KeyStroke.Esc, Command.CloseModal, "close")],

        // THE TWO THINGS THAT CAN BE DONE TO A RUNNER, and the way out. `r' and
        // `x' are repositories and forget-a-credential in Normal mode and mean
        // these here, which is what a modal owning the keyboard is for.
        // ONLY OVER A RUNNER THIS CONSOLE CAN REACH. Both act through a pidfile
        // this machine wrote, so over somebody else's runner the best case is a
        // key that does nothing and the worst is one that shuts down the local
        // runner while a person is looking at another row. Close is always
        // there: a modal with no way out is worse than one with nothing to do.
        // AND `w` OVER ANY RUNNER THAT IS FLYING, ours or not - which is the
        // whole point of it. `watch` reaches a machine through the control
        // plane rather than through a pidfile this one wrote, so unlike `r` and
        // `x` it is exactly as available over somebody else's runner as over
        // this one. Withheld while nothing is in the air, because a channel to
        // a runner exists only while a flight does.
        UiMode.Runner => context.RunnerIsOurs
            ?
            [
                .. Watching(context),
                new(KeyStroke.Char('r'), Command.RestartRunner, "restart it")
                {
                    When = "over the runner on this machine",
                },
                new(KeyStroke.Char('x'), Command.StopRunner, "shut it down")
                {
                    When = "over the runner on this machine",

                    // NO BUTTON ANY MORE, AND THIS WAS THE ONE THAT ENDED
                    // SOMETHING. It was argued onto the clickable side - the
                    // runner beside it starts the same thing again, and nothing
                    // a runner carries is lost by stopping it - and that
                    // argument is untouched. What removed it is the room: this
                    // modal's foot is a tab bar now, and a button row under a
                    // bar somebody can already click is two clickable things in
                    // one place.
                },
                Turning,
                new(KeyStroke.Esc, Command.CloseModal, "close"),
            ]
            :
            [
                .. Watching(context),
                Turning,
                new(KeyStroke.Esc, Command.CloseModal, "close"),
            ],

        // THE ONE PLACE A FLIGHT CAN BE ENDED, because it is the one place the
        // flight being ended is named on the screen. Every other key in this
        // console acts on the row under the cursor, which is right for opening
        // one and wrong for stopping one.
        //
        // `x' STOPS THE THING THIS MODAL IS ABOUT, which is what it already
        // means in the runner's modal. One letter, one idea, in the two places
        // a modal is about something that can be stopped.
        // NEITHER ACTS WITHOUT ASKING. `x` grounded on one keypress - the
        // session ended and an editor opened for a reason before anybody had
        // agreed - and a prompt asks what, not whether.
        UiMode.ConfirmGround =>
        [
            new(KeyStroke.Char('y'), Command.GroundFlight, "ground it"),
            new(KeyStroke.Esc, Command.CloseModal, "leave it flying"),
        ],

        UiMode.AirspacePath =>
        [
            // TWO KEYS, AND EVERY OTHER ONE FALLS THROUGH TO THE FIELD. That is
            // the whole reason this is a mode: a path contains letters, and a
            // keymap that answered them would make them unreachable.
            new(KeyStroke.EnterKey, Command.SetAirspacePath, "set it")
                { Label = "Set" },

            // A CONTROL COMBINATION, BECAUSE EVERY PLAIN LETTER IS THE FIELD'S.
            // `d` inside a path is a character, so an affordance in this mode
            // has to be a key a path cannot contain.
            new(KeyStroke.Control('d'), Command.AirspacePathFromCwd,
                "use the directory gg was launched from"),
            new(KeyStroke.Control('v'), Command.AirspacePathFromClipboard,
                "paste the clipboard"),
            new(KeyStroke.Control('o'), Command.AirspacePathFromDialog,
                "browse for a directory"),
            new(KeyStroke.Esc, Command.CloseModal, "leave it as it is")
                { Label = "Leave it" },
        ],

        // TWO VIEWS, REACHABLE FROM EACH OTHER, which is HostedBar's shape
        // and its reason: comparing what governs against what you are about to
        // change is why both are here, and inside a modal the letters are
        // free. `d' costs nothing here where it would have cost a Normal-mode
        // letter - and a tab-scoped `d' would never have fired anyway, since
        // Resolve answers the first match and Normal's is declared earlier.
        //
        // READING, NOT ANSWERING, so nothing is labelled and there is no
        // button row: Keymap.Buttons wants every non-Esc answer labelled and
        // gives none otherwise, which is right for a document.
        UiMode.ReadingEnvelope =>
        [
            new(KeyStroke.Char('d'), Command.ReadChangeset, "what would change"),
            new(KeyStroke.Char('o'), Command.ReadOutcome, "the last apply"),
            new(KeyStroke.Char('c'), Command.CopyModal, "copy"),
            new(KeyStroke.Esc, Command.CloseModal, "close"),
        ],

        UiMode.ReadingChangeset =>
        [
            new(KeyStroke.Char('e'), Command.ReadEnvelope, "the rules in force"),
            new(KeyStroke.Char('o'), Command.ReadOutcome, "the last apply"),

            // `x' HERE AND NOWHERE ELSE. The names that can be retired are the
            // ones whose files are gone, so there is no tree row to put a
            // cursor on - and `x' is ForgetCredential in Normal mode, with the
            // Envelope tab's spread declared EARLIER than that global, so a
            // tab-scoped one would win and take forget-credential away on that
            // tab without saying so. Inside a modal nothing is shadowed.
            new(KeyStroke.Char('x'), Command.AskToRetire, "retire what is missing"),
            new(KeyStroke.Char('c'), Command.CopyModal, "copy"),
            new(KeyStroke.Esc, Command.CloseModal, "close"),
        ],

        // THE ONE ACT HERE THAT REMOVES GOVERNANCE, so it is asked rather than
        // done - and reversing it would need another gated change and an
        // approver, which is a stronger reason to ask than any other question
        // in this console has.
        UiMode.ConfirmRetire =>
        [
            new(KeyStroke.Char('y'), Command.RetireNames, "retire them")
                { Label = "Retire" },
            new(KeyStroke.Esc, Command.CloseModal, "leave the names as they are")
                { Label = "Leave them" },
        ],

        // THE VIEW THE MODAL OPENS ITSELF ON. `o' is here too so somebody who
        // stepped away to the diff can come back to what just happened - the
        // three views answer what happened, what would change, and what
        // governs, and reading one against another is why they share a box.
        UiMode.ReadingOutcome =>
        [
            new(KeyStroke.Char('d'), Command.ReadChangeset, "what would change"),
            new(KeyStroke.Char('e'), Command.ReadEnvelope, "the rules in force"),

            // `c' HERE AND NOT OUT THERE. It is add-credential in Normal mode,
            // and inside a modal the letters are free - the same argument `d',
            // `o' and `x' make. What is in this one is usually a refusal
            // somebody has to paste somewhere.
            new(KeyStroke.Char('c'), Command.CopyModal, "copy"),
            new(KeyStroke.Esc, Command.CloseModal, "close"),
        ],

        UiMode.ConfirmApply =>
        [
            new(KeyStroke.Char('y'), Command.ApplyEstate, "apply them")
                { Label = "Apply" },
            new(KeyStroke.Esc, Command.CloseModal, "leave the airspace as it is")
                { Label = "Leave it" },
        ],

        UiMode.ConfirmFlyAgain =>
        [
            new(KeyStroke.Char('y'), Command.FlyAgain, "open one on this intent"),
            new(KeyStroke.Esc, Command.CloseModal, "leave it alone"),
        ],

        UiMode.FlightDetail =>
        [
            new(KeyStroke.Char('x'), Command.AskToGround, "ground it"),
            new(KeyStroke.Char('f'), Command.AskToFlyAgain, "fly it again"),

            // v, AND DELIBERATELY NOT tab. tab cycles the bar one level up,
            // so it was the obvious choice and it is the wrong one: an
            // unresolved key falls through to Terminal.Gui, and in THIS mode
            // tab falling through is what moves focus between the intent, the
            // fields and the log. Those fields are focusable for one reason -
            // a Label cannot be copied out of and the flight id is the value
            // most often wanted out of this modal - so binding tab here would
            // have taken the flight id away to save a keystroke.
            //
            // v is what shows evidence one level up, so it is already the
            // console's word for this.
            new(KeyStroke.Char('v'), Command.NextFlightTab, "gate"),
            new(KeyStroke.Esc, Command.CloseModal, "close"),
            // THE LOG'S CURSOR, and the entry it lands on is the one that
            // unwraps. Untaught and off the hint line for the reason the queue's
            // are: the arrows do this through the table widget, so the one line
            // of hints goes to keys a person has no other way to find.
            new(KeyStroke.Char('j'), Command.SelectNext, "down")
                { OffTheHintLine = true, Untaught = true },
            new(KeyStroke.Char('k'), Command.SelectPrevious, "up")
                { OffTheHintLine = true, Untaught = true },
        ],

        // THE ONE MODAL A KEY DOES NOT OPEN, and it owns the keyboard exactly
        // like the ones that do. Without an arm here it fell through to Normal
        // mode and offered every key in the console - fly, take over, forget a
        // credential - to a person the control plane will refuse, over a queue
        // that is empty because nobody is signed in.
        //
        // Escaping is a real answer rather than a dismissal: somebody who wants
        // to look at an empty console, or who opened gg to read the help, is
        // allowed to.
        //
        // ONE STEP, AND APPROVING IS NOT A KEY. There was a second press here
        // - `a`, "I have approved it" - because the poll blocked and could not
        // be started while this modal was up. It could only ever tell the
        // console something it was about to find out, and what it actually did
        // was leave somebody who had already approved in front of a console
        // showing no sign of it. The poll now runs from the moment the code is
        // drawn, so the only keys once a code is showing are the ones that help
        // a person reach the browser.
        // THE ONE QUESTION, AND ITS TWO ANSWERS - ON THE LETTERS NORMAL MODE
        // ACTUALLY LEAVES FREE, which is not the ones the words start with.
        // `e` is the envelope, `a` is actions and `g` is refresh, all in the
        // mode this modal is opened FROM; a person who pressed `n` and then
        // reached for `e` would be reaching for a key that means something else
        // one keypress earlier. Of what is left - m, o, s, w, z - `w` is "write
        // it myself" and `m` is the model, and both say what they do rather
        // than being the first letter of a word that was taken.
        // A FEW SHARES AND A KEY EACH. Digits rather than letters, because
        // there is an order to them and a person reading 1/2/3 does not have
        // to learn which letter stood for which fraction. `0` is keep
        // nothing, which sits at the end of the same sequence.
        UiMode.FloorChoice =>
        [
            new(KeyStroke.Char('1'), Command.KeepATenth, "keep a tenth back")
                { Label = "10%" },
            new(KeyStroke.Char('2'), Command.KeepAQuarter, "keep a quarter back")
                { Label = "25%" },
            new(KeyStroke.Char('3'), Command.KeepAHalf, "keep half back")
                { Label = "50%" },

            // ITS OWN KEY, not escape. Escaping is "I did not mean to open
            // this"; keeping nothing is a decision, and one keypress must not
            // be able to mean both.
            new(KeyStroke.Char('0'), Command.KeepNothing, "keep nothing back")
                { Label = "None" },

            new(KeyStroke.Esc, Command.CloseModal, "leave the floor as it is"),
        ],

        UiMode.ComposeChoice =>
        [
            new(KeyStroke.Char('w'), Command.ComposeInEditor, "write it in your editor")
                { Label = "Editor" },
            new(KeyStroke.Char('m'), Command.ComposeWithAgent, "compose it with an agent")
                { Label = "Agent" },

            // THE ONE WAY OUT, the same key it is in every other modal - which
            // is what makes it findable without being learned. Escaping opens
            // nothing: a flight nobody confirmed is a number that was never
            // taken.
            new(KeyStroke.Esc, Command.CloseModal, "open nothing"),
        ],

        UiMode.SignIn => context.SignInStarted
            ?
            [
                // WHAT A PERSON OTHERWISE READS ACROSS BY HAND. gg owns this
                // terminal, so nothing on the screen can be clicked and nothing
                // can be selected without fighting the alternate screen. All
                // three are here rather than in the step before, because there
                // is no URL and no code until one is showing.
                // KEYED ON THE LABELS THE MODAL DRAWS. It writes `Open:` and
                // `Code:`, so `o` and `c` are read off the screen rather than
                // learned, and the link takes `l` - which is what is left and
                // what it is.
                new(KeyStroke.Char('o'), Command.OpenSignInUri, "open in browser")
                {
                    When = "once a code is showing",

                    // THE THREE READ AS A SET, so they are worded as one: a
                    // verb and its object, same shape, same length. `Browser',
                    // `Link' and `Code' would be three nouns a person has to
                    // work out the verb for.
                    Label = "Open browser",
                },
                new(KeyStroke.Char('l'), Command.CopySignInUri, "copy the link")
                {
                    When = "once a code is showing",
                    Label = "Copy link",
                },

                // AND THE CODE, which is the half that has to be TYPED. The
                // link can be opened; the code has to arrive in a box in a
                // browser, and reading eight characters across by hand from a
                // terminal that will not let them be selected is the one part
                // of this a person could get wrong.
                new(KeyStroke.Char('c'), Command.CopySignInCode, "copy the code")
                {
                    When = "once a code is showing",
                    Label = "Copy code",
                },
                new(KeyStroke.Esc, Command.CloseModal, "give up"),
            ]
            :
            [
                // THE STEP BEFORE, WHICH IS THE ONE A PERSON ARRIVES AT
                // WITHOUT ASKING: gg opens this modal on a console nobody is
                // signed in to. One answer, and it is the reason they are
                // looking at it.
                new(KeyStroke.Char('y'), Command.SignIn, "sign in") { Label = "Sign in" },
                new(KeyStroke.Esc, Command.CloseModal, "carry on signed out"),
            ],

        _ =>
        [
            new(KeyStroke.Char('q'), Command.Quit, "quit"),
            // `g` for "get again". `r` is reject inside the gate modal and `R`
            // would be the only capital in the map, which is a shape somebody
            // has to learn rather than read.
            new(KeyStroke.Char('g'), Command.Refresh,
                context.Refresh is { Length: > 0 } says ? $"refresh {says}" : "refresh"),
            new(KeyStroke.Char('?'), Command.ToggleHelp, "help"),
            // WHERE THE FLIGHTS ARE. Both tabs that list them, because the
            // cursor is on a flight in either and this opens what the cursor
            // is on. Scoped, not hidden: the binding is unchanged and `a`
            // answers on every tab exactly as it did.
            // ONE KEY, TWO MEANINGS, AND THE TAB DECIDES WHICH - the shape
            // argued for over `f' three arms down, and here it also repairs
            // something. `a' was bound on EVERY tab and merely hidden from the
            // line when no flight was under a cursor, which on the airspace tab
            // is always: it opened a flight modal about no flight.
            //
            // AND IT HAS TO BE DECIDED HERE. Resolve answers the FIRST match
            // and this arm is above the tab spreads, so an `a' added down there
            // would never fire - the trap a red test caught for `enter'.
            new(KeyStroke.Char('a'),
                    context.Showing == TabId.Envelope
                        ? Command.ToggleAirspaceActions
                        : Command.ToggleFlightActions,
                    "actions")
                { OffTheHintLine = context.Showing != TabId.Envelope
                                   && !OverAFlight(context) },

            // IN HELP, WITH THE CREDENTIAL KEYS. A gate is decided from the
            // modal that put the question on the screen and named the
            // approver; the line was advertising the shortcut past a question
            // nobody had read yet.
            new(KeyStroke.Char('d'), Command.OpenGate, "decide")
                { OffTheHintLine = true },

            // A CONVENTION, NOT A FEATURE. Every terminal program moves focus
            // with tab, and this one prints its six tab keys on the tabs
            // themselves - so the line was spending a slot to teach the one
            // key nobody has to be taught.
            new(KeyStroke.TabKey, Command.FocusNextPane, "next tab")
                { OffTheHintLine = true },
            // ON THE ROW UNDER THE CURSOR, whichever list has the screen. Not
            // on the hint line: a person presses enter on a row without being
            // told to, and the two lists that answer it are a table and a
            // queue, where it is the obvious thing to try.
            // ON THE ROW UNDER THE CURSOR, and which row that is depends on
            // which tab has the screen. On the fleet it is this machine's
            // runner, which is where the log and the two actions live.
            // AND ON THE AIRSPACE TAB THE THING UNDER THE CURSOR IS WHERE THE
            // AIRSPACE IS, which takes a field rather than a modal. It joins
            // this expression rather than arriving as its own binding, because
            // Resolve answers the FIRST match and a second `enter` further down
            // the list would never be reached.
            context.Showing switch
            {
                TabId.Runners =>
                    new(KeyStroke.EnterKey, Command.ShowRunner, "open this runner")
                        { OffTheHintLine = true },

                // OFF THE LINE LIKE ITS TWO SIBLINGS, once the field it
                // focuses became a box that is drawn at all times. It used to
                // be the only way to learn an unset airspace could be
                // answered, which is why it was on the line; the box's own
                // title says "airspace - enter to edit" now, so the line was
                // the second place saying it - and of two places that say one
                // thing, the one nobody is looking at is the one that goes
                // stale.
                TabId.Envelope =>
                    new(KeyStroke.EnterKey, Command.FocusAirspacePath,
                        "say where the airspace is") { OffTheHintLine = true },

                _ => new(KeyStroke.EnterKey, Command.ShowFlight, "open this flight")
                    { OffTheHintLine = true },
            },
            // BOUND AND NOT TAUGHT. See KeyBinding.Hidden: the arrows do this
            // through the list widget, so the hint line's slots go to keys a
            // person has no other way to find.
            new(KeyStroke.Char('j'), Command.SelectNext, "down")
                { OffTheHintLine = true, Untaught = true },
            new(KeyStroke.Char('k'), Command.SelectPrevious, "up")
                { OffTheHintLine = true, Untaught = true },
            // THE FLEET, AND IT COSTS NOTHING TO SHOW. `u' because every letter
            // that reads is taken: r is repositories, n is new flight, e is
            // envelope. It is in the word and it is free, which is the whole
            // claim - see Tabs.KeyFor.
            new(KeyStroke.Char('u'), Command.ToggleRunners,
                Closes(context, TabId.Runners, "runners")) { OffTheHintLine = true },
            // WHAT A SECOND PRESS WILL DO, and under tabs that is "close" only
            // while you are looking at it. A key that said "hide" for an open
            // tab you had switched away from would advertise a close that does
            // not happen - the key brings it forward instead.
            //
            // HIDDEN BECAUSE THE TAB SAYS IT. Each of these six is on its own
            // tab in the bar, with its key on the label - so the hint line,
            // which is one line, keeps only the keys that have nowhere else to
            // be advertised. Bound, not advertised twice: the rule
            // KeyBinding.Hidden was written for.
            new(KeyStroke.Char('l'), Command.ToggleLive, Closes(context, TabId.Live, "live"))
                { OffTheHintLine = true },
            new(KeyStroke.Char('b'), Command.ToggleBrowse, Closes(context, TabId.Browse, "browse"))
                { OffTheHintLine = true },
            new(KeyStroke.Char('r'), Command.ToggleRepositories,
                Closes(context, TabId.Repositories, "repositories")) { OffTheHintLine = true },
            // `p` for plan, which is the verb it calls.
            // `e` for envelope, which is the noun and the verb it calls.
            new(KeyStroke.Char('e'), Command.ToggleEnvelope,
                Closes(context, TabId.Envelope, "envelope")) { OffTheHintLine = true },
            // ONE KEY, TWO MEANINGS, AND THE TAB DECIDES WHICH. This was two
            // booleans with an explicit precedence between them, because live
            // and browse shared a region: both flags on was a state the console
            // could not reach and the pure function could still be handed.
            // Exactly one tab is showing, so the ambiguity is gone by
            // construction rather than by a rule somebody has to maintain.
            .. context.Showing == TabId.Live
                ? (KeyBinding[])[new(KeyStroke.Char('f'), Command.ToggleFreeze,
                    context.Frozen ? "unfreeze" : "freeze to copy")
                    { When = "while the live tab is showing" }]
                : [],
            // WHAT IS LEFT ON THE TAB ITSELF: the two keys that move around
            // what is already on screen. Pull, apply, draft and the outcome
            // moved behind `a' - see UiMode.AirspaceActions above - because a
            // line carrying ten keys truncated mid-sentence, and the four that
            // went are occasional acts on the whole airspace while these two
            // are pressed constantly.
            .. context.Showing == TabId.Envelope
                ? (KeyBinding[])[

                    // `v' TURNS THE PANE, and only over a document. A folder
                    // row has no document to show three ways, and a key that
                    // resolves there and changes nothing on screen is the
                    // dead-key shape Article XI names.
                    //
                    // WHAT IT NO LONGER DOES is open a modal. The pane beside
                    // the tree shows the document now, so the modal kept only
                    // what is about the airspace as a whole - and that is `o'
                    // below.
                    .. context.OverADocument
                        ? (KeyBinding[])
                        [
                            new(KeyStroke.Char('v'), Command.NextAirspaceView, "next view")
                                { When = "while the cursor is on a document" },
                        ]
                        : [],

                    // `w' CROSSES, and only over a document. The pane beside a
                    // folder row is a sentence rather than something to
                    // scroll, so the keyboard would go somewhere a person
                    // cannot see it go - which is Article XI's dead key.
                    .. context.OverADocument
                        ? (KeyBinding[])
                        [
                            new(KeyStroke.Char('w'), Command.NextAirspacePane,
                                    context.ReadingTheDocument
                                        ? "back to the tree"
                                        : "read the document")
                                { When = "while the cursor is on a document" },
                        ]
                        : []]
                : [],
            .. context.Showing == TabId.Browse
                ? (KeyBinding[])[new(KeyStroke.Char('f'), Command.FlyPicked, "fly this")
                    { When = "while the browse tab is showing" }]
                : [],
            // Only offered when there is something to take. A key advertised
            // against a flight with no held tree is a key that does nothing, and
            // the hints come from the same context dispatch does so the two
            // cannot drift.
            .. context.Takeable
                ? (KeyBinding[])[new(KeyStroke.Char('t'), Command.TakeFlight, "take over")
                    { When = "when the flight has a tree somebody is holding" }]
                : [],
            // Only after somebody has taken it. Handing back a flight nobody
            // took is a key that does nothing, and the hints come from the same
            // context dispatch does.
            .. context.HandedBackable
                ? (KeyBinding[])[new(KeyStroke.Char('h'), Command.HandBack, "hand back")
                    { When = "after you have taken it" }]
                : [],

            // TENANT-LEVEL WRITES, in Normal mode only. A modal holds the keyboard
            // while it is open, and one of these reachable from a gate decision
            // would be a key doing something unrelated to the question on screen.
            // ASKS, RATHER THAN OPENING. There are two ways to compose a flight
            // now and neither is the obvious one, so the key opens the question
            // and the answer decides which child the loop starts.
            new(KeyStroke.Char('n'), Command.AskHowToCompose, "new flight"),
            // THE TWO CREDENTIAL KEYS ARE IN HELP AND NOT ON THE LINE. Adding
            // and forgetting a credential is a thing a person does when they
            // set the tenant up and then about twice a year, and it was
            // spending two of the line's slots every second of every session.
            // BOTH GATES, AND THE KEY IS DEAD UNLESS BOTH ARE OPEN. An
            // advertised key that does nothing is how a person concludes the
            // console is broken - and this one would be advertising somebody
            // else's authority.
            .. context.FleetAllowancesOffered
                ?
                [
                    new KeyBinding(
                        KeyStroke.Char('v'), Command.ToggleAllowances,
                        context.Showing == TabId.Allowances
                            ? "close the fleet's allowances"
                            : "the fleet's allowances")
                    {
                        OffTheHintLine = true,
                        When = "for an administrator whose file asked for the pane",
                    },
                ]
                : (KeyBinding[])[],

            .. context.AllowanceIsMine && context.Showing == TabId.Runners
                ?
                [
                    new KeyBinding(
                        KeyStroke.Char('o'), Command.AskToKeepAShare, "keep a share back")
                    {
                        When = "on your own machine's allowance, on the runners tab",
                    },
                ]
                : (KeyBinding[])[],

            new(KeyStroke.Char('c'), Command.AddCredential, "add credential")
                { OffTheHintLine = true },
            // `x` for forget, because `f` is freeze and fly-this and `r` is
            // reject. A store you cannot clean is a store people work around.
            new(KeyStroke.Char('x'), Command.ForgetCredential, "forget credential")
                { OffTheHintLine = true },
            // WITH THE TWO CREDENTIAL KEYS, AND FOR THEIR REASON. Inviting
            // somebody happens when a tenant is set up and then about twice a
            // year, and it was spending a slot of the line every second of
            // every session.
            new(KeyStroke.Char('i'), Command.Invite, "invite")
                { OffTheHintLine = true },
            // `y` because every letter in `fly by hand` is taken: f is freeze
            // and fly-this, l is nothing yet but reads as live, b is browse, h
            // is hand back, a and n and d are taken. A key chosen for its
            // mnemonic and then silently shadowing another is worse than one
            // chosen for being free and said to be.
            // ASKS, LIKE `n` DOES. Flying by hand still needs an intent written,
            // and it is written the same two ways - so the same question, and a
            // person does not have to remember which doors ask.
            // WHERE FLIGHTS ARE, beside `n`, which is the other way to start
            // one. Scoped for `a`'s reason rather than hidden: a person on the
            // airspace tab is not starting a flight by hand, and the key still
            // answers if they do.
            new(KeyStroke.Char('y'), Command.AskHowToFlyByHand, "fly by hand")
                { OffTheHintLine = !OverAFlight(context) },
        ],
    };

    /// <summary>
    /// What a keystroke does here, or nothing.
    /// </summary>
    /// <remarks>
    /// Ctrl-C is handled ahead of the table and in every mode. A modal that
    /// could swallow it would be a modal that can trap the terminal, which is
    /// the failure the escape hatch exists to make impossible - this is the
    /// belt to that braces.
    /// </remarks>
    public static Command? Resolve(KeyStroke key, KeymapContext context)
    {
        if (key == Interrupt)
        {
            return Command.Quit;
        }

        foreach (var binding in Bindings(context))
        {
            if (binding.Key == key)
            {
                return binding.Command;
            }
        }

        return null;
    }

    /// <summary>
    /// The one key that leaves the modal in this context, or null outside one.
    /// </summary>
    /// <remarks>
    /// Named rather than assumed, so the property test can ask the keymap what
    /// its escape hatch is instead of hard-coding a guess and proving the
    /// guess.
    /// </remarks>
    public static KeyStroke? EscapeHatch(KeymapContext context) =>
        context.Mode == UiMode.Normal ? null : KeyStroke.Esc;

    /// <summary>
    /// Whether the tab on the screen is a list of flights.
    /// </summary>
    /// <remarks>
    /// <b>ONE PREDICATE FOR THE TWO KEYS THAT ACT ON A FLIGHT</b>, because two
    /// copies of "which tabs are flights" would be two things to update the day
    /// a third such tab exists, and the one that is missed is the one nobody is
    /// looking at. Both tabs, not just the one called Flights: the queue is
    /// flights needing you, the cursor sits on one in either, and these keys
    /// open and start what the cursor is on.
    /// </remarks>
    private static bool OverAFlight(KeymapContext context) =>
        context.Showing is TabId.Queue or TabId.Flights;

    /// <summary>
    /// The status line, rendered from the bindings that are live.
    /// </summary>
    /// <remarks>
    /// Generated from <see cref="Bindings"/> rather than written alongside it.
    /// A hand-written hint string is a second list, and a second list drifts.
    /// </remarks>
    public static string Hints(KeymapContext context) =>
        string.Join(" · ", Bindings(context)
            .Where(b => !b.OffTheHintLine)
            .Select(b => $"{b.Key.Name} {b.Description}"));

    /// <summary>
    /// Every key this console answers, in any context, once each.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>THE HELP PAGE ASKS A DIFFERENT QUESTION FROM THE HINT LINE.</b> The
    /// line shows what is live, which is right: an advertised key that does
    /// nothing teaches a person the console is broken. The page is where
    /// somebody looks for a key they do not know, and it was showing one
    /// context's bindings - so <c>f</c> was absent whenever neither the live
    /// pane nor browse was showing, and the gate modal's <c>a</c> and <c>r</c>
    /// were never on it at all.
    /// </para>
    /// <para>
    /// <b>A union over the contexts rather than a second list.</b> Written out
    /// by hand this would be the third list of keys in the program, after
    /// <see cref="Bindings"/> and the hint line - and the one people read when
    /// they are already confused, so the one that must not drift. The contexts
    /// enumerated here are every mode crossed with every flag that changes what
    /// is bound; a flag that changed the set and was left out would show up as
    /// a key missing from the page, which is what
    /// <c>HelpNamesEveryKeyTests</c> asserts.
    /// </para>
    /// <para>
    /// Ordered by mode, then by the order <see cref="Bindings"/> writes them,
    /// so the page reads in the order somebody wrote it rather than in
    /// dictionary order.
    /// </para>
    /// </remarks>
    /// <summary>
    /// The answers this modal should draw as buttons, if it draws any.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Derived from the bindings, so a button cannot say something the key
    /// does not do.</b> Which answers a modal has is written once, here; a list
    /// kept beside it would be a second place the same thing lives.
    /// </para>
    /// <para>
    /// <b>Empty unless every answer is labelled.</b> See
    /// <see cref="KeyBinding.Label"/>: a partly-buttoned modal hides one of its
    /// own options behind a keystroke nobody was shown.
    /// </para>
    /// <para>
    /// <b>The escape hatch is never one.</b> It is on every modal and the frame
    /// already means there is a way out; a button for it would be the one
    /// affordance nobody needed help finding.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<KeyBinding> Buttons(KeymapContext context)
    {
        if (context.Mode == UiMode.Normal)
        {
            return [];
        }

        var answers = Bindings(context).Where(b => b.Key != KeyStroke.Esc).ToList();

        return answers.Count > 0 && answers.TrueForAll(b => b.Label is { Length: > 0 })
            ? answers
            : [];
    }

    public static IReadOnlyList<KeyCatalogueEntry> Catalogue()
    {
        var entries = new List<KeyCatalogueEntry>();

        // BY WHAT IT DOES, NOT BY WHAT IT SAYS. A toggle's description reads
        // the state it will change - "browse" and "close browse" are one
        // binding - so keying this on the description put every toggle on the
        // page twice with the two halves of a sentence. Keyed on the command,
        // the first context wins, and the first context is the plainest one.
        //
        // `f` still appears twice, and should: it is two commands over one key,
        // and each says when it applies.
        var seen = new HashSet<(UiMode Mode, KeyStroke Key, Command Command)>();

        foreach (var mode in Enum.GetValues<UiMode>())
        {
            foreach (var context in Shapes(mode))
            {
                foreach (var binding in Bindings(context))
                {
                    if (seen.Add((mode, binding.Key, binding.Command)))
                    {
                        entries.Add(new KeyCatalogueEntry(mode, binding));
                    }
                }
            }
        }

        return entries;
    }

    /// <summary>
    /// Every shape of context that can change what is bound in one mode.
    /// </summary>
    /// <remarks>
    /// Every tab crossed with the three flags that change what is bound. The
    /// union has to be complete rather than reachable: this is a pure function
    /// over a struct, and a shape left out shows up as a key missing from the
    /// help page, which is what HelpNamesEveryKeyTests asserts.
    /// </remarks>
    private static IEnumerable<KeymapContext> Shapes(UiMode mode) =>
        // ORDER IS THE PAGE'S ORDER. The plainest shape comes first - the queue
        // tab, nothing frozen, nothing to take - so the keys that always work
        // are listed first and in the order they are written.
        from showing in Enum.GetValues<TabId>()
        from frozen in (bool[])[false, true]
        from takeable in (bool[])[false, true]
        from handedBack in (bool[])[false, true]
        // THE SIGN-IN MODAL'S TWO STEPS, which are two sets of keys behind one
        // mode. Left out, `a` resolved in the running console and appeared on
        // no page - a key nobody could discover, which is the thing this
        // catalogue exists to prevent.
        from signInStarted in (bool[])[false, true]
        // THE RUNNER MODAL'S TWO SHAPES, for the sign-in modal's reason: two
        // sets of keys behind one mode. Left out, `x shut it down` would appear
        // on the help page unconditionally while resolving in only one of them.
        from runnerIsOurs in (bool[])[false, true]
        // AND WHETHER THERE IS ANYTHING TO WATCH, which is a third set of keys
        // behind the same mode. Left out, `w watch what it is flying` resolved
        // over a flying runner and appeared on no page - the sign-in modal's
        // defect exactly, one modal over, which is what a catalogue built by
        // enumeration rather than by hand is for.
        from runnerIsFlying in (bool[])[false, true]
        // AND WHOSE ALLOWANCE THE SELECTED MACHINE SPENDS FROM, for the reason
        // the three clauses above it each record: a flag the bindings branch on
        // and this product leaves out is a key that resolves in the running
        // console and appears on no page.
        from allowanceIsMine in (bool[])[false, true]
        from fleetOffered in (bool[])[false, true]
        // AND WHETHER THE AIRSPACE CURSOR IS ON A DOCUMENT, for the reason the
        // four clauses above it each record. `v' means two different things
        // across this flag - read this document back, or read the rules in
        // force - so one of the two appeared on no page.
        from overADocument in (bool[])[false, true]
        // AND WHICH HALF HOLDS THE KEYBOARD. `w' says "the document" from one
        // side and "the tree" from the other, so one of the two would appear
        // on no page - which is the argument the clause above it records.
        from reading in (bool[])[false, true]

        // AND WHETHER THE HELP CURSOR IS ON A GROUP. Crossed here so the fold
        // key reaches the catalogue, which is what the help page is built
        // from - a key offered only in one shape and left out of this would be
        // advertised nowhere.
        from overAFold in (bool[])[false, true]
        select new KeymapContext(
            mode, showing, frozen, takeable, handedBack, overADocument, reading,
            overAFold)
        {
            SignInStarted = signInStarted,
            RunnerIsOurs = runnerIsOurs,
            RunnerIsFlying = runnerIsFlying,
            AllowanceIsMine = allowanceIsMine,
            FleetAllowancesOffered = fleetOffered,
        };

    /// <summary>
    /// A toggle's description: what a second press will do from here.
    /// </summary>
    /// <remarks>
    /// "close" only while the tab is the one showing, because that is the only
    /// place the key closes anything. From another tab it brings this one
    /// forward, and advertising a close that does not happen is how a person
    /// learns to stop trusting the line.
    /// </remarks>
    private static string Closes(KeymapContext context, TabId tab, string name) =>
        context.Showing == tab ? $"close {name}" : name;
}
