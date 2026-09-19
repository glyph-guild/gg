using Gg.Contracts;
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
/// <summary>
/// The condition the Look page's keys carry, written once.
/// </summary>
/// <remarks>
/// Four keys with one condition would otherwise spell it four ways, and the
/// help page prints whichever each of them said.
/// </remarks>
internal static class LookPageCondition
{
    internal const string Said = "on the Look page";
}

/// <summary>
/// The condition the compose modal's marking keys carry, written once.
/// </summary>
internal static class ComposeRepositoriesCondition
{
    internal const string Said = "on the repositories tab";
}

public readonly record struct KeyStroke(
    char? Input, bool Ctrl = false, bool Escape = false, bool Tab = false, bool Enter = false,
    bool Left = false, bool Right = false)
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

    /// <summary>
    /// The arrows that mean "less" and "more" rather than "up" and "down".
    /// </summary>
    /// <remarks>
    /// <b>Named keys for <see cref="EnterKey"/>'s reason</b>: what arrives from
    /// a terminal is a named key rather than a rune, and matching an escape
    /// sequence as a character would be matching one terminal's idea of it.
    /// </remarks>
    /// <remarks>
    /// <c>LeftKey</c> rather than <c>Left</c>, exactly as <see cref="EnterKey"/>
    /// is not <c>Enter</c>: the positional parameter already has the name, and
    /// the compiler says so rather than letting the two quietly disagree.
    /// </remarks>
    public static KeyStroke LeftKey { get; } = new(null, Left: true);

    /// <summary>The other one.</summary>
    public static KeyStroke RightKey { get; } = new(null, Right: true);

    /// <summary>How this key is written where a person will read it.</summary>
    public string Name =>
        Escape ? "esc"
        : Tab ? "tab"
        : Enter ? "enter"
        : Left ? "left"
        : Right ? "right"
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
    bool OverAFold = false,

    /// <summary>
    /// Whether the help modal is showing the Look page.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The distinction `e' and `o' deliberately do NOT make, and this one
    /// has to.</b> Those two are bound for the whole modal because knowing the
    /// page would mean a new field here and every property test's cross-product
    /// doubling "for a distinction a person does not feel". A person feels this
    /// one: left and right change a VALUE, and on the Keys page there is no
    /// value under the cursor for them to change — an advertised key that does
    /// nothing is the dead key Article XI names.
    /// </para>
    /// <para>
    /// <b>Last, because this is a positional record</b> — the reason
    /// <see cref="OverAFold"/> gives directly above.
    /// </para>
    /// </remarks>
    bool OnTheLookPage = false,

    /// <summary>
    /// Whether the compose modal is showing its repositories rather than its
    /// kinds.
    /// </summary>
    /// <remarks>
    /// <b>Space means something on one half and nothing on the other</b>, and
    /// a key offered where it does nothing is the dead key Article XI names.
    /// Last, because this is a positional record.
    /// </remarks>
    bool OnTheRepositoriesHalf = false,

    /// <summary>
    /// Whether the flight on screen names a ticket this machine can read.
    /// </summary>
    /// <remarks>
    /// <b>Both halves, because either one alone leads nowhere.</b> A flight
    /// opened from words has no ticket to go to, and a ticket whose provider
    /// has no reader declared here opens a modal that can only say it could not
    /// ask. Last, because this is a positional record.
    /// </remarks>
    bool OverAReadableTicket = false,

    /// <summary>
    /// Whether a gate is waiting on the row the queue's cursor is on.
    /// </summary>
    /// <remarks>
    /// <b>What separates a row that can be answered from one that can only be
    /// read.</b> The two acts an approval offers are bound only where there is
    /// something to approve - a decision page about no decision is a button
    /// that opens a sentence saying so. Last, because this is a positional
    /// record.
    /// </remarks>
    bool AGateWaits = false)
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
    /// Whether the flight on screen names a link and no ticket a reader here
    /// can read.
    /// </summary>
    /// <remarks>
    /// <b>The second half is what keeps one key honest.</b> Where a reader can
    /// read the item, the modal is the better answer and this stays false; this
    /// is every other flight that names somewhere to go - which is what a
    /// sweep's nomination produces, since it carries the work item's url and no
    /// provider at all.
    /// </remarks>
    public bool OverALink { get; init; }

    /// <summary>
    /// Whether the activity line is showing part of its message rather than
    /// all of it.
    /// </summary>
    /// <remarks>
    /// <b>Both halves of the question, answered once.</b> Clipping is the text
    /// against the width, and neither alone decides it — a long message on a
    /// wide terminal is not clipped, and a short one is not clipped anywhere.
    /// In the CONTEXT rather than read off a widget, like everything else here,
    /// so the key that opens the rest of it is advertised exactly where it
    /// works.
    /// </remarks>
    public bool SaidIsClipped { get; init; }

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

    /// <summary>Whether the control plane says whose the selected machine is at all.</summary>
    /// <remarks>
    /// <b>Empty is not open, and here it is not a key either.</b> A control
    /// plane with no claim door leaves every row's ownership blank, and
    /// offering a claim against one would advertise a refusal.
    /// </remarks>
    public bool RunnerOwnershipIsKnown { get; init; }

    /// <summary>Whether the person at this console is the one who claimed it.</summary>
    public bool RunnerIsClaimedByYou { get; init; }

    /// <summary>Whether it takes only its owner's flights.</summary>
    public bool RunnerIsReserved { get; init; }

    /// <summary>
    /// Whether the gate on screen is a machine saying what it lacks.
    /// </summary>
    /// <remarks>
    /// <b>The one gate with no answer on it.</b> Every other gate is a person's
    /// to approve or reject; this one is cleared by the machine's next reading,
    /// so both keys would be answers that do not answer.
    /// </remarks>
    public bool GateIsABringUpAsk { get; init; }

    /// <summary>Whether an admin has held this machine back for the tenant.</summary>
    /// <remarks>
    /// <b>Which way the admin's key reads</b>, and nothing else: the act is a
    /// toggle between the tenant's and open, and a key labelled with the state
    /// it is already in would be a key that does nothing.
    /// </remarks>
    public bool RunnerIsTheTenants { get; init; }

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
    /// Whether the runner the cursor is on is beating.
    /// </summary>
    /// <remarks>
    /// <b>What decides whether watching is offered at all.</b> It used to ask
    /// whether the runner was FLYING, which was the same question while a
    /// channel existed only for a flight - and withheld the key in the one
    /// state a person most wants to attach in, which is a machine waiting for
    /// work.
    /// <para>
    /// <b>Beating is what is actually required.</b> An introduction is picked up
    /// on a heartbeat, so a runner that is not beating never sees one and the
    /// console would sit out its whole life to learn nothing.
    /// </para>
    /// </remarks>
    public bool RunnerIsBeating { get; init; }

    /// <summary>
    /// Whether the gate under the queue's cursor asks for an agent login.
    /// </summary>
    /// <remarks>
    /// <b>Derived from the closed kind</b>, in the one place contexts are
    /// built, so the button and the dispatch read one answer - and so a
    /// maintenance kind a newer control plane invents renders as an ordinary
    /// gate rather than offering a key that would start the wrong ceremony.
    /// </remarks>
    public bool GateAsksForAgentLogin { get; init; }

    /// <summary>
    /// Whether the board's cursor is on a nomination that can still be
    /// answered.
    /// </summary>
    /// <remarks>
    /// <b><see cref="AGateWaits"/>'s question, asked about the other
    /// decision.</b> The board holds two kinds of row and one of them is
    /// machinery: a watch has nothing on it for a person to answer, and a row
    /// already ended is a 409 at the door. Derived with the rest, so the hint
    /// line and the dispatch cannot disagree about whether the key is live.
    /// </remarks>
    public bool ANominationWaits { get; init; }

    /// <summary>Whether the board's cursor is on a row at all.</summary>
    /// <remarks>
    /// <b>Any row, which is the wider of the two.</b> A watch's row and an
    /// ended nomination cannot be ANSWERED and can both be read - so opening
    /// one asks this, and the answers inside ask
    /// <see cref="ANominationWaits"/>. The two are correlated on purpose: a
    /// nomination waiting is always a row under the cursor.
    /// </remarks>
    public bool ABoardRowIsUnderTheCursor { get; init; }

    /// <summary>
    /// Whether the board's cursor is on a row whose flight this console holds.
    /// </summary>
    /// <remarks>
    /// <b>Two things at once, deliberately.</b> The row has to have opened into
    /// a flight - which no standing row has - and that flight has to be on the
    /// page this console has loaded, because the flights tab is paged. Either
    /// miss means there is nowhere to send the cursor, and a key offered
    /// against nowhere is a key that does nothing.
    /// </remarks>
    public bool TheRowsFlightIsLoaded { get; init; }

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
            state.HelpFold is not null,

            // AND WHETHER THE LOOK PAGE IS SHOWING, for that same reason: four
            // keys that change a value have nothing to act on anywhere else in
            // this modal.
            //
            // THE PAGE ALONE, NOT THE PAGE AND THE MODE. Resolve dispatches on
            // Mode first and this flag is read only in the Help arm, so testing
            // it here too is a second answer to a question already asked - and
            // it makes the flag underivable from any model that is not in the
            // modal, which is what TheSignInModalReads' completeness guard sets
            // one of everything on. HelpFold, one clause up, does not ask
            // either.
            state.HelpPage is HelpPage.Look,

            // AND WHETHER THE FLIGHT ON SCREEN NAMES A TICKET THIS MACHINE CAN
            // READ, derived here with the rest so the hint line and the
            // dispatch read one answer.
            // AND WHICH HALF OF THE COMPOSE MODAL, for that reason: space
            // marks a repository and there are none to mark on the other.
            state.WorkKindTab is WorkKindTab.Repositories,
            FlightDetails.TicketAReaderHere(state) is not null,

            // AND WHETHER THE ROW UNDER THE QUEUE'S CURSOR IS WAITING ON AN
            // ANSWER, derived here with the rest so the buttons and the keys
            // cannot disagree about whether there is one.
            state.SelectedGate is not null)
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

            // FROM THE ROW, which is the only place that knows: the modal is
            // about whatever the cursor is on, and step 1 put ownership on the
            // row so that these three could be asked without a second fetch.
            RunnerOwnershipIsKnown = Rows.Selected(state) is { Ownership.Length: > 0 },
            RunnerIsClaimedByYou =
                Rows.Selected(state) is { Ownership: RunnerOwnerships.Claimed } claimed
                && state.PrincipalId is { Length: > 0 } you
                && string.Equals(claimed.OwnerPrincipalId, you, StringComparison.Ordinal),
            RunnerIsReserved = Rows.Selected(state) is { Reserved: true },
            RunnerIsTheTenants =
                Rows.Selected(state) is { Ownership: RunnerOwnerships.Tenant },

            // WHETHER THERE IS ANYTHING TO REACH. Derived here with the rest,
            // so the hint line and the dispatch cannot disagree about whether
            // the key is live.
            // WHETHER THE GATE IN FRONT OF SOMEBODY ASKS FOR A LOGIN, read
            // through the one method that knows which kinds this build can
            // act on, so the key and the act cannot disagree.
            GateAsksForAgentLogin = ConsoleAgentLogin.Asking(state) is not null,
            GateIsABringUpAsk = ConsoleBringUp.Asking(state) is not null,

            // BEATING, NOT MERELY NOT OFFLINE: a maintainer is alive and never
            // beats, and an introduction is picked up on a heartbeat.
            RunnerIsBeating = Rows.Selected(state) is { } watchable
                && Gg.Client.RunnerReach.Beats(watchable.State),

            // WHOSE ALLOWANCE THE SELECTED MACHINE SPENDS FROM. Yours rather
            // than Mine: an allowance belongs to the people who registered the
            // machines reporting it, which is a fact the control plane
            // recorded - where Mine is an inference from this console's own
            // file about one particular machine.
            FleetAllowancesOffered = state.IsAdmin && state.FleetAllowancesShown,

            AllowanceIsMine = Rows.Selected(state) is { Yours: true }
                              && AllowanceRows.SelectedName(state) is not null,

            // AND WHETHER THE ROW UNDER THE BOARD'S CURSOR IS ONE SOMEBODY CAN
            // ANSWER, for AGateWaits' reason one field up: two kinds of row
            // share that table and only one of them is a decision.
            ANominationWaits = Rows.StandingUnder(state) is not null,

            // AND WHETHER THERE IS A ROW AT ALL, which is what OPENING one
            // asks. Derived from the same rows the table draws, so a key
            // offered here is a key over something a person can see.
            ABoardRowIsUnderTheCursor = BoardDetails.Under(state) is not null,
            TheRowsFlightIsLoaded = BoardDetails.FlightInTheList(state) is not null,

            // AND WHETHER IT NAMES SOMEWHERE TO GO WITH NO READER FOR IT,
            // derived here with the rest so the hint line and the dispatch
            // cannot disagree about which of the two `t' means.
            OverALink = FlightDetails.LinkHere(state) is not null,

            // WHETHER THE LINE BELOW IS SHOWING ALL OF ITSELF, measured
            // against the width the layout last found. Derived here with
            // everything else, so the key that opens the rest is offered on
            // exactly the messages that have a rest.
            SaidIsClipped = state.SaidColumns > 0
                            && PaneText.Activity(state).Length > state.SaidColumns,

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
    /// Whether this key is about the console rather than about what is on the
    /// screen.
    /// </summary>
    /// <remarks>
    /// <b>Quit, refresh and help, and the hint line draws them at its
    /// right-hand end.</b> They are true on every tab and in every state,
    /// where everything else on the line changes as somebody moves around -
    /// so they are pinned to one place and the left-hand end is what the tab
    /// decides. Not a second way to hide a key: both ends are advertised, and
    /// <see cref="Keymap.Hints"/> is still the union.
    /// </remarks>
    public bool Standing { get; init; }

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
    /// <summary>
    /// Whose this machine is, changed from the modal that says whose it is.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Two toggles rather than four keys</b>, each labelled with what a
    /// press will do from here - the shape `Closes` already uses. Claim and
    /// unclaim are one idea seen from two sides, and so are reserve and
    /// release.
    /// </para>
    /// <para>
    /// <b>Offered over anybody's machine, exactly as watching is</b>, because
    /// the door decides: a tenant runner refuses a claim in a sentence naming
    /// what an admin would have to do, and a keymap that made that decision
    /// would be a second copy of the rule. What the console will not do is
    /// offer a key against a control plane that has no such door at all -
    /// which is `RunnerOwnershipIsKnown`, and is the same "empty is not open"
    /// the pane draws by.
    /// </para>
    /// </remarks>
    private static IReadOnlyList<KeyBinding> Owning(KeymapContext context) =>
        context.RunnerOwnershipIsKnown
            ?
            [
                new(KeyStroke.Char('m'), Command.ClaimRunner,
                    context.RunnerIsClaimedByYou ? "give it up" : "claim it for yourself")
                {
                    When = "over a machine whose control plane says whose it is",
                },
                new(KeyStroke.Char('o'), Command.AskWhoMayClaim,
                    context.RunnerIsTheTenants
                        ? "open it for anybody here to claim"
                        : "hold it back for the tenant")
                {
                    When = "over a machine whose control plane says whose it is",
                },
                new(KeyStroke.Char('h'), Command.ReserveRunner,
                    context.RunnerIsReserved
                        ? "let it take the tenant's work again"
                        : "hold it to your own flights")
                {
                    When = "over a machine somebody has claimed",
                },
            ]
            : [];

    private static KeyBinding Turning { get; } =
        new(KeyStroke.Char('v'), Command.NextRunnerView, "next view");

    private static IReadOnlyList<KeyBinding> Watching(KeymapContext context) =>
        context.RunnerIsBeating
            ?
            [
                new(KeyStroke.Char('w'), Command.WatchRunner, "watch this runner")
                {
                    When = "while it is beating",
                },

                // BESIDE `w` AND ON `w`'s TERMS, because it is the same
                // mechanism: an introduction over the control plane, picked up
                // on a heartbeat. A machine that is not beating never sees one,
                // so offering the key would spend somebody's twenty seconds to
                // tell them what the fleet pane already said.
                //
                // AND IN BOTH SHAPES OF THIS MODAL, unlike restart and
                // shut-down. Those go through a pidfile this machine wrote;
                // this goes through the control plane, which is exactly as able
                // to introduce you to somebody else's runner as to this one.
                // ASKS FIRST, ON THIS SCREEN. It used to end the session and
                // type "Which repository is this credential for?" at a bare
                // prompt - handing the question to the one place that holds
                // neither the registry nor the runner it is about. The send
                // still needs the terminal, for the secret; the question does
                // not.
                new(KeyStroke.Char('c'), Command.ChooseCredentialRepository,
                    "give it a credential")
                {
                    When = "while it is beating",
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

            // THE LOOK PAGE'S OWN, and only there. Four keys that change a
            // value have nothing to act on where the cursor is on a key or a
            // setting name, and a hint line generated from this same context is
            // what stops them being advertised there either.
            .. context.OnTheLookPage
                ? (KeyBinding[])
                [
                    new(KeyStroke.RightKey, Command.NextLookValue, "next value")
                        { When = LookPageCondition.Said },
                    new(KeyStroke.LeftKey, Command.PreviousLookValue, "previous value")
                        { When = LookPageCondition.Said },

                    // WHAT THE PAGE IS SITTING ON TOP OF. Bound before reset
                    // because it is the key somebody reaches for second, right
                    // after changing something they cannot see.
                    new(KeyStroke.Char('h'), Command.PeekBehindTheModal, "hide this / bring it back")
                    {
                        When = LookPageCondition.Said,
                        Label = "Hide",
                    },

                    new(KeyStroke.Char('r'), Command.ResetLook, "put it all back")
                    {
                        // SAYS WHEN, because it is not live in the plainest
                        // form of its own mode - a key whose condition a person
                        // cannot see is one they will press on the Keys page
                        // and conclude is broken.
                        When = LookPageCondition.Said,

                        // LABELLED, because the all-or-nothing rule means it
                        // must be: an unlabelled answer takes this page's
                        // buttons away entirely rather than adding one that
                        // does nothing.
                        Label = "Reset",
                    },

                    // WHAT THE SPIKE IS FOR. The page is for trying looks on;
                    // this is how the two that worked leave the console.
                    new(KeyStroke.Char('c'), Command.CopyModal, "copy the changes")
                    {
                        When = LookPageCondition.Said,
                        Label = "Copy changes",
                    },
                ]
                : [],

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
            // FIRST, AND ONLY WHERE THERE IS ONE TO DO. RenderModalButtons
            // focuses the first button, and on a gate asking for a login the
            // act that fixes the machine is the one to land on: approving it
            // answers nothing, because the runner still cannot start its
            // agent. The two answers stay bound - somebody who has decided
            // the machine is not coming back still rejects it - and a gate
            // that asks for nothing offers no such key, which is the rule
            // AModalDoesNotAdvertiseDeadKeysTests holds.
            .. context.GateAsksForAgentLogin
                ? (IReadOnlyList<KeyBinding>)
                    [new(KeyStroke.Char('s'), Command.LogAgentIn, "log the agent in")
                    {
                        // SAID, because a person whose fleet is healthy will
                        // never meet this key on a gate, and the help page is
                        // where they find out it exists before the day they
                        // need it.
                        When = "when this gate is a runner's agent-login ask",
                    }]
                : [],
            // AND NEITHER ANSWER ON A BRING-UP ASK, which is the one gate where
            // both would be a lie. What clears it is the machine's next
            // reading: approving ends this flight and the next reading opens
            // another, and rejecting ends it without the item ever having been
            // fixed. A person offered two answers that do not answer learns to
            // stop reading the surface. What the modal says instead is what is
            // missing and where it is answered.
            .. context.GateIsABringUpAsk
                ? (IReadOnlyList<KeyBinding>)[]
                :
                [
                    new(KeyStroke.Char('a'), Command.ApproveGate, "approve"),
                    new(KeyStroke.Char('r'), Command.RejectGate, "reject"),
                ],
            new(KeyStroke.Esc, Command.CloseModal, "close"),
        ],

        // THE OTHER DECISION, AND IT OWNS THE KEYBOARD THE SAME WAY. Both
        // answers together, because declining has to be as reachable as
        // opening - a console that made one of them the easy key would have an
        // opinion about which answer somebody came to give.
        //
        // `o' AND `d', WHICH ARE THE CONTRACT'S OWN WORDS for the two endings a
        // person may cause. Both are live in Normal mode and neither can be
        // reached from here, which is what a modal owning the keyboard means -
        // GateDecision's `a' and `r' are the same two letters in the same
        // situation.
        //
        // NOT `y'/`n', although this is a confirmation. What confirms it is the
        // sentence the door demands next, and a yes/no in front of that would
        // be two confirmations where the second one is the one with something
        // in it afterwards.
        UiMode.BoardDetail =>
        [
            // BOTH ANSWERS, AND ONLY WHERE THERE IS SOMETHING TO ANSWER. A
            // watch's row is machinery and an ended row is a 409 at the door,
            // so on either the modal is a read and Esc is the whole arm.
            .. context.ANominationWaits
                ? (KeyBinding[])
                [
                    new(KeyStroke.Char('o'), Command.OpenNomination, "open it")
                        { Label = "Open", When = "on a standing nomination" },
                    new(KeyStroke.Char('d'), Command.DeclineNomination, "decline it")
                        { Label = "Decline", When = "on a standing nomination" },
                ]
                : [],

            // WHAT IT BECAME. Offered on an ENDED row, which is the opposite of
            // the two above - they are the answer and this is the consequence
            // of one, so a row can never offer all three.
            .. context.TheRowsFlightIsLoaded
                ? (KeyBinding[])
                [
                    new(KeyStroke.Char('f'), Command.GoToTheFlight, "go to the flight")
                        { Label = "Flight", When = "on a row that opened into one" },
                ]
                : [],
            new(KeyStroke.Esc, Command.CloseModal, "close") { Label = "Close" },
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
            // THE READING ONE FIRST, AND THAT IS THE SAFETY RATHER THAN A
            // PREFERENCE. RenderModalButtons focuses the first button and marks
            // it as the default, so declaration order is what a reflex does -
            // and this modal is now opened with enter, which makes enter twice
            // the commonest thing a person will do to it.
            new(KeyStroke.Char('v'), Command.ShowFlight, "open the flight")
                { Label = "Open flight" },

            // THE TWO ANSWERS, ONLY WHERE THERE IS A QUESTION. `d` was bound
            // here unconditionally and opened a decision page that said "there
            // is no decision waiting on this row any more" - a true sentence
            // reached by a key that should not have been offered.
            //
            // AND `a` MEANS APPROVE HERE, as it does in the decision modal, for
            // the reason a modal owns the keyboard at all: inside one the
            // letters are free, and the letter somebody already knows for this
            // act is the one to use.
            .. context.AGateWaits
                ? (KeyBinding[])
                [
                    new(KeyStroke.Char('d'), Command.OpenGate, "decide a gate on this flight")
                        { Label = "Decide", When = "when a gate is waiting on this flight" },
                ]
                : [],

            // APPROVE IS A SHORTCUT, and it is withheld where the shortcut is
            // wrong. An agent-login gate is not a yes-or-no a person answers:
            // it asks for a machine to be repaired, and it clears itself when
            // that runner next reports ready. An approval typed here closes the
            // ask while the member still cannot fly.
            //
            // AND A BRING-UP ASK IS THE SAME ARGUMENT, one slice later and
            // stronger: what clears it is the machine's next reading, so an
            // approval here ends the flight with the item still missing and the
            // next reading opens another. FOUND BY THE WALK - the decision
            // modal withheld both keys and this shortcut still offered one, so
            // the console said two different things about one gate.
            //
            // NOT A CAPABILITY REMOVED. Decide still reaches both answers for
            // somebody who means one, and giving up on a machine has its own
            // verb - `x` grounds the flight, one level in, which the escalation
            // now handles so a later reading asks again rather than vanishing.
            .. context.AGateWaits && !context.GateAsksForAgentLogin
                                 && !context.GateIsABringUpAsk
                ? (KeyBinding[])
                [
                    new(KeyStroke.Char('a'), Command.ApproveGate, "approve it")
                    {
                        Label = "Approve",
                        When = "when an ordinary gate is waiting on this flight",
                    },
                ]
                : [],

            // AND THE ACT THAT CLEARS THE GATE, where a person looking for
            // what can be done will actually meet it. Binding it in the
            // decision modal alone put it one keypress out of sight: somebody
            // on a held runner's flight was shown three keys and none of them
            // was the one that fixes the machine.
            //
            // AFTER the two answers rather than first, unlike the decision
            // modal. There the act is the point of the page; here the reading
            // key is the default and must stay so - enter twice is the
            // commonest thing a person does to this modal.
            .. context.GateAsksForAgentLogin
                ? (KeyBinding[])
                [
                    new(KeyStroke.Char('s'), Command.LogAgentIn, "log the agent in")
                    {
                        Label = "Log in",
                        When = "when this flight's gate is a runner's agent-login ask",
                    },
                ]
                : [],

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

        UiMode.Notifications => [new(KeyStroke.Esc, Command.CloseModal, "put them down")],

        // THE TWO THINGS THAT CAN BE DONE TO A RUNNER, and the way out. `r' and
        // `x' are repositories and forget-a-credential in Normal mode and mean
        // these here, which is what a modal owning the keyboard is for.
        // ONLY OVER A RUNNER THIS CONSOLE CAN REACH. Both act through a pidfile
        // this machine wrote, so over somebody else's runner the best case is a
        // key that does nothing and the worst is one that shuts down the local
        // runner while a person is looking at another row. Close is always
        // there: a modal with no way out is worse than one with nothing to do.
        // AND `w` OVER ANY RUNNER THAT IS BEATING, ours or not - which is the
        // whole point of it. `watch` reaches a machine through the control
        // plane rather than through a pidfile this one wrote, so unlike `r` and
        // `x` it is exactly as available over somebody else's runner as over
        // this one. Withheld only while a machine is not beating: an
        // introduction is picked up on a heartbeat, so one that is not sending
        // them never sees it.
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
                .. Owning(context),
                Turning,
                new(KeyStroke.Esc, Command.CloseModal, "close"),
            ]
            :
            [
                .. Watching(context),
                .. Owning(context),
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
        // AN ADMIN'S WORD ABOUT THE TENANT'S MACHINES, which is why it asks at
        // all: the other two ownership keys act on one keypress because they
        // act on one person's claim, and this one takes a machine away from
        // whoever holds it or hands every person here one that was held back.
        // THE LIST, AND ONE ACT ON IT. Minting is not here: its one output is a
        // secret shown once, and a console repaints - a screen share, a
        // scrollback and a screenshot all keep what it painted.
        UiMode.FleetTokens =>
        [
            new(KeyStroke.Char('x'), Command.AskToRevokeToken, "revoke this one"),
            new(KeyStroke.Esc, Command.CloseModal, "close"),
        ],

        // ONE WAY, SO IT ASKS. A token revoked by mistake cannot be
        // un-revoked; another is minted, and whoever was about to use the
        // first is left holding a secret that no longer works.
        UiMode.ConfirmRevoke =>
        [
            new(KeyStroke.Char('y'), Command.RevokeToken, "revoke it"),
            new(KeyStroke.Esc, Command.CloseModal, "leave it live"),
        ],

        UiMode.ConfirmOwnership =>
        [
            new(KeyStroke.Char('y'), Command.SetOwnership, "say so"),
            new(KeyStroke.Esc, Command.CloseModal, "leave it as it is"),
        ],

        UiMode.ConfirmGround =>
        [
            new(KeyStroke.Char('y'), Command.GroundFlight, "ground it"),
            new(KeyStroke.Esc, Command.CloseModal, "leave it flying"),
        ],

        UiMode.BrowseFind =>
        [
            // TWO KEYS, AND EVERY OTHER ONE FALLS THROUGH TO THE FIELD - the
            // airspace path's rule: a title is letters, and a keymap that
            // answered them would make it untypeable.
            new(KeyStroke.EnterKey, Command.GoToOrFind, "go there or find it")
                { Label = "Go" },
            new(KeyStroke.Esc, Command.CloseModal, "back to the list"),
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

        // THE LINE ITSELF, WITH ROOM FOR ALL OF IT. Two keys and no views to
        // cross to: what is in this one is the message a person was already
        // reading, so the only questions are "show me the rest" - answered by
        // being open - and "let me keep it".
        UiMode.ReadingSaid =>
        [
            // `c' FOR THE REASON THE THREE VIEWS GIVE IT. What lands on that
            // line is most often a refusal, and a refusal is the thing somebody
            // pastes into a message to somebody else.
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

            // THE ITEM THIS FLIGHT IS ABOUT, where there is one and this
            // machine can read it. Not offered otherwise: a flight opened from
            // words has nowhere to go, and a ticket whose provider has no
            // reader here would open a modal that could only say so.
            .. context.OverAReadableTicket
                ? (KeyBinding[])[new(KeyStroke.Char('t'), Command.OpenTheTicket, "the ticket")
                    { When = "when the flight names a ticket a reader here can read" }]
                : [],

            // AND THE SAME KEY WHERE THERE IS ONLY A LINK. A sweep nominates a
            // work item by its url, so the flight it opens carries no provider
            // and no id - and a flight about a page somebody can open offered
            // nothing at all until this. The browser is the console's existing
            // way out to a page; the hint says which of the two this is, so
            // neither is advertised where the other would happen.
            .. context.OverALink && !context.OverAReadableTicket
                ? (KeyBinding[])[new(KeyStroke.Char('t'), Command.OpenTheLink, "the link")
                    { When = "when the flight names a link and no ticket a reader here can read" }]
                : [],

            // THE COMMAND, THE CLIPBOARD AND THE WIRING ALL EXISTED, and three
            // reading modes already bind this key to it. A modal full of
            // somebody else's prose is exactly where a person wants to take
            // the words with them.
            new(KeyStroke.Char('c'), Command.CopyModal, "copy"),
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

        // A DOCUMENT, SO ONE KEY. What is in it is one tracker's rendering of
        // one item; there is nothing in it to decide, and the way out is the
        // way out of every other modal.
        UiMode.WorkItemDetail =>
        [
            // INSIDE A MODAL THE LETTERS ARE FREE, so this one can be the word
            // it means. `o` is taken on the runners tab and in two other modals,
            // and a modal owns the keyboard while it is up.
            // THE HISTORY'S CURSOR, untaught and off the hint line for every
            // other modal list's reason: the arrows do this through the widget,
            // so the one line of hints goes to keys with nowhere else to be
            // found.
            new(KeyStroke.Char('j'), Command.SelectNext, "down")
                { Untaught = true, OffTheHintLine = true },
            new(KeyStroke.Char('k'), Command.SelectPrevious, "up")
                { Untaught = true, OffTheHintLine = true },

            // THE SAME KEY THE FLIGHT MODAL USES, because it is the same act.
            // A second word for "show me the other half" would be a second
            // thing to learn for no gain.
            // AND THE HINT NAMES BOTH, because there are three tabs now and a
            // hint that named only the next one would leave the third
            // reachable and undiscoverable - which is the same as absent.
            new(KeyStroke.Char('v'), Command.NextWorkItemTab, "history, fields, then actions"),

            // The same key the reading modes use, and it takes whichever tab
            // is showing - see PaneText.WorkItem.
            new(KeyStroke.Char('c'), Command.CopyModal, "copy"),

            new(KeyStroke.Char('o'), Command.OpenWorkItem, "open it in a browser"),
            new(KeyStroke.Esc, Command.CloseModal, "close"),
        ],

        // A LIST WITH A CURSOR, NOT A KEY PER ANSWER. The kinds are the
        // tenant's and there may be any number of them, so a binding each would
        // be a keymap whose SHAPE depends on somebody's airspace - and the help
        // page is built by enumerating shapes. Six fixed keys over a list of any
        // length is the table's answer, and it is already how every other list
        // in this console is walked.
        // THE SAME LIST-WITH-A-CURSOR SHAPE, over values that are a tracker's.
        // A key per answer would be a keymap whose length depends on how many
        // sprints a team has run.
        UiMode.BrowseFilter =>
        [
            // THE LIST'S CURSOR, untaught and off the hint line for the reason
            // every other modal list's is: the arrows do this through the
            // widget, so the one line of hints goes to keys with nowhere else
            // to be found.
            new(KeyStroke.Char('j'), Command.SelectNext, "down")
                { Untaught = true, OffTheHintLine = true },
            new(KeyStroke.Char('k'), Command.SelectPrevious, "up")
                { Untaught = true, OffTheHintLine = true },

            // PICKING IS NOT BROWSING, and two keys say so. A browse tears this
            // session down and starts a child holding a credential, so a toggle
            // that re-queried would spawn a reader per cursor move - and a
            // person narrowing three ways would watch the screen blink three
            // times to see one answer.
            new(KeyStroke.EnterKey, Command.PickFilterValue, "pick this") { Label = "Pick" },

            // INSIDE A MODAL THE LETTERS ARE FREE, so both of these can be the
            // word they mean.
            // THE BAR, WITH THE RUNNER MODAL'S KEY. Both are a foot of tabs
            // over a table, and a person who has turned one should not have to
            // learn a second letter for the other.
            new(KeyStroke.Char('v'), Command.NextFilterView, "next criterion")
                { Label = "Next" },

            new(KeyStroke.Char('b'), Command.BrowseFiltered, "list the work again")
                { Label = "Browse" },
            new(KeyStroke.Char('x'), Command.ClearFilter, "take the filter off")
                { Label = "Clear" },

            new(KeyStroke.Esc, Command.CloseModal, "close"),
        ],

        UiMode.WorkKindChoice =>
        [
            // THE LIST'S CURSOR, untaught and off the hint line for the reason
            // the flight modal's are: the arrows do this through the list
            // widget, so the one line of hints goes to keys a person has no
            // other way to find.
            new(KeyStroke.Char('j'), Command.SelectNext, "down")
                { Untaught = true, OffTheHintLine = true },
            new(KeyStroke.Char('k'), Command.SelectPrevious, "up")
                { Untaught = true, OffTheHintLine = true },

            // ENTER ON ROW ZERO IS A DECISION, and it is not the same decision
            // as escaping. `No kind' opens a flight that inherits the floor -
            // which is what every flight before kinds existed did - where esc
            // opens nothing at all. One keypress must not be able to mean both.
            new(KeyStroke.EnterKey, Command.FlyForKind, "fly it for this") { Label = "Fly" },

            // THE SECOND HALF OF THE QUESTION. Which repositories a flight
            // names used to be a console-wide switch on another tab, which is
            // the wrong range for it - a person opening one flight against a
            // different repository had to change what every flight after it
            // would do.
            new(KeyStroke.TabKey, Command.NextWorkKindTab, "kind / repositories")
            {
                // NOT EITHER NAME, which is wrong half the time. The strip
                // above says which one is showing; what a person cannot see is
                // that there is a way to the other - the help modal's own
                // argument for this same key.
                Label = "Turn page",
            },

            // `x', NOT SPACE, AND NOT ENTER. Enter already flies, so the first
            // repository picked would open the flight. Space looks right and
            // is worse: Terminal.Gui's Tabs binds it to Activate, so whether it
            // reaches the keymap depends on which view happens to hold focus -
            // measured, it marked a row when focus was on the table and did
            // nothing at all when the registry had not loaded yet and focus was
            // on the sentence in its place. A key that works depending on what
            // else is on screen is worse than one a person has to learn.
            //
            // `x' is what the mark itself draws, and no widget claims it.
            // SPACE IS WHAT A PERSON REACHES FOR ON A LIST OF BOXES, and it
            // needed the focus fix beside this to be trustworthy: Terminal.Gui's
            // Tabs binds Space to Activate, so it reaches the keymap only when
            // the keyboard is on the table rather than on the strip. It now
            // always is - every arm of Focus() returns rather than falling
            // through to the tab behind the modal, and a view hidden under the
            // keyboard is treated as no focus at all.
            //
            // BOUND ONLY ON THE HALF WITH BOXES. On the kinds table there is
            // nothing to mark, and a key that does nothing there is worse than
            // one that is not offered.
            .. context.OnTheRepositoriesHalf
                ? (KeyBinding[])
                [
                    new(KeyStroke.Char(' '), Command.ToggleFlightRepository, "name it, or stop")
                    {
                        // SAYS WHEN, because it is not live in the plainest
                        // form of its own mode: the modal opens on the kinds,
                        // where there is nothing to mark.
                        When = ComposeRepositoriesCondition.Said,
                        Label = "Name it",
                    },

                    // AND NOT `x' BESIDE IT. That was a workaround for space
                    // being unreliable, and the focus fix removed the reason:
                    // a second key for one act is findable nowhere, which is
                    // what TheHelpPage's own guard says about a binding that
                    // is untaught AND off the hint line.
                ]
                : [],

            new(KeyStroke.Esc, Command.CloseModal, "open nothing"),
        ],

        UiMode.CredentialRepositoryChoice =>
        [
            // THE LIST'S CURSOR, on WorkKindChoice's terms: the arrows do this
            // through the list widget, so the hint line goes to keys a person
            // has no other way to find.
            new(KeyStroke.Char('j'), Command.SelectNext, "down")
                { Untaught = true, OffTheHintLine = true },
            new(KeyStroke.Char('k'), Command.SelectPrevious, "up")
                { Untaught = true, OffTheHintLine = true },

            // ENTER ENDS THE SESSION, because the secret is read with the echo
            // off and a Terminal.Gui session cannot arrange that. Esc sends
            // nothing at all, and the two must not be confusable: one asks for
            // a token and one does not.
            new(KeyStroke.EnterKey, Command.SendCredential, "send one for this")
                { Label = "Send" },

            new(KeyStroke.Esc, Command.CloseModal, "send nothing"),
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
            // THE ONE THAT COMES AND GOES, in front of the three that do not,
            // so `q quit` keeps the corner it was given. It is standing because
            // it is about the console's own line rather than about the tab -
            // the same message is on that line whichever tab is showing.
            //
            // ONLY WHILE THERE IS A REST TO READ. A key offered over a sentence
            // already on the screen in full opens a modal that says what the
            // screen says, which is the dead key Article XI names arriving as a
            // no-op rather than as silence.
            //
            // CTRL, SO IT SPENDS NO LETTER, for ctrl+f's reason below - and
            // `r` alone is reject inside the gate modal.
            // AND NOT WHILE THE SCREEN IS FROZEN, where the pixels have stopped
            // and a modal opened here is one nobody sees. The frozen sentence
            // is itself longer than a narrow terminal, so without this clause a
            // freeze on a small screen advertises a key that does nothing
            // visible - the failure Article XI names, arriving as a no-op.
            .. context.SaidIsClipped && !context.Frozen
                ? (KeyBinding[])
                [
                    new(KeyStroke.Control('r'), Command.ReadSaid, "read")
                    {
                        Standing = true,
                        When = "while the line below is showing part of its message",
                    },
                ]
                : [],

            // THE THREE THAT ARE ALWAYS TRUE, drawn at the other end of the
            // line - see KeyBinding.Standing.
            //
            // AND DECLARED IN THE ORDER THEY ARE DRAWN IN, which is the owner's
            // order and puts quit hardest against the right edge: the one that
            // ends the session is furthest from everything else, and the one
            // that moves on its own is furthest from the corner. Declared here
            // rather than sorted in the renderer, because the help page is
            // built from this order too and two orders is one that drifts.
            //
            // `g` for "get again". `r` is reject inside the gate modal and `R`
            // would be the only capital in the map, which is a shape somebody
            // has to learn rather than read.
            new(KeyStroke.Char('g'), Command.Refresh,
                context.Refresh is { Length: > 0 } says ? $"refresh {says}" : "refresh")
                { Standing = true },
            new(KeyStroke.Char('?'), Command.ToggleHelp, "help") { Standing = true },

            // THE SCREEN STOPS AND THE MOUSE GOES BACK, so a person can select
            // anything on it with their own terminal. On every tab: the live
            // pane was never the only place text appears.
            //
            // CTRL, SO IT SPENDS NO LETTER. There is one plain letter left in
            // this keymap and a view nobody can otherwise reach will want it;
            // `f` alone is already fly-this on the browse tab. Ctrl is a whole
            // keyboard nobody has spent here - and deliberately not ctrl+s,
            // ctrl+q or ctrl+z, which are flow control and a suspend: a freeze
            // key that stopped the console by stopping the PROGRAM would be the
            // joke version of this.
            //
            // OFF THE LINE, because the line is capped at seven keys and this
            // one has somewhere better to be advertised: while the screen is
            // frozen the activity line says so and names the way out, which is
            // the only moment anybody needs to be told. Found on the help page
            // the rest of the time, like `d decide` and the cursor keys.
            new(KeyStroke.Control('f'), Command.ToggleFreeze,
                    context.Frozen ? "unfreeze" : "freeze to select")
                { OffTheHintLine = true },
            new(KeyStroke.Char('q'), Command.Quit, "quit") { Standing = true },
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
            // WHICH ENTER THIS TAB HAS, OR NONE. Spread rather than a switch
            // expression, because KeyBinding is a value type and "no binding
            // here" cannot be null - and a harmless placeholder would still be
            // the answer Resolve returns and the help page advertises.
            .. Enter(context),
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
            // ENROLLMENT, WHERE THE MACHINES ARE. A conditional arm inside the
            // Normal list, decided by tab, which is the established shape -
            // `a` and `v` already do it - because Normal has no free letter
            // to give a global one and a tab-scoped binding for a letter that
            // is already global never fires.
            .. context.Showing == TabId.Runners
                ? (IReadOnlyList<KeyBinding>)
                    [new(KeyStroke.Char('t'), Command.ShowFleetTokens, "enrollment tokens")
                    {
                        When = "on the runners tab",
                    }]
                : [],
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
            // THE TWO THAT CANNOT BE CLOSED, so they show rather than toggle -
            // and punctuation rather than letters, because there are no
            // letters left. Tabs.KeyFor carries the argument; these are the
            // same two keys read out of it, and EveryTabIsOnTheBarTests
            // asserts the bar and the keymap agree about them.
            //
            // OFF THE HINT LINE like the other six: the key is on the tab.
            new(KeyStroke.Char(','), Command.ShowQueueTab, "queue")
                { OffTheHintLine = true },
            new(KeyStroke.Char('.'), Command.ShowFlightsTab, "flights")
                { OffTheHintLine = true },
            // THE THIRD OF THE PUNCTUATION FAMILY, beside the two lists it
            // completes: what needs somebody, what has run, and what has been
            // nominated. `;` is free in every mode, so it means one thing.
            new(KeyStroke.Char(';'), Command.ShowBoardTab, "board")
                { OffTheHintLine = true },

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
            // FREEZE IS THE WHOLE SCREEN'S NOW, so it is not spread by tab any
            // more - see the arm below, which binds it everywhere.
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
            .. context.Showing == TabId.Board
                ? (KeyBinding[])[
                    // CHOSEN FOR BEING FREE, AND SAID TO BE - `/`'s rule, one
                    // tab over. Every letter is a tab key or is spoken for by
                    // the compose modal, and `p` may never be a tab key; `*` is
                    // bound in no mode and reads as "all" in a list.
                    new(KeyStroke.Char('*'), Command.ShowEverybodysRows, "everybody's rows")
                        { When = "while the board is showing" },
                ]
                : [],
            .. context.Showing == TabId.Browse
                ? (KeyBinding[])[
                    new(KeyStroke.Char('f'), Command.FlyPicked, "fly this")
                        { When = "while the browse tab is showing" },

                    // CHOSEN FOR BEING FREE, AND SAID TO BE. Every letter this
                    // console binds was taken before this key was needed, and a
                    // mnemonic that silently shadows another key is worse than
                    // one picked for being unused - which `/` is, in every mode.
                    // It also says the right thing: it is what narrows a list
                    // everywhere else a person has narrowed one.
                    new(KeyStroke.Char('/'), Command.FilterBrowse, "narrow the list")
                        { When = "while the browse tab is showing" },

                    // THE PAIR. `/` picks from what the tracker offers; this
                    // says what you are after, which is the half a facet cannot
                    // express. Ctrl because every plain letter is bound, and
                    // ctrl+/ reaches the keymap on every terminal - see
                    // KeyTranslator, where the two encodings become one.
                    new(KeyStroke.Control('/'), Command.FindInBrowse, "go to or find")
                        { When = "while the browse tab is showing" },
                ]
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
        HintsHere(context) is { Length: > 0 } here
        && HintsStanding(context) is { Length: > 0 } standing
            ? here + " · " + standing
            : HintsHere(context) + HintsStanding(context);

    /// <summary>
    /// The advertised keys about what is on the screen - the left-hand end.
    /// </summary>
    /// <remarks>
    /// <b>What the tab decides, and it changes as somebody moves around.</b>
    /// Read left to right, this is the half worth reading, so it goes first
    /// and it starts in the same column whatever else is true.
    /// </remarks>
    public static string HintsHere(KeymapContext context) =>
        Line(context, standing: false);

    /// <summary>
    /// The advertised keys about the console - the right-hand end.
    /// </summary>
    /// <remarks>
    /// <b>Pinned, because they never change and are rarely read.</b> Left in
    /// the flow they sat in a different column on every tab; at the right edge
    /// they are always in the same one. Empty inside a modal, which offers none
    /// of them - so there the line is exactly the line it always was.
    /// </remarks>
    public static string HintsStanding(KeymapContext context) =>
        Line(context, standing: true);

    private static string Line(KeymapContext context, bool standing) =>
        string.Join(" · ", Bindings(context)
            .Where(b => !b.OffTheHintLine && b.Standing == standing)
            .Select(b => $"{b.Key.Name} {b.Description}"));

    /// <summary>
    /// Which columns of the hint line are the countdown's seconds, or nothing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Answered here because this is what builds the line.</b> The view
    /// paints those columns a different colour as they run down; searching the
    /// finished string for a number would find whichever <c>30s</c> came first,
    /// and the description that carries it is written four lines up.
    /// </para>
    /// <para>
    /// <b>Columns rather than a split into three strings.</b> The line is one
    /// label and stays one label - what goes over the top is three characters
    /// wide - so a caller that got two halves would have to measure one of them
    /// to find out where to put the other.
    /// </para>
    /// <para>
    /// <b>Nothing where nothing is counting.</b> A read in the air shows a mark
    /// instead of a number, and inside a modal the key is not offered at all -
    /// in both cases there are no seconds on the line to paint.
    /// </para>
    /// </remarks>
    public static (int At, int Length)? Counting(KeymapContext context)
    {
        if (context.Refresh is not { Length: > 0 } counted
            || !counted.EndsWith('s')
            || !char.IsAsciiDigit(counted[0]))
        {
            return null;
        }

        // THE WHOLE DESCRIPTION, so the seconds are located by what the line
        // says rather than by looking for digits in it. `refresh 30s` appears
        // once; `30s` on its own could be anybody's.
        //
        // AND MEASURED AGAINST THE END THAT DRAWS IT. Refresh is a standing
        // key, so its seconds are columns of the right-hand label - an offset
        // into the whole line would land somewhere in the left-hand one.
        var said = $"refresh {counted}";
        var at = HintsStanding(context).IndexOf(said, StringComparison.Ordinal);

        return at < 0 ? null : (at + said.Length - counted.Length, counted.Length);
    }

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
    /// <summary>
    /// Whether a key the keymap does not answer must still be taken.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Because handing it on is not neutral.</b> A key nobody here binds
    /// travels up to Terminal.Gui, which has meanings of its own - and escape's
    /// is to stop the runnable, which ends the session, which ends gg. Measured
    /// in a pty: the console booted, took one escape and exited with status
    /// zero, silently.
    /// </para>
    /// <para>
    /// <b>The exception, not the rule.</b> A screen that took every key it did
    /// not understand would be one no widget under it could hear from, and the
    /// tables' own arrows never reach this function at all - they are how three
    /// of these panes are walked.
    /// </para>
    /// <para>
    /// <b>Only where the keymap declined.</b> Inside a modal escape resolves to
    /// <c>CloseModal</c> and is dispatched; this answers about the console
    /// itself, where there is nothing to leave and `q' is how gg is left.
    /// </para>
    /// </remarks>
    public static bool Swallowed(KeyStroke key, KeymapContext context) =>
        key == KeyStroke.Esc
        && Resolve(key, context) is null;

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

    public static IReadOnlyList<KeyCatalogueEntry> Catalogue() => Catalogued.Value;

    /// <summary>
    /// The catalogue, built once.
    /// </summary>
    /// <remarks>
    /// <b>Three readers asked and three answers were built.</b> HelpTree, the
    /// runner modal and the help page's own renderer each call this, so the
    /// cost was paid three times for one keypress. Nothing about it can change
    /// while the console is running - it is a pure walk over the keymap, which
    /// is a pure function - so the second caller is asking a question that has
    /// already been answered.
    /// </remarks>
    private static readonly Lazy<IReadOnlyList<KeyCatalogueEntry>> Catalogued = new(Build);

    private static IReadOnlyList<KeyCatalogueEntry> Build()
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
    /// <summary>
    /// The enter this tab answers, or none at all.
    /// </summary>
    /// <remarks>
    /// <b>On the row under the cursor, and which row that is depends on which
    /// tab has the screen.</b> Two tabs list flights, one lists machines, one
    /// holds the airspace field - and the rest have nothing enter could open,
    /// so they have no enter. It used to be a fallback arm, which meant Live,
    /// Browse, Repositories and Allowances all offered "open this flight".
    /// </remarks>
    private static KeyBinding[] Enter(KeymapContext context) => context.Showing switch
    {
        // THE TAB THAT LISTS DECISIONS, so enter reaches the deciding - the
        // queue's argument, over rows that are not flights.
        //
        // AND ONLY OVER A ROW THAT IS ONE. This tab holds two kinds: a
        // nomination somebody can answer, and the watch that made it, which is
        // machinery. A row already ended is a 409 at the door. The flag is
        // derived from the same model the hint line reads, so the key is
        // offered exactly where it does something.
        // ANY ROW, BOTH KINDS. This tab holds a nomination somebody can answer
        // and the watch that made it, which is machinery - and a person needs
        // to read either. What can be ANSWERED is asked inside the modal,
        // where the reasons are; what can be OPENED is only "is there a row".
        TabId.Board => context.ABoardRowIsUnderTheCursor
            ?
            [
                new(KeyStroke.EnterKey, Command.ShowBoardRow, "open this row")
                    { OffTheHintLine = true, When = "on a board row" },
            ]
            : [],

        TabId.Runners =>
        [
            new(KeyStroke.EnterKey, Command.ShowRunner, "open this runner")
                { OffTheHintLine = true, When = "on the runners tab" },
        ],

        // AND THE WORK ITEM UNDER IT, which is the row this tab lists. It was in
        // the arm for tabs with nothing to open, which was true while the row
        // was a headline and nothing else - an id, a state and a title, chosen
        // from without ever being read.
        TabId.Browse =>
        [
            new(KeyStroke.EnterKey, Command.ShowWorkItem, "read this item")
                { OffTheHintLine = true, When = "on the browse tab" },
        ],

        // OFF THE LINE LIKE ITS SIBLINGS, once the field it focuses became a
        // box drawn at all times. It used to be the only way to learn an unset
        // airspace could be answered; the box's own title says "airspace -
        // enter to edit" now, and of two places that say one thing, the one
        // nobody is looking at is the one that goes stale.
        TabId.Envelope =>
        [
            new(KeyStroke.EnterKey, Command.FocusAirspacePath, "say where the airspace is")
                { OffTheHintLine = true, When = "on the airspace tab" },
        ],

        // THE TAB THAT IS A LIST OF THINGS WAITING ON A PERSON, so enter
        // reaches the doing. It opened the flight - the reading modal, which
        // deliberately binds nothing that acts on its flight - and the way to
        // act from there was esc, then `a`, then a key off the hint line.
        TabId.Queue =>
        [
            new(KeyStroke.EnterKey, Command.ToggleFlightActions, "what can be done")
                { OffTheHintLine = true, When = "on the queue tab" },
        ],

        // AND THE TAB THAT IS A LIST OF EVERY FLIGHT KEEPS ITS OWN, because
        // that one is for reading. One key, two meanings, and the tab decides
        // which - the shape `a` two hundred lines up already uses.
        TabId.Flights =>
        [
            new(KeyStroke.EnterKey, Command.ShowFlight, "open this flight")
                { OffTheHintLine = true, When = "on the flights tab" },
        ],

        _ => [],
    };

    /// <summary>
    /// Every shape of context that can change what one mode binds.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>ONE FLAG AT A TIME, NOT ALL OF THEM AT ONCE.</b> This was a cross
    /// product: eight tabs by sixteen booleans is 524,288 contexts for one
    /// mode, and twenty-four modes is twelve and a half million - each
    /// allocating its own array of bindings, to find 137 distinct entries. It
    /// cost ten seconds, and every flag added to the keymap doubled it, which
    /// is a bill nobody sees until somebody presses `?`.
    /// </para>
    /// <para>
    /// <b>What it has to cover, and does.</b> A binding is conditional on the
    /// mode, the tab, and one or two of these flags - so for each tab this
    /// walks the plainest shape, then that shape with each flag raised alone,
    /// then every flag raised together. Any pair of flags is then covered in
    /// all four of its combinations: neither from the plain shape, each alone
    /// from its own, and both from the last one.
    /// </para>
    /// <para>
    /// <b>And the exhaustive product is still run, in the tests.</b>
    /// <c>HelpNamesEveryKeyTests</c> crosses every flag and asserts this
    /// catalogue holds every key that product can resolve - so a binding that
    /// needed a combination this misses is a failing test rather than a key
    /// missing from the page. The proof is exhaustive; the thing a person
    /// waits for is not.
    /// </para>
    /// <para>
    /// <b>ORDER IS THE PAGE'S ORDER.</b> The plainest shape comes first - the
    /// queue tab, nothing frozen, nothing to take - so the keys that always
    /// work are listed first, and in the order they are written.
    /// </para>
    /// </remarks>
    private static IEnumerable<KeymapContext> Shapes(UiMode mode)
    {
        foreach (var showing in Enum.GetValues<TabId>())
        {
            var plain = new KeymapContext(mode, showing);

            yield return plain;

            foreach (var raised in Raised)
            {
                yield return raised(plain);
            }

            // ALL OF THEM, which is what covers a binding that needs two at
            // once - `w watch this runner` is bound over a runner that is ours
            // AND beating, and neither alone reaches it.
            var everything = plain;

            foreach (var raise in Raised)
            {
                everything = raise(everything);
            }

            yield return everything;
        }
    }

    /// <summary>
    /// Each flag the keymap dispatches on, as a way to raise just that one.
    /// </summary>
    /// <remarks>
    /// <b>Written out, because a flag left out of this is a key that resolves
    /// in the running console and appears on no page</b> - the mistake the old
    /// cross product made impossible and this one has to be told about. It is
    /// the same list <c>KeymapContext</c> declares, and
    /// <c>HelpNamesEveryKeyTests</c> counts them both.
    /// </remarks>
    private static readonly Func<KeymapContext, KeymapContext>[] Raised =
    [
        c => c with { Frozen = true },
        c => c with { Takeable = true },
        c => c with { HandedBackable = true },
        c => c with { OverADocument = true },
        c => c with { ReadingTheDocument = true },
        c => c with { OverAFold = true },
        c => c with { OnTheLookPage = true },
        c => c with { OnTheRepositoriesHalf = true },
        c => c with { OverAReadableTicket = true },
        c => c with { AGateWaits = true },
        c => c with { ABoardRowIsUnderTheCursor = true },
        c => c with { TheRowsFlightIsLoaded = true },
        c => c with { SignInStarted = true },
        c => c with { RunnerIsOurs = true },
        c => c with { RunnerOwnershipIsKnown = true },
        c => c with { RunnerIsClaimedByYou = true },
        c => c with { RunnerIsReserved = true },
        c => c with { RunnerIsTheTenants = true },
        c => c with { RunnerIsBeating = true },
        c => c with { AllowanceIsMine = true },
        c => c with { FleetAllowancesOffered = true },
        c => c with { GateAsksForAgentLogin = true },
        c => c with { GateIsABringUpAsk = true },
        c => c with { ANominationWaits = true },
        c => c with { SaidIsClipped = true },
        c => c with { OverALink = true },
    ];

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
