using System.Collections.ObjectModel;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Gg.Console.Views;

/// <summary>
/// The whole screen, built FROM an <see cref="AppState"/>.
/// </summary>
/// <remarks>
/// <para>
/// Views render the model and forward input; they are never the source of
/// truth. When the session ends, <see cref="State"/> is everything worth
/// keeping - and everything worth keeping is in it, which is the property the
/// serialization tests hold.
/// </para>
/// <para>
/// Rendering is the part that matters least. Every line of text shown here
/// comes from <see cref="PaneText"/>, which is a pure function of the model
/// and is tested without a terminal; this file is the part that cannot be.
/// Keeping the split sharp is what makes the console testable at all.
/// </para>
/// </remarks>
public sealed class ConsoleScreen : Window
{
    private readonly IApplication _app;
    private readonly ListView _queue;
    private readonly Label _flight;

    // THE MARK A WAITING TAB BREATHES. One Label over the whole screen rather
    // than one per tab: what it says does not depend on which tab is waiting,
    // and eight copies would be eight things to keep in step.
    private readonly Label _waiting;
    private readonly Label _live;
    private readonly Label _browse;
    private readonly FrameView _queuePane;
    private readonly FrameView _flightPane;
    private readonly FrameView _livePane;
    private readonly FrameView _browsePane;
    private readonly FrameView _repositoriesPane;
    private readonly Label _repositories;
    /// <summary>
    /// The airspace working copy, as a tree.
    /// </summary>
    /// <remarks>
    /// <b>THE SIXTH TABLE, AND THE LAST LIST-OF-THINGS PANE TO GET ONE.</b>
    /// This tab rendered a hand-counted ten-column role field and a
    /// free-width name into a <c>Label</c> — no header, no widths measured
    /// from the data, no cursor — while the other five went through
    /// <c>CollectionViews</c>' table factory and <c>Rows.cs</c>.
    /// </remarks>
    private readonly TableView _airspaceTable;

    /// <summary>The border round the tree, so it has an edge like its neighbour.</summary>
    /// <remarks>
    /// The document beside it is drawn inside a tab bar with a border of its
    /// own. A half with an edge next to a half without one reads as one pane
    /// and some loose text.
    /// </remarks>
    private readonly FrameView _airspaceTreePane;

    /// <summary>The three views of the selected document, beside the tree.</summary>
    /// <remarks>
    /// <b>ALONG THE BOTTOM, because the window's own tabs run across the top.</b>
    /// A second row of them up there would read as more of that bar rather than
    /// as a question about the row the cursor is on.
    /// </remarks>
    private readonly Terminal.Gui.Views.Tabs _airspaceViews;

    /// <summary>Each view's body, and the list inside it that holds the lines.</summary>
    private readonly (AirspaceView View, View Pane, ListView Said)[] _viewTabbed;

    /// <summary>Which views that bar is holding.</summary>
    /// <remarks>See <see cref="_onTheBar"/>; this one changes on an arrow key.</remarks>
    private readonly List<AirspaceView> _onTheViewBar = [];

    /// <summary>The lines the document pane is showing.</summary>
    private IReadOnlyList<string>? _airspaceSaidShowing;

    /// <summary>What the pane says when the cursor is not on a document.</summary>
    private readonly Label _airspaceNoDocument;

    /// <summary>Said instead of the tree, when there is no tree to draw.</summary>
    /// <remarks>
    /// A pane's three absences are three sentences — nothing read, nothing
    /// pulled, nowhere to pull — and none of them is a header over no rows,
    /// which is <c>Rows.cs</c>'s own rule.
    /// </remarks>
    private readonly Label _airspaceAbsent;

    /// <summary>
    /// What is being read in the modal - the rules in force, or what would
    /// change - as lines that scroll.
    /// </summary>
    /// <remarks>
    /// <b>A LIST RATHER THAN A LABEL, for the runner log's reason.</b> A
    /// composed envelope runs to a screenful and the plain modal body is a
    /// <c>Label</c> that clips: <c>UiMode.Help</c> is declared a document and
    /// gets a 92%×88% box, and its content is cut at the edge because nothing
    /// in this console scrolls a label. <c>TextView</c> is the widget that
    /// fits and 2.4.17 marks it obsolete, with warnings as errors here.
    /// </remarks>
    private readonly ListView _readingSaid;

    /// <summary>What is in the list now, so a redraw does not lose the scroll.</summary>
    private IReadOnlyList<string>? _readingSaidShowing;

    private readonly View _readingBody;

    /// <summary>
    /// Where the airspace is, and the ONE writable widget in this console.
    /// </summary>
    /// <remarks>
    /// <b>It collects; it does not write.</b> The rule beside the read-only
    /// field below still holds - a write happens between sessions with the
    /// terminal provably free - and this is what changed: the text arrives by
    /// typing, and <c>enter</c> ends the session so the shell can write it.
    /// <para>
    /// <b>Unfocusable except in its own mode.</b> A focusable field on this
    /// tab would eat every single-letter key while it held focus, which is
    /// the hazard <c>QuietTable</c> was measured into existence by. CanFocus
    /// is turned on only while <c>UiMode.AirspacePath</c> is open, so the
    /// tab's own keys cannot be swallowed by a widget nobody asked for.
    /// </para>
    /// </remarks>
    private readonly TextField _airspacePath;

    /// <summary>Where a person types an item to go to, or words to find.</summary>
    private readonly TextField _browseFind;

    private readonly FrameView _browseFindBox;

    /// <summary>The box the field sits in, so it reads as one.</summary>
    /// <remarks>
    /// <b>An unlabelled field on the last row is invisible when it is
    /// empty</b>, which is exactly the state somebody needs to see it in -
    /// a machine that has configured no airspace. A frame with a title is
    /// always something on the screen, whatever it holds.
    /// </remarks>
    private readonly FrameView _airspacePathBox;
    private readonly FrameView _envelopePane;
    private readonly FrameView _allowancesPane;
    private readonly Label _allowances;
    private readonly Label _flights;
    private readonly FrameView _flightsPane;
    private readonly Label _board;
    private readonly FrameView _boardPane;

    /// <summary>
    /// The three views that are lists of one shape of thing.
    /// </summary>
    /// <remarks>
    /// <b>A table draws what a Label was formatting.</b> Each of these panes
    /// counted characters into a format string, so a column was as wide as the
    /// widest value anybody imagined and nothing said what a column held. The
    /// rows come from <c>Rows</c>, which is pure; measuring the screen is the
    /// widget's job.
    /// </remarks>
    private readonly TableView _flightsTable;
    private readonly TableView _boardTable;

    // WHAT THE FLIGHT PANE LAST SAID, AND WHAT IT SAID IT ABOUT. Building that
    // pane walks every entry of the selected flight's story - measured at
    // 23-45ms - and Render fills every pane on every paint whatever tab is
    // showing, so a person on the board rebuilt it once a second and on every
    // click to produce text that tab does not display.
    //
    // KEYED ON WHAT PaneText.Flight ACTUALLY READS: the flight, the story, the
    // diagnosis, and whether anything is selected at all - which is Queue.Count
    // rather than Selected, because Selected is computed and would allocate a
    // row every paint just to be compared.
    private string _flightPaneSaid = string.Empty;
    private Gg.Contracts.FlightSummary? _flightPaneAbout;
    private Gg.Contracts.FlightStory? _flightPaneStory;
    private string? _flightPaneDiagnosis;
    private bool _flightPaneHadNothingSelected = true;

    // AND WHETHER IT NEEDS SAYING AGAIN. Set whenever a paint skipped the pane
    // because its tab was not showing, so coming back to the queue repaints it
    // once rather than leaving whatever was on it last.
    private bool _flightPaneWantsSaying = true;
    private readonly TableView _browseTable;
    private readonly TableView _repositoriesTable;
    private readonly FrameView _runnersPane;
    private readonly Label _runners;
    private readonly Label _runnerNotice;
    private readonly Button _runnerStart;
    private readonly TableView _runnersTable;
    private readonly Dialog _modal;
    private readonly Label _modalBody;

    /// <summary>The help modal's tabbed body. See the construction for why it is widgets now.</summary>
    private readonly View _helpBody;

    /// <summary>
    /// The filter modal's own body: a sentence, three tabs of tables, a foot.
    /// </summary>
    /// <remarks>
    /// <b>Beside <see cref="_modalBody"/> rather than instead of it</b>, which
    /// is the argument <see cref="_flightBody"/> already makes. A label with a
    /// caret in it cannot be scrolled or clicked and had to be windowed by hand
    /// to fit a screen; what a person walks here is the widget every other list
    /// in this console uses.
    /// </remarks>
    /// <summary>
    /// The work item modal's body: what it says, its scalars, its history.
    /// </summary>
    /// <remarks>
    /// <b>The flight's three regions, because it is the same three things.</b>
    /// <c>FlightDetails</c> states the rule - an identity is a heading, prose
    /// somebody wrote is a document, the scalars are fields and a history is a
    /// table - and this modal had all four drawn as one Label with a rule of
    /// dashes in the middle.
    /// </remarks>
    private readonly View _itemBody;
    private readonly FrameView _itemSaidPane;
    private readonly Markdown _itemSaid;
    private readonly View _itemFields;
    private readonly FrameView _itemHistoryPane;
    private readonly TableView _itemHistory;
    private readonly Terminal.Gui.Views.Tabs _itemTabs;
    private readonly View _itemDetailsTab;
    private readonly View _itemHistoryTab;

    /// <summary>
    /// The third tab: everything the tracker records, as a table.
    /// </summary>
    /// <remarks>
    /// <b>A table, because it is an inventory.</b> What the item SAYS is prose
    /// and what has HAPPENED to it is a log; what the tracker records about it
    /// is a list of names and values, and nothing else in this modal is.
    /// </remarks>
    private readonly View _itemFieldsTab;

    private readonly TableView _itemFieldsTable;

    private readonly Label _itemFieldsAbsent;

    /// <summary>
    /// The fourth tab: what can be done about the item, and the button.
    /// </summary>
    /// <remarks>
    /// <b>The first three say what it IS; this one acts.</b> A button rather
    /// than a key because <see cref="Keymap.Buttons"/> is all-or-none per mode
    /// and this modal's other answers are cursor keys - see
    /// <c>TheWorkItemModalFliesItTests</c> for the whole argument.
    /// </remarks>
    private readonly View _itemActionsTab;

    private readonly Label _itemActionSaid;

    private readonly Button _itemFly;
    private readonly FrameView _itemChangePane;
    private readonly ListView _itemChange;
    private readonly Label _itemHistoryAbsent;
    private IReadOnlyList<FlightField>? _itemFieldsShowing;

    /// <summary>
    /// The work kind question's body: a sentence, and a table of kinds.
    /// </summary>
    /// <remarks>
    /// <b>A table for the filter modal's reason.</b> The kinds are a tenant's
    /// own and there may be any number of them, each with a sentence beside it
    /// - which a label with a caret cannot scroll, cannot be clicked, and
    /// aligns by hand.
    /// </remarks>
    private readonly View _kindBody;
    private readonly Label _kindSentence;
    private readonly TableView _kindChoices;

    /// <summary>
    /// The compose modal's two tabs: what kind of work, and against what.
    /// </summary>
    /// <remarks>
    /// <b>Which repositories a flight names used to be a console-wide switch
    /// on another tab.</b> That is the wrong range for it — one flight against
    /// a different repository meant changing what every flight after it would
    /// do. The registry's marks are a DEFAULT now, and this is where a flight
    /// departs from it.
    /// </remarks>
    private readonly Terminal.Gui.Views.Tabs _workKindTabs;

    private readonly View _composeKindTab;

    private readonly View _composeRepoTab;

    private readonly TableView _composeRepos;

    private readonly Label _composeReposAbsent;

    /// <summary>
    /// The registry a credential is being sent for, in the kinds' shape.
    /// </summary>
    /// <remarks>
    /// A table rather than a label with a caret, for the reason above it: a
    /// list somebody drives is a widget every other list in this console
    /// already uses.
    /// </remarks>
    private readonly View _credentialRepoBody;
    private readonly Label _credentialRepoSentence;
    private readonly TableView _credentialRepoChoices;

    private readonly View _filterBody;
    private readonly Label _filterSentence;
    private readonly Terminal.Gui.Views.Tabs _filterViews;
    private readonly (BrowseFacet View, View Pane, TableView Table, Label Empty)[] _filterTabbed;
    private readonly Label _filterInForce;
    private BrowseFacet _landedFilterView;

    /// <summary>
    /// Which tab of the flight modal focus was last placed in.
    /// </summary>
    /// <remarks>
    /// <b>Beside <see cref="_landedRunnerView"/> and for its reason.</b> The
    /// modal's tab can turn while the modal keeps focus, so the keyboard has to
    /// follow — and it has to follow only when the tab TURNED, or a render once
    /// a second takes the log's cursor off whatever a person had scrolled to.
    /// </remarks>
    private FlightTab _landedFlightTab;

    /// <summary>Which tab of the compose modal focus was last placed in.</summary>
    /// <remarks>Beside <see cref="_landedFlightTab"/> and for its reason.</remarks>
    private WorkKindTab _landedWorkKindTab;

    /// <summary>
    /// Which tab of the work item modal focus was last placed in.
    /// </summary>
    /// <remarks>The pair above, one modal over.</remarks>
    private WorkItemTab _landedWorkItemTab;

    private readonly Terminal.Gui.Views.Tabs _helpTabs;

    private readonly View _helpKeysTab;

    private readonly View _helpEnvironmentTab;

    private readonly TreeView<HelpNode> _helpKeys;

    /// <summary>
    /// The Environment page. A list of lines, because a Label does not scroll.
    /// </summary>
    /// <remarks>
    /// <b>The shape the three other long documents already use.</b> The reading
    /// pane, the runner's log and the airspace document are all a ListView of
    /// lines wrapped to the viewport; a Label draws what fits in the box and
    /// drops the rest with no mark, which on a machine with a dozen variables
    /// is most of the page. Reported as "the environment textbox is not
    /// scrollable".
    /// </remarks>
    private readonly ListView _helpEnvironment;

    /// <summary>What that page is showing, so a render a second does not refill it.</summary>
    private IReadOnlyList<string>? _helpEnvironmentShowing;

    /// <summary>The key groups, built once. See RenderHelp for why not every render.</summary>
    private List<HelpNode>? _helpGroups;

    private readonly View _helpDoctorTab;

    /// <summary>The Doctor page, a list for the reason its neighbour is one.</summary>
    private readonly ListView _helpDoctor;

    /// <summary>The Look page's tab, its table of settings, and the pane under it.</summary>
    /// <remarks>
    /// <b>A SPIKE.</b> A table above and a sentence below is the shape the
    /// flight log and the work item's history already have, and for the reason
    /// they have it: a value belongs in a cell and the prose that explains it
    /// does not fit in one.
    /// </remarks>
    private readonly View _helpLookTab;

    private readonly TableView _helpLook;

    private readonly FrameView _helpLookAboutPane;

    private readonly Label _helpLookAbout;

    /// <summary>What that page is showing. Same guard, same reason.</summary>
    private IReadOnlyList<string>? _helpDoctorShowing;

    /// <summary>The modal's buttons, rebuilt whenever what it asks changes.</summary>
    /// <remarks>
    /// <b>Rebuilt rather than hidden.</b> Which answers exist depends on the
    /// mode AND the model - the sign-in modal offers different keys once a code
    /// is showing - so a fixed set toggled visible would be a second thing to
    /// keep in step with <see cref="Keymap.Buttons"/>.
    /// </remarks>
    private readonly List<Button> _modalButtons = [];

    /// <summary>
    /// The flight modal's own body: a document, a form and a table.
    /// </summary>
    /// <remarks>
    /// <b>Beside <see cref="_modalBody"/> rather than instead of it.</b> Every
    /// other modal is a few lines and two keys, which is what a label is for.
    /// This one is a flight - an intent somebody wrote, eleven scalars and a
    /// history - and each of those three wants a different widget. Exactly one
    /// of the two bodies is visible at a time; <c>PaneText.ModalIsADocument</c>
    /// already sizes the frame and this decides what is in it.
    /// </remarks>
    private readonly View _flightBody;
    private readonly Terminal.Gui.Views.Tabs _flightTabs;
    private readonly View _flightDetailsTab;
    private readonly View _flightGateTab;
    private readonly View _flightLogTab;
    private readonly View _flightFactsTab;
    private readonly Label _flightFacts;
    private readonly FrameView _flightLogDetailPane;
    private readonly ListView _flightLogDetail;
    private readonly Label _flightGate;
    private readonly FrameView _flightIntentPane;
    private readonly Markdown _flightIntent;
    private readonly View _flightFields;
    private readonly FrameView _flightLogPane;
    private readonly TableView _flightLog;
    private readonly Label _flightLogAbsent;
    private readonly View _runnerBody;
    private readonly View _runnerFields;
    private readonly View _runnerLogPane;
    private readonly ListView _runnerSaid;
    private readonly Terminal.Gui.Views.Tabs _runnerViews;
    private readonly (RunnerView View, View Pane)[] _runnerViewTabbed;
    private readonly TableView _runnerEnvironments;
    private readonly TableView _runnerMembers;
    private readonly Label _runnerNothingHere;
    private readonly Label _runnerLogAbsent;
    private IReadOnlyList<FlightField>? _runnerFieldsShowing;
    private IReadOnlyList<string>? _runnerSaidShowing;

    /// <summary>
    /// Which flight's log the table is currently holding, and how many rows.
    /// </summary>
    /// <remarks>
    /// <b>So a render does not snap the cursor back to the top under somebody
    /// reading.</b> Filling a <c>TableView</c> replaces its source, which
    /// resets the selection - harmless for the tabs, whose cursors are in the
    /// model, and not harmless here: the log's cursor is the person's place in
    /// a history and is deliberately not kept anywhere, because a modal is a
    /// question with an answer and a way out. The story cannot change while the
    /// modal is open - a UI session makes no network call - so the flight it is
    /// about and the number of entries settle whether a refill is needed.
    /// </remarks>
    private (string Flight, int Rows, int Entry, int Width)? _logShowing;

    /// <summary>
    /// The fields the column is currently built out of, or null before it is.
    /// </summary>
    /// <remarks>
    /// <b>The same argument as <see cref="_logShowing"/>, and a sharper reason.</b>
    /// A column of labels and read-only fields is built rather than assigned
    /// into, because a flight waiting on three people has three rows more than
    /// one waiting on nobody - and <c>RemoveAll</c> hands the caller the
    /// lifetime of what it removed. Rebuilding on every render would drop two
    /// undisposed views per field per tick of the live tail's timer. The fields
    /// are records, so whether they changed is a comparison.
    /// </remarks>
    private IReadOnlyList<FlightField>? _fieldsShowing;

    /// <summary>
    /// The dimmer scheme, computed once. The field captions are rebuilt on
    /// every render, and asking the theme for it each time would be a palette
    /// mixed per label per frame.
    /// </summary>
    private readonly Terminal.Gui.Drawing.Scheme _muted = ConsoleTheme.Muted();
    private readonly Label _hints;

    /// <summary>Which gg this is, in the corner, dim.</summary>
    private readonly Label _version;

    /// <summary>The keys about the console, at the line's right-hand end.</summary>
    private readonly Label _hintsStanding;

    /// <summary>The countdown's seconds, painted over the hint line as they fade.</summary>
    private readonly Label _hintsCounting;
    private readonly Label _activity;

    /// <summary>
    /// The notifications, in the corner over the tab strip.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Drawn from the model and nothing else</b>, like every other region:
    /// what it says is <c>PaneText</c>'s, which keys reach it is the keymap's,
    /// and whether it holds the keyboard is <see cref="UiMode.Notifications"/>.
    /// </para>
    /// <para>
    /// <b>It never has focus it was not asked for.</b> Unfocused it cannot be
    /// focused at all, so no stray tab lands in it; a notification that took the
    /// keyboard when it arrived would swallow whatever key somebody was halfway
    /// through pressing.
    /// </para>
    /// </remarks>
    private readonly View _notifications;

    private readonly Label _notificationText;

    /// <summary>The corner's two keys, while it is not holding the keyboard.</summary>
    private readonly Label _notificationHint;

    /// <summary>
    /// A button per answer while it is, rebuilt only when the answers change -
    /// <see cref="_modalButtons"/>' rule, for its reason.
    /// </summary>
    private readonly List<Button> _notificationButtons = [];

    private readonly Expectations? _expectations;

    /// <summary>How wide the corner is: a flight number, a name, and four buttons.</summary>
    private const int NotificationsWidth = 50;

    /// <summary>
    /// The bar, and the one view under it.
    /// </summary>
    /// <remarks>
    /// <b>Terminal.Gui's own component rather than a string in the title.</b>
    /// The first version composed the bar into the window's <c>Title</c>, which
    /// cannot be selected, scrolled or clicked - so a person could see the tabs
    /// and reach them only by key. What goes on it comes from
    /// <c>Tabs.Title</c>; which one shows comes from the model. This holds
    /// neither.
    /// </remarks>
    private readonly Terminal.Gui.Views.Tabs _bar;

    /// <summary>Each tab's body, in the order the bar shows them.</summary>
    private readonly (TabId Tab, View Pane)[] _tabbed;

    /// <summary>Which tabs the bar is actually holding, in its own order.</summary>
    /// <remarks>
    /// <b>RECORDED, BECAUSE THE MODEL IS WHAT DISAGREED WITH IT.</b> The bar
    /// used to be built once and then asked about every frame through
    /// <c>Tabs.Offered</c>, which is the model's answer rather than the
    /// widget's. When the two parted - and they part the moment a tab becomes
    /// offered mid-session - selecting the pane the model named threw, because
    /// the bar had never received it. This is the widget's answer, maintained
    /// by the one method that adds and removes.
    /// </remarks>
    private readonly List<TabId> _onTheBar = [];

    /// <summary>
    /// True while the view is syncing the bar to the model.
    /// </summary>
    /// <remarks>
    /// Assigning <c>Tabs.Value</c> raises <c>ValueChanged</c>, which is also
    /// how a person's click arrives - so without this the render after a click
    /// answers its own event, and a tab that costs a read would ask for one on
    /// every frame.
    /// </remarks>
    private bool _syncing;

    public AppState State { get; private set; }

    public Command ExitCommand { get; private set; } = Command.Quit;

    private readonly LiveTails? _tails;
    private readonly IRunnerLog? _runnerLog;
    private readonly AutoRefresh? _refresh;
    private readonly BackgroundReads? _reads;

    /// <summary>
    /// Whether the sign-in this console started has been approved.
    /// </summary>
    /// <remarks>
    /// <b>A question, not a session.</b> The screen has no business holding the
    /// thing that owns a device code; all it needs is whether the answer is in,
    /// and the composition root is the one place that holds both ends.
    /// </remarks>
    private readonly Func<bool>? _signInLanded;

    /// <summary>
    /// The tab focus was last placed on, or null when it has not been placed.
    /// </summary>
    /// <remarks>
    /// Null is also how a modal holding the focus is recorded, so that closing
    /// one counts as a change again - the tab did not move while it was open,
    /// and a decision keyed only on that would leave focus on a modal no longer
    /// on the screen.
    /// </remarks>
    private TabId? _landed;

    /// <summary>
    /// Which of the runner modal's views focus was last placed in.
    /// </summary>
    /// <remarks>
    /// <b>Beside <see cref="_landedReading"/> and for its reason.</b> Focus is
    /// moved when the view TURNED rather than whenever the model and the widget
    /// differ — the second re-places it once a second and drags it out of
    /// whatever a person had just clicked into.
    /// </remarks>
    private RunnerView _landedRunnerView;

    /// <summary>Which half of the airspace tab focus was last placed on.</summary>
    /// <remarks>
    /// <b>Beside <see cref="_landed"/> and for its reason.</b> Focus is moved
    /// when the ANSWER changes, not while the answer stands - a decision
    /// re-asserted once a second drags the keyboard out of whichever half
    /// somebody just clicked into, and puts a cursor back to the top of a
    /// document they had scrolled.
    /// </remarks>
    private bool _landedReading;

    /// <summary>How often the pane looks, when somebody is watching.</summary>
    /// <remarks>
    /// <b>Four times a second is a person's idea of "as it happens" and a
    /// laptop's idea of nothing.</b> Most flights write nothing most of the
    /// time - the walk measured 37 lines in 51 seconds - so a poll per frame
    /// would be a fan spinning for an empty file. The timer stops when the pane
    /// is detached, which is also how somebody who does not want it makes it
    /// stop.
    /// </remarks>
    private static readonly TimeSpan LookEvery = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// How somebody has been pressing against the edge of a table, and which
    /// table it is.
    /// </summary>
    /// <remarks>
    /// <b>Held in the view rather than on the model, and that is the point.</b>
    /// Which row a person is on is the model's; whether their finger is still
    /// down on an arrow key is not. These reset when the console hands the
    /// terminal to an editor and rebuilds from <c>AppState</c> - which is right,
    /// because nobody was holding a key while they wrote a commit message.
    /// <see cref="TableEdge"/> holds the decision itself and is pure.
    /// </remarks>
    private readonly Func<AppState?>? _booted;

    private EdgePresses _edge = EdgePresses.None;
    private TableView? _edgeAt;
    private Terminal.Gui.Drawing.Scheme? _edgeWas;
    private bool _edgeBlinking;

    public ConsoleScreen(
        IApplication app,
        AppState state,
        LiveTails? tails = null,
        IRunnerLog? runnerLog = null,
        AutoRefresh? refresh = null,
        Func<bool>? signInLanded = null,
        // A READ A KEYPRESS ASKED FOR, folded on the tick beside the one the
        // timer asks for. Last and defaulted, because every existing caller
        // passes positionally.
        BackgroundReads? reads = null,

        // WHAT THE BOOT BROUGHT, ONCE IT HAS. Asked on a tick the way the
        // sign-in's approval is, and for the same reason: it is happening on a
        // task the composition root owns, and a session may fold an answer that
        // arrives from somewhere owned outside it. Null until it lands, and it
        // answers once.
        Func<AppState?>? booted = null,

        // WHAT THE CONSOLE'S WRITES SAID THEY DID, looked for on a tick of its
        // own. Last and defaulted, for reads' reason.
        Expectations? expectations = null)
    {
        _app = app;
        _expectations = expectations;
        _tails = tails;
        _runnerLog = runnerLog;
        _refresh = refresh;
        _reads = reads;
        _signInLanded = signInLanded;
        _booted = booted;
        State = state;
        Title = PaneText.WindowTitle(state);

        _queuePane = new FrameView
        {
            Title = "queue",
            X = 0,
            Y = 0,
            Width = Dim.Percent(38),
            Height = Dim.Fill(1),
        };
        _queue = CollectionViews.List();
        _queuePane.Add(_queue);

        _flightPane = new FrameView
        {
            Title = "flight",
            X = Pos.Right(_queuePane),
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(1),
        };
        _flight = new Label { Width = Dim.Fill(), Height = Dim.Fill(), CanFocus = true };
        _flightPane.Add(_flight);

        // CENTRED, AND NOT FOCUSABLE. It is a thing to look at while waiting,
        // never a thing to land on - a stop in the tab order over a pane that
        // is about to fill would move somebody's cursor for them.
        _waiting = new Label
        {
            X = Pos.Center(),
            Y = Pos.Center(),
            CanFocus = false,
            Visible = false,
        };

        _livePane = new FrameView
        {
            Title = "live",
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(1),
            Visible = false,
        };
        // A Label, not a TextView. TextView is obsolete in this Terminal.Gui
        // and obsolete warnings are errors here - which turned out to be a
        // better answer than the one it blocked. Copying out of a TUI is the
        // TERMINAL's own selection, and what defeats it is the application
        // repainting underneath. Freeze stops the repaint, so the terminal's
        // selection works, and no widget has to reimplement selection at all.
        _live = new Label { Width = Dim.Fill(), Height = Dim.Fill(), CanFocus = true };
        _livePane.Add(_live);

        // THE SAME REGION AS EVIDENCE AND LIVE, and never on at the same time.
        // Three panes over one region is why BrowseToggled turns the other two
        // off rather than trusting the order these are added in.
        _browsePane = new FrameView
        {
            Title = "browse",
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(1),
            Visible = false,
        };
        _browse = new Label { Width = Dim.Fill(), Height = Dim.Fill(), CanFocus = true };
        _browsePane.Add(_browse);

        // THE FOURTH OCCUPANT OF THAT ONE REGION.
        _envelopePane = new FrameView
        {
            Title = "envelope",
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(1),
            Visible = false,
        };
        // THE TREE ON THE LEFT AND THE DOCUMENT ON THE RIGHT, which is why
        // the reading modal went away: the cursor picks the subject and the
        // pane answers about it, so comparing one row against the next is an
        // arrow key rather than two keypresses and a memory of the last one.
        //
        // THREE ROWS SHORTER, which is what the box below it takes: two for
        // its border and one for the line inside.
        _airspaceTreePane = new FrameView
        {
            X = 0,
            Y = 0,
            Width = Dim.Percent(42),
            Height = Dim.Fill(3),
        };

        _airspaceTable = CollectionViews.Table();
        _airspaceTreePane.Add(_airspaceTable);
        _airspaceAbsent = new Label
        {
            Width = Dim.Fill(),
            Height = Dim.Fill(3),
            CanFocus = true,
        };

        _airspaceViews = new Terminal.Gui.Views.Tabs
        {
            X = Pos.Right(_airspaceTreePane),
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(3),
            TabSide = Side.Bottom,

            // NOT IN THE TAB RING. `tab' is the keymap's on this screen and
            // moves between the WINDOW's tabs, so a widget claiming a stop
            // would give that key a second meaning on one screen - which is
            // the airspace field's reason, one widget over. `v' turns this bar,
            // and a click on a header turns it too.
            TabStop = TabBehavior.NoStop,
        };

        _viewTabbed =
        [
            .. Enum.GetValues<AirspaceView>().Select(view =>
            {
                // A LIST RATHER THAN A LABEL, because nothing in this console
                // scrolls a Label and a composed envelope is longer than any
                // box. The same pattern the runner log and the reading modal
                // use: a pure producer, wrapped to the width, set only when
                // the lines actually change.
                var said = CollectionViews.List();
                said.ViewportChanged += OnAirspaceDocumentResized;

                var pane = new View
                {
                    Width = Dim.Fill(),
                    Height = Dim.Fill(),
                    CanFocus = true,
                    TabStop = TabBehavior.NoStop,
                };

                pane.Add(said);

                return (View: view, Pane: (View)pane, Said: said);
            }),
        ];

        // WHEN THE CURSOR IS ON A FOLDER, which has no document and so has no
        // views. A bar with no tabs would be a frame around nothing; this says
        // which of the nothings it is, from the same producer the lists use.
        _airspaceNoDocument = new Label
        {
            X = Pos.Right(_airspaceTreePane),
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(3),
        };

        // ALWAYS ON THE SCREEN WHILE THE TAB IS, and bordered so it is on it
        // visibly. The path is the question every other key on this tab depends
        // on, so it is not something to go and find - and an empty unlabelled
        // field is nothing at all to look at, in exactly the state that needs
        // looking at.
        _airspacePathBox = new FrameView
        {
            Title = "airspace",
            X = 0,
            Y = Pos.AnchorEnd(3),
            Width = Dim.Fill(),
            Height = 3,
        };

        _airspacePath = new TextField
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),

            // FOCUSABLE ALWAYS, AND INERT UNTIL ENTER. Two stages, because
            // focus and editing are different things: a person arriving on the
            // tab gets the cursor in the box - which is what makes the path
            // selectable and copyable, the same reason the read-only fields in
            // the flight modal are focusable - and the keystrokes stay the
            // tab's until enter says otherwise.
            CanFocus = true,
            ReadOnly = true,

            // NOT IN THE TAB RING. `tab` is the keymap's here and moves between
            // TABS, so a widget claiming a stop would give that key a second
            // meaning on one screen.
            TabStop = TabBehavior.NoStop,
        };

        _airspacePathBox.Add(_airspacePath);

        // ON THE FIELD, NOT ON THE SCREEN. Whether a focused TextField lets
        // enter and esc bubble to the window is a Terminal.Gui behaviour this
        // console has been wrong about before - enter arriving as KeyCode 13
        // matched no binding at all until KeyTranslator was given an arm for
        // it. So these two are intercepted where they certainly arrive,
        // which is the same thing the runners table and its button do.
        _airspacePath.KeyDown += OnAirspacePathKeyDown;

        _airspaceViews.ValueChanged += OnAirspaceViewChanged;

        _envelopePane.Add(_airspaceTreePane);
        _envelopePane.Add(_airspaceAbsent);
        _envelopePane.Add(_airspaceViews);
        _envelopePane.Add(_airspaceNoDocument);
        _envelopePane.Add(_airspacePathBox);

        // AND THE SIXTH, which shares the same region as the four above it.
        _allowancesPane = new FrameView
        {
            Title = "allowances",
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(1),
            Visible = false,
        };
        _allowances = new Label { Width = Dim.Fill(), Height = Dim.Fill(), CanFocus = true };
        _allowancesPane.Add(_allowances);

        // THE SAME REGION AGAIN. Four panes now share it and never two at
        // once, which RepositoriesToggled enforces rather than the order these
        // are added in.
        _repositoriesPane = new FrameView
        {
            Title = "repositories",
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(1),
            Visible = false,
        };
        _repositories = new Label { Width = Dim.Fill(), Height = Dim.Fill(), CanFocus = true };
        _repositoriesPane.Add(_repositories);

        // THE FLEET, AND THIS MACHINE'S RUNNER FIRST. Already in the model from
        // the boot, so this tab is never waiting on a read.
        _runnersPane = new FrameView
        {
            Title = "runners",
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(1),
            Visible = false,
        };
        _runners = new Label { Width = Dim.Fill(), Height = Dim.Fill(), CanFocus = true };

        // THE NOTICE AND THE BUTTON, above the table. `gg runner up' is a
        // command a person cannot type while this console owns the terminal it
        // would be typed into, so the remedy has to be something on this screen.
        _runnerNotice = new Label { X = 0, Y = 0, Width = Dim.Fill(), Visible = false };
        _runnerStart = new Button
        {
            X = 0,
            Y = 1,
            Text = "Start a runner here",
            Visible = false,

            // NO HOTKEY OF ITS OWN. A Button takes a letter out of its own
            // caption, and Keymap is the only place a printable key means
            // anything in this console - the same rule CollectionViews holds for
            // the tables' type-to-search.
            HotKeySpecifier = new System.Text.Rune('\uffff'),
        };
        _runnerStart.Accepting += OnStartRunner;
        _runnerStart.KeyDown += OnButtonKeyDown;
        _runnersPane.Add(_runners, _runnerNotice, _runnerStart);

        // EVERY FLIGHT, NEEDED OR NOT. The queue is what needs a person and is
        // right to be; this is the tab that answers "where did the thing I just
        // started go". Open from the start, like the queue.
        _flightsPane = new FrameView
        {
            Title = "flights",
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(1),
            Visible = false,
        };
        _flights = new Label { Width = Dim.Fill(), Height = Dim.Fill(), CanFocus = true };
        _flightsTable = CollectionViews.Table();
        _flightsPane.Add(_flights, _flightsTable);

        // THE BOARD: nominations and the watches that make them, one table and
        // one cursor. Built here with the flights pane because it is the same
        // shape - a table over a label that speaks when there are no rows.
        _boardPane = new FrameView
        {
            Title = "board",
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(1),
            Visible = false,
        };
        _board = new Label { Width = Dim.Fill(), Height = Dim.Fill(), CanFocus = true };
        _boardTable = CollectionViews.Table();
        _boardPane.Add(_board, _boardTable);
        _browseTable = CollectionViews.Table();
        _browsePane.Add(_browseTable);

        // THE AIRSPACE FIELD'S SHAPE, one tab over, and for its reasons: a box
        // at the bottom that is focusable throughout and writable only while
        // the question is open, with the title carrying what the field alone
        // cannot say.
        _browseFindBox = new FrameView
        {
            Title = "go to or find",
            X = 0,
            Y = Pos.AnchorEnd(3),
            Width = Dim.Fill(),
            Height = 3,
            Visible = false,
        };

        _browseFind = new TextField
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            CanFocus = true,
            ReadOnly = true,
            TabStop = TabBehavior.NoStop,
        };

        _browseFindBox.Add(_browseFind);
        _browsePane.Add(_browseFindBox);
        _browseFind.KeyDown += OnBrowseFindKeyDown;
        _repositoriesTable = CollectionViews.Table();
        _repositoriesPane.Add(_repositoriesTable);
        _runnersTable = CollectionViews.Table();
        _runnersPane.Add(_runnersTable);

        // WHAT IS OVER RECEDES, AND ITS ENDING KEEPS ITS COLOUR. Only this
        // table: the rows are flights, and only flights have endings. Set once
        // at construction because the getters read the table they are handed,
        // so there is nothing to reassert when the rows change.
        LookStyles.FlightStates(
            _flightsTable, Rows.FlightColumns.ToList().IndexOf("state"));

        // AND THE BOARD, on the same rule with one difference: what recedes
        // there is a nomination that ended, never a watch - a watch is the live
        // thing on that tab.
        LookStyles.BoardStates(
            _boardTable, Rows.BoardColumns.ToList().IndexOf("state"));

        _flightsTable.ValueChanged += OnRowPointedAt;
        _boardTable.ValueChanged += OnRowPointedAt;
        _browseTable.ValueChanged += OnRowPointedAt;
        _repositoriesTable.ValueChanged += OnRowPointedAt;
        _runnersTable.ValueChanged += OnRowPointedAt;
        _airspaceTable.ValueChanged += OnRowPointedAt;
        // AND THE FLEET, on the flights tab's rule: a machine that will take no
        // work recedes, and its state says whether it is gone or withheld.
        LookStyles.RunnerStates(
            _runnersTable, Rows.RunnerColumns.ToList().IndexOf("state"));

        _runnersTable.KeyDown += OnTableKeyDown;

        // AND EVERY TAB'S TABLE HOLDS AT ITS OWN EDGES. Attached AFTER the
        // fleet's handler above, because that one answers up-from-row-zero by
        // focusing the button over the table - a deliberate move inside one
        // pane, and OnTableEdge stands down for a key already handled.
        //
        // The six tab tables and not the modals': what this stops is a key
        // falling through to the tab bar, and a modal has no bar under it.
        foreach (var table in (TableView[])
                 [_flightsTable, _boardTable, _browseTable, _repositoriesTable,
                  _runnersTable, _airspaceTable])
        {
            table.KeyDown += OnTableEdge;
        }

        _hints = new Label { X = 0, Y = Pos.AnchorEnd(1), Width = Dim.Fill() };

        // THE SECONDS, OVER THE TOP OF THE LINE THEY ARE ALREADY ON. Three
        // characters painted on the same row rather than the line split into
        // pieces: a split would make every render lay out three views whose
        // widths depend on somebody else's text, and the failure mode of
        // getting that wrong is a truncated hint line. The failure mode of
        // getting THIS wrong is the ordinary line showing through, which is
        // what it says anyway.
        //
        // Added after _hints and before _modal, because that is the order
        // these are drawn in.
        // THE OTHER END OF THE LINE, pinned to the right edge. Auto-width
        // rather than Dim.Fill with the text aligned: a full-width label draws
        // its own background across the row and would wipe the end it shares
        // the line with. Drawn after the left one, so where a long line would
        // have run under it, what is lost is the tail of the context keys and
        // `q quit' stays on the screen.
        _hintsStanding = new Label
        {
            X = Pos.AnchorEnd(),
            Y = Pos.AnchorEnd(1),
            Width = Dim.Auto(DimAutoStyle.Text),
        };

        // AND THE SECONDS GO INSIDE IT, because that is the end that draws
        // them - Keymap.Counting answers columns of this label's text, so a
        // child of it needs no arithmetic about where the label itself is.
        _hintsCounting = new Label { Visible = false };
        _hintsStanding.Add(_hintsCounting);

        // WHICH GG THIS IS, in the top right. The hint line's own shape the
        // other way up: anchored to the end with an auto width, so it draws its
        // own characters and not a full-width background across the row it
        // shares - which is the mistake that end of the hint line documents.
        //
        // Muted rather than a grey somebody picked. That scheme computes a
        // foreground halfway to the background and COPIES the background
        // instead of naming one, so the badge stays quiet and stays legible on
        // whatever theme is in force.
        _version = new Label
        {
            // ONE COLUMN IN FROM THE EDGE, because the badge sits on the
            // frame's own row and anchoring flush to the end paints over the
            // corner - the box stops closing, which reads as a broken frame
            // rather than as a label on one.
            // Pos.AnchorEnd() anchors the RIGHT edge to the end; the
            // int overload moves the LEFT edge that far from it, which for an
            // auto-width label is the whole badge clipped to nothing. Measured
            // by trying it.
            X = Pos.AnchorEnd() - 1,
            Y = 0,
            Width = Dim.Auto(DimAutoStyle.Text),
            Text = Corner.Badge(Gg.Client.GgVersions.Binary, Corner.UpdateWaiting(state)),
        };
        _version.SetScheme(ConsoleTheme.Stamp());

        // ABOVE THE HINTS, on a line of its own. A write a person cannot see is
        // indistinguishable from a key that does nothing.
        _activity = new Label { X = 0, Y = Pos.AnchorEnd(2), Width = Dim.Fill() };

        // AND HOW WIDE IT CAME OUT, back into the model. A Label draws what
        // fits and drops the rest, so whether the whole message is on the
        // screen is the text against THIS number - and the keymap, which
        // advertises the key that opens the rest, can see a model and never a
        // view.
        _activity.ViewportChanged += OnSaidResized;

        // A DIALOG FOR THE BUTTON ROW AND THE SHADOW, and NOT run as one.
        // Dialog derives from Runnable and its own shape is run-me-and-take-a-
        // Result, which would put the answer inside Terminal.Gui: where Keymap
        // cannot see it, the generated key walk cannot prove the escape hatch,
        // and AppState.Mode would stop describing the screen. So it is a child
        // view whose visibility comes from the model, exactly as the frame's
        // did, and its buttons send the same commands the keys send.
        _modal = new Dialog
        {
            X = Pos.Center(),
            Y = Pos.Center(),
            Visible = false,
        };
        _modalBody = new Label { Width = Dim.Fill(), Height = Dim.Fill(), CanFocus = true };

        // THE HELP MODAL'S OWN BODY: a real tab bar, and a real tree under the
        // Keys page. What changed to make this possible is the model, not the
        // library version - HelpPage and HelpFolds hold which page is showing
        // and what is folded, and these follow them the way _bar follows
        // ActiveTab, behind the same sync flag.
        //
        // The text bar this replaced was defended on the grounds that a widget
        // "would put which page is showing inside a widget, where no test can
        // assert it". True of a widget that OWNS the answer. These do not.
        _helpTabs = CollectionViews.Bar();

        _helpKeys = CollectionViews.Tree<HelpNode>();
        _helpEnvironment = CollectionViews.Document();
        _helpEnvironment.ViewportChanged += OnHelpPageResized;

        _helpKeysTab = new View
        {
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            Title = "keys",
            CanFocus = true,
        };
        _helpKeysTab.Add(_helpKeys);

        _helpEnvironmentTab = new View
        {
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            Title = "environment",
            CanFocus = true,
        };
        _helpEnvironmentTab.Add(_helpEnvironment);

        _helpDoctor = CollectionViews.Document();
        _helpDoctor.ViewportChanged += OnHelpPageResized;
        _helpDoctorTab = new View
        {
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            Title = "doctor",
            CanFocus = true,
        };
        _helpDoctorTab.Add(_helpDoctor);

        // THE LOOK PAGE: a table of what can be changed, and a sentence about
        // whichever row the cursor is on. The table answers its own arrows, so
        // the cursor is the widget's and the model follows it - the pattern
        // every other table in this console uses.
        _helpLook = CollectionViews.Table();
        _helpLook.ValueChanged += OnModalRowPointedAt;
        _helpLook.KeyDown += OnModalKeyDown;

        _helpLookAboutPane = new FrameView
        {
            Title = "what it does",
            X = 0,
            Y = Pos.AnchorEnd(5),
            Width = Dim.Fill(),
            Height = 5,
            TabStop = TabBehavior.TabStop,
        };

        _helpLookAbout = new Label
        {
            Width = Dim.Fill(),
            Height = Dim.Fill(),
        };
        _helpLookAboutPane.Add(_helpLookAbout);

        _helpLookTab = new View
        {
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            Title = "look",
            CanFocus = true,
        };
        _helpLookTab.Add(_helpLook, _helpLookAboutPane);

        _helpTabs.Add(_helpKeysTab);
        _helpTabs.Add(_helpEnvironmentTab);
        _helpTabs.Add(_helpDoctorTab);
        _helpTabs.Add(_helpLookTab);
        _helpTabs.ValueChanged += OnHelpPageChanged;

        // CanFocus, WHICH A PLAIN View IS NOT. Nothing inside a view that
        // cannot take focus can take it either, so the tree never got the
        // keyboard and its arrows moved nothing - and SetFocus on a view that
        // cannot take it does nothing at all, silently, which is why this
        // looked like an arrow-key problem rather than a container one.
        _helpBody = new View
        {
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            Visible = false,
            CanFocus = true,
        };
        _helpBody.Add(_helpTabs);

        // THE FILTER'S THREE TABLES, one per criterion. Each pane is CanFocus
        // for the reason the help body is: nothing inside a view that cannot
        // take focus can take it either, and SetFocus on such a view does
        // nothing at all - silently, which reads as an arrow-key problem.
        _filterTabbed =
        [
            .. FilterViews.All.Select(view =>
            {
                var table = CollectionViews.Table();

                // AN EMPTY TAB IS AN ANSWER, and it needs the sentence that
                // says which answer: a project that files nothing by sprint is
                // not a reader that failed.
                var empty = new Label
                {
                    X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill(),
                    Text = $"The tracker offered no {FilterViews.Title(view)}.",
                };

                var pane = new View
                {
                    Width = Dim.Fill(),
                    Height = Dim.Fill(),
                    CanFocus = true,
                    TabStop = TabBehavior.NoStop,
                };

                pane.Add(table, empty);

                return (View: view, Pane: (View)pane, Table: table, Empty: empty);
            }),
        ];

        _filterSentence = new Label { X = 0, Y = 0, Width = Dim.Fill(), Height = 2 };

        // THE FOOT SAYS ALL THREE AT ONCE, because the tabs show one. A modal
        // that named what was picked only on the tab it was picked in is a
        // person turning the bar to remember what they chose - and what `b'
        // will ask for is the combination, not the tab.
        _filterInForce = new Label { X = 0, Y = Pos.AnchorEnd(1), Width = Dim.Fill() };

        _filterViews = new Terminal.Gui.Views.Tabs
        {
            X = 0,
            Y = Pos.Bottom(_filterSentence),
            Width = Dim.Fill(),
            Height = Dim.Fill(1),
            TabSide = Side.Bottom,

            // NOT IN THE TAB RING, the runner bar's reason: `tab' is the
            // keymap's on this screen, so a widget claiming a stop would give
            // that key a second meaning. `v' turns this bar, and a click on a
            // header turns it too.
            TabStop = TabBehavior.NoStop,
        };

        foreach (var (view, pane, table, _) in _filterTabbed)
        {
            pane.Title = FilterViews.Title(view);
            _filterViews.Add(pane);

            table.ValueChanged += OnModalRowPointedAt;

            // AND THE KEYS, BECAUSE THE TABLE IS WHAT HAS THE KEYBOARD. A key
            // reaches the focused view first and a TableView means things by
            // some of them; enter was spent exactly this way once already, one
            // widget up.
            table.KeyDown += OnModalKeyDown;
        }

        _filterViews.ValueChanged += OnFilterViewChanged;

        _filterBody = new View
        {
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            Visible = false,
            CanFocus = true,
        };

        _filterBody.Add(_filterSentence, _filterViews, _filterInForce);

        // THE WORK ITEM'S THREE REGIONS, laid out like the flight's and for its
        // reasons - see that body for why every container on the way down is a
        // TabStop, and why the fields are focusable at all.
        // WHAT IT SAYS, WITH THE ROOM THE TAB GIVES IT. This was capped at 45%
        // to leave space for the fields and the history below; the history is
        // on its own tab now and the fields are sized from their own count, so
        // the prose takes what is left rather than a fraction chosen against
        // regions that have gone.
        _itemSaidPane = new FrameView
        {
            Title = WorkItemDetails.SaidTitle,
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            TabStop = TabBehavior.TabStop,
        };

        _itemSaid = new Markdown
        {
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            CanFocus = true,
            ShowHeadingPrefix = false,
        };
        _itemSaidPane.Add(_itemSaid);

        _itemFields = new View
        {
            X = 0,
            Y = Pos.AnchorEnd(),
            Width = Dim.Fill(),

            // SET PER RENDER, from the number of fields there are. A tracker
            // that files nothing by sprint gives one row fewer, and the history
            // below has to start under whichever it is.
            Height = 0,
            CanFocus = true,
            TabStop = TabBehavior.TabStop,
        };

        // THE TOP OF ITS OWN TAB, and sharing it with what a change says.
        _itemHistoryPane = new FrameView
        {
            Title = WorkItemDetails.HistoryTitle,
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Percent(60),
            TabStop = TabBehavior.TabStop,
        };

        _itemHistory = CollectionViews.Table();
        _itemHistory.ValueChanged += OnModalRowPointedAt;
        _itemHistory.KeyDown += OnModalKeyDown;

        // AND THE SENTENCE WHEN THERE ARE NO ROWS. An empty table claims the
        // tracker answered and had nothing to say, which is a different thing
        // from a reader that does not declare the tool.
        _itemHistoryAbsent = new Label
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),

            // THE SAME, and it was already wrong here: NoHistory names
            // `get_work_item_history` and this label was showing it a character
            // short. See the fields absence below.
            HotKeySpecifier = new System.Text.Rune('\uffff'),
        };

        _itemHistoryPane.Add(_itemHistory, _itemHistoryAbsent);

        // WHAT THE CHANGE UNDER THE CURSOR SAYS. `What` is a sentence a tracker
        // wrote and a cell shows as much of it as the column is wide, which is
        // the log's problem one modal over and gets the log's answer.
        _itemChangePane = new FrameView
        {
            Title = WorkItemDetails.ChangeDetailTitle,
            X = 0,
            Y = Pos.Bottom(_itemHistoryPane),
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            TabStop = TabBehavior.TabStop,
            CanFocus = true,
        };
        // The log's pane one modal over, and a list for its reason.
        _itemChange = CollectionViews.Document();
        _itemChange.ViewportChanged += OnChangeDetailResized;
        _itemChangePane.Add(_itemChange);

        _itemDetailsTab = new View
        {
            Title = "details",
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            CanFocus = true,
            TabStop = TabBehavior.TabStop,
        };
        _itemDetailsTab.Add(_itemSaidPane, _itemFields);

        _itemHistoryTab = new View
        {
            Title = WorkItemDetails.HistoryTitle,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            CanFocus = true,
            TabStop = TabBehavior.TabStop,
        };
        _itemHistoryTab.Add(_itemHistoryPane, _itemChangePane);

        _itemTabs = new Terminal.Gui.Views.Tabs
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
        };
        _itemFieldsTable = CollectionViews.Table();
        _itemFieldsTable.ValueChanged += OnModalRowPointedAt;
        _itemFieldsTable.KeyDown += OnModalKeyDown;

        // AND THE SENTENCE WHEN A READER SENDS NOTHING EXTRA. The seven are
        // always rows, so the table is never empty - this says why there is
        // nothing BELOW them, which is a different fact from an empty table.
        _itemFieldsAbsent = new Label
        {
            X = 0,
            Y = Pos.AnchorEnd(2),
            Width = Dim.Fill(),
            Height = 2,
            // NO HOTKEY OUT OF SOMEBODY ELSE'S SENTENCE. A Label takes the
            // character after the first `_` as a hotkey and eats the
            // underscore, and what goes in here is a reader's own words - which
            // name tools like `get_work_item_fields`. An operator reading
            // `getwork_item_fields` off this line cannot act on it, and nothing
            // anywhere would say a character had been removed.
            HotKeySpecifier = new System.Text.Rune('\uffff'),
        };

        _itemFieldsTab = new View
        {
            Title = WorkItemDetails.FieldsTitle,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            CanFocus = true,
            TabStop = TabBehavior.TabStop,
        };
        _itemFieldsTab.Add(_itemFieldsTable, _itemFieldsAbsent);

        // WHAT FLYING THIS WOULD DO, above the button that does it. The
        // sentence is the pane's because a caption cannot say which tracker,
        // which id and against what - see WorkItemDetails.ActionsSaid.
        _itemActionSaid = new Label
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = 2,

            // NO HOTKEY OUT OF SOMEBODY ELSE'S SENTENCE, for the reason the
            // absence line above gives: a tracker's id and a repository name
            // both reach this line and either may carry an underscore.
            HotKeySpecifier = new System.Text.Rune('￿'),
        };

        _itemFly = new Button
        {
            X = 0,
            Y = 2,
            Text = WorkItemDetails.FlyLabel,

            // NO HOTKEY OF ITS OWN - _runnerStart's rule, and the same reason.
            // A Button takes a letter out of its own caption, and Keymap is the
            // only place a printable key means anything in this console.
            HotKeySpecifier = new System.Text.Rune('￿'),
        };
        _itemFly.Accepting += OnFlyTheOpenItem;

        _itemActionsTab = new View
        {
            Title = WorkItemDetails.ActionsTitle,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            CanFocus = true,
            TabStop = TabBehavior.TabStop,
        };
        _itemActionsTab.Add(_itemActionSaid, _itemFly);

        _itemTabs.Add(_itemDetailsTab);
        _itemTabs.Add(_itemHistoryTab);
        _itemTabs.Add(_itemFieldsTab);
        _itemTabs.Add(_itemActionsTab);
        _itemTabs.ValueChanged += OnWorkItemTabChanged;

        _itemBody = new View
        {
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            Visible = false,
            CanFocus = true,
            TabStop = TabBehavior.TabStop,
        };

        _itemBody.Add(_itemTabs);

        // THE KINDS, AND THE ONE LINE ABOVE THEM. Two rows for the sentence
        // because it wraps at this width; the table takes the rest.
        _kindSentence = new Label { X = 0, Y = 0, Width = Dim.Fill(), Height = 2 };

        _kindChoices = CollectionViews.Table();
        _kindChoices.ValueChanged += OnModalRowPointedAt;
        _kindChoices.KeyDown += OnModalKeyDown;

        _kindBody = new View
        {
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            Visible = false,
            CanFocus = true,
        };

        _kindChoices.X = 0;
        _kindChoices.Y = Pos.Bottom(_kindSentence);
        _kindChoices.Width = Dim.Fill();
        _kindChoices.Height = Dim.Fill();

        _composeKindTab = new View
        {
            Title = "kind",
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            CanFocus = true,
            TabStop = TabBehavior.TabStop,
        };
        _composeKindTab.Add(_kindSentence, _kindChoices);

        // AGAINST WHAT. The same registry the Repositories tab lists, marked
        // with what THIS flight names rather than with what every new one
        // starts as.
        _composeRepos = CollectionViews.Table();
        _composeRepos.ValueChanged += OnModalRowPointedAt;
        _composeRepos.KeyDown += OnModalKeyDown;

        // AND THE SENTENCE WHEN THERE ARE NONE. An empty table claims the
        // tenant registered nothing, which is a different fact from a console
        // that has not read the registry yet.
        // FOCUSABLE, WHICH A LABEL IS NOT BY DEFAULT, and it is load-bearing:
        // Terminal.Gui's Tabs follows FOCUS, so a tab with nothing focusable in
        // it cannot be reached by tabbing at all. Measured - with the registry
        // unread the table is hidden, and the modal's second tab was
        // unreachable rather than merely empty.
        _composeReposAbsent = new Label
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            CanFocus = true,
        };

        _composeRepoTab = new View
        {
            Title = "repositories",
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            CanFocus = true,
            TabStop = TabBehavior.TabStop,
        };
        _composeRepoTab.Add(_composeRepos, _composeReposAbsent);

        _workKindTabs = new Terminal.Gui.Views.Tabs
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
        };
        _workKindTabs.Add(_composeKindTab);
        _workKindTabs.Add(_composeRepoTab);
        _workKindTabs.ValueChanged += OnWorkKindTabChanged;

        _kindBody.Add(_workKindTabs);

        // THE REGISTRY, AND THE ONE LINE ABOVE IT. The kinds' shape exactly.
        // Three rows for the sentence rather than two, because it says what
        // happens AFTER the answer as well as what the list is - and a person
        // who is not told the terminal is about to go away reads it as a crash.
        _credentialRepoSentence = new Label
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = 3,
        };

        _credentialRepoChoices = CollectionViews.Table();
        _credentialRepoChoices.ValueChanged += OnModalRowPointedAt;
        _credentialRepoChoices.KeyDown += OnModalKeyDown;

        _credentialRepoBody = new View
        {
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            Visible = false,
            CanFocus = true,
        };

        _credentialRepoChoices.X = 0;
        _credentialRepoChoices.Y = Pos.Bottom(_credentialRepoSentence);
        _credentialRepoChoices.Width = Dim.Fill();
        _credentialRepoChoices.Height = Dim.Fill();

        _credentialRepoBody.Add(_credentialRepoSentence, _credentialRepoChoices);

        // THE FLIGHT'S OWN BODY, three regions down one column. The intent is
        // as tall as the top third because it is the only part whose length
        // nobody controls; the fields take what they need; the log gets the
        // rest, which is right because it is the part that grows.
        _flightIntentPane = new FrameView
        {
            Title = FlightDetails.IntentTitle,
            X = 0,
            Y = 0,
            Width = Dim.Fill(),

            // A STOP, NOT A GROUP, AND EVERY CONTAINER ON THE WAY DOWN NEEDS
            // TO BE ONE. Focus advances by asking a view for its DIRECT
            // subviews whose TabStop matches the behaviour being advanced, so a
            // container that does not match is not descended into - its
            // children are simply not candidates. FrameView is created as a
            // TabGroup, so with tab the intent, the fields and the log were all
            // invisible to navigation and focus could not leave the one control
            // it started on. Measured against the library's own AdvanceFocus,
            // which would not move either.
            TabStop = TabBehavior.TabStop,
        };

        // A MARKDOWN VIEW, because what is in it is markdown. `gg fly' takes
        // whatever somebody typed and people type paragraphs, steps and code
        // fences; a Label would draw the asterisks and wrap the numbering into
        // the prose. It scrolls itself, which is the other half - an intent
        // taller than the pane is an intent a person can still read.
        _flightIntent = new Markdown
        {
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            CanFocus = true,
            ShowHeadingPrefix = false,
        };
        _flightIntentPane.Add(_flightIntent);

        // THE SCALARS, EACH IN A FIELD A CURSOR CAN ENTER. Read-only, so
        // nothing here pretends to be editable - the console writes through
        // verbs with the terminal released, never through a widget - and
        // focusable, which is the whole point: the flight id is the value most
        // often wanted out of this modal and a Label cannot be copied out of.
        _flightFields = new View
        {
            X = 0,
            Y = Pos.Bottom(_flightIntentPane),
            Width = Dim.Fill(),

            // SET PER RENDER, from the number of fields there are. A flight
            // waiting on three people has three rows more than one waiting on
            // nobody, and the log below has to start under whichever it is.
            Height = 0,

            // FOCUSABLE BECAUSE ITS CHILDREN ARE. Terminal.Gui will not focus a
            // view whose SuperView cannot be, and a plain View is created with
            // CanFocus false - so ten read-only fields nobody could put a cursor
            // in, which is the only reason they are fields. A stop as well as
            // focusable, because navigation descends only through containers
            // that match the behaviour it is advancing.
            CanFocus = true,
            TabStop = TabBehavior.TabStop,
        };

        // THE TOP OF ITS OWN TAB NOW, not the bottom of the details one. It
        // used to sit under the fields and take whatever height they left,
        // which is what made a log of any length fight a form of any length.
        _flightLogPane = new FrameView
        {
            Title = FlightDetails.LogTitle,
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Percent(60),

            // The same, and see the intent's frame for why.
            TabStop = TabBehavior.TabStop,
        };
        _flightLog = CollectionViews.Table();

        // ITS OWN SUBSCRIPTION, NOT OnRowPointedAt. That one hands a row to
        // Reducer.Pointed, which dispatches on the tab that has the screen -
        // and the tab under this modal is the flights list, so a log row landed
        // on would have moved the cursor behind the modal. It is also not an
        // entry: OnLogRowPointedAt maps it through LogRow.Entry first.
        _flightLog.ValueChanged += OnLogRowPointedAt;

        // BECAUSE A RENDER HAPPENS BEFORE THE LAYOUT DOES. The wrap needs the
        // column's width and the widget has none until it has been laid out, so
        // the first fill wrapped nothing and there was no second one - the
        // unwrap simply never happened. This is also the resize path: a
        // narrower terminal is a narrower column, and the text has to be broken
        // again.
        _flightLog.ViewportChanged += OnLogResized;
        _flightLogAbsent = new Label { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill() };
        _flightLogPane.Add(_flightLog, _flightLogAbsent);

        // WHAT THE ENTRY UNDER THE CURSOR SAID, in the half of the tab a table
        // cannot use. A Label rather than a table because this is prose: it is
        // the text the log used to break into continuation rows, and the
        // reason those rows existed at all.
        _flightLogDetailPane = new FrameView
        {
            Title = FlightDetails.LogDetailTitle,
            X = 0,
            Y = Pos.Bottom(_flightLogPane),
            Width = Dim.Fill(),
            Height = Dim.Fill(),

            // The same, and see the intent's frame for why.
            TabStop = TabBehavior.TabStop,
            CanFocus = true,
        };
        // A LIST RATHER THAN A LABEL, because a Label draws what fits and
        // drops the rest - which for this pane is the defect it was built to
        // fix. The reading views already scroll prose this way, so the arrows
        // do here what they do in every other list in this console.
        // Document rather than List, for the scroll bar: without one "a full
        // box and a long page look exactly alike", which is this complaint
        // said from the other side.
        _flightLogDetail = CollectionViews.Document();
        _flightLogDetail.ViewportChanged += OnLogDetailResized;
        _flightLogDetailPane.Add(_flightLogDetail);

        // FOCUSABLE, FOR THE SAME REASON AND WITH MORE AT STAKE: this one is
        // between the modal and ALL THREE regions, so with it left as a plain
        // View the whole modal was a picture.
        _flightBody = new View
        {
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            Visible = false,
            CanFocus = true,
            TabStop = TabBehavior.TabStop,
        };

        // THE THREE REGIONS BECOME ONE TAB, unchanged. Their layout is
        // relative to each other rather than to what contains them, so the
        // container moving down a level costs them nothing - and keeping them
        // together is the point: "everything the modal used to be" is one tab,
        // not three that a person has to reassemble.
        _flightDetailsTab = new View
        {
            Title = "details",
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            CanFocus = true,
            TabStop = TabBehavior.TabStop,
        };
        _flightDetailsTab.Add(_flightIntentPane, _flightFields);

        // A LABEL, LIKE THE PANE IT CAME FROM. A gate is read rather than
        // picked from, and the renderer it delegates to already produces the
        // whole block as text.
        _flightGate = new Label
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            CanFocus = true,
        };
        _flightGateTab = new View
        {
            Title = FlightDetails.GateTitle,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            CanFocus = true,
            TabStop = TabBehavior.TabStop,
        };
        _flightGateTab.Add(_flightGate);

        // THE THIRD TAB: the table above, what it cannot hold below.
        _flightLogTab = new View
        {
            Title = FlightDetails.LogTitle,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            CanFocus = true,
            TabStop = TabBehavior.TabStop,
        };
        _flightLogTab.Add(_flightLogPane, _flightLogDetailPane);

        // THE FOURTH TAB, and it existed everywhere but here. `FlightTab.Facts`
        // was in the enum, the cycle and the linear text with no widget behind
        // it, so pressing for a flight's facts fetched the read and drew the
        // details tab. A Label and not a table, because what a flight recorded
        // reads as prose - the gate tab's shape, for the gate tab's reason.
        _flightFacts = new Label { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill() };
        _flightFactsTab = new View
        {
            Title = FlightDetails.FactsTitle,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            CanFocus = true,
            TabStop = TabBehavior.TabStop,
        };
        _flightFactsTab.Add(_flightFacts);

        // THE SAME WIDGET THE CONSOLE'S OWN BAR USES, one level in. A second
        // way of drawing a row of tabs would be a second set of behaviours for
        // one act, and this one already answers arrow keys the way a person
        // who has used the bar expects.
        _flightTabs = new Terminal.Gui.Views.Tabs
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
        };
        _flightTabs.Add(_flightDetailsTab);
        _flightTabs.Add(_flightGateTab);
        _flightTabs.Add(_flightLogTab);
        _flightTabs.Add(_flightFactsTab);
        _flightTabs.ValueChanged += OnFlightTabChanged;

        _flightBody.Add(_flightTabs);

        // THE RUNNER'S OWN BODY, two regions down one column: what the fleet
        // and the child know, and what the child has said. No intent, because
        // a runner is not asked for anything - it is a machine that took work.
        _runnerFields = new View
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),

            // Set per render, from the number of fields there are: a busy
            // runner has rows an idle one does not.
            Height = 0,

            // FOCUSABLE AND A STOP, for the reason written out over the
            // flight's fields - a plain View is created with CanFocus false and
            // navigation descends only through containers whose TabStop matches
            // what is being advanced, so either one missing makes every field
            // beneath it unreachable and the modal a picture.
            CanFocus = true,
            TabStop = TabBehavior.TabStop,
        };

        // NO FRAME AND NO TITLE ANY MORE: the tab along the foot says which
        // of the three this is, and a bordered box inside a bordered tab is one
        // border too many. RunnerDetails.LogTitle still names it in the text
        // rendering, which is the surface that has no tabs.
        _runnerLogPane = new View
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            CanFocus = true,
            TabStop = TabBehavior.TabStop,
        };

        // A LIST OF LINES, WHERE THE FLIGHT'S LOG IS A TABLE, and the
        // difference is the content rather than the modal. A flight's log has a
        // time, an attempt and an event, so it has columns. A runner's log is
        // whatever a child wrote to its own stdout - a stack trace, a wrapped
        // sentence, a line of JSON - and giving that columns would invent a
        // structure it does not have.
        //
        // NOT A TextView, WHICH IS THE WIDGET THAT FITS. It scrolls text and
        // selects across lines, and 2.4.17 marks it obsolete in favour of an
        // editor that is not in this library. Taking a deprecated widget for a
        // modal that shows a few lines of output buys a selection nobody asked
        // for and a migration somebody will have to do.
        _runnerSaid = CollectionViews.List();

        // BECAUSE A RENDER HAPPENS BEFORE THE LAYOUT DOES, and the wrap needs a
        // width the widget does not have until it has been laid out. #315 lost
        // its whole unwrap to exactly this, with every unit test passing. It is
        // the resize path too: a narrower terminal is a narrower frame.
        _runnerSaid.ViewportChanged += OnRunnerLogResized;

        _runnerLogAbsent = new Label { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill() };
        _runnerLogPane.Add(_runnerSaid, _runnerLogAbsent);

        // WHAT IT RUNS, AND WHAT RUNS BESIDE IT. Two tables rather than lists:
        // these have columns, where a runner's output has none.
        _runnerEnvironments = CollectionViews.Table();
        _runnerMembers = CollectionViews.Table();

        // ONE LABEL FOR BOTH, because both absences have one cause: the peers
        // are found THROUGH the environments this runner advertises, so no
        // environments means no peers and the same sentence explains both.
        _runnerNothingHere = new Label
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
        };

        _runnerViewTabbed =
        [
            .. RunnerViews.All.Select(view =>
            {
                if (view == RunnerView.Log)
                {
                    return (View: view, Pane: _runnerLogPane);
                }

                var pane = new View
                {
                    Width = Dim.Fill(),
                    Height = Dim.Fill(),
                    CanFocus = true,
                    TabStop = TabBehavior.NoStop,
                };

                pane.Add(view == RunnerView.Environments ? _runnerEnvironments : _runnerMembers);

                return (View: view, Pane: (View)pane);
            }),
        ];

        _runnerViews = new Terminal.Gui.Views.Tabs
        {
            X = 0,
            Y = Pos.Bottom(_runnerFields),
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            TabSide = Side.Bottom,

            // NOT IN THE TAB RING, the airspace bar's reason: `tab' is the
            // keymap's on this screen, so a widget claiming a stop would give
            // that key a second meaning. `v' turns this bar, and a click on a
            // header turns it too.
            TabStop = TabBehavior.NoStop,
        };

        foreach (var (view, pane) in _runnerViewTabbed)
        {
            pane.Title = RunnerViews.Title(view);
            _runnerViews.Add(pane);
        }

        // AND SOMEBODY LISTENS TO IT, which is what the window's own bar has
        // always had. Arrowing along these headers or clicking one changes
        // Value, and a bar nobody hears is one whose every move the next render
        // undoes - reported as "it switches back to the log after a second".
        _runnerViews.ValueChanged += OnRunnerViewChanged;

        // THEIR OWN HANDLER, NOT OnRowPointedAt. That one routes through
        // Reducer.Pointed by ACTIVE TAB, and the tab behind this modal is
        // Runners - so a click in here would change which runner the modal is
        // about. The flight log is wired to its own for the same reason.
        _runnerEnvironments.ValueChanged += OnModalRowPointedAt;
        _runnerMembers.ValueChanged += OnModalRowPointedAt;

        _runnerBody = new View
        {
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            Visible = false,
            CanFocus = true,
            TabStop = TabBehavior.TabStop,
        };
        _runnerBody.Add(_runnerFields, _runnerViews);

        // THE INTENT IS AS TALL AS WHAT IS IN IT, capped against the room there
        // is. A third of the modal was a box sized for a page around `fix the
        // login bug', and the log underneath is what paid for it. Dim.Func
        // rather than a height set per render, because the cap wants the
        // laid-out size and a render happens before the layout does - and here
        // rather than in the initializer, because the body it measures against
        // has to exist first.
        // AGAINST THE TAB, NOT THE BODY. The intent shares its room with the
        // fields and the log, and since those three moved inside a tab the
        // body is a row taller than what they actually get - the tab strip.
        // Measuring against the body would hand the intent room that is not
        // there and take the difference out of the log, which is the half that
        // has already paid for this once.
        _flightIntentPane.Height = Dim.Func(
            _ => FlightDetails.IntentRows(
                FlightDetails.IntentLines(State), _flightDetailsTab.Viewport.Height),
            _flightIntentPane);

        // THE READING MODAL - the rules in force and what would change, which
        // are ONE list showing whichever is open. Two lists would be two
        // scroll positions, two wraps and two resize handlers for one box a
        // person switches views inside with a letter.
        //
        // In a frame of its own so the list inside it can take focus - a
        // nested container left as a plain View made the whole of the runner
        // modal a picture.
        _readingSaid = CollectionViews.List();
        _readingSaid.ViewportChanged += OnReadingResized;

        _readingBody = new View
        {
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            Visible = false,
            CanFocus = true,
            TabStop = TabBehavior.TabStop,
        };

        _readingBody.Add(_readingSaid);

        _modal.Add(
            _modalBody, _flightBody, _runnerBody, _readingBody, _helpBody, _filterBody,
            _itemBody, _kindBody, _credentialRepoBody);

        // THE QUEUE TAB IS TWO PANES, so it gets a container: the list a person
        // drives and the detail of whatever it lands on are one view of one
        // thing.
        var queueTab = new View { Title = "queue", Width = Dim.Fill(), Height = Dim.Fill() };
        _queuePane.Height = Dim.Fill();
        _flightPane.Height = Dim.Fill();
        queueTab.Add(_queuePane, _flightPane);

        // EVERY TAB, FROM THE START. The bar's job is to say what there is, so
        // all eight panes are built and all eight are inserted; which one draws
        // is the model's to say and the component's to show.
        _tabbed =
        [
            (TabId.Queue, queueTab),

            // SECOND, BESIDE THE QUEUE. The queue is what needs somebody and a
            // standing nomination is already one of its rows, so this is where
            // a person goes the moment they have answered one. Declared in the
            // enum's order, which is the rule six lines down.
            (TabId.Board, Tabbed(_boardPane)),

            (TabId.Flights, Tabbed(_flightsPane)),

            // BESIDE THE FLIGHTS, WHERE IT IS DECLARED. This was appended after
            // Repositories, so the bar drew it seventh while Tabs.Next - which
            // walks the enum - reached it third, and `tab' skipped six tabs.
            // TabGoesLeftToRightTests holds the two orders together now.
            (TabId.Runners, Tabbed(_runnersPane)),
            (TabId.Live, Tabbed(_livePane)),
            (TabId.Browse, Tabbed(_browsePane)),
            (TabId.Repositories, Tabbed(_repositoriesPane)),
            (TabId.Envelope, Tabbed(_envelopePane)),


            // LAST, WHERE IT IS DECLARED, for the reason written three tabs
            // up - and the only one of these that may not be on the bar at
            // all. It is in this list so the source order and Tabs.All agree;
            // whether it reaches the bar is the loop below.
            (TabId.Allowances, Tabbed(_allowancesPane)),
        ];

        _bar = new Terminal.Gui.Views.Tabs
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(2),
        };

        // OFFERED RATHER THAN EVERY TAB, which is true of exactly one: the
        // fleet's allowances need an administrator and a line in this
        // machine's file. A tab on the bar for anybody else would promise a
        // fleet and show them their own machine - so it is absent, rather than
        // present and empty.
        //
        // THE SAME METHOD THE FRAME USES, rather than a loop here and a
        // recomputation there. An empty bar reconciled against the offered set
        // is a bar being built, so construction is not a special case - and
        // the special case is exactly what froze the membership before.
        FollowTheOffered();

        _bar.ValueChanged += OnTabChanged;

        // THE PANES THAT ARE DOCUMENTS RATHER THAN LISTS. A person reads these
        // rather than picking from them, and reading is what grey is for; the
        // queue and the tables keep the scheme their widgets came with, because
        // a highlighted row has to stand out from what is around it.
        // THE WHOLE WINDOW FIRST, and the document panes after. Views inherit
        // their scheme from the one above them, so grounding the window is what
        // puts every border, header and label on the same dark surface - and
        // what makes "muted" mean something relative to it.
        SetScheme(ConsoleTheme.Grounded());

        // THE ROW UNDER THE CURSOR, AS A BLOCK. On the tab with two halves the
        // row is what says which document the pane beside it is about, so it
        // has to read as a cursor - and has to keep reading as one when the
        // keyboard crosses to the document.
        // ON THE TABLE, NOT ON THE FRAME AROUND IT. Measured: setting it on the
        // frame left every row drawn in the window's plain attribute - the
        // table takes its scheme from somewhere other than its immediate
        // parent, and the block is about the ROW rather than the border
        // anyway.
        _airspaceTable.SetScheme(ConsoleTheme.Picked());
        Muted(_airspaceAbsent, _airspaceNoDocument, _live, _flight, _modalBody,
            _runners, _flightIntent, _flightLogAbsent);

        // THE CORNER, OVER THE RIGHT-HAND END OF THE TAB STRIP - the least
        // read stretch of the screen - and above the activity line, which is
        // the receipt it follows. Added before the modal, so a modal draws over
        // it; it is not drawn under one at all, which Render decides.
        _notificationText = new Label { X = 1, Y = 0, Width = Dim.Fill(1), Height = 2 };
        _notificationHint = new Label
        {
            X = Pos.AnchorEnd(),
            Y = 2,
            Width = Dim.Auto(DimAutoStyle.Text),
        };
        _notifications = new View
        {
            X = Pos.AnchorEnd(NotificationsWidth + 2),
            Y = Pos.AnchorEnd(7),
            Width = NotificationsWidth,
            Height = 5,
            CanFocus = false,
            Visible = false,
            BorderStyle = Terminal.Gui.Drawing.LineStyle.Rounded,
            Arrangement = ViewArrangement.Overlapped,

            // ITS OWN GROUP, so tab walks its buttons and does not wander off
            // into the panes behind it while it holds the keyboard.
            TabStop = TabBehavior.TabGroup,
        };
        _notifications.Add(_notificationText, _notificationHint);

        // A COLOUR OF ITS OWN, the one place this console names one outright:
        // the corner has to read as something that arrived rather than as a
        // pane that was always there. Light on a deep blue, and the pair turned
        // over for whatever has focus inside it.
        var ink = new Terminal.Gui.Drawing.Color(230, 237, 243);
        var navy = new Terminal.Gui.Drawing.Color(31, 58, 95);
        _notifications.SetScheme(new Terminal.Gui.Drawing.Scheme(ConsoleTheme.Grounded())
        {
            Normal = new Terminal.Gui.Drawing.Attribute(ink, navy),
            HotNormal = new Terminal.Gui.Drawing.Attribute(ink, navy),
            Focus = new Terminal.Gui.Drawing.Attribute(navy, ink),
            HotFocus = new Terminal.Gui.Drawing.Attribute(navy, ink),
        });

        // BEFORE THE MODAL, so a dialog covers the badge and the corner
        // rather than either sitting on top of one - the order these are
        // added is the order they are drawn. `_version' came from main and
        // `_notifications' from the corner; taking either list whole would
        // have dropped the other with nothing failing to compile.
        Add(_bar, _version, _activity, _hints, _hintsStanding, _notifications, _modal);

        // AFTER THE PANES, so it sits over whichever one is waiting rather than
        // under it. It is the last thing added for the reason `_modal' is near
        // the end: order here is depth.
        Add(_waiting);

        KeyDown += OnScreenKeyDown;

        // AND THE DIALOG, BELOW IT. A key travels from the focused view
        // upwards, so while a modal has the keyboard the screen's handler is
        // the LAST thing to see a key - and Terminal.Gui gives enter its own
        // meaning on a Dialog, which means enter never arrived at all. Found in
        // a pty: the filter modal's "pick this" resolved, reduced, and did
        // nothing anybody could see.
        _modal.KeyDown += OnModalKeyDown;

        // AND THE BODY, WHICH IS THE ONE THAT ACTUALLY HAS THE KEYBOARD. The
        // label is CanFocus, so a dialog handing focus to its first focusable
        // child hands it here - and a key goes to the focused view first. That
        // is where enter was being spent: measured with a temporary trace on
        // both handlers, which showed j and b arriving at the dialog and enter
        // arriving nowhere at all.
        _modalBody.KeyDown += OnModalKeyDown;
        _queue.ValueChanged += OnQueueSelectionChanged;

        Render();
        Watch();
    }

    /// <summary>
    /// Looks at the live view on a timer, so the pane advances without a keypress.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is the console's only mid-session read, and it is scoped on
    /// purpose.</b> A UI session may advance state from a local file whose path
    /// the console already holds. It may not make a network call, resolve a
    /// credential, or spawn a process - <c>LiveStreamingTests</c> asserts that
    /// over what this file may reach, so the scope is structural rather than a
    /// comment somebody can drift away from.
    /// </para>
    /// <para>
    /// <b>The state is still the source of truth.</b> The tick folds new lines
    /// in through the same reducer a keystroke uses and re-renders; nothing is
    /// retained anywhere but <see cref="State"/>, and <see cref="LiveTails"/> is
    /// a collaborator owned outside this lifetime rather than something the
    /// session accumulated.
    /// </para>
    /// <para>
    /// <b>It stops when nobody is watching.</b> Returning false ends the timer,
    /// and the pane is off by default - so a console nobody attached costs no
    /// syscalls at all, and detaching is how a person who does not want it makes
    /// it stop.
    /// </para>
    /// </remarks>
    private void Watch()
    {
        // THE BREATH, AND ONLY WHILE SOMETHING IS WAITING. A tick that painted
        // regardless would be this console repainting four times a second for
        // ever - which is the cost just taken out of it, put back for a mark
        // nobody is looking at. When nothing waits this does one comparison and
        // returns.
        //
        // A QUARTER SECOND, which is six shades to a breath and a second and a
        // half to come back around: slow enough to read as breathing rather
        // than flickering, quick enough to say something is happening.
        _app.AddTimeout(TimeSpan.FromMilliseconds(50), () =>
        {
            if (!LoadingArt.Waiting(State))
            {
                return true;
            }

            State = State with { LoadingPulse = State.LoadingPulse + 1 };

            // ONLY THE MARK, NEVER THE SCREEN. A full Render twenty times a
            // second is the cost this console just had taken out of it, put
            // back for one Label - so this moves the colour and asks for that
            // one view to be drawn again.
            _waiting.SetScheme(ConsoleTheme.Waiting(LoadingArt.Glow(State.LoadingPulse)));
            _waiting.SetNeedsDraw();
            return true;
        });

        if (_expectations is not null)
        {
            // WHAT THE CONSOLE IS WAITING TO SEE, on AutoRefresh's terms: the
            // look runs on a task owned outside this lifetime, and this tick
            // only folds what has already landed and ages the corner. A quarter
            // of a second, because the first gap is a quarter of a second and a
            // coarser tick would stretch it.
            _app.AddTimeout(TimeSpan.FromMilliseconds(250), () =>
            {
                var advanced = _expectations.Advance(State);

                if (ReferenceEquals(advanced, State))
                {
                    return true;
                }

                State = advanced;
                Render();
                return true;
            });
        }

        if (_tails is not null)
        {
            _app.AddTimeout(LookEvery, () =>
            {
                if (!State.LiveVisible)
                {
                    return false;
                }

                var advanced = _tails.Advance(State);
                if (ReferenceEquals(advanced, State))
                {
                    return true;
                }

                State = advanced;
                Render();
                return true;
            });
        }

        if (_refresh is not null)
        {
            // A SECOND EXCEPTION, ARGUED IN AutoRefresh. The session does not
            // read: it folds a result that has already arrived, and every tick
            // returns whether or not one has. What it must never do is wait,
            // because a keyboard frozen for as long as the control plane takes
            // is the thing the rule is protecting.
            //
            // ONCE A SECOND, because the only thing that changes between ticks
            // is a countdown measured in seconds.
            _app.AddTimeout(TimeSpan.FromSeconds(1), () =>
            {
                var advanced = _refresh.Advance(State);

                if (ReferenceEquals(advanced, State) && advanced.Refresh == State.Refresh)
                {
                    return true;
                }

                State = advanced;
                Render();
                return true;
            });
        }

        if (_booted is not null)
        {
            // THE READS THE CONSOLE OPENED WITHOUT. Measured before this
            // existed: four and a half seconds from launch to the first BYTE of
            // output, because every read happened before Terminal.Gui was
            // initialised - two serial rounds, eleven reads in the first and a
            // log per open flight in the second, all against a control plane in
            // another country. What a person saw for that time was a black
            // terminal.
            //
            // So the console opens on what is known locally and this folds the
            // rest in when it arrives. Every pane already has a sentence for a
            // view nobody has fetched, which is what makes an empty console
            // honest rather than broken-looking.
            //
            // LookEvery rather than a second, because this is the one tick a
            // person is actually waiting on.
            _app.AddTimeout(LookEvery, () =>
            {
                if (_booted() is not { } arrived)
                {
                    return true;
                }

                // WHAT THEY HAVE DONE MEANWHILE STAYS. The boot is a whole
                // model rather than a patch - it is the state the console would
                // have opened with - so the fields a person can have MOVED in
                // the seconds before it landed are carried over it. They are
                // few and they are all view: which tab, which modal, and the
                // look. Everything a selection indexes is empty until this
                // lands, so a cursor has nothing to have moved over yet.
                State = arrived with
                {
                    ActiveTab = State.ActiveTab,
                    Mode = State.Mode,
                    Look = State.Look,

                    // AND ASK AGAIN FOR THE TAB THEY ACTUALLY MOVED TO, which
                    // is what makes folding a whole model safe here without
                    // listing which fields are the read plane - the list this
                    // console's patches exist to avoid.
                    //
                    // Measured: pressing the board's key at one second started
                    // its read, the boot landed at three and a half and the
                    // fold put a null board back over it, so the pane a person
                    // was looking at went empty and stayed empty until the next
                    // tick. The boot fills the tab it opens on and nothing
                    // else; anything further is a tab's own read, so the honest
                    // repair is to ask for that tab again rather than to
                    // preserve fields by name.
                    //
                    // Only when they HAVE moved. Staying put would otherwise
                    // buy a second identical round of the heaviest read the
                    // console makes, at the one moment it has just finished.
                    Refresh = arrived.Refresh with
                    {
                        Wanted = State.ActiveTab != arrived.ActiveTab,
                    },
                };

                Render();
                return false;
            });
        }

        if (_signInLanded is not null)
        {
            // THE APPROVAL HAPPENS IN A BROWSER, so nothing about this terminal
            // says when. The poll is already running on a task the root owns;
            // this asks once a second whether it has finished and ends the
            // session when it has, which is the same door `y` went out of - the
            // loop folds the answer and reloads with the terminal free, because
            // a sign-in invalidates every read the console makes rather than
            // one pane.
            //
            // ONLY WHILE A CODE IS SHOWING. The result is not consumed by
            // asking, so an unguarded timer would end the session again on the
            // tick after the loop folded it.
            _app.AddTimeout(TimeSpan.FromSeconds(1), () =>
            {
                if (State.SignIn is null)
                {
                    return false;
                }

                if (!_signInLanded())
                {
                    return true;
                }

                ExitCommand = Command.SignIn;
                _app.RequestStop(this);
                return false;
            });
        }

        if (_runnerLog is null)
        {
            return;
        }

        // THE SAME EXCEPTION, THE SAME SHAPE. A runner coming up is watched for
        // a few seconds and the whole reason to open the modal is to see it
        // happen - so the log is read on a tick, out of a local file whose path
        // this console chose, which is exactly what the live pane's exception
        // allows and no more. The timer stops the moment the modal closes.
        _app.AddTimeout(LookEvery, () =>
        {
            if (State.Mode != UiMode.Runner)
            {
                return false;
            }

            var lines = _runnerLog.Read();

            if (State.Here is not { } here || here.Log.SequenceEqual(lines))
            {
                return true;
            }

            State = State with { Here = here with { Log = lines } };
            Render();
            return true;
        });
    }

    /// <summary>
    /// A table, styled the way all three are.
    /// </summary>
    /// <remarks>
    /// Whole rows select, because every one of these lists is read a row at a
    /// time and a single highlighted cell says a person is choosing a value.
    /// The header keeps its underline and nothing else is ruled: lines between
    /// every cell spend a column of screen on each border and these tables are
    /// three, four and five columns wide.
    /// </remarks>
    /// <summary>
    /// A person put the cursor on a row - by clicking it, or by any of the
    /// movements the widget knows and the keymap does not.
    /// </summary>
    /// <remarks>
    /// <b>Through the reducer, with the row.</b> The queue's list can only say
    /// "up" or "down" - <c>QueueSelection.Wanted</c> collapses every jump into
    /// one step - so a click five rows down moved the cursor one. A table hands
    /// over the row it landed on and <c>Reducer.Pointed</c> takes it.
    /// </remarks>
    /// <summary>
    /// The button under the notice, which is the key by another door.
    /// </summary>
    /// <remarks>
    /// One path, whether a person clicked or typed: it ends the session with
    /// the same command the keymap resolves, and the loop does the work with
    /// the terminal provably free.
    /// </remarks>
    private void OnStartRunner(object? sender, EventArgs args)
    {
        ExitCommand = Command.StartRunner;
        _app.RequestStop(this);
    }

    /// <summary>The actions tab's button, which is the browse tab's `f`.</summary>
    /// <remarks>
    /// <b>It ends the session, exactly as the key does.</b>
    /// <c>Command.FlyPicked</c> is in <c>ShellCommands.Handled</c> because it
    /// writes, so the button cannot perform it here - it hands the same command
    /// to the loop that the keymap would, and the views are rebuilt from what
    /// the loop leaves in <c>AppState</c>. <c>OnStartRunner</c>'s shape, for
    /// <c>OnStartRunner</c>'s reason.
    /// </remarks>
    private void OnFlyTheOpenItem(object? sender, EventArgs args)
    {
        ExitCommand = Command.FlyPicked;
        _app.RequestStop(this);
    }

    private void OnRowPointedAt(object? sender, ValueChangedEventArgs<TableSelection?> args)
    {
        if (_syncing || args.NewValue is not { } selection)
        {
            return;
        }

        // THE MOUSE'S OWN PATH. Clicking a row never reaches Dispatch, so a
        // capture full of paints and empty of `input.' lines says nothing about
        // whether the clicks arrived. This is where they arrive.
        using var clicked = Gg.Local.Timings.Active.Measure("input.row-pointed");

        var pointed = Reducer.Pointed(State, selection.SelectedCell.Y);

        if (ReferenceEquals(pointed, State))
        {
            return;
        }

        State = pointed;

        // AND THE NEXT PAGE, IF THIS ROW WAS THE LAST ONE. This is the only
        // place that knows which absolute row somebody is on, which is why the
        // ask is here rather than on a key: reaching the bottom of a list IS
        // the gesture for "show me more", and a key for it would be a key whose
        // meaning depended on where a cursor happened to be.
        //
        // ASKED, NOT FETCHED: Asked hands it to BackgroundReads, which owns the
        // task, and what comes back is applied to whatever is on screen then.
        //
        // REDUCED FIRST, exactly as Dispatch does it one screen over, and for a
        // sharper reason than the panes there: the reducer's arm is what records
        // that a page is coming, and WantsMore refuses while one is. Asking
        // without it would let a person who steps off the last row and back
        // abandon a page already on the wire to ask for the same one again.
        if (Reducer.WantsMore(State) is { } more)
        {
            State = Reducer.Reduce(State, more);
            Asked(more);
        }

        Render();
    }

    /// <summary>
    /// Put rows in a table, or hand the pane back to the words that explain
    /// why there are none.
    /// </summary>
    /// <remarks>
    /// <b>The cursor is set from the model rather than read from the widget.</b>
    /// A table repopulated raises its own selection event, which is also how a
    /// click arrives - so the caller holds the sync flag while this runs, for
    /// the reason the tab bar does.
    /// </remarks>
    private static void Fill<T>(
        TableView table,
        Label? empty,
        IReadOnlyList<T> rows,
        IReadOnlyList<string> columns,
        int cursor,
        Func<T, string[]> cells)
    {
        table.Visible = rows.Count > 0;

        if (empty is not null)
        {
            empty.Visible = rows.Count == 0;
        }

        if (rows.Count == 0)
        {
            CollectionViews.Fill(table, null);
            return;
        }

        // THE COLUMNS AND THE CELLS, BUILT WHERE A TEST CAN READ THEM. A
        // nameless column came back captioned `Column1' because DataTable
        // invents one, and nothing here could be asked what heading it had
        // produced.
        var data = CollectionViews.Rows(columns, [.. rows.Select(row => cells(row))]);

        // WHERE THE VIEW WAS SCROLLED TO, read before the source under it is
        // replaced. The render path resets this to zero - not provably by any
        // one call, since a table with no driver keeps it - and what came after
        // only guaranteed the cursor was somewhere on screen, never that the
        // rows stayed where a person was looking.
        //
        // Measured in a pty, flights tab, two pages down: clicking the row at
        // screen line 20 selected the right flight and then moved the list
        // twenty-one rows, parking the selection at the bottom edge and putting
        // a different flight under the pointer. Reported as "clicking doesn't
        // click on the row I'm hovering over", and the click was innocent.
        var from = table.RowOffset;

        CollectionViews.Fill(table, new DataTableSource(data));
        table.SetSelection(0, Math.Clamp(cursor, 0, rows.Count - 1), extendExistingSelection: false, null);
        table.EnsureValidSelection();

        // PUT BACK WHAT THE FILL TOOK, and nothing more. This restores rather
        // than chooses: it does not decide where the cursor should sit, so a
        // cursor walked off the bottom with `j' still scrolls by one line below
        // rather than jumping, which is the widget's job and stays its job.
        table.RowOffset = from;

        // AND THE VIEW HAS TO FOLLOW IT. A table handed a new source starts at
        // the top, and this hands it one every render - so the offset was zero
        // on every pass while the selection walked to row ninety-one, and
        // everything past the first screenful was unreachable. EnsureValidSelection
        // clamps the SELECTION; this is the one that moves the offset - and with
        // the offset restored above it now has nothing to do unless the cursor
        // genuinely left the screen.
        table.EnsureCursorIsVisible();
    }

    /// <summary>
    /// The document panes, dimmer than the console around them.
    /// </summary>
    /// <remarks>
    /// <b>The colours moved to <see cref="ConsoleTheme"/> and the reason is that
    /// they were wrong here.</b> Mixed in this class they were beyond the reach
    /// of any test - nothing can construct a <c>ConsoleScreen</c> without a
    /// terminal - and they shipped inverted: black text on a grey block. What is
    /// left here is which panes are documents, which is a judgement about the
    /// content and belongs in the view.
    /// </remarks>
    private static void Muted(params View[] views)
    {
        var muted = ConsoleTheme.Muted();

        foreach (var view in views)
        {
            view.SetScheme(muted);
        }
    }

    /// <summary>One pane, as the body of a tab.</summary>
    /// <remarks>
    /// The frame keeps its title, because two of them say something the tab
    /// cannot - the live pane's frozen note, and the repository this console is
    /// flying against.
    /// </remarks>
    private static View Tabbed(FrameView pane)
    {
        pane.X = 0;
        pane.Y = 0;
        pane.Width = Dim.Fill();
        pane.Height = Dim.Fill();
        pane.Visible = true;

        var body = new View { Width = Dim.Fill(), Height = Dim.Fill() };
        body.Add(pane);
        return body;
    }

    /// <summary>
    /// A person chose a tab, which is the same act as pressing its key.
    /// </summary>
    /// <remarks>
    /// <b>Through the shell, not around it.</b> Four of these views are a READ
    /// and a UI session may not make one, so the click issues the command the
    /// key issues and the session ends exactly as it does for the keystroke. A
    /// bar that changed the model itself would be a second way to do one thing,
    /// and the two would come to disagree.
    /// </remarks>
    /// <summary>
    /// A person clicked one of the flight modal's two tabs.
    /// </summary>
    /// <remarks>
    /// <b>Through the command, not around it.</b> Clicking a tab and pressing
    /// tab are the same act, so the click reduces what the key reduces rather
    /// than assigning the field - one path, and the model stays the thing that
    /// decides. Neither tab is a READ, so unlike the bar's handler there is no
    /// session to end: both halves are already in the state.
    /// </remarks>
    /// <summary>
    /// A tab picked with the mouse, turned into the command that means it.
    /// </summary>
    /// <remarks>
    /// <b>Guarded like the flight modal's and the console's own bar.</b>
    /// Assigning Value raises ValueChanged, so without the flag the assignment
    /// answers its own event and reduces a command for a tab nobody pressed.
    /// </remarks>
    private void OnWorkItemTabChanged(object? sender, ValueChangedEventArgs<View?> args)
    {
        if (_syncing || args.NewValue is not { } chosen)
        {
            return;
        }

        var wanted = ReferenceEquals(chosen, _itemHistoryTab) ? WorkItemTab.History
            : ReferenceEquals(chosen, _itemFieldsTab) ? WorkItemTab.Fields
            : ReferenceEquals(chosen, _itemActionsTab) ? WorkItemTab.Actions
            : WorkItemTab.Details;

        if (wanted == State.WorkItemTab)
        {
            return;
        }

        // CYCLED UNTIL IT MATCHES, because the command is "next" and a person
        // clicking a tab picked one. Two tabs made this a single step and
        // three do not - the flight modal's own sentence, one modal over.
        while (State.WorkItemTab != wanted)
        {
            State = Reducer.Reduce(State, Command.NextWorkItemTab);
        }

        Render();
    }

    /// <summary>
    /// A person turned the compose modal to its other half.
    /// </summary>
    /// <remarks>
    /// Guarded like every other bar here: assigning Value raises ValueChanged,
    /// so without the flag the assignment answers its own event and reduces a
    /// command for a tab nobody pressed.
    /// </remarks>
    private void OnWorkKindTabChanged(object? sender, ValueChangedEventArgs<View?> args)
    {
        if (_syncing || args.NewValue is not { } chosen)
        {
            return;
        }

        var wanted = ReferenceEquals(chosen, _composeRepoTab)
            ? WorkKindTab.Repositories
            : WorkKindTab.Kind;

        if (wanted == State.WorkKindTab)
        {
            return;
        }

        State = Reducer.Reduce(State, Command.NextWorkKindTab);
        Render();
    }

    private void OnFlightTabChanged(object? sender, ValueChangedEventArgs<View?> args)
    {
        if (_syncing || args.NewValue is not { } chosen)
        {
            return;
        }

        var wanted = ReferenceEquals(chosen, _flightGateTab) ? FlightTab.Gate
            : ReferenceEquals(chosen, _flightLogTab) ? FlightTab.Log
            : ReferenceEquals(chosen, _flightFactsTab) ? FlightTab.Facts
            : FlightTab.Details;

        if (wanted == State.FlightTab)
        {
            return;
        }

        // CYCLED UNTIL IT MATCHES, because the command is "next" and a person
        // clicking a tab picked one. Two tabs made this a single step and
        // three do not.
        while (State.FlightTab != wanted)
        {
            State = Reducer.Reduce(State, Command.NextFlightTab);
        }
        Render();
    }

    /// <summary>
    /// Makes the bar hold exactly the tabs the model offers, where they are
    /// declared.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>THE OFFERED SET CAN GROW WHILE THE CONSOLE IS RUNNING.</b> Whether
    /// the fleet's allowances are offered depends on whether this person is an
    /// administrator, and that answer arrives from a read the console lets
    /// fail - so the terminal opens against an unreachable control plane. A
    /// later refresh folds a good identity in, the offered set gains a tab,
    /// and a bar built at construction cannot show it. Terminal.Gui does not
    /// ignore a selection it cannot honour; it throws.
    /// </para>
    /// <para>
    /// <b>INSERTED WHERE IT IS DECLARED, not appended.</b> The bar's order and
    /// the order <c>Tabs.Next</c> walks have already been made to agree once,
    /// after `tab' skipped six tabs; a late arrival landing at the end would
    /// part them again for whoever turned the flag on.
    /// </para>
    /// <para>
    /// <b>Cheap when nothing moved</b>, which is every frame but the one where
    /// it matters.
    /// </para>
    /// </remarks>
    private void FollowTheOffered()
    {
        Follow(_bar, _onTheBar, Tabs.Offered(State),
            tab => (_tabbed.First(t => t.Tab == tab).Pane, Tabs.Title(State, tab)));

        // THE SAME METHOD, and the bar that moves constantly is not the copy.
        // The window's offered set changes once in the life of a console; this
        // one changes whenever the cursor lands on a row with different views,
        // which is an arrow key.
        Follow(_airspaceViews, _onTheViewBar, AirspaceViews.Offered(State),
            view => (_viewTabbed.First(t => t.View == view).Pane, AirspaceViews.Title(view)));
    }

    /// <summary>Make a bar hold exactly what is offered, where it is declared.</summary>
    /// <remarks>
    /// <b>Offered is always a subsequence of the declared order</b> - both
    /// callers filter a fixed list rather than building one - so a tab's
    /// position in the offered set is its position on the bar.
    /// </remarks>
    private static void Follow<T>(
        Terminal.Gui.Views.Tabs bar,
        List<T> held,
        IReadOnlyList<T> offered,
        Func<T, (View Pane, string Title)> tabbed)
    {
        if (held.SequenceEqual(offered))
        {
            return;
        }

        // GONE FIRST, so the indices below are the offered set's own. Removing
        // the selected tab makes Terminal.Gui choose another and announce it;
        // the caller holds the sync flag over this, so that announcement is
        // not mistaken for a person clicking a tab.
        foreach (var gone in held.Where(t => !offered.Contains(t)).ToList())
        {
            bar.Remove(tabbed(gone).Pane);
            held.Remove(gone);
        }

        for (var index = 0; index < offered.Count; index++)
        {
            var tab = offered[index];

            if (held.Contains(tab))
            {
                continue;
            }

            var (pane, title) = tabbed(tab);
            pane.Title = title;

            bar.InsertTab(index, pane);
            held.Insert(index, tab);
        }
    }

    /// <summary>A view was picked off the pane's bar with the mouse.</summary>
    /// <remarks>
    /// <b>The same shape as the window's bar, and for its reason:</b> a click
    /// is a person asking, so it goes through the model rather than round it.
    /// `v' walks the same field from the keymap.
    /// </remarks>
    private void OnAirspaceViewChanged(object? sender, ValueChangedEventArgs<View?> args)
    {
        if (_syncing || args.NewValue is not { } chosen)
        {
            return;
        }

        var picked = _viewTabbed.FirstOrDefault(t => ReferenceEquals(t.Pane, chosen));

        if (picked.Pane is null || picked.View == State.AirspaceView)
        {
            return;
        }

        State = State with { AirspaceView = picked.View };
        Render();
    }

    /// <summary>
    /// A person picked one of the runner modal's views off the bar.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><see cref="OnTabChanged"/>'s shape, one bar down, and for its
    /// reason.</b> Which view is showing is the MODEL's to say — the render
    /// draws from <c>State.RunnerView</c> — so a bar that moved without telling
    /// it would be corrected on the next pass and look like a key that did
    /// nothing. The widget is an input here, not a second source of truth.
    /// </para>
    /// <para>
    /// <b>THE SYNC FLAG IS WHY THIS DOES NOT EAT ITSELF.</b> Render assigns
    /// <c>Value</c> from the model every pass, and that assignment raises this
    /// — so without the guard a render would be read as a person choosing
    /// something, which is the re-entry the window's bar holds the same flag
    /// for.
    /// </para>
    /// </remarks>
    /// <summary>
    /// A person picked a help page, which is the same act as pressing tab.
    /// </summary>
    /// <remarks>
    /// <b>Through the model, never around it.</b> The widget reports; the state
    /// decides; Render puts the widget back where the state says. That is what
    /// keeps "which page is showing" something a test can assert, which is the
    /// whole objection the text tab bar was defended on.
    /// </remarks>
    private void OnHelpPageChanged(object? sender, ValueChangedEventArgs<View?> args)
    {
        if (_syncing || args.NewValue is not { } chosen)
        {
            return;
        }

        var page = ReferenceEquals(chosen, _helpEnvironmentTab) ? HelpPage.Environment
                 : ReferenceEquals(chosen, _helpDoctorTab) ? HelpPage.Doctor
                 : ReferenceEquals(chosen, _helpLookTab) ? HelpPage.Look
                 : HelpPage.Keys;

        if (page == State.HelpPage)
        {
            return;
        }

        State = State with { HelpPage = page };
        Render();
    }

    /// <summary>
    /// The help cursor moved, so the model learns what it is over.
    /// </summary>
    /// <remarks>
    /// <b>It renders nothing.</b> A hint line is all that changes, and
    /// re-rendering the tree from inside its own selection event is how a
    /// cursor ends up fighting the thing that moved it.
    /// </remarks>
    private void OnHelpCursorMoved(object? sender, SelectionChangedEventArgs<HelpNode> args)
    {
        var over = args.NewValue is { Group: true } group ? group.Mode : (UiMode?)null;

        if (over == State.HelpFold)
        {
            return;
        }

        State = State with { HelpFold = over };
        _hints.Text = Keymap.HintsHere(Context());
        _hintsStanding.Text = Keymap.HintsStanding(Context());
        Counting();
    }

    /// <summary>
    /// Puts the help widgets where the model says they are.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>One direction only.</b> The tab follows <c>HelpPage</c> and the tree
    /// follows <c>HelpFolds</c>; neither is asked what it thinks. The sync flag
    /// stops the assignment answering its own event, as the main bar does it.
    /// </para>
    /// <para>
    /// <b>The tree is rebuilt rather than mutated.</b> Its rows come from one
    /// pure function of the state, so there is no second place a group could
    /// exist or a fold could be open - and a rebuilt tree lands on the same
    /// folds because the folds are not the tree's.
    /// </para>
    /// </remarks>
    private void RenderHelp()
    {
        _syncing = true;

        try
        {
            var showing = State.HelpPage switch
            {
                HelpPage.Environment => _helpEnvironmentTab,
                HelpPage.Doctor => _helpDoctorTab,
                HelpPage.Look => _helpLookTab,
                _ => _helpKeysTab,
            };

            if (!ReferenceEquals(_helpTabs.Value, showing))
            {
                _helpTabs.Value = showing;
            }

        }
        finally
        {
            _syncing = false;
        }

        // THE TITLE SAYS WHICH PAGE, because it used to say "Keys" whichever
        // one was showing - a constant from when the modal had one page, and a
        // label that names the wrong thing is worse than none.
        _modal.Title = $"help — {HelpPages.Title(State.HelpPage)}";

        FillHelpPages();

        // BUILT ONCE, NOT EVERY SECOND. Render runs on a one-second timer for
        // the refresh countdown, and ClearObjects/AddObjects hands the tree a
        // fresh set of nodes each time - so whatever a person had selected is
        // no longer in the tree and the cursor springs back to the top. That
        // is the rule RenderModalButtons already states one method along: do
        // the work when the thing is different, not when something asked.
        //
        // The keys themselves never change at runtime: Keymap.Catalogue() is
        // static. What changes is which groups are open, and that is expand
        // and collapse below rather than a rebuild.
        _helpGroups ??= HelpTree.Keys()
            .Select(group => new HelpNode
            {
                Text = group.Heading,
                Mode = group.Mode,
                Group = true,
                Keys =
                [
                    .. group.Keys.Select(key => new HelpNode
                    {
                        Text = key.When is { Length: > 0 } when
                            ? $"{key.Name,-8}{key.Description}   ({when})"
                            : $"{key.Name,-8}{key.Description}",
                        Mode = group.Mode,
                        Group = false,
                    }),
                ],
            })
            .ToList();

        if (_helpKeys.Objects?.Any() is not true)
        {
            _helpKeys.SelectionChanged += OnHelpCursorMoved;
            _helpKeys.TreeBuilder = new HelpBranches();
            _helpKeys.AddObjects(_helpGroups);
        }

        foreach (var group in _helpGroups)
        {
            if (HelpTree.IsOpen(State, group.Mode))
            {
                _helpKeys.Expand(group);
            }
            else
            {
                _helpKeys.Collapse(group);
            }
        }

        // GIVE THE TREE A CURSOR, because it opens without one. SelectedObject
        // is null until something selects and SelectionChanged does not fire
        // for standing still, so the fold key worked and was advertised nowhere
        // until somebody pressed an arrow - worse than a key that is missing.
        if (_helpKeys.SelectedObject is null && _helpGroups.Count > 0)
        {
            _helpKeys.SelectedObject = _helpGroups[0];
        }

        var over = _helpKeys.SelectedObject is { Group: true } group_
            ? group_.Mode
            : (UiMode?)null;

        if (over != State.HelpFold)
        {
            State = State with { HelpFold = over };
        }
    }

    /// <summary>
    /// The two text pages, wrapped to their own width.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Only when the lines change</b>, because setting a list's source sends
    /// the cursor back to the top and <c>Render</c> runs once a second for the
    /// countdown - so an unguarded page can be scrolled for at most a second.
    /// That is not hypothetical: it is what the tree beside these did on the
    /// first cut of this modal, reported in one press.
    /// </para>
    /// <para>
    /// <b>Both, not just the one showing.</b> A hidden tab has a viewport too,
    /// and filling it here means switching pages shows the page rather than a
    /// blank that fills in on the next tick.
    /// </para>
    /// </remarks>
    private void FillHelpPages()
    {
        Fill(_helpEnvironment, ref _helpEnvironmentShowing, HelpPage.Environment);
        Fill(_helpDoctor, ref _helpDoctorShowing, HelpPage.Doctor);

        FillLookPage();

        void Fill(ListView list, ref IReadOnlyList<string>? showing, HelpPage page)
        {
            var lines = PaneText.HelpPageLines(State, page, CollectionViews.TextWidth(list));

            if (showing is not null && showing.SequenceEqual(lines))
            {
                return;
            }

            showing = lines;
            list.SetSource(new ObservableCollection<string>(lines));
        }
    }

    /// <summary>
    /// The Look page: the settings table, and the sentence about the row under
    /// the cursor.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Its own method rather than a line in <see cref="FillHelpPages"/>,</b>
    /// because that one declares a local <c>Fill</c> for the two pages that are
    /// lines and it shadows the table's. The compiler said so, which is the
    /// only reason anybody would have noticed.
    /// </para>
    /// <para>
    /// <b>Held in the sync flag</b>: filling a table raises its own selection
    /// event, which is also how a click arrives - so without it the fill
    /// answers itself and moves the cursor a person did not move.
    /// </para>
    /// </remarks>
    private void FillLookPage()
    {
        _syncing = true;

        try
        {
            Fill(
                _helpLook,
                null,
                Looks.Rows(State.Look),
                Looks.Columns,
                State.Look.Selected,
                row => [row.Setting, row.Value, row.Changed]);
        }
        finally
        {
            _syncing = false;
        }

        _helpLookAbout.Text = Looks.About(Looks.Under(State.Look));
    }

    /// <summary>A page changed width, so its lines have to be broken again.</summary>
    private void OnHelpPageResized(object? sender, EventArgs args)
    {
        if (State.Mode is not UiMode.Help)
        {
            return;
        }

        FillHelpPages();
    }

    /// <summary>Puts the keyboard on the page a person is reading.</summary>
    /// <remarks>
    /// <b>Without this the modal has no cursor at all</b> - arrows do nothing,
    /// because a Dialog's focus sits on the dialog and every page is a subview
    /// of a tab. Each page is quiet, so the keys it does not use still reach
    /// the keymap.
    /// </remarks>
    private void FocusPage() => Showing().SetFocus();

    /// <summary>The content of the help page that is showing.</summary>
    private View Showing() => State.HelpPage switch
    {
        HelpPage.Environment => _helpEnvironment,
        HelpPage.Doctor => _helpDoctor,

        // THE TABLE, because it is the page. The sentence below it is read
        // rather than driven, and focus there would leave the arrows moving
        // nothing - the defect every widget page here has had once.
        HelpPage.Look => _helpLook,
        _ => _helpKeys,
    };

    /// <summary>How the tree finds a group's keys. One answer, from the node.</summary>
    private sealed class HelpBranches : ITreeBuilder<HelpNode>
    {
        public bool SupportsCanExpand => true;

        public bool CanExpand(HelpNode node) => node?.Keys.Count > 0;

        public IEnumerable<HelpNode> GetChildren(HelpNode node) =>
            node?.Keys ?? (IEnumerable<HelpNode>)[];
    }

    private void OnRunnerViewChanged(object? sender, ValueChangedEventArgs<View?> args)
    {
        if (_syncing || args.NewValue is not { } chosen)
        {
            return;
        }

        var picked = _runnerViewTabbed
            .FirstOrDefault(t => ReferenceEquals(t.Pane, chosen));

        if (picked.Pane is null || picked.View == State.RunnerView)
        {
            return;
        }

        State = State with { RunnerView = picked.View };
        Render();
    }

    private void OnTabChanged(object? sender, ValueChangedEventArgs<View?> args)
    {
        if (_syncing || args.NewValue is not { } chosen)
        {
            return;
        }

        var tab = _tabbed.FirstOrDefault(t => ReferenceEquals(t.Pane, chosen)).Tab;

        if (tab == State.ActiveTab)
        {
            return;
        }

        if (Tabs.CommandFor(tab) is not { } command)
        {
            State = State with { ActiveTab = tab };
            Render();
            return;
        }

        // THE SHELL'S, AND THAT IS THE WHOLE QUESTION AGAIN. It briefly had a
        // second half - whether a reader was running yet - because the press
        // that had to START one was the shell's. The spawn folds in now too:
        // measured, it places no credential and holds no stream of the
        // terminal, and the exception allowing it is scoped in
        // LiveStreamingTests beside the clipboard's.
        if (ShellCommands.Handled.Contains(command))
        {
            ExitCommand = command;
            _app.RequestStop(this);
            return;
        }

        State = Reducer.Reduce(State, command);

        // AND THE READ IT WANTED. One method, because a click arrives here
        // and a keystroke arrives at Dispatch, and a second copy of this is a
        // second place for one of them to be forgotten - which is exactly
        // what happened: this block existed here only, so pressing the key
        // that fetches the airspace asked nobody anything.
        Asked(command);

        Render();
    }

    /// <summary>
    /// Starts the read this command wanted, if it wanted one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>ONE METHOD, TWO CALLERS, and it was one caller for as long as the
    /// set existed.</b> A command in <see cref="ShellCommands.Reads"/> can
    /// arrive by a click on the tab bar or by its key, and the key's path did
    /// not start a read — so <c>ConsoleEstate.Read</c>, the only thing that
    /// fills the airspace tab's documents, was reachable by neither the shell
    /// (the command is not the shell's) nor the screen.
    /// </para>
    /// <para>
    /// <b>TOLD, NOT POLLED.</b> Terminal.Gui's guidance is that all UI work
    /// happens on the main thread and a background result reaches it through
    /// <c>Invoke</c>, so the read says when it has landed and this hands the
    /// fold back to the thread allowed to draw. The first version asked every
    /// hundred and twenty milliseconds whether it had finished, which is a
    /// timer spinning for something that can simply say so, and up to that
    /// long late when it had.
    /// </para>
    /// </remarks>
    private void Asked(Command command)
    {
        if (_reads is null || !ShellCommands.Reads.Contains(command))
        {
            return;
        }

        _reads.Start(command, State, () => _app.Invoke(() =>
        {
            var advanced = _reads.Advance(State);

            if (ReferenceEquals(advanced, State)
                && advanced.ReadInFlight == State.ReadInFlight)
            {
                return;
            }

            State = advanced;
            Render();
        }));
    }

    /// <summary>
    /// Up from the top of the runners table reaches the button above it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>On the table rather than on the screen, because the tab bar is in
    /// between.</b> A key the table declines bubbles through the pane to
    /// <c>Tabs</c>, which answers an up arrow by moving to the previous tab -
    /// so a handler on the root saw the key only after the bar had already
    /// acted on it. Measured: pressing up on the fleet landed on Repositories.
    /// </para>
    /// <para>
    /// <b>Which means asking the cursor rather than being told.</b> This runs
    /// before the table's own bindings, so "there is nowhere to go" has to be
    /// checked here instead of inferred from the table declining the key.
    /// </para>
    /// <para>
    /// Focus is the view's, for the reason the arrows already move a table's
    /// cursor without the keymap knowing: what a widget does with the screen is
    /// not what a key MEANS, and <c>Keymap</c> stays the only place a printable
    /// key means anything.
    /// </para>
    /// </remarks>
    /// <summary>The two keys the field does not get to keep.</summary>
    /// <remarks>
    /// <b>Read out of the widget here, at the moment it is committed.</b>
    /// <c>Command</c> is a parameterless enum, so a per-keystroke reduce
    /// would need it to carry a string - a change to this console's central
    /// dispatch type for one field. What matters holds either way: the value
    /// is in the model before the session ends, and the file is written
    /// after it.
    /// </remarks>
    /// <summary>
    /// The find field's own two keys, and everything else typed into it.
    /// </summary>
    /// <remarks>
    /// <c>OnAirspacePathKeyDown</c>'s shape and its reasons: while the question
    /// is not open every key is the tab's, and enter is intercepted here
    /// because whether a focused field lets it bubble is a Terminal.Gui
    /// behaviour this console has been wrong about before.
    /// </remarks>
    private void OnBrowseFindKeyDown(object? sender, Key key)
    {
        if (State.Mode != UiMode.BrowseFind)
        {
            if (Keymap.Resolve(KeyTranslator.Translate(key), Context()) is { } command)
            {
                Dispatch(command);
            }

            key.Handled = true;
            return;
        }

        if (key == Key.Enter)
        {
            // WHAT WAS TYPED, INTO THE MODEL, BEFORE THE READ. Command is a
            // parameterless enum, so the field's text has to be somewhere the
            // read can find it - the airspace path's rule.
            State = Reducer.BrowseFindTyped(State, _browseFind.Text);
            key.Handled = true;
            Dispatch(Command.GoToOrFind);
            return;
        }

        if (key == Key.Esc)
        {
            key.Handled = true;
            Dispatch(Command.CloseModal);
        }
    }

    private void OnAirspacePathKeyDown(object? sender, Key key)
    {
        // WHILE IT IS NOT BEING EDITED, ITS KEYS ARE THE TAB'S. A focused
        // TextField consumes printable keys - that is what ate all twenty-one
        // of them through TableView's type-to-search - so holding focus without
        // editing means handing every key back to the one keymap rather than
        // keeping it. Silence here would make `p` insert a character instead of
        // pulling, and nothing would say so.
        if (State.Mode != UiMode.AirspacePath)
        {
            if (Keymap.Resolve(KeyTranslator.Translate(key), Context()) is { } command)
            {
                key.Handled = true;
                Dispatch(command);
            }
            else
            {
                // AND ANYTHING THE KEYMAP DOES NOT ANSWER IS SWALLOWED RATHER
                // THAN TYPED. The field is read-only in this state, so a letter
                // would be dropped anyway - marking it handled is what stops it
                // reaching the screen and being resolved a second time.
                key.Handled = true;
            }

            return;
        }

        if (key == Key.Enter)
        {
            State = State with { AirspacePathTyped = _airspacePath.Text };
            key.Handled = true;
            Dispatch(Command.SetAirspacePath);
            return;
        }

        // PERFORMED HERE RATHER THAN DISPATCHED, because what these change is
        // the widget's in-progress text and the model cannot hold that: Command
        // is a parameterless enum. The keymap still owns the bindings, which is
        // the split Dispatch describes - the keymap says what a key means, and
        // this is what happens once something means it. Both are declared in
        // ShellCommands.OnTheWidget, or they would read as keys that do
        // nothing.
        switch (Keymap.Resolve(KeyTranslator.Translate(key), Context()))
        {
            case Command.AirspacePathFromCwd:
                _airspacePath.Text = State.Cwd;
                key.Handled = true;
                return;

            case Command.AirspacePathFromClipboard:
                Paste();
                key.Handled = true;
                return;

            case Command.AirspacePathFromDialog:
                Browse();
                key.Handled = true;
                return;
        }

        if (key == Key.Esc)
        {
            key.Handled = true;
            Dispatch(Command.CloseModal);
        }
    }

    /// <summary>
    /// Runs a directory picker and puts what it chose into the airspace field.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A DIRECTORY, NOT A FILE, and that is the whole configuration.</b> An
    /// airspace is a directory — <c>AirspaceTree.Write</c> renders
    /// <c>airspace/</c> into it. <c>OpenMode</c> defaults to <c>Mixed</c>, so a
    /// dialog left alone would let somebody pick a file, the path would be
    /// stored without complaint, and the next pull would render a tree beside
    /// that file rather than in a directory anybody meant.
    /// </para>
    /// <para>
    /// <b><c>MustExist</c> stays false, which is what the field beside it
    /// does.</b> The order a person works in is set-then-pull, and the render
    /// creates the tree — so a picker that could only name what already
    /// exists would be narrower than typing, in the one case where typing is
    /// most tedious.
    /// </para>
    /// <para>
    /// <b>A SECOND EXCEPTION TO THE SESSION RULE, narrower than the
    /// clipboard's.</b> It reads directory listings a person walks, which is
    /// more than the local file whose path the console already holds that
    /// <c>LiveStreamingTests</c> scopes a session's read to. It spawns nothing
    /// and reaches no network, which is why it is the cheaper of this field's
    /// two exceptions — and it is recorded in the same place rather than
    /// left to be inferred from the other one.
    /// </para>
    /// <para>
    /// <b>Cancelled says nothing.</b> Somebody who opened a picker and thought
    /// better of it has told gg nothing, and a sentence about it would be a
    /// report on a decision not taken.
    /// </para>
    /// </remarks>
    private void Browse()
    {
        using var picker = new FileDialog
        {
            Title = "airspace",

            // A DIRECTORY, because that is what an airspace is. The default is
            // Mixed, which would accept a file.
            OpenMode = OpenMode.Directory,
            AllowsMultipleSelection = false,

            // NOT REQUIRED TO EXIST, for the field's own reason: set-then-pull
            // is the order, and the render is what creates the tree.
            MustExist = false,
        };

        // STARTED WHERE THE ANSWER PROBABLY IS - what is configured now, or
        // failing that the directory gg was launched from. A picker that opens
        // somewhere else makes a person walk back to where they were already
        // standing. Empty is left alone, because an empty path is not a
        // starting point.
        var start = _airspacePath.Text is { Length: > 0 } showing ? showing : State.Cwd;

        if (start is { Length: > 0 })
        {
            picker.Path = start;
        }

        _app.Run(picker);

        // CANCELLED IS NOT AN ANSWER AND NOT A FAILURE. Canceled is true when
        // the dialog produced no result; the path is checked as well, because a
        // dialog that accepted an empty box would otherwise blank the field.
        if (picker.Canceled || picker.Path is not { Length: > 0 } chosen)
        {
            return;
        }

        _airspacePath.Text = chosen;
    }

    /// <summary>
    /// Puts the clipboard into the airspace field, or says why it could not.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A STATED EXCEPTION TO THE SESSION RULE.</b> A UI session may read a
    /// local file and nothing else, and on every platform this ships to a
    /// clipboard is a child process — <c>ConsoleLink</c> says exactly that
    /// about its own copy: <i>"Both spawn a child, which is why neither is a
    /// session's."</i> This one is allowed anyway, deliberately: a path is most
    /// often already on the clipboard, and a terminal-release round trip in the
    /// middle of editing one field would throw away what is typed in it.
    /// <c>LiveStreamingTests</c> records the exception where the rule is
    /// enforced, so the guard is not quietly satisfied by the spawn happening
    /// one layer down inside Terminal.Gui.
    /// </para>
    /// <para>
    /// <b>Through the driver, so it is cross-platform once rather than three
    /// times.</b> <c>IClipboard</c> is what Terminal.Gui already implements per
    /// platform; rolling a second pbpaste/xclip/PowerShell switch here would be
    /// a copy of that to keep in agreement, and this console has no business
    /// knowing which one it is on.
    /// </para>
    /// <para>
    /// <b>A clipboard with nothing in it, and a platform with no clipboard at
    /// all, are different answers and both are said.</b> Silence is the one
    /// response that reads as a key that does not work — which is what a person
    /// concludes about the whole feature, not about their clipboard.
    /// </para>
    /// </remarks>
    private void Paste()
    {
        // ABSENT AND UNSUPPORTED ARE THE SAME FACT to a person: there is no
        // clipboard here. The driver's is nullable, and treating null as
        // "supported" would be a NullReferenceException in place of a sentence.
        if (_app.Clipboard is not { IsSupported: true } clipboard)
        {
            State = State with
            {
                LastEstate = "There is no clipboard gg can read on this platform. "
                           + "Type the path, or ctrl-d for this directory.",
            };

            return;
        }

        // TRY RATHER THAN GET, because the throwing form turns an empty
        // clipboard and a missing helper into the same exception - and one of
        // those is a person's mistake while the other is a machine's setup.
        if (!clipboard.TryGetClipboardData(out var pasted)
            || pasted is not { Length: > 0 })
        {
            State = State with
            {
                LastEstate = "The clipboard is empty, so nothing was pasted.",
            };

            return;
        }

        // ONE LINE, because a path is one and a clipboard holding a document
        // would otherwise fill a single-line field with the first of it and
        // silently drop the rest. Taking the first line says what happened by
        // showing it.
        _airspacePath.Text = pasted
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault()
            ?.Trim() ?? "";
    }

    private void OnTableKeyDown(object? sender, Key key)
    {
        if (key != Key.CursorUp
            || !_runnerStart.Visible
            || _runnersTable.Value?.SelectedCell.Y is not 0)
        {
            return;
        }

        _runnerStart.SetFocus();
        key.Handled = true;
    }

    /// <summary>
    /// Pressing on past a table's last row holds the cursor there and blinks
    /// it; three deliberate taps leave.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What this stops.</b> A key a table declines walks up the focused
    /// chain to the tab bar, which is a plain <c>Tabs</c> with the library's
    /// own arrow bindings - so pressing past the end of a list quietly left
    /// it. Measured on the flights tab: a hundred and thirty presses ended on
    /// Repositories.
    /// </para>
    /// <para>
    /// <b>And what it costs when a list is paged.</b> Reaching the last row
    /// asks for the next page; changing tab asks for the new tab's read, and
    /// <c>BackgroundReads</c> keeps one read at a time - so the page is
    /// abandoned and the list stays short. Asking again needs the cursor moved
    /// off the last row and back, because <c>Reducer.WantsMore</c> is only
    /// consulted when the cursor moves.
    /// </para>
    /// <para>
    /// <b>On the table and asking the cursor</b>, for the reason
    /// <see cref="OnTableKeyDown"/> gives one method up: <c>KeyDown</c> runs
    /// before the table's own bindings, so "there is nowhere to go" has to be
    /// checked here rather than inferred from the table declining the key.
    /// </para>
    /// <para>
    /// <b>The clock is read here.</b> A hold and a tap differ only in how fast
    /// they arrive, and there is no key-up event in a terminal. The decision
    /// itself is <see cref="TableEdge.Pressed"/>, which takes the time as an
    /// argument and is tested without one of these.
    /// </para>
    /// </remarks>
    private void OnTableEdge(object? sender, Key key)
    {
        // ALREADY ANSWERED. The fleet's own handler sends up-from-row-zero to
        // the button above the table, which is a move inside the pane rather
        // than the escape this method exists to stop.
        if (key.Handled || sender is not TableView table)
        {
            return;
        }

        var down = key == Key.CursorDown;

        if (!down && key != Key.CursorUp)
        {
            return;
        }

        var rows = table.Table?.Rows ?? 0;
        var row = table.Value?.SelectedCell.Y ?? 0;

        if (rows is 0 || (down ? row < rows - 1 : row > 0))
        {
            // SOMEWHERE TO GO, so this is not a press against an edge at all.
            // Forgotten rather than kept: three taps are three taps AT an edge,
            // and a walk back up the list must not leave two of them banked.
            _edge = EdgePresses.None;
            return;
        }

        var (now, leaves) = TableEdge.Pressed(_edge, DateTimeOffset.UtcNow);
        _edge = now;

        if (leaves)
        {
            Unblink();

            // ONTO THE TAB IT IS ALREADY ON, and no further. Letting the key
            // through sent it to the bar, whose own binding answers a down
            // arrow by selecting the NEXT tab - so leaving a table jumped a
            // person somewhere they had not asked to go, which is most of what
            // made the old behaviour a surprise.
            //
            // The strip holds focus per tab on that tab's border title - which
            // is how the bar's own SelectNextTab moves - so focusing the
            // CURRENT tab's title is "out of the table and onto the tab",
            // leaving the next press to move tabs if that is what somebody
            // wants.
            var strip = (_bar.Value?.Border.View as Terminal.Gui.ViewBase.BorderView)?.TitleView;

            if (strip?.SetFocus() is true)
            {
                key.Handled = true;
            }

            return;
        }

        key.Handled = true;
        _edgeAt = table;
        Blink();
    }

    /// <summary>
    /// Blink the row until the pressing stops.
    /// </summary>
    /// <remarks>
    /// <b><c>Watch</c>'s idiom, and its rule about stopping.</b> The timer ends
    /// itself the moment nothing is pressing, so a console nobody is holding a
    /// key on has no timer running - which is the same sentence the live pane's
    /// tick has for why it detaches.
    /// </remarks>
    private void Blink()
    {
        if (_edgeBlinking)
        {
            return;
        }

        _edgeBlinking = true;

        // TWICE PER HALF-CYCLE, so each half gets drawn. Sampling once per half
        // relies on the timer firing exactly on the phase boundary, and a
        // timeout that lands a few milliseconds late draws the same half twice
        // and skips the other - which at this speed is the difference between a
        // flash and a flicker that sometimes is not there.
        _app.AddTimeout(TableEdge.HalfABlink / 2, () =>
        {
            var now = DateTimeOffset.UtcNow;

            if (_edgeAt is not { } table || !TableEdge.Blinks(_edge, now))
            {
                Unblink();
                return false;
            }

            Lit(table, TableEdge.Lit(_edge, now));
            table.SetNeedsDraw();

            return true;
        });
    }

    /// <summary>
    /// Show the selection, or hide it for half a blink.
    /// </summary>
    /// <remarks>
    /// <b>Through the table's scheme, which reaches a receding row too.</b> A
    /// table view draws its selected row from <c>Focus</c> or <c>Active</c>, and
    /// <c>LookStyles</c>' row getters answer with a scheme that changes only
    /// <c>Normal</c> - so an ended flight inherits this and blinks like every
    /// other row. Flattening the selection to the plain attribute is what makes
    /// the blink: the cursor goes away and comes back.
    /// </remarks>
    private void Lit(TableView table, bool lit)
    {
        _edgeWas ??= table.GetScheme();

        if (_edgeWas is not { } was)
        {
            return;
        }

        if (lit)
        {
            table.SetScheme(was);
            return;
        }

        var plain = was.GetAttributeForRole(Terminal.Gui.Drawing.VisualRole.Normal);

        table.SetScheme(new Terminal.Gui.Drawing.Scheme(was)
        {
            Focus = plain,
            HotFocus = plain,
            Active = plain,
            HotActive = plain,
        });
    }

    /// <summary>Put the selection back exactly as it was found.</summary>
    private void Unblink()
    {
        _edgeBlinking = false;

        if (_edgeAt is { } table && _edgeWas is { } was)
        {
            table.SetScheme(was);
            table.SetNeedsDraw();
        }

        _edgeWas = null;
    }

    /// <summary>And down off the button goes back to what it is about.</summary>
    private void OnButtonKeyDown(object? sender, Key key)
    {
        if (key != Key.CursorDown)
        {
            return;
        }

        (_runnersTable.Visible ? (View)_runnersTable : _runners).SetFocus();
        key.Handled = true;
    }

    /// <summary>A button per answer the open modal has.</summary>
    /// <remarks>
    /// <b>From <see cref="Keymap.Buttons"/>, which answers with nothing until a
    /// mode has labelled every one of its answers.</b> That is the rollout: a
    /// modal joins in when somebody writes its labels, and looks exactly as it
    /// does today until then - rather than growing a half-set of buttons that
    /// hides the options nobody got round to naming.
    /// </remarks>
    private void RenderModalButtons()
    {
        var wanted = Keymap.Buttons(Context());

        // ONLY WHEN THEY ACTUALLY CHANGE, and this is the half that was broken.
        // Render runs on a one-second timer for the refresh countdown, and
        // rebuilding the buttons every time destroys the view a person had
        // moved focus to - so focus jumped back once a second and tab looked
        // like it did nothing. The same rule the pty host learned about
        // resizing: do the work when the thing is different, not when something
        // asked.
        if (_modalButtons.Count == wanted.Count
            && _modalButtons.Zip(wanted).All(p => p.First.Text == p.Second.Label))
        {
            return;
        }

        foreach (var old in _modalButtons)
        {
            _modal.Remove(old);
            old.Dispose();
        }

        _modalButtons.Clear();

        foreach (var binding in wanted)
        {
            var button = new Button { Text = binding.Label! };

            // THE SAME PATH A KEYSTROKE TAKES - the command itself, handed to
            // the one place that acts on one. Not a copy of what it does.
            var command = binding.Command;
            button.Accepting += (_, e) =>
            {
                e.Handled = true;
                Dispatch(command);
            };

            _modalButtons.Add(button);
            _modal.AddButton(button);
        }

        // AND FOCUS STARTS SOMEWHERE A PERSON CHOSE. Left to itself the frame
        // focused whichever child it liked - which was the SECOND button, so
        // enter pressed by reflex composed with an agent rather than opening an
        // editor. The first answer is the one to land on, and the first answer
        // is the least consequential by the order they are declared in.
        if (_modalButtons.Count > 0)
        {
            _modalButtons[0].SetFocus();
        }

        // AND THE MARKS GO WITH IT. A dialog draws arrows around one button to
        // say "this is what enter does"; AddButton puts them on the last button
        // added and enter presses the focused one, so the two disagreed the
        // moment a modal had two answers. On the runner's the marked one was
        // `Shut down' while enter restarted.
        //
        // SET ON EVERY BUTTON, not just the one. Marking a button is not a radio
        // button - the marks left by AddButton stay until something says false -
        // so a loop rather than an assignment.
        for (var i = 0; i < _modalButtons.Count; i++)
        {
            _modalButtons[i].IsDefault = i == 0;
        }
    }

    /// <summary>
    /// The modal's own keys, taken before the dialog can mean something else by them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The same keymap and the same dispatch as the screen's handler</b> -
    /// deliberately, because what a key MEANS has one authority and this is
    /// only about which view hears it first.
    /// </para>
    /// <para>
    /// <b>Unresolved keys are left alone, unlike the airspace field's.</b> The
    /// tables inside the flight and runner modals move their own cursors with
    /// the arrows and the keymap binds none of those; swallowing here would
    /// take the arrows off every list in a modal.
    /// </para>
    /// </remarks>
    private void OnModalKeyDown(object? sender, Key key)
    {
        if (Keymap.Resolve(KeyTranslator.Translate(key), Context()) is not { } command)
        {
            return;
        }

        key.Handled = true;
        Dispatch(command);
    }

    private void OnScreenKeyDown(object? sender, Key key)
    {
        var stroke = KeyTranslator.Translate(key);
        var command = Keymap.Resolve(stroke, Context());

        if (command is null)
        {
            // AND A KEY IT DECLINED MAY STILL HAVE TO BE TAKEN. Handed on, a
            // key reaches Terminal.Gui's own meanings - escape's is to stop the
            // runnable, which ended gg on one keystroke, silently.
            key.Handled = Keymap.Swallowed(stroke, Context());
            return;
        }

        key.Handled = true;
        Dispatch(command.Value);
    }

    /// <summary>Act on a command, whatever produced it.</summary>
    /// <remarks>
    /// <b>One place, because there is now more than one input.</b> A keystroke
    /// and a button press are the same decision arriving by two routes, and two
    /// copies of "is this the shell's or the reducer's" would be two places to
    /// get that split wrong. <see cref="Keymap"/> stays the authority on what a
    /// KEY means; this is what happens once something means it.
    /// </remarks>
    private void Dispatch(Command command)
    {
        // WHAT A PERSON ACTUALLY FEELS. Everything else here measures work;
        // this measures the wait between asking for something and the console
        // having done it - which is the number somebody means by "laggy".
        using var acted = Gg.Local.Timings.Active.Measure($"input.{command}");

        // ONE DECLARATION, READ HERE. A literal list is what this was, and it
        // silently excluded four commands the shell already had arms for.
        // THE SHELL'S, AND THAT IS THE WHOLE QUESTION AGAIN. It briefly had a
        // second half - whether a reader was running yet - because the press
        // that had to START one was the shell's. The spawn folds in now too:
        // measured, it places no credential and holds no stream of the
        // terminal, and the exception allowing it is scoped in
        // LiveStreamingTests beside the clipboard's.
        if (ShellCommands.Handled.Contains(command))
        {
            ExitCommand = command;
            _app.RequestStop(this);
            return;
        }

        State = Reducer.Reduce(State, command);

        // THE READ A KEY ASKED FOR. Reduced first, so the pane is already
        // open and saying the read is coming rather than saying there is
        // nothing - and started here because the reducer cannot and the shell
        // no longer sees a command in Reads at all.
        Asked(command);

        Render();
    }

    private void OnQueueSelectionChanged(object? sender, ValueChangedEventArgs<int?> args)
    {
        // The list is an input device here, not a second store: the model
        // decides what is selected and the view reports what was clicked. What
        // a change MEANS is QueueSelection's, because a redraw raises this event
        // twice and neither raise is a person.
        if (QueueSelection.Wanted(args.NewValue, State.SelectedRow) is { } command)
        {
            State = Reducer.Reduce(State, command);
            Render();
        }
    }

    /// <summary>
    /// What the keymap is dispatching on, derived rather than restated.
    /// </summary>
    /// <remarks>
    /// This was a literal beside the model, and it did not carry
    /// <c>Takeable</c>, <c>HandedBackable</c> or which step the sign-in modal
    /// is on - so the help page could name a key this would not resolve. One
    /// derivation, read here and by the tests, is what stops the advertised
    /// keys and the live ones drifting - the same argument
    /// <c>ShellCommands.Handled</c> already carries one type over.
    /// </remarks>
    private KeymapContext Context() => KeymapContext.For(State);

    /// <summary>One-way: model in, pixels out.</summary>
    /// <summary>What the screen currently reflects, so a freeze is entered once.</summary>
    /// <remarks>
    /// Not the model's <c>Frozen</c>, which says what was ASKED for. This says
    /// whether the pixels have already been stopped - and the difference is the
    /// one paint in between, the one that puts the sentence up.
    /// </remarks>
    private bool _stopped;

    /// <summary>
    /// Whether gg is currently receiving mouse events, as last set.
    /// </summary>
    /// <remarks>
    /// <b>So the sequences are written on the change rather than on every
    /// paint.</b> Render runs once a second for the countdown, and three escape
    /// sequences a second is a terminal being told something it already knows.
    /// </remarks>
    private bool _mouseIsOurs = true;

    /// <summary>When the last paint began, so the next one can say the period.</summary>
    /// <remarks>
    /// <b>The measurement that was missing.</b> Every other number here is the
    /// duration of something this file chose to wrap, and a console frozen
    /// BETWEEN two one-millisecond renders reads as two one-millisecond
    /// renders. Start to start catches that: whatever holds the loop up -
    /// Terminal.Gui's own draw, which happens after Render returns, input
    /// handling, or a thread pool with nothing free - lands in this number
    /// even though nothing here wraps it.
    /// </remarks>
    private long _lastPaintBeganAt;

    private void Render()
    {
        if (Gg.Local.Timings.Active.Asked && _lastPaintBeganAt != 0)
        {
            Gg.Local.Timings.Active.Took(
                "paint.period",
                System.Diagnostics.Stopwatch.GetElapsedTime(_lastPaintBeganAt));
        }

        // WHAT A PAINT COSTS, when somebody set GG_TIMING. A render makes no
        // requests - which is why it reports no count - and the reason to
        // measure it anyway is that a console reported as unresponsive to
        // CLICKING is being slow between the reads rather than inside them.
        //
        // This is a file append and not a read of anything: the scan in
        // LiveStreamingTests forbids a network call, a child process and a
        // credential here, and this is none of the three.
        using var painted = Gg.Local.Timings.Active.Measure($"render.{State.ActiveTab}");

        // THE MARK, WHEN THIS TAB HAS NOTHING YET. Tabs.HasRead asked the other
        // way round - see LoadingArt.Waiting, which is deliberately the only
        // place that asks, so a pane cannot breathe over a table that arrived.
        var waiting = LoadingArt.Waiting(State);

        if (waiting)
        {
            // THE TEXT ONLY ONCE. The shape does not change - the light on it
            // does - so re-assigning it forty times a breath would be the
            // Label-layout cost this console just spent an evening removing.
            if (_waiting.Text.Length == 0)
            {
                _waiting.Text = string.Join('\n', LoadingArt.Mark);
            }

            _waiting.SetScheme(ConsoleTheme.Waiting(LoadingArt.Glow(State.LoadingPulse)));
        }

        if (_waiting.Visible != waiting)
        {
            _waiting.Visible = waiting;
        }

        // WHAT THE RUNTIME LOOKS LIKE AT THIS PAINT. A loop that wakes every
        // seven seconds with 2ms of work to show for it is not slow - it is not
        // being let run, and these say by what. A pool with no worker free is
        // the shape that starts fast and degrades, because this console blocks
        // pool threads on reads and abandons them rather than cancelling.
        if (Gg.Local.Timings.Active.Asked)
        {
            System.Threading.ThreadPool.GetAvailableThreads(out var workers, out var io);
            Gg.Local.Timings.Active.Count("pool.workers-free", workers);
            Gg.Local.Timings.Active.Count("pool.io-free", io);
            Gg.Local.Timings.Active.Count("pool.threads", System.Threading.ThreadPool.ThreadCount);
            Gg.Local.Timings.Active.Count(
                "pool.queued", System.Threading.ThreadPool.PendingWorkItemCount);
            Gg.Local.Timings.Active.Count("mem.mb", GC.GetTotalMemory(false) / 1_048_576);
            Gg.Local.Timings.Active.Count("gc.gen2", GC.CollectionCount(2));
        }

        // START TO START, so an early return below still leaves a usable
        // stamp - and so the number includes whatever happens after Render
        // hands back, which is where the actual drawing is.
        _lastPaintBeganAt = System.Diagnostics.Stopwatch.GetTimestamp();


        // THE PIXELS STOP, AND THE MOUSE GOES BACK. One paint happens after the
        // key - the one carrying "frozen" on the activity line - and then
        // nothing, because a repaint under a selection is what takes it away.
        //
        // The model keeps moving underneath: reads land, the tail banks its
        // lines, and the first paint after the thaw shows all of it.
        if (State.Frozen)
        {
            if (_stopped)
            {
                return;
            }

            _stopped = true;
        }
        else if (_stopped)
        {
            _stopped = false;
        }

        _queue.SetSource(new ObservableCollection<string>(PaneText.QueueRows(State)));
        if (State.Queue.Count > 0)
        {
            _queue.SelectedItem = Math.Clamp(State.SelectedRow, 0, State.Queue.Count - 1);
        }

        // ONLY WHEN IT IS ON SCREEN. `_flightPane' is added to the queue tab and
        // to no other, so every paint on any other tab was building this text
        // and handing it to a Label nobody can see - and handing a large string
        // to a Label is what costs: measured at 1,933ms for one assignment on a
        // real tenant, inside a 2,031ms board paint.
        if (State.ActiveTab == TabId.Queue)
        {
            var nothingSelected = State.Queue.Count == 0;

            if (_flightPaneWantsSaying
                || !ReferenceEquals(_flightPaneAbout, State.Flight)
                || !ReferenceEquals(_flightPaneStory, State.Story)
                || !ReferenceEquals(_flightPaneDiagnosis, State.Diagnosis)
                || _flightPaneHadNothingSelected != nothingSelected)
            {
                using (Gg.Local.Timings.Active.Measure("paint.flight-build"))
                {
                    _flightPaneSaid = PaneText.WhatAPaneCanShow(PaneText.Flight(State));
                }

                using (Gg.Local.Timings.Active.Measure("paint.flight-assign"))
                {
                    _flight.Text = _flightPaneSaid;
                }

                _flightPaneAbout = State.Flight;
                _flightPaneStory = State.Story;
                _flightPaneDiagnosis = State.Diagnosis;
                _flightPaneHadNothingSelected = nothingSelected;
                _flightPaneWantsSaying = false;
            }
        }
        else
        {
            _flightPaneWantsSaying = true;
        }

        // Frozen means the pixels stop moving, so the terminal's own selection
        // can survive being made. Held lines are already kept in the model;
        // this is the half of the promise the view owes.
        // THE FLIGHT PANE'S RULE, AND THE SAME SENTENCE: `_livePane' is on the
        // live tab and no other, so handing this Label a tail nobody is looking
        // at buys a layout and nothing else. A tail is the one pane that can be
        // arbitrarily long, which makes it the worst of the three to paint
        // blind.
        if (State.ActiveTab == TabId.Live && !State.Frozen)
        {
            using (Gg.Local.Timings.Active.Measure("paint.live-pane"))
            {
                _live.Text = PaneText.Live(State);
            }
        }

        if (State.ActiveTab == TabId.Browse)
        {
            using (Gg.Local.Timings.Active.Measure("paint.browse-pane"))
            {
                _browse.Text = PaneText.Browse(State);
            }
        }

        // THE TABLE WHEN THERE ARE ROWS, THE SENTENCE WHEN THERE ARE NOT. A
        // header over no rows claims a read succeeded and found nothing, which
        // is one of three things an empty pane can mean - so each pane keeps
        // its own words for the other two.
        _syncing = true;
        try
        {
            using (Gg.Local.Timings.Active.Measure("paint.flights-table"))
            {
                Fill(_flightsTable, _flights, Rows.Flights(State), Rows.FlightColumns,
                    State.FlightSelected,
                    r => [r.Number, r.State, r.Kind, r.Loop, r.Age, r.Work]);
            }

            // SPLIT, because "the board render is slow" is two different
            // findings with two different fixes: deriving the rows is this
            // console's own code, and filling the table is Terminal.Gui
            // measuring every cell it was handed.
            IReadOnlyList<BoardRow> boardRows;

            using (Gg.Local.Timings.Active.Measure("board.rows"))
            {
                boardRows = Rows.Board(State);
            }

            using (Gg.Local.Timings.Active.Measure(
                       "board.fill",
                       reads: Gg.Local.Timings.Active.Asked ? boardRows.Count : null))
            {
                Fill(_boardTable, _board, boardRows, Rows.BoardColumns,
                    State.BoardSelected,
                    r => [r.What, r.Subject, r.For, r.State, r.Kind, r.Since, r.Next, r.Cost]);
            }

            Fill(_browseTable, null, Rows.Browse(State), Rows.BrowseColumns,
                State.BrowseSelected,
                r => [r.Id, r.State, r.Where ?? "", r.Title]);

            // THE LABEL IS THE EMPTY CASE, and passing null left it visible
            // underneath the table - which was invisible while both said the
            // same two columns, and stops being so the moment the sentence
            // says more than the row.
            Fill(_repositoriesTable, _repositories, Rows.Repositories(State), Rows.RepositoryColumns,
                State.RepositorySelected,
                r => [r.Chosen, r.Path, r.Name, r.Provider, r.Credential, r.Ref, r.Narrowings]);


            // OFF THE MODEL, like the other three. This passed a literal 0 and
            // a comment saying nothing here is selectable - true of the model
            // and never true of the widget, so the cursor snapped back to the
            // top on every render under the person moving it.
            Fill(_runnersTable, _runners, Rows.Runners(State), Rows.RunnerColumns,
                State.RunnerSelected,
                Rows.RunnerCells);

            // THE NOTICE LABEL ONLY WHEN THE TABLE IS SHOWING. With no rows the
            // empty-state label already leads with it, and the same sentence
            // twice reads as two problems.
            var notice = PaneText.RunnerNotice(State);
            _runnerNotice.Text = notice;
            _runnerNotice.Visible = notice.Length > 0 && _runnersTable.Visible;
            _runnerStart.Visible = notice.Length > 0;
            _runnersTable.Y = _runnerNotice.Visible ? 2 : 0;

            // INSIDE THE FLAG LIKE THE OTHER FOUR, and it was appended below
            // the finally. Fill raises the table's own ValueChanged, which is
            // indistinguishable from a click - so out there it dispatched a
            // cursor move nobody made on every render and called Render again
            // from inside the render that did it.
            var tree = AirspaceRows.Tree(State);
            var absence = PaneText.AirspaceAbsence(State);

            _airspaceAbsent.Text = absence;
            _airspaceAbsent.Visible = absence.Length > 0;

            Fill(_airspaceTable, null, tree, AirspaceRows.AirspaceColumns,
                State.AirspaceSelected,
                r => [r.Document, r.Basis, r.State]);

            // THE TABLE OR THE SENTENCE, never both and never neither. Fill
            // already hides an empty table; what it cannot know is which of
            // the three absences this is.
            _airspaceTable.Visible = absence.Length == 0 && tree.Count > 0;
            _airspaceTreePane.Visible = _airspaceTable.Visible;

            // AND THE DOCUMENT BESIDE IT. The bar's membership was reconciled
            // at the top of this block, so what is offered is what it holds;
            // a folder row offers nothing and gets the sentence instead.
            var views = AirspaceViews.Offered(State);
            var beside = _airspaceTreePane.Visible;

            _airspaceViews.Visible = beside && views.Count > 0;
            _airspaceNoDocument.Visible = beside && views.Count == 0;

            if (_airspaceNoDocument.Visible)
            {
                _airspaceNoDocument.Text = string.Join(
                    '\n',
                    PaneText.AirspaceDocument(
                        State, _airspaceNoDocument.Viewport.Width));
            }

            if (_viewTabbed.FirstOrDefault(t => t.View == State.AirspaceView).Pane
                    is { } turned
                && _onTheViewBar.Contains(State.AirspaceView))
            {
                if (!ReferenceEquals(_airspaceViews.Value, turned))
                {
                    _airspaceViews.Value = turned;
                }

                FillAirspaceDocument();
            }
        }
        finally
        {
            _syncing = false;
        }
        _allowances.Text = PaneText.ForTab(State, TabId.Allowances);

        // SEEDED FROM THE MODEL WHENEVER THE QUESTION IS NOT OPEN, so
        // arriving on the tab shows the path that is in force - and NOT
        // while it is open, because overwriting the field on a once-a-second
        // render would delete what somebody is typing into it.
        if (State.Mode != UiMode.AirspacePath)
        {
            _airspacePath.Text = PaneText.AirspacePath(State);
        }

        // THE SECOND STAGE, and the only thing that changes between them: the
        // box is focusable throughout and writable only here.
        _airspacePath.ReadOnly = State.Mode != UiMode.AirspacePath;

        // THE FIELD IS THERE WHILE THE QUESTION IS, and gone otherwise: a box
        // sitting empty under every listing is three rows of the pane spent on
        // a question nobody asked.
        _browseFindBox.Visible = State.Mode == UiMode.BrowseFind;
        _browseFind.ReadOnly = State.Mode != UiMode.BrowseFind;

        if (State.Mode != UiMode.BrowseFind)
        {
            _browseFind.Text = "";
        }

        // THE TITLE CARRIES WHAT THE PATH ALONE CANNOT SAY: that nothing is set,
        // that git cannot see it, or that this is the moment to type. A box
        // reading only a path leaves the two states a person acts on looking
        // identical to the one they do not.
        _airspacePathBox.Title = PaneText.AirspaceBox(State);

        _flights.Text = PaneText.Flights(State);
        _board.Text = PaneText.Board(State);
        _repositories.Text = PaneText.Repositories(State);
        _runners.Text = PaneText.Runners(State);
        _livePane.Title = State.Frozen ? "live (frozen — ctrl+f to resume)" : "live";

        // WHICH ONE IS CHOSEN, IN THE TITLE. It changes what every flight this
        // console opens will name, so a person glancing at the frame should
        // learn it without reading the rows.
        _repositoriesPane.Title = State.ChosenRepositories.Count > 0
            ? "Repositories — new flights start with "
            + string.Join(", ", State.ChosenRepositories)
            : "Repositories";

        // THE TRACKER IS IN THE TITLE, because a tenant may configure more than
        // one and a list of work items with no attribution is a list nobody can
        // act on. It is in the body too; a person reading either should not
        // have to look at the other.
        // AND WHAT IT WAS NARROWED BY, for the reason the tracker is here: a
        // title composed in the view is one the model cannot be asked about,
        // which is how the filter came to be drawn only on the empty pane.
        _browsePane.Title = PaneText.BrowseTitle(State);

        // WHICH TAB IS SHOWING IS STILL THE MODEL'S, and the component is told
        // rather than asked. Tabs.Showing answers true for exactly one tab -
        // asserted over generated states rather than over pixels - and the sync
        // flag is what stops the assignment answering its own event.
        _syncing = true;
        try
        {
            // WHAT THE BAR HOLDS, BEFORE ANYTHING IS SELECTED OUT OF IT. The
            // offered set moves - an identity arriving is enough - and a bar
            // that does not follow it is a bar being asked for a pane it never
            // received. Inside the flag because removing the selected tab makes
            // the widget announce a replacement.
            FollowTheOffered();

            foreach (var (tab, pane) in _tabbed.Where(t => _onTheBar.Contains(t.Tab)))
            {
                pane.Title = Tabs.Title(State, tab);
            }

            // FIRST OF WHAT THE BAR HOLDS, which is the widget's answer and not
            // the model's. Tabs.Showing answers true for exactly one tab and
            // Tabs.Next only walks the offered set, so the active tab is on the
            // bar - and if those two ever part again, this draws the tab it was
            // already drawing rather than throwing in a person's face.
            var showing = _tabbed
                .Where(t => _onTheBar.Contains(t.Tab))
                .FirstOrDefault(t => Tabs.Showing(State, t.Tab)).Pane;

            if (showing is not null && !ReferenceEquals(_bar.Value, showing))
            {
                _bar.Value = showing;
            }
        }
        finally
        {
            _syncing = false;
        }

        // NOT "anything but Normal", which drew an empty dialog over the
        // airspace field the moment its mode opened - on top of the one
        // thing that mode exists to focus.
        // HELD OUT OF THE WAY RATHER THAN CLOSED, when somebody is looking at
        // what the Look page just changed. The MODE does not move, so the
        // keyboard still belongs to the page and `h' brings it back - closing
        // it would lose the cursor, the page, and everything they had set.
        _modal.Visible = Modals.IsDrawn(State.Mode)
            && !(State.Mode is UiMode.Help
                 && State.HelpPage is HelpPage.Look
                 && State.Look.Peeking);

        // THE FLIGHT NAMES ITSELF UP THERE. Every other mode keeps the title
        // written for it, because a refusal is a refusal whichever one it is;
        // a document about one subject gets the subject.
        _modal.Title = PaneText.ModalTitle(State);

        // ONE BODY OR THE OTHER. A label for the modals that are a question,
        // the widgets for the one that is a flight - and the label is left
        // holding the flight's linear rendering only when nothing is open, so
        // there is never a frame with both.
        var flight = State.Mode is UiMode.FlightDetail;
        var runner = State.Mode is UiMode.Runner;
        // EITHER READING VIEW DRAWS THE SAME LIST. Which one it is showing is
        // PaneText's to answer, and it answers by mode - so this asks only
        // whether a person is reading.
        var reading = State.Mode is UiMode.ReadingEnvelope or UiMode.ReadingChangeset
                                 or UiMode.ReadingOutcome or UiMode.ReadingSaid;

        var helping = State.Mode is UiMode.Help;
        var filtering = State.Mode is UiMode.BrowseFilter;
        var item = State.Mode is UiMode.WorkItemDetail;
        var kind = State.Mode is UiMode.WorkKindChoice;
        var credentialRepo = State.Mode is UiMode.CredentialRepositoryChoice;

        _flightBody.Visible = flight;
        _runnerBody.Visible = runner;
        _readingBody.Visible = reading;
        _helpBody.Visible = helping;
        _filterBody.Visible = filtering;
        _itemBody.Visible = item;
        _kindBody.Visible = kind;
        _credentialRepoBody.Visible = credentialRepo;
        _modalBody.Visible =
            !flight && !runner && !reading && !helping && !filtering && !item && !kind
            && !credentialRepo;

        if (helping)
        {
            RenderHelp();
        }

        if (filtering)
        {
            RenderFilter();
        }

        if (item)
        {
            RenderWorkItem();
        }

        if (kind)
        {
            RenderWorkKinds();
        }

        if (credentialRepo)
        {
            RenderCredentialRepositories();
        }

        if (flight)
        {
            RenderFlight();
        }
        else if (runner)
        {
            RenderRunner();
        }
        else if (reading)
        {
            FillReading();
        }
        else if (!filtering && !item && !kind)
        {
            _modalBody.Text = PaneText.Modal(State);
        }

        RenderModalButtons();

        // SIZED BY WHAT IS IN IT. A question with two answers wants a box a
        // person's eye can take in at once; a document wants the screen. The
        // help page is twenty-one keys and a flight's detail is its whole log,
        // and both were being drawn into fifty-two columns by twelve rows -
        // which is a scrollbar where a reader wanted a page.
        var document = PaneText.ModalIsADocument(State.Mode);

        // AND BY WHAT IS IN IT NOW THAT THERE IS MORE. Fifty-two by twelve fitted
        // a one-sentence question; a body that explains two answers, with a row
        // of buttons under it, is cut off in that box - and a cut-off answer
        // reads as a shorter answer rather than as a box that is too small.
        // WIDE ENOUGH FOR THE WORDS AS WELL AS THE BUTTONS. Sizing to the
        // buttons alone left the body hand-wrapped to whatever happened to fit,
        // which breaks a sentence where the box ends rather than where it reads
        // - and goes wrong silently the moment somebody rewords it.
        // WRAPPED BEFORE IT IS MEASURED, which is the whole of not asking for a
        // box wider than the screen. The width below is the longest LINE, so an
        // unwrapped paragraph is a request for a two-hundred-column dialog -
        // and a terminal with eighty gives back one running off the side with
        // its text uncut. A document wraps itself in columns and is left alone.
        var body = (PaneText.ModalIsADocument(State.Mode)
                ? PaneText.Modal(State)
                : PaneText.Wrapped(PaneText.Modal(State), PaneText.QuestionColumns))
            .Split('\n');

        if (!document)
        {
            _modalBody.Text = string.Join('\n', body);
        }


        var wide = Math.Max(
            _modalButtons.Sum(b => b.Text.Length + 6) + 4,
            body.Max(line => line.Length) + 4);

        // MEASURED RATHER THAN GUESSED: a border top and bottom, a button row,
        // the shadow under it, and the blank line the dialog keeps above the
        // buttons. Five was the guess and it cut the last two lines of the body.
        var tall = body.Length + (_modalButtons.Count > 0 ? 7 : 3);

        _modal.Width = document ? Dim.Percent(92) : Math.Max(52, wide);
        _modal.Height = document ? Dim.Percent(88) : Math.Max(12, tall);

        // THE WINDOW, WHICH IS READ FROM OUTSIDE THIS ONE. Only when it
        // changes: Terminal.Gui pushes the title out as OSC 0, and re-sending
        // it every second would be a write to the terminal on a tick that
        // changed nothing.
        if (PaneText.WindowTitle(State) is { } window && Title != window)
        {
            Title = window;
        }

        _activity.Text = PaneText.Activity(State);
        _hints.Text = Keymap.HintsHere(Context());
        _hintsStanding.Text = Keymap.HintsStanding(Context());

        // AND THE CORNER, ON EVERY RENDER RATHER THAN ONCE. Whether a newer gg
        // exists arrives with the boot read, which now lands AFTER this screen
        // is built - so a badge set in the constructor would be composed from
        // an empty model and could never light up. The version half does not
        // change; the notice half is the whole point of the badge and is the
        // half that arrives late.
        _version.Text = Corner.Badge(Gg.Client.GgVersions.Binary, Corner.UpdateWaiting(State));

        Applied(State.Look);

        // AFTER THE LOOK, DELIBERATELY. Applied walks the tree setting the
        // palette and then dims these two lines; a fade written before it is a
        // fade that gets painted over on the same frame. It also means the
        // basis this reads is the line's FINAL background, which is the colour
        // the seconds have to sit on.
        Counting();

        // AND THE MOUSE CHANGES HANDS LAST, on the paint that froze the screen
        // or opened the modal. After this the terminal draws its own selection
        // over whatever is on it, so what is on it has to be finished first -
        // including the line that says the screen has stopped and how to start
        // it again.
        //
        // A MODAL IS THE SECOND REASON, and it is not cosmetic: a click on what
        // a modal covers asks the library to focus a hidden view, which ends
        // the process - see ConsoleMouse. Written once here rather than at the
        // two places that used to ask about freezing, so the console cannot
        // hold the mouse in a state nobody decided it should.
        var ours = ConsoleMouse.OursWhile(State);

        if (ours != _mouseIsOurs)
        {
            _mouseIsOurs = ours;

            if (ours)
            {
                TerminalMouse.ToTheConsole(_app);
            }
            else
            {
                TerminalMouse.ToTheTerminal(_app);
            }
        }

        RenderNotifications();

        Focus();
    }

    /// <summary>The corner, from the model.</summary>
    /// <remarks>
    /// <b>Not under a modal.</b> A corner of it showing past a dialog's edge reads
    /// as a drawing fault; the notifications wait, and are there when the modal
    /// closes. Focusable only while it holds the keyboard - set here, before
    /// <see cref="Focus"/> runs, because SetFocus on a view that cannot take
    /// focus does nothing.
    /// </remarks>
    private void RenderNotifications()
    {
        var holding = State.Mode == UiMode.Notifications;
        var showing = State.Notifications.Count > 0 && (State.Mode == UiMode.Normal || holding);

        _notifications.Visible = showing;
        _notifications.CanFocus = showing && holding;

        if (!showing)
        {
            RenderNotificationButtons([]);
            return;
        }

        var inside = NotificationsWidth - 4;
        _notifications.Title = PaneText.NotificationTitle(State);
        _notificationText.Text = string.Join("\n", PaneText.NotificationLines(State)
            .Select(line => line.Length > inside ? line[..(inside - 1)] + "…" : line));
        _notifications.BorderStyle = holding
            ? Terminal.Gui.Drawing.LineStyle.Heavy
            : Terminal.Gui.Drawing.LineStyle.Rounded;

        _notificationHint.Text = PaneText.NotificationHint(Context()) + " ";
        _notificationHint.Visible = !holding;

        RenderNotificationButtons(holding ? Keymap.Buttons(Context()) : []);
    }

    private void RenderNotificationButtons(IReadOnlyList<KeyBinding> wanted)
    {
        if (_notificationButtons.Count == wanted.Count
            && _notificationButtons.Zip(wanted).All(p => p.First.Text == p.Second.Label))
        {
            return;
        }

        foreach (var old in _notificationButtons)
        {
            _notifications.Remove(old);
            old.Dispose();
        }

        _notificationButtons.Clear();

        View? before = null;

        foreach (var binding in wanted)
        {
            var button = new Button
            {
                Y = 2,
                X = before is null ? 1 : Pos.Right(before) + 1,
                Text = binding.Label!,
                ShadowStyle = ShadowStyles.None,

                // NO HOTKEY OF ITS OWN, _runnerStart's rule: a Button takes a
                // letter out of its caption, and the keymap is the only place a
                // printable key means anything here.
                HotKeySpecifier = new System.Text.Rune('\uffff'),
            };

            // THE SAME PATH A KEYSTROKE TAKES - the command itself, handed to the
            // one place that acts on one.
            var command = binding.Command;
            button.Accepting += (_, e) =>
            {
                e.Handled = true;
                Dispatch(command);
            };

            _notificationButtons.Add(button);
            _notifications.Add(button);
            before = button;
        }
    }

    /// <summary>
    /// Puts the countdown's seconds over the hint line, at the brightness the
    /// wait has got to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Three answers from elsewhere, and nothing decided here.</b> Where the
    /// columns are is <c>Keymap.Counting</c>'s, how far through the wait is
    /// <c>AutoRefresh.Left</c>'s, and what colour that is belongs to
    /// <c>LookStyles</c> - so the only thing this does is put a view where the
    /// first one says and hand it what the other two answered. A screen cannot
    /// be built in a test, and everything above can.
    /// </para>
    /// <para>
    /// <b>Hidden rather than blank when nothing counts.</b> A read in the air
    /// shows a mark instead of a number and a modal does not offer the key at
    /// all; an empty label left over the line would be three spaces painted on
    /// top of whatever is under it.
    /// </para>
    /// </remarks>
    private void Counting()
    {
        if (Keymap.Counting(Context()) is not { } columns)
        {
            _hintsCounting.Visible = false;
            return;
        }

        var line = _hintsStanding.Text;

        // THE CHARACTERS THE LINE ITSELF HAS THERE. Re-deriving them would be a
        // second rendering of one number, and the two would disagree on
        // whichever frame the model moved between the two reads.
        if (columns.At + columns.Length > line.Length)
        {
            _hintsCounting.Visible = false;
            return;
        }

        _hintsCounting.Text = line.Substring(columns.At, columns.Length);
        _hintsCounting.X = columns.At;
        _hintsCounting.Width = columns.Length;
        _hintsCounting.Visible = true;

        _hintsCounting.SetScheme(LookStyles.Counting(
            _hintsStanding.GetScheme(), AutoRefresh.Left(State.Refresh)));
    }

    /// <summary>
    /// Whether whatever holds the keyboard has stopped being on the screen.
    /// </summary>
    /// <remarks>
    /// <b>A general answer to a hazard this console creates on purpose.</b>
    /// Several panes answer "no rows" by hiding a table and showing a
    /// sentence, and swap them back when rows arrive — so the view holding the
    /// keyboard can vanish under it between two renders. <c>FocusChange</c> is
    /// pure and cannot see visibility; this is the one thing it needs to know
    /// that it cannot be told without handing it the widget tree.
    /// </remarks>
    private bool Stranded()
    {
        for (var view = Focused; view is not null; view = view.SuperView)
        {
            if (!view.Visible)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Draw the console the way the Look page says to.</summary>
    /// <remarks>
    /// <para>
    /// <b>A SPIKE, and this is the whole of what it costs the view.</b> One
    /// method, called at the end of a render, walking the tree rather than
    /// naming every pane — because naming them is how a pane added later gets
    /// left behind, which is the shape <c>Tabs.Offered</c>'s followers already
    /// taught this console once.
    /// </para>
    /// <para>
    /// <b>Nothing is applied for <see cref="Palette.Default"/>.</b> A console
    /// nobody has touched must be identical to one built before this existed,
    /// so the palette arm answers null and the scheme is left exactly as
    /// Terminal.Gui set it. Borders still apply, because their default IS what
    /// the record ships with.
    /// </para>
    /// <para>
    /// <b>Cheap enough to do every render.</b> Render runs on a one-second
    /// timer, and these are property writes that no-op when the value has not
    /// moved — the same reasoning the tab bar's <c>Value</c> assignment uses.
    /// </para>
    /// </remarks>
    private void Applied(Look look)
    {
        var outer = LookStyles.Line(look.PaneBorder);
        var inner = LookStyles.Line(look.InnerBorder);
        var colours = LookStyles.Colours(look.Palette);

        // THE APPLICATION'S OWN FRAME, which the walk below cannot reach: this
        // is a Window rather than a FrameView, and it is the biggest line on
        // the screen.
        BorderStyle = LookStyles.Line(look.AppBorder);

        // AND THE DIALOG, separately from either layer of pane, because what
        // tells a modal apart from what is behind it is the line round it.
        _modal.BorderStyle = LookStyles.Line(look.ModalBorder);

        var tabLine = LookStyles.Line(look.TabLine);
        var side = LookStyles.Side(look.TabSide);

        foreach (var strip in (Terminal.Gui.Views.Tabs[])
                 [_bar, _helpTabs, _flightTabs, _itemTabs, _filterViews, _runnerViews,
                  _airspaceViews])
        {
            strip.TabLineStyle = tabLine;
            strip.TabSide = side;
            strip.TabDepth = look.TabDepth;
            strip.TabSpacing = look.TabSpacing;

            // THE TAB A PERSON IS ON, MARKED. Terminal.Gui offers nothing for
            // this, so it goes on the title the model wrote a moment ago - and
            // because the model rewrites it every render, the decoration lands
            // on a clean value instead of stacking up.
            foreach (var tab in strip.TabCollection)
            {
                var showing = ReferenceEquals(strip.Value, tab);

                tab.Title = LookStyles.Marked(tab.Title, showing, look.TabMark);

                if (LookStyles.TabColours(showing, look.TabMark, look.Palette) is { } accent)
                {
                    tab.SetScheme(accent);
                }

                // AND THE ONES A PERSON IS NOT ON, PUSHED BACK. The other half
                // of marking the selected tab: a strip of eight equally bright
                // ones makes the eye hunt for the live one.
                //
                // THE BASIS IS THE STRIP'S, NOT THE TAB'S, so this never reads
                // its own output - and DECIDED EVERY RENDER, clear included,
                // because a tab left holding a dim scheme after the setting
                // goes back to Normal is a change with no way back.
                else if (!showing)
                {
                    tab.SetScheme(
                        LookStyles.Dimmed(colours ?? strip.GetScheme(), look.UnselectedTabs)
                        ?? colours);
                }
            }
        }

        // EVERY LAYER, COUNTED. A pane inside a pane gets its own line, which
        // is what stops a screen three frames deep reading as a grid - and the
        // count is what makes "which layer" a question this can answer without
        // a list of panes to keep up to date.
        Walk(this, depth: 0);

        // THE TWO LINES THAT ARE ALWAYS THERE AND RARELY READ, after the walk
        // for the reason the tabs are excluded from it: the palette would
        // otherwise land on top of the dimming. They are reference rather than
        // content, and at full brightness they compete with the pane above.
        foreach (var line in (View[])[_activity, _hints, _hintsStanding])
        {
            // SET EVERY RENDER, CLEAR INCLUDED. Turning the setting back to
            // Normal has to put these back, and the walk above only reasserts
            // where there is a palette to reassert - a console in the
            // terminal's own colours would keep the dimming for ever.
            line.SetScheme(
                LookStyles.Dimmed(colours ?? GetScheme(), look.StatusText) ?? colours);
        }

        void Walk(View view, int depth)
        {
            foreach (var child in view.SubViews)
            {
                var below = depth;

                // A DIALOG IS A LAYER EVEN THOUGH IT IS NOT A FRAME. Dialog
                // descends from Runnable rather than FrameView, so counting
                // frames alone put the modal's panes at the same depth as the
                // ones behind it - and `inner border' reached NOTHING, which is
                // a setting that appears to work. Measured by driving it: three
                // borders set, two lines on the screen.
                if (child is Runnable)
                {
                    below = depth + 1;
                }

                // A FRAME IS A PANE. Every region of this console is a
                // FrameView and nothing else is, so this is the whole of
                // "each pane" without a list to keep up to date.
                if (child is FrameView frame)
                {
                    frame.BorderStyle = depth == 0 ? outer : inner;
                    below = depth + 1;
                }

                // AND EVERY TABLE, which is most of this console: nine panes
                // and four modals are a table with something beside it.
                if (child is TableView table)
                {
                    LookStyles.Table(table, look);
                }

                // THE TAB TITLES ARE ALREADY DONE, and a palette assigned after
                // an accent would undo it. Everything else takes the palette.
                // AND THE COUNTDOWN IS NOT THE WALK'S EITHER. Its colour is
                // the one thing on this screen that changes every second, and
                // a palette asserted over the top of it would flatten the fade
                // to whatever the rest of the line is - which is the state it
                // was added to escape.
                if (colours is not null && !IsATab(child) && !ReferenceEquals(child, _hintsCounting))
                {
                    child.SetScheme(colours);
                }

                Walk(child, below);
            }
        }

        // A TAB'S OWN SCHEME IS THE TAB LOOP'S, and the walk runs after it - so
        // without this the palette lands on top and undoes both the accent and
        // the dimming. Only when something is actually styling them, or a
        // console with neither set would stop taking the palette on its tabs.
        bool IsATab(View view) =>
            (look.TabMark is TabMark.Accent || look.UnselectedTabs is not Dimming.Normal)
            && view.SuperView is Terminal.Gui.Views.Tabs;
    }

    /// <summary>
    /// The flight modal, bound to the four things the model produces for it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Bound, never formatted.</b> Every string here comes from
    /// <see cref="FlightDetails"/> or <see cref="Rows"/>, which are pure and
    /// tested without a terminal. What is left is which widget - a decision
    /// about the content that a person has to look at anyway - and the two
    /// guards in <c>AFlightIsReadInFieldsRatherThanAWallOfTextTests</c> hold
    /// this file to it.
    /// </para>
    /// <para>
    /// <b>The fields are rebuilt on every render and the log is not.</b>
    /// Rebuilding a column of labels and read-only text is free and keeps their
    /// number honest - a flight waiting on three people has three more rows
    /// than one waiting on nobody. Refilling the table is not free: it replaces
    /// the source, which resets the cursor a person is scrolling with. Hence
    /// <see cref="_logShowing"/>.
    /// </para>
    /// </remarks>
    private void RenderFlight()
    {
        _flightGate.Text = FlightDetails.Gate(State);

        // THE ABSENCE OR THE FACTS, and the absence is three different
        // sentences on purpose: nobody has looked, the read is still coming,
        // and the flight recorded nothing are three different things, and only
        // the last is a fact about the flight.
        _flightFacts.Text = FlightDetails.FactsAbsence(State) is { Length: > 0 } absent
            ? absent
            : FlightDetails.FactsLines(State);

        // WHICH TAB HAS THE BODY IS THE MODEL'S TO SAY. Guarded the way the
        // console's own bar is: assigning Value raises ValueChanged, and
        // without the flag the assignment answers its own event and reduces a
        // command for a tab nobody pressed.
        _syncing = true;
        try
        {
            var showing = State.FlightTab switch
            {
                FlightTab.Gate => _flightGateTab,
                FlightTab.Log => _flightLogTab,
                FlightTab.Facts => _flightFactsTab,
                _ => _flightDetailsTab,
            };

            if (!ReferenceEquals(_flightTabs.Value, showing))
            {
                _flightTabs.Value = showing;
            }
        }
        finally
        {
            _syncing = false;
        }

        _flightIntent.Text = FlightDetails.Intent(State);

        var fields = FlightDetails.Fields(State);

        if (_fieldsShowing is not null && _fieldsShowing.SequenceEqual(fields))
        {
            RenderLog();
            return;
        }

        _fieldsShowing = fields;
        Lay(_flightFields, fields);

        RenderLog();
    }

    /// <summary>
    /// Lays a column of read-only fields into a container, replacing whatever
    /// was there.
    /// </summary>
    /// <remarks>
    /// <b>One implementation, because two modals want the same column.</b> The
    /// flight's scalars and the runner's are the same shape - a dim caption in
    /// a fixed gutter, a value a cursor can enter - and a second copy would be
    /// a second place for the read-only rule to lapse.
    /// <para>
    /// <b>What was removed is disposed here.</b> RemoveAll's own words:
    /// "Removing a SubView causes ownership of the SubView's lifecycle to be
    /// transferred to the caller; the caller must call Dispose". Each caller's
    /// own guard is what makes this rare - Render runs four times a second on
    /// the live tail's timer - and this is what makes it correct when it does
    /// happen.
    /// </para>
    /// </remarks>
    private void Lay(View container, IReadOnlyList<FlightField> fields)
    {
        foreach (var gone in container.RemoveAll())
        {
            gone.Dispose();
        }

        for (var i = 0; i < fields.Count; i++)
        {
            // THE CAPTION DIMMER THAN THE VALUE, which is the one thing the
            // two-space indents it replaces could not do: a column of labels
            // drawn as brightly as the facts beside them competes with them.
            var caption = new Label
            {
                X = 1,
                Y = i,
                Width = FieldLabelWidth,
                Text = fields[i].Label,
            };

            caption.SetScheme(_muted);
            container.Add(caption);

            container.Add(new TextField
            {
                X = FieldLabelWidth + 1,
                Y = i,
                Width = Dim.Fill(1),

                // READ-ONLY, AND STILL FOCUSABLE. Nothing in this console is
                // written by typing into a widget - a write happens between
                // sessions with the terminal provably free - so a field that
                // accepted a keystroke would be a promise it cannot keep. What
                // focus is for here is the cursor: a value a person can select
                // is a value a person can copy.
                ReadOnly = true,
                Text = fields[i].Value,
            });
        }

        container.Height = fields.Count;
    }

    /// <summary>
    /// The runner modal, bound to what the model produces for it.
    /// </summary>
    /// <remarks>
    /// <b>The fields are replaced only when they change</b>, for
    /// <see cref="RenderFlight"/>'s reason: this runs on the live tail's timer
    /// four times a second, and rebuilding widgets at that rate leaks whatever
    /// the last pass made. The log is a string, so it is simply assigned - it
    /// is also the thing most likely to have changed, because a runner coming
    /// up is why this modal is open.
    /// </remarks>
    private void RenderRunner()
    {
        var fields = RunnerDetails.Fields(State);

        if (_runnerFieldsShowing is null || !_runnerFieldsShowing.SequenceEqual(fields))
        {
            _runnerFieldsShowing = fields;
            Lay(_runnerFields, fields);
        }

        // THE LOG WHEN THERE IS ONE, THE SENTENCE WHEN THERE IS NOT - and the
        // sentence says WHICH absence it is, because a child that has not
        // spoken yet and a runner whose output was never coming here are
        // different facts that would otherwise read the same.
        var absence = RunnerDetails.LogAbsence(State);

        _runnerLogAbsent.Text = absence;
        _runnerLogAbsent.Visible = absence.Length > 0;
        _runnerSaid.Visible = absence.Length == 0;

        if (absence.Length == 0)
        {
            FillRunnerLog();
        }

        RenderRunnerViews();
    }

    /// <summary>
    /// The two views beside the log, and which of the three is showing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The sync flag is held for the same reason the tab bar holds it.</b>
    /// Assigning <c>Value</c> raises <c>ValueChanged</c>, and a table
    /// repopulated raises its own selection event - so without it a render
    /// would be read as a person choosing something and reduce from inside
    /// itself.
    /// </para>
    /// <para>
    /// <b>ONE SENTENCE SERVES BOTH EMPTY VIEWS.</b> The peers are found through
    /// the environments this runner advertises, so no environments means no
    /// peers and the cause is the same - and it is moved between the two panes
    /// rather than written twice.
    /// </para>
    /// </remarks>
    /// <summary>
    /// The kinds a tenant declared, with what each says it is for.
    /// </summary>
    /// <remarks>
    /// <b>The answer that names no kind is row zero</b>, which is
    /// <c>WorkKinds.Rows</c>' to say rather than this: inheriting the floor is
    /// an answer and it has to be at a known index, because the cursor is what
    /// the decision reads.
    /// </remarks>
    private void RenderWorkKinds()
    {
        _kindSentence.Text = PaneText.Modal(State);

        _syncing = true;

        try
        {
            var rows = WorkKinds.Rows(State);

            Fill(
                _kindChoices, null, rows, Rows.WorkKindColumns, State.KindSelected,
                row => [row.Name, row.Said]);

            // AND THE OTHER TAB, filled in the same guarded window: a table
            // handed a source raises its own selection event, which is also
            // how a click arrives.
            var against = Rows.FlyingWith(State);

            Fill(
                _composeRepos, _composeReposAbsent, against, Rows.FlyingWithColumns,
                State.RepositorySelected,
                row => [row.Mark, row.Path, row.Name, row.Credential]);

            _composeReposAbsent.Text = PaneText.FlyingWithAbsence(State);

            // WHICH TAB HAS THE BODY IS THE MODEL'S TO SAY, guarded the way
            // every other bar in this console is.
            var showing = State.WorkKindTab is WorkKindTab.Repositories
                ? _composeRepoTab
                : _composeKindTab;

            if (!ReferenceEquals(_workKindTabs.Value, showing))
            {
                _workKindTabs.Value = showing;
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    /// <summary>
    /// The registry a credential can be sent for, and the line above it.
    /// </summary>
    /// <remarks>
    /// <b>The standing is the second column because it says what the answer
    /// costs.</b> A repository this machine already holds a secret for is one
    /// the send reuses; one it does not is a paste. Both end the session, and
    /// only one of them asks for anything.
    /// </remarks>
    private void RenderCredentialRepositories()
    {
        _credentialRepoSentence.Text = PaneText.Modal(State);

        _syncing = true;

        try
        {
            var rows = CredentialRepositories.Rows(State);

            Fill(
                _credentialRepoChoices, null, rows, CredentialRepositories.Columns,
                State.CredentialRepoSelected,
                row => [row.Path, row.Said]);
        }
        finally
        {
            _syncing = false;
        }
    }

    /// <summary>
    /// What the item says, its scalars, and what has happened to it.
    /// </summary>
    /// <remarks>
    /// <b>The fields are re-laid only when they changed</b>, which is
    /// <c>RenderFlight</c>'s guard and for its reason: laying a column disposes
    /// the views it replaces, and this runs on every render.
    /// </remarks>
    private void RenderWorkItem()
    {
        _itemSaid.Text = WorkItemDetails.Said(State);

        var fields = WorkItemDetails.Fields(State);

        if (_itemFieldsShowing is null || !_itemFieldsShowing.SequenceEqual(fields))
        {
            _itemFieldsShowing = fields;
            Lay(_itemFields, fields);
        }

        _syncing = true;

        try
        {
            var changes = WorkItemDetails.Changes(State);

            Fill(
                _itemHistory, _itemHistoryAbsent, changes, Rows.WorkItemColumns,
                State.WorkItemSelected,
                row => [row.When, row.Who, row.What]);

            _itemHistoryAbsent.Text = WorkItemDetails.HistoryAbsence(State);

            // AND THE INVENTORY. Filled in the same guarded window: a table
            // handed a source raises its own selection event, which is also
            // how a click arrives.
            //
            // NO EMPTY LABEL PASSED, because this table is never empty - the
            // seven named fields are always rows, even the ones the tracker
            // said nothing for. The sentence below it says why there is
            // nothing AFTER them, which is the different fact.
            var inventory = WorkItemDetails.AllFields(State);

            Fill(
                _itemFieldsTable, null, inventory, WorkItemDetails.FieldColumns,
                State.WorkItemSelected,
                row => [row.Name, row.Value]);

            _itemFieldsAbsent.Text = WorkItemDetails.FieldsAbsence(State);

            // WHAT THE ROW UNDER THE CURSOR SAYS, in the half of the tab a
            // table cannot use.
            _itemChange.SetSource(new System.Collections.ObjectModel.ObservableCollection<string>(
                [.. WorkItemDetails.ChangeDetailLines(
                    State, CollectionViews.TextWidth(_itemChange))]));

            // WHICH TAB HAS THE BODY IS THE MODEL'S TO SAY, guarded the way
            // the flight modal's is.
            var showing = State.WorkItemTab is WorkItemTab.History
                ? _itemHistoryTab
                : State.WorkItemTab is WorkItemTab.Fields
                    ? _itemFieldsTab
                    : State.WorkItemTab is WorkItemTab.Actions
                        ? _itemActionsTab
                        : _itemDetailsTab;

            // WHAT THE BUTTON WOULD DO, AND WHETHER IT IS OFFERED. Both are
            // the model's answer, so a tab opened over an item with no tracker
            // says why rather than drawing a button that refuses when pressed.
            _itemActionSaid.Text = WorkItemDetails.ActionsSaid(State);
            _itemFly.Visible = WorkItemDetails.CanFly(State);

            if (!ReferenceEquals(_itemTabs.Value, showing))
            {
                _itemTabs.Value = showing;
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    /// <summary>
    /// The three tables, the bar over them, and the foot under it.
    /// </summary>
    /// <remarks>
    /// <b>All three are filled, not just the one showing.</b> A table filled
    /// only when its tab is turned to shows the previous tab's rows for the
    /// frame the turn happens in - and each keeps its own cursor, so filling
    /// from the showing view's index would land the others on somebody else's
    /// row.
    /// </remarks>
    private void RenderFilter()
    {
        _syncing = true;

        try
        {
            var showing = _filterTabbed
                .FirstOrDefault(t => t.View == State.FilterView).Pane;

            if (showing is not null && !ReferenceEquals(_filterViews.Value, showing))
            {
                _filterViews.Value = showing;
            }

            foreach (var (view, _, table, empty) in _filterTabbed)
            {
                Fill(
                    table, empty,
                    BrowseFilters.Offered(State, view), Rows.FilterColumns(view),
                    BrowseFilters.Cursor(State, view),
                    row => [row.Chosen ? Rows.Picked : " ", row.Value]);
            }

            _filterSentence.Text = PaneText.Modal(State);
            _filterInForce.Text = PaneText.FilterInForce(State);
        }
        finally
        {
            _syncing = false;
        }
    }

    /// <summary>
    /// The bar turned, by a click or an arrow along the headers.
    /// </summary>
    /// <remarks>
    /// A bar nobody listens to is one whose every move the next render undoes,
    /// which is what the runner modal's was reported as.
    /// </remarks>
    private void OnFilterViewChanged(object? sender, ValueChangedEventArgs<View?> args)
    {
        if (_syncing || args.NewValue is null)
        {
            return;
        }

        var picked = _filterTabbed
            .FirstOrDefault(t => ReferenceEquals(t.Pane, args.NewValue));

        if (picked.Pane is null || picked.View == State.FilterView)
        {
            return;
        }

        State = State with { FilterView = picked.View };
        Render();
    }

    private void RenderRunnerViews()
    {
        _syncing = true;

        try
        {
            var showing = _runnerViewTabbed
                .FirstOrDefault(t => t.View == State.RunnerView).Pane;

            if (showing is not null && !ReferenceEquals(_runnerViews.Value, showing))
            {
                _runnerViews.Value = showing;
            }

            Fill(_runnerEnvironments, null,
                EnvironmentRows.Environments(State), EnvironmentRows.EnvironmentColumns,
                State.RunnerEnvironmentSelected,
                r => [r.Environment, r.Strategy, r.Pool, r.Wants, r.Attested, r.Measured]);

            Fill(_runnerMembers, null,
                EnvironmentRows.Members(State), EnvironmentRows.MemberColumns,
                State.RunnerMemberSelected,
                r => [r.Here, r.Environment, r.Member, r.State, r.Work, r.Heard]);

            var nothing = State.RunnerView == RunnerView.Members
                ? RunnerDetails.MemberAbsence(State)
                : RunnerDetails.EnvironmentAbsence(State);

            _runnerNothingHere.Text = nothing;
            _runnerNothingHere.Visible = nothing.Length > 0
                                      && State.RunnerView != RunnerView.Log;

            var beside = _runnerViewTabbed
                .FirstOrDefault(t => t.View == State.RunnerView).Pane;

            if (_runnerNothingHere.SuperView is { } was)
            {
                was.Remove(_runnerNothingHere);
            }

            if (_runnerNothingHere.Visible && beside is not null)
            {
                beside.Add(_runnerNothingHere);
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    /// <summary>The log's lines, wrapped to whatever room the frame has.</summary>
    /// <remarks>
    /// <b>Only when they changed.</b> Setting a list's source resets where a
    /// person had scrolled to, and this runs on the live tail's timer four
    /// times a second - so a person reading back through a stack trace would be
    /// dragged to the top of it while they read.
    /// </remarks>
    private void FillRunnerLog()
    {
        var lines = RunnerDetails.Lines(State, _runnerSaid.Viewport.Width);

        if (_runnerSaidShowing is not null && _runnerSaidShowing.SequenceEqual(lines))
        {
            return;
        }

        _runnerSaidShowing = lines;
        _runnerSaid.SetSource(new ObservableCollection<string>(lines));
    }

    /// <summary>
    /// Whichever of the two readings is open, wrapped to the box it is in.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Only when the lines change</b>, because setting a list's source
    /// resets where a person had scrolled to and <c>Render</c> runs once a
    /// second for the countdown.
    /// </para>
    /// <para>
    /// <b>Which also means a view switch scrolls back to the top, and that is
    /// right.</b> Pressing <c>d</c> is asking a different question; landing
    /// forty rows into the answer would be the list's old position pretending
    /// to be a place in the new document.
    /// </para>
    /// </remarks>
    private void FillReading()
    {
        var lines = State.Mode switch
        {
            UiMode.ReadingChangeset => PaneText.ChangesetLines(State, _readingSaid.Viewport.Width),
            UiMode.ReadingOutcome => PaneText.ApplyLines(State, _readingSaid.Viewport.Width),
            UiMode.ReadingSaid => PaneText.SaidLines(State, _readingSaid.Viewport.Width),
            _ => PaneText.EnvelopeLines(State, _readingSaid.Viewport.Width),
        };

        if (_readingSaidShowing is not null && _readingSaidShowing.SequenceEqual(lines))
        {
            return;
        }

        _readingSaidShowing = lines;
        _readingSaid.SetSource(new ObservableCollection<string>(lines));
    }

    /// <summary>
    /// The selected document, in the view the pane's bar is showing.
    /// </summary>
    /// <remarks>
    /// <b>Only the showing one.</b> The other two panes are behind it and hold
    /// whatever they last drew; they are refilled when they come forward,
    /// which is the same render.
    /// <para>
    /// <b>And only when the lines change</b>, because setting a list's source
    /// resets where a person had scrolled to, and Render runs once a second
    /// for the countdown.
    /// </para>
    /// </remarks>
    private void FillAirspaceDocument()
    {
        if (_viewTabbed.FirstOrDefault(t => t.View == State.AirspaceView).Said
            is not { } said)
        {
            return;
        }

        var lines = PaneText.AirspaceDocument(State, said.Viewport.Width);

        if (_airspaceSaidShowing is not null && _airspaceSaidShowing.SequenceEqual(lines))
        {
            return;
        }

        _airspaceSaidShowing = lines;
        said.SetSource(new ObservableCollection<string>(lines));
    }

    /// <summary>The pane changed width, so the lines have to be broken again.</summary>
    private void OnAirspaceDocumentResized(object? sender, EventArgs args)
    {
        if (State.ActiveTab is not TabId.Envelope)
        {
            return;
        }

        FillAirspaceDocument();
    }

    /// <summary>
    /// The activity line was laid out, so the model learns how wide it is.
    /// </summary>
    /// <remarks>
    /// <b>Re-rendered only when the number moved</b>, the way a pointed row is:
    /// a layout pass happens for reasons that have nothing to do with width,
    /// and a render inside a layout that renders is the loop this shape avoids.
    /// <c>Reducer.SaidMeasured</c> hands back the same instance when nothing
    /// changed, which is what makes the check free.
    /// </remarks>
    private void OnSaidResized(object? sender, EventArgs args)
    {
        var measured = Reducer.SaidMeasured(State, _activity.Viewport.Width);

        if (ReferenceEquals(measured, State))
        {
            return;
        }

        State = measured;
        Render();
    }

    /// <summary>The frame changed width, so the lines have to be broken again.</summary>
    private void OnReadingResized(object? sender, EventArgs args)
    {
        if (State.Mode is not (UiMode.ReadingEnvelope or UiMode.ReadingChangeset
                                                      or UiMode.ReadingOutcome
                                                      or UiMode.ReadingSaid))
        {
            return;
        }

        FillReading();
    }

    private void OnRunnerLogResized(object? sender, EventArgs args)
    {
        if (State.Mode is not UiMode.Runner)
        {
            return;
        }

        FillRunnerLog();
    }

    /// <summary>
    /// The log, refilled only when it is holding the wrong thing.
    /// </summary>
    /// <remarks>
    /// <b>Filling a table replaces its source, which resets the selection.</b>
    /// Harmless for the tabs, whose cursors are in the model, and not harmless
    /// here: the log's cursor is a person's place in a history and is
    /// deliberately kept nowhere, because a modal is a question with an answer
    /// and a way out. The story cannot change while the modal is open - a UI
    /// session makes no network call - so the flight it is about and the number
    /// of entries settle whether a refill is needed.
    /// </remarks>
    private void RenderLog()
    {
        // THE TABLE WHEN THERE ARE ROWS, THE SENTENCE WHEN THERE ARE NOT - and
        // the sentence says WHICH absence it is, because a story nobody fetched
        // and a flight nothing happened to are different facts.
        var absence = FlightDetails.LogAbsence(State);
        var log = Rows.Log(State);

        _flightLogAbsent.Text = absence;
        _flightLogAbsent.Visible = absence.Length > 0;
        _flightLog.Visible = log.Count > 0;

        // WHAT AN ENTRY SAYS GOES IN THE PANE BELOW, which is why no width is
        // measured here any more. The table used to wrap prose into
        // continuation rows because Terminal.Gui has no variable row heights;
        // a pane of its own needs no arithmetic and leaves one row per entry.
        _flightLogDetail.SetSource(new System.Collections.ObjectModel.ObservableCollection<string>(
            [.. FlightDetails.LogDetailLines(State, CollectionViews.TextWidth(_flightLogDetail))]));
        _flightLogDetail.Visible = log.Count > 0;

        var shown = log;
        var showing = (State.Story?.FlightId ?? "", shown.Count, State.LogSelected, 0);

        if (_logShowing != showing)
        {
            _syncing = true;

            // WHERE THE HIGHLIGHT IS ON THE SCREEN, read before the rows under
            // it are replaced. Both numbers mean rows of the set being thrown
            // away, so they are worth nothing once the fill has happened.
            var was = _flightLog.Value?.SelectedCell.Y ?? 0;
            var from = _flightLog.RowOffset;

            try
            {
                CollectionViews.Fill(
                    _flightLog,
                    shown.Count == 0
                        ? null
                        : new DataTableSource(CollectionViews.Rows(
                            Rows.LogColumns,
                            [.. shown.Select(r => new[] { r.Time, r.Attempt, r.Event })])));

                // ON THE ENTRY'S FIRST ROW. Filling replaces the source, which
                // resets the selection - and the model's cursor is an entry, so
                // where that entry STARTS is the row the highlight belongs on.
                if (shown.Count > 0)
                {
                    var at = Math.Max(0, IndexOfEntry(shown, State.LogSelected));

                    _flightLog.SetSelection(0, at, extendExistingSelection: false, null);
                    _flightLog.EnsureValidSelection();

                    // AND SCROLLED SO THE HIGHLIGHT IS WHERE IT WAS. Neither of
                    // the two calls above touches the offset - measured - so
                    // without this the view stays scrolled to a row number that
                    // meant something in the set just replaced, and the cursor
                    // can end up off the top of the screen entirely.
                    _flightLog.RowOffset = Rows.KeepingTheCursorsLine(was, from, at);

                    // THE BACKSTOP, and the widget's own arithmetic rather than
                    // a second copy of it: an offset that would leave the last
                    // row short of the bottom is the widget's to correct.
                    _flightLog.EnsureCursorIsVisible();
                }
            }
            finally
            {
                _syncing = false;
            }

            _logShowing = showing;
        }
    }

    /// <summary>
    /// The log's column got wider or narrower, so the text is broken again.
    /// </summary>
    /// <remarks>
    /// <b>Only the log, and only its rows.</b> A full <c>Render</c> here would
    /// re-assert focus during a layout, which is the thing the countdown taught
    /// this file not to do. <c>RenderLog</c> keys on the width, so this is a
    /// no-op whenever nothing actually moved.
    /// </remarks>
    private void OnLogResized(object? sender, EventArgs args)
    {
        if (_syncing || State.Mode is not UiMode.FlightDetail)
        {
            return;
        }

        RenderLog();
    }

    /// <summary>The detail pane changed width, so the prose is broken again.</summary>
    /// <remarks>
    /// <b>Only this pane.</b> A full <c>Render</c> during a layout re-asserts
    /// focus, which is the thing the countdown taught this file not to do.
    /// </remarks>
    private void OnLogDetailResized(object? sender, EventArgs args)
    {
        if (_syncing || State.Mode is not UiMode.FlightDetail)
        {
            return;
        }

        RenderLog();
    }

    /// <summary>The same, one modal over.</summary>
    private void OnChangeDetailResized(object? sender, EventArgs args)
    {
        if (_syncing || State.Mode is not UiMode.WorkItemDetail)
        {
            return;
        }

        Render();
    }

    /// <summary>Where an entry starts, among the rows it and its neighbours make.</summary>
    private static int IndexOfEntry(IReadOnlyList<LogRow> rows, int entry)
    {
        for (var i = 0; i < rows.Count; i++)
        {
            if (rows[i].Entry == entry)
            {
                return i;
            }
        }

        return 0;
    }

    /// <summary>
    /// A person put the cursor on a row of the log.
    /// </summary>
    /// <remarks>
    /// <b>Its own handler, and not <c>OnRowPointedAt</c>.</b> That one hands the
    /// row straight to <c>Reducer.Pointed</c>, which is right for the four
    /// tables a tab drives and wrong here twice over: the tab under this modal
    /// is the flights list, and a row of this table is not an entry. What is
    /// handed over is <c>LogRow.Entry</c>, so a continuation row means the entry
    /// it continues.
    /// </remarks>
    /// <summary>
    /// A person pointed at a row in one of the runner modal's two tables.
    /// </summary>
    /// <remarks>
    /// <b>Its own handler, for <see cref="OnLogRowPointedAt"/>'s reason.</b>
    /// <c>OnRowPointedAt</c> routes through <c>Reducer.Pointed</c> by active
    /// tab, and the tab behind this modal is Runners — so sharing it would
    /// move the FLEET's cursor and change which runner the modal is about,
    /// under somebody reading it.
    /// </remarks>
    /// <summary>A click inside a modal's table, routed by the mode it is in.</summary>
    /// <remarks>
    /// <b>Not <c>OnRowPointedAt</c>.</b> That one routes through
    /// <c>Reducer.Pointed</c> by ACTIVE TAB, and the tab behind a modal is
    /// whatever was showing - so a click in here would move the cursor of a
    /// list the modal is not about. <c>Pointed</c> answers by mode first.
    /// </remarks>
    private void OnModalRowPointedAt(object? sender, ValueChangedEventArgs<TableSelection?> args)
    {
        if (_syncing || args.NewValue is not { } selection)
        {
            return;
        }

        var pointed = Reducer.Pointed(State, selection.SelectedCell.Y);

        if (ReferenceEquals(pointed, State))
        {
            return;
        }

        State = pointed;
        Render();
    }

    private void OnLogRowPointedAt(object? sender, ValueChangedEventArgs<TableSelection?> args)
    {
        if (_syncing || args.NewValue is not { } selection)
        {
            return;
        }

        // ONE ROW PER ENTRY, so the widget's row number IS the entry. It used
        // to be a lookup because an unwrapped entry spanned several rows.
        var log = Rows.Log(State);
        var row = selection.SelectedCell.Y;

        if (row < 0 || row >= log.Count)
        {
            return;
        }

        var pointed = Reducer.Pointed(State, log[row].Entry);

        if (ReferenceEquals(pointed, State))
        {
            return;
        }

        State = pointed;
        Render();
    }

    /// <summary>
    /// How much of the line the field names take.
    /// </summary>
    /// <remarks>
    /// Fixed rather than measured, so the values start at the same column
    /// whichever flight is open - a form whose gutter moves between two flights
    /// reads as two different forms. Eleven fits the longest label this
    /// produces, <c>waiting on</c>, with a space after it.
    /// </remarks>
    private const int FieldLabelWidth = 11;

    /// <summary>
    /// Focus follows the tab, because the tab is the only thing on screen.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It used to follow a <c>FocusedPane</c> that tab cycled independently of
    /// what was visible. With one view on the screen there is nothing to choose
    /// between: whichever pane the tab bar is showing is the one a person is
    /// driving.
    /// </para>
    /// <para>
    /// <b>FOLLOWS means WHEN THE TAB CHANGES, and that used to be the same
    /// thing.</b> <c>Render</c> ends here, and until the countdown existed a
    /// render only ever followed a keypress - the keypress having just decided
    /// where focus belonged, so re-asserting it changed nothing. The countdown
    /// renders once a second whether anybody pressed anything, and re-asserting
    /// focus then overrules the person: the runners tab's button could be
    /// reached with an arrow and taken away again before it could be pressed.
    /// </para>
    /// <para>
    /// <b>So a tab that already holds the focus is left alone.</b> A pane
    /// reports <c>HasFocus</c> for anything inside it, so "is the focus already
    /// in this tab" is one question with one answer - and where inside is the
    /// person's business, not this method's.
    /// </para>
    /// <para>
    /// <b>Through <c>_tabbed</c>, which the bar already uses.</b> This was a
    /// switch with an arm per tab naming a view; an arm cannot ask whether its
    /// tab has focus without naming a pane too, and a second list beside
    /// <c>_tabbed</c> is the drift this console keeps finding one field at a
    /// time. The queue keeps a line of its own because its tab has two panes
    /// and only one of them is driven.
    /// </para>
    /// </remarks>
    private void Focus()
    {
        // A MODAL WHOSE FOCUSED VIEW HAS BEEN HIDDEN DOES NOT HAVE FOCUS, and
        // asking it that way is what lets every arm below place the keyboard
        // properly. The first attempt at this broke out of the LeaveAlone arm
        // instead, which falls through to the TAB landing at the bottom of
        // this switch - the main bar's, behind the modal. Focusing a widget in
        // a sibling tab's pane is what raises "FocusChanging was not cancelled
        // and the HasFocus value did not change", which is what the owner saw.
        switch (FocusChange.Wanted(
            State.Mode, State.ActiveTab, _landed, _modal.HasFocus && !Stranded(),
            _airspacePath.HasFocus,
            State.AirspaceReading, _landedReading, State.RunnerView, _landedRunnerView,
            filterView: State.FilterView, landedFilterView: _landedFilterView,
            flightTab: State.FlightTab, landedFlightTab: _landedFlightTab,
            workItemTab: State.WorkItemTab, landedWorkItemTab: _landedWorkItemTab,
            workKindTab: State.WorkKindTab, landedWorkKindTab: _landedWorkKindTab,
            notificationsHaveFocus: _notifications.HasFocus))
        {
            case FocusTarget.LeaveAlone:
                return;

            case FocusTarget.Notifications:
                // "GO TO IT", because it is what somebody picked the corner up
                // to do - and never dismiss, which is the one a reflexive enter
                // would regret.
                (_notificationButtons.FirstOrDefault(b => b.Text == "Go to it")
                    ?? _notificationButtons.FirstOrDefault()
                    ?? (View)_notifications).SetFocus();
                _landed = null;
                return;

            case FocusTarget.AirspacePath:
                // CanFocus WAS SET IN Render, WHICH RUNS FIRST. SetFocus on a
                // view that cannot take it does nothing at all, so the order of
                // these two is load-bearing rather than incidental.
                _airspacePath.SetFocus();
                _landed = null;
                return;

            case FocusTarget.FlightTab:
                // WHICHEVER TAB IS SHOWING, because the bar follows the focused
                // pane: a landing that always named the log made the other two
                // tabs unreachable the moment the log became one of them, and
                // dragging the bar from inside a focus transition is what threw
                // "FocusChanging was not cancelled".
                //
                // AND THE WIDGET WHEN IT HAS ROWS, THE FRAME WHEN IT HAS NONE -
                // the runner views' fallback, for its reason: focus is what
                // makes the arrows move a cursor a person can see, and an empty
                // log has none to move.
                (State.FlightTab switch
                {
                    FlightTab.Log when _flightLog.Visible => _flightLog,
                    FlightTab.Gate => (View)_flightGate,

                    // AND THE FACTS, which scroll for the same reason the gate
                    // does: it is one Label of prose and it can outrun the tab.
                    FlightTab.Facts => _flightFacts,

                    // THE INTENT, which is the half of this tab with anything to
                    // move. The fields below it are read, and Terminal.Gui would
                    // pick the first of them; the intent scrolls, and an intent
                    // taller than the pane is why it does.
                    FlightTab.Details => _flightIntent,
                    _ => (View)_modal,
                }).SetFocus();

                _landed = null;
                _landedFlightTab = State.FlightTab;
                return;

            case FocusTarget.RunnerView:
                // WHICHEVER VIEW IS SHOWING, because the bar follows the
                // focused pane: a landing that always named the log assigned
                // Value back to it every render, and the other two views were
                // reachable for about a second each.
                //
                // AND THE WIDGET WHEN IT HAS ROWS, THE FRAME WHEN IT HAS NONE -
                // the flight log's fallback, for its reason: focus is what
                // makes the arrows move a cursor a person can see, and an empty
                // view has none to move.
                (State.RunnerView switch
                {
                    RunnerView.Environments when _runnerEnvironments.Visible
                        => _runnerEnvironments,
                    RunnerView.Members when _runnerMembers.Visible => _runnerMembers,
                    RunnerView.Log when _runnerSaid.Visible => _runnerSaid,
                    _ => (View)_modal,
                }).SetFocus();

                _landed = null;
                _landedRunnerView = State.RunnerView;
                return;

            case FocusTarget.WorkKindChoices:
                // THE TABLE IN WHICHEVER TAB IS SHOWING. It was the kinds table
                // unconditionally, which was right while that was the whole
                // modal - the flight modal and the work item modal each learned
                // this the same way, by putting the keyboard in a tab nobody
                // had turned to and dragging the bar after it.
                (State.WorkKindTab is WorkKindTab.Repositories && _composeRepos.Visible
                    ? _composeRepos
                    : (View)_kindChoices).SetFocus();

                // RETURNS, LIKE EVERY ARM AROUND IT. It ended in `break', which
                // falls through to the TAB landing below - so placing the
                // keyboard in this modal also placed it in the tab behind, and
                // Terminal.Gui threw on the second move. Harmless while this
                // arm almost never ran; the tab made it run on every turn.
                _landed = null;
                _landedWorkKindTab = State.WorkKindTab;
                return;

            case FocusTarget.CredentialRepositoryChoices:
                // THE TABLE AGAIN, for the reason above it.
                _credentialRepoChoices.SetFocus();
                _landed = null;
                return;

            case FocusTarget.WorkItemTab:
                // THE TAB THAT IS SHOWING, for the reason the flight modal's
                // arm gives: this modal gained a second tab and the landing
                // still named the first one's table, so opening an item put the
                // keyboard in a tab nobody had turned to.
                //
                // AND THE TABLE WHEN IT HAS ROWS, THE FRAME WHEN IT HAS NONE -
                // the same fallback, for the same reason: an empty history has
                // no cursor to move.
                (State.WorkItemTab switch
                {
                    // THE TABLE, which is the whole of this tab and the only
                    // thing in it with a cursor.
                    WorkItemTab.Fields when _itemFieldsTable.Visible => _itemFieldsTable,

                    WorkItemTab.History when _itemHistory.Visible => _itemHistory,

                    // THE BUTTON, which is the one thing on this tab a person
                    // came to press - so enter does it without a tab-stop walk
                    // first. When there is nothing to fly it is hidden, and
                    // focus falls through to the modal rather than landing on
                    // a view that is not drawn.
                    WorkItemTab.Actions when _itemFly.Visible => _itemFly,

                    // THE PROSE, which is what this tab is. The fields below it
                    // are read and would be picked first otherwise, and what a
                    // person opened the item to do is read what it says.
                    WorkItemTab.Details => (View)_itemSaid,
                    _ => _modal,
                }).SetFocus();

                _landed = null;
                _landedWorkItemTab = State.WorkItemTab;
                return;

            case FocusTarget.FilterView:
                // THE TABLE IN WHICHEVER TAB IS SHOWING, and the pane when it
                // has no rows - the runner views' fallback, for its reason:
                // focus is what makes the arrows move a cursor a person can
                // see, and an empty list has none to move.
                (_filterTabbed.FirstOrDefault(t => t.View == State.FilterView) is
                { Pane: not null } tab && tab.Table.Visible
                        ? tab.Table
                        : (View)_modal).SetFocus();

                _landed = null;
                _landedFilterView = State.FilterView;
                return;

            case FocusTarget.AirspaceDocument:
                // THE LIST IN WHICHEVER VIEW IS SHOWING, because that is the
                // one with the lines in it. The other two are behind it and
                // hold whatever they last drew.
                if (_viewTabbed.FirstOrDefault(t => t.View == State.AirspaceView).Said
                    is { } said)
                {
                    said.SetFocus();
                }

                _landed = State.ActiveTab;
                _landedReading = true;
                return;

            case FocusTarget.Modal:
                _modal.SetFocus();

                // AND INTO THE PAGE, because a Dialog stops at itself and the
                // arrows then move nothing. The page that is showing is the
                // only one visible, so this cannot land in a hidden tab.
                if (State.Mode is UiMode.Help)
                {
                    FocusPage();
                }

                // AND INSIDE THE HELP MODAL, ON THE PAGE RATHER THAN ON THE
                // BAR. A Dialog gives focus to its first focusable child, which
                // since the help modal grew a tab bar is the BAR - so the bar
                // took the keyboard, answered tab itself, and `tab turns the
                // page' stopped being true. The bar is a renderer here: the
                // model says which page shows and Render puts it there.
                //
                // The page's own content is the right place for the keyboard
                // anyway: it is what the arrows should move, and the tree is
                // quiet, so tab bubbles past it to the keymap - which is the
                // only thing that decides what tab means.
                // ONLY THE TREE, and only because it has a cursor to move.
                // The other two pages are read, not navigated, and focusing a
                // plain Label was the bug this replaced: Terminal.Gui advances
                // focus on tab for any focusable view that does not say
                // otherwise, so tab stopped turning the page the moment a
                // person reached Environment or Doctor. The tree says
                // otherwise - it is quiet, like the tables - so tab bubbles
                // past it to the keymap, which is the only thing that decides
                // what tab means.

                // FORGOTTEN WHILE THE MODAL HAS IT, which is what makes closing
                // one a change. The tab does not move while a modal is open, so
                // remembering it here would leave focus on a modal that is no
                // longer on the screen.
                _landed = null;
                return;
        }

        // WHERE FOCUS LANDS WHEN THE TAB IS NEW. The tables take it when they
        // have rows, because focus is what makes the arrow keys move a cursor a
        // person can see, and the label beside each is only on screen when there
        // is nothing to point at.
        //
        // EXHAUSTIVE, AND THAT IS THE POINT. This had a `_ => _queue` default,
        // so Allowances - the one tab added since - landed on the QUEUE tab's
        // list. Focusing a widget inside a SIBLING tab's pane makes
        // Terminal.Gui's Tabs notice that another tab now has focus, assign
        // Value to it and raise ValueChanged; the screen reads that as a person
        // picking a tab, reduces and renders from inside that, and the focus
        // transition that started it comes back to find HasFocus moved. It
        // throws: "FocusChanging was not cancelled and the HasFocus value did
        // not change."
        //
        // A default ANSWERED that question, so a new tab could never fail to
        // compile and never fail a test. Without one the compiler asks.
        View landing = State.ActiveTab switch
        {
            TabId.Flights => _flightsTable.Visible ? _flightsTable : _flights,
            TabId.Board => _boardTable.Visible ? _boardTable : _board,
            TabId.Live => _live,
            TabId.Browse => _browseTable.Visible ? _browseTable : _browse,
            TabId.Repositories => _repositoriesTable.Visible ? _repositoriesTable : _repositories,

            // THE TABLE, NOT THE BUTTON ABOVE IT. Terminal.Gui would pick the
            // button, because it is the first focusable child - and a tab whose
            // arrow keys do nothing until you press one to get off a button is
            // a tab that reads as broken.
            TabId.Runners => _runnersTable.Visible ? _runnersTable : _runners,
            // THE BOX, NOT THE LABEL ABOVE IT. Landing on the path is what
            // makes it selectable on arrival and puts enter one keystroke
            // from editing - and the label has nothing a cursor means.
            // THE TREE, NOT THE BOX BELOW IT. Landing on the path was right
            // while the pane above it was a label with nothing a cursor
            // meant. It is a table now, and `enter` still reaches the box
            // from it.
            TabId.Envelope => _airspaceTable.Visible ? _airspaceTable : _airspacePath,

            // ITS OWN LABEL, which is the whole of that pane. There is nothing
            // to point at on it - it is read rather than driven - so this is
            // about being INSIDE the right pane rather than about a cursor.
            TabId.Allowances => _allowances,

            // The queue tab is the one with two panes, and the list is the half
            // a person drives - the flight beside it is what the cursor means.
            TabId.Queue => _queue,

            // REFUSES RATHER THAN ANSWERING. C# needs an arm for values the
            // enum does not name, so this cannot be deleted - and it must not
            // name a widget, or it is the default that hid this bug. Every
            // other TabId switch in this console ends the same way.
            _ => throw new ArgumentOutOfRangeException(
                nameof(State.ActiveTab), State.ActiveTab, "unknown tab"),
        };

        landing.SetFocus();
        _landed = State.ActiveTab;
        _landedReading = State.AirspaceReading;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            KeyDown -= OnScreenKeyDown;
            _modal.KeyDown -= OnModalKeyDown;

            foreach (var (_, _, table, _) in _filterTabbed)
            {
                table.KeyDown -= OnModalKeyDown;
                table.ValueChanged -= OnModalRowPointedAt;
            }

            _filterViews.ValueChanged -= OnFilterViewChanged;

            _itemHistory.KeyDown -= OnModalKeyDown;
            _itemHistory.ValueChanged -= OnModalRowPointedAt;

            _kindChoices.KeyDown -= OnModalKeyDown;
            _kindChoices.ValueChanged -= OnModalRowPointedAt;
            _modalBody.KeyDown -= OnModalKeyDown;
            _runnerStart.Accepting -= OnStartRunner;
            _runnerStart.KeyDown -= OnButtonKeyDown;
            _runnersTable.KeyDown -= OnTableKeyDown;

            foreach (var table in (TableView[])
                     [_flightsTable, _boardTable, _browseTable, _repositoriesTable,
                      _runnersTable, _airspaceTable])
            {
                table.KeyDown -= OnTableEdge;
            }
            _runnerViews.ValueChanged -= OnRunnerViewChanged;
            _runnerEnvironments.ValueChanged -= OnModalRowPointedAt;
            _runnerMembers.ValueChanged -= OnModalRowPointedAt;
            _airspacePath.KeyDown -= OnAirspacePathKeyDown;
            _airspaceTable.ValueChanged -= OnRowPointedAt;
            _airspaceViews.ValueChanged -= OnAirspaceViewChanged;

            foreach (var (_, _, said) in _viewTabbed)
            {
                said.ViewportChanged -= OnAirspaceDocumentResized;
            }
            _readingSaid.ViewportChanged -= OnReadingResized;

            // ALL FOUR, and three of them were missed. The file already let go
            // of the key handler and the queue's, so the convention was there
            // and the tables were outside it - which is how the fourth came to
            // be built without a subscription at all.
            _flightsTable.ValueChanged -= OnRowPointedAt;
            _boardTable.ValueChanged -= OnRowPointedAt;
            _browseTable.ValueChanged -= OnRowPointedAt;
            _repositoriesTable.ValueChanged -= OnRowPointedAt;
            _runnersTable.ValueChanged -= OnRowPointedAt;
            _flightLog.ValueChanged -= OnLogRowPointedAt;
            _flightLog.ViewportChanged -= OnLogResized;
            _queue.ValueChanged -= OnQueueSelectionChanged;
        }
        base.Dispose(disposing);
    }
}
