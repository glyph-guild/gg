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
    private readonly Label _evidence;
    private readonly Label _live;
    private readonly Label _browse;
    private readonly FrameView _queuePane;
    private readonly FrameView _flightPane;
    private readonly FrameView _evidencePane;
    private readonly FrameView _livePane;
    private readonly FrameView _browsePane;
    private readonly FrameView _repositoriesPane;
    private readonly Label _repositories;
    private readonly Label _checklist;
    private readonly FrameView _checklistPane;
    private readonly Label _envelope;
    private readonly FrameView _envelopePane;
    private readonly Label _flights;
    private readonly FrameView _flightsPane;

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
    private readonly TableView _browseTable;
    private readonly TableView _repositoriesTable;
    private readonly FrameView _runnersPane;
    private readonly Label _runners;
    private readonly Label _runnerNotice;
    private readonly Button _runnerStart;
    private readonly TableView _runnersTable;
    private readonly FrameView _modal;
    private readonly Label _modalBody;

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
    private readonly FrameView _flightIntentPane;
    private readonly Markdown _flightIntent;
    private readonly View _flightFields;
    private readonly FrameView _flightLogPane;
    private readonly TableView _flightLog;
    private readonly Label _flightLogAbsent;
    private readonly View _runnerBody;
    private readonly View _runnerFields;
    private readonly FrameView _runnerLogPane;
    private readonly ListView _runnerSaid;
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
    private readonly Label _activity;

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

    public ConsoleScreen(
        IApplication app,
        AppState state,
        LiveTails? tails = null,
        IRunnerLog? runnerLog = null,
        AutoRefresh? refresh = null,
        Func<bool>? signInLanded = null)
    {
        _app = app;
        _tails = tails;
        _runnerLog = runnerLog;
        _refresh = refresh;
        _signInLanded = signInLanded;
        State = state;
        Title = "Good Grief";

        _queuePane = new FrameView
        {
            Title = "Queue",
            X = 0,
            Y = 0,
            Width = Dim.Percent(38),
            Height = Dim.Fill(1),
        };
        _queue = CollectionViews.List();
        _queuePane.Add(_queue);

        _flightPane = new FrameView
        {
            Title = "Flight",
            X = Pos.Right(_queuePane),
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(1),
        };
        _flight = new Label { Width = Dim.Fill(), Height = Dim.Fill(), CanFocus = true };
        _flightPane.Add(_flight);

        // FULL SCREEN, LIKE EVERY OTHER TAB. It used to take the top half of
        // the right-hand side with live or browse underneath it, which is why
        // the model had to keep six flags from colliding.
        _evidencePane = new FrameView
        {
            Title = "Evidence",
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(1),
            Visible = false,
        };
        _evidence = new Label { Width = Dim.Fill(), Height = Dim.Fill(), CanFocus = true };
        _evidencePane.Add(_evidence);

        _livePane = new FrameView
        {
            Title = "Live",
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
            Title = "Browse",
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(1),
            Visible = false,
        };
        _browse = new Label { Width = Dim.Fill(), Height = Dim.Fill(), CanFocus = true };
        _browsePane.Add(_browse);

        // THE FOURTH OCCUPANT OF THAT REGION. ChecklistToggled turns the other
        // three off for the same reason BrowseToggled turns two off: two visible
        // flags over one region is two panes drawn on top of each other.
        _checklistPane = new FrameView
        {
            Title = "Checklist",
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(1),
            Visible = false,
        };
        _checklist = new Label { Width = Dim.Fill(), Height = Dim.Fill(), CanFocus = true };
        _checklistPane.Add(_checklist);

        // THE FIFTH OCCUPANT OF THAT ONE REGION.
        _envelopePane = new FrameView
        {
            Title = "Envelope",
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(1),
            Visible = false,
        };
        _envelope = new Label { Width = Dim.Fill(), Height = Dim.Fill(), CanFocus = true };
        _envelopePane.Add(_envelope);

        // THE SAME REGION AGAIN. Four panes now share it and never two at
        // once, which RepositoriesToggled enforces rather than the order these
        // are added in.
        _repositoriesPane = new FrameView
        {
            Title = "Repositories",
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
            Title = "Runners",
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
            Title = "Flights",
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(1),
            Visible = false,
        };
        _flights = new Label { Width = Dim.Fill(), Height = Dim.Fill(), CanFocus = true };
        _flightsTable = CollectionViews.Table();
        _flightsPane.Add(_flights, _flightsTable);
        _browseTable = CollectionViews.Table();
        _browsePane.Add(_browseTable);
        _repositoriesTable = CollectionViews.Table();
        _repositoriesPane.Add(_repositoriesTable);
        _runnersTable = CollectionViews.Table();
        _runnersPane.Add(_runnersTable);

        _flightsTable.ValueChanged += OnRowPointedAt;
        _browseTable.ValueChanged += OnRowPointedAt;
        _repositoriesTable.ValueChanged += OnRowPointedAt;
        _runnersTable.ValueChanged += OnRowPointedAt;
        _runnersTable.KeyDown += OnTableKeyDown;

        _hints = new Label { X = 0, Y = Pos.AnchorEnd(1), Width = Dim.Fill() };

        // ABOVE THE HINTS, on a line of its own. A write a person cannot see is
        // indistinguishable from a key that does nothing.
        _activity = new Label { X = 0, Y = Pos.AnchorEnd(2), Width = Dim.Fill() };

        _modal = new FrameView
        {
            X = Pos.Center(),
            Y = Pos.Center(),
            Visible = false,
        };
        _modalBody = new Label { Width = Dim.Fill(), Height = Dim.Fill(), CanFocus = true };

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

        _flightLogPane = new FrameView
        {
            Title = FlightDetails.LogTitle,
            X = 0,
            Y = Pos.Bottom(_flightFields),
            Width = Dim.Fill(),
            Height = Dim.Fill(),

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
        _flightBody.Add(_flightIntentPane, _flightFields, _flightLogPane);

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

        _runnerLogPane = new FrameView
        {
            Title = RunnerDetails.LogTitle,
            X = 0,
            Y = Pos.Bottom(_runnerFields),
            Width = Dim.Fill(),
            Height = Dim.Fill(),
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

        _runnerBody = new View
        {
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            Visible = false,
            CanFocus = true,
            TabStop = TabBehavior.TabStop,
        };
        _runnerBody.Add(_runnerFields, _runnerLogPane);

        // THE INTENT IS AS TALL AS WHAT IS IN IT, capped against the room there
        // is. A third of the modal was a box sized for a page around `fix the
        // login bug', and the log underneath is what paid for it. Dim.Func
        // rather than a height set per render, because the cap wants the
        // laid-out size and a render happens before the layout does - and here
        // rather than in the initializer, because the body it measures against
        // has to exist first.
        _flightIntentPane.Height = Dim.Func(
            _ => FlightDetails.IntentRows(
                FlightDetails.IntentLines(State), _flightBody.Viewport.Height),
            _flightIntentPane);

        _modal.Add(_modalBody, _flightBody, _runnerBody);

        // THE QUEUE TAB IS TWO PANES, so it gets a container: the list a person
        // drives and the detail of whatever it lands on are one view of one
        // thing.
        var queueTab = new View { Title = "Queue", Width = Dim.Fill(), Height = Dim.Fill() };
        _queuePane.Height = Dim.Fill();
        _flightPane.Height = Dim.Fill();
        queueTab.Add(_queuePane, _flightPane);

        // EVERY TAB, FROM THE START. The bar's job is to say what there is, so
        // all eight panes are built and all eight are inserted; which one draws
        // is the model's to say and the component's to show.
        _tabbed =
        [
            (TabId.Queue, queueTab),
            (TabId.Flights, Tabbed(_flightsPane)),

            // BESIDE THE FLIGHTS, WHERE IT IS DECLARED. This was appended after
            // Repositories, so the bar drew it seventh while Tabs.Next - which
            // walks the enum - reached it third, and `tab' skipped six tabs.
            // TabGoesLeftToRightTests holds the two orders together now.
            (TabId.Runners, Tabbed(_runnersPane)),
            (TabId.Evidence, Tabbed(_evidencePane)),
            (TabId.Live, Tabbed(_livePane)),
            (TabId.Browse, Tabbed(_browsePane)),
            (TabId.Repositories, Tabbed(_repositoriesPane)),
            (TabId.Checklist, Tabbed(_checklistPane)),
            (TabId.Envelope, Tabbed(_envelopePane)),
        ];

        _bar = new Terminal.Gui.Views.Tabs
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(2),
        };

        foreach (var (tab, pane) in _tabbed)
        {
            pane.Title = Tabs.Title(State, tab);
            _bar.Add(pane);
        }

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
        Muted(_envelope, _checklist, _evidence, _live, _flight, _modalBody, _runners,
            _flightIntent, _flightLogAbsent);

        Add(_bar, _activity, _hints, _modal);

        KeyDown += OnScreenKeyDown;
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

    private void OnRowPointedAt(object? sender, ValueChangedEventArgs<TableSelection?> args)
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

        CollectionViews.Fill(table, new DataTableSource(data));
        table.SetSelection(0, Math.Clamp(cursor, 0, rows.Count - 1), extendExistingSelection: false, null);
        table.EnsureValidSelection();
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

        if (ShellCommands.Handled.Contains(command))
        {
            ExitCommand = command;
            _app.RequestStop(this);
            return;
        }

        State = Reducer.Reduce(State, command);
        Render();
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

    private void OnScreenKeyDown(object? sender, Key key)
    {
        var stroke = KeyTranslator.Translate(key);
        var command = Keymap.Resolve(stroke, Context());
        if (command is null)
        {
            return;
        }

        key.Handled = true;
        // ONE DECLARATION, READ HERE. A literal list is what this was, and it
        // silently excluded four commands the shell already had arms for.
        if (ShellCommands.Handled.Contains(command.Value))
        {
            ExitCommand = command.Value;
            _app.RequestStop(this);
            return;
        }

        State = Reducer.Reduce(State, command.Value);
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
    private void Render()
    {
        _queue.SetSource(new ObservableCollection<string>(PaneText.QueueRows(State)));
        if (State.Queue.Count > 0)
        {
            _queue.SelectedItem = Math.Clamp(State.SelectedRow, 0, State.Queue.Count - 1);
        }

        _flight.Text = PaneText.Flight(State);
        _evidence.Text = PaneText.Evidence(State);

        // Frozen means the pixels stop moving, so the terminal's own selection
        // can survive being made. Held lines are already kept in the model;
        // this is the half of the promise the view owes.
        if (!State.Frozen)
        {
            _live.Text = PaneText.Live(State);
        }

        _browse.Text = PaneText.Browse(State);

        // THE TABLE WHEN THERE ARE ROWS, THE SENTENCE WHEN THERE ARE NOT. A
        // header over no rows claims a read succeeded and found nothing, which
        // is one of three things an empty pane can mean - so each pane keeps
        // its own words for the other two.
        _syncing = true;
        try
        {
            Fill(_flightsTable, _flights, Rows.Flights(State), Rows.FlightColumns,
                State.FlightSelected,
                r => [r.Number, r.State, r.Loop, r.Age, r.Work]);

            Fill(_browseTable, null, Rows.Browse(State), Rows.BrowseColumns,
                State.BrowseSelected,
                r => [r.Id, r.State, r.Title]);

            Fill(_repositoriesTable, null, Rows.Repositories(State), Rows.RepositoryColumns,
                State.RepositorySelected,
                r => [r.Chosen, r.Path, r.Name]);

            // OFF THE MODEL, like the other three. This passed a literal 0 and
            // a comment saying nothing here is selectable - true of the model
            // and never true of the widget, so the cursor snapped back to the
            // top on every render under the person moving it.
            Fill(_runnersTable, _runners, Rows.Runners(State), Rows.RunnerColumns,
                State.RunnerSelected,
                r => [r.Here, r.Runner, r.State, r.Work, r.Labels, r.Heard]);

            // THE NOTICE LABEL ONLY WHEN THE TABLE IS SHOWING. With no rows the
            // empty-state label already leads with it, and the same sentence
            // twice reads as two problems.
            var notice = PaneText.RunnerNotice(State);
            _runnerNotice.Text = notice;
            _runnerNotice.Visible = notice.Length > 0 && _runnersTable.Visible;
            _runnerStart.Visible = notice.Length > 0;
            _runnersTable.Y = _runnerNotice.Visible ? 2 : 0;
        }
        finally
        {
            _syncing = false;
        }
        _checklist.Text = PaneText.Checklist(State);
        _envelope.Text = PaneText.Envelope(State);

        _flights.Text = PaneText.Flights(State);
        _repositories.Text = PaneText.Repositories(State);
        _runners.Text = PaneText.Runners(State);
        _livePane.Title = State.Frozen ? "Live (frozen — f to resume)" : "Live";

        // WHICH ONE IS CHOSEN, IN THE TITLE. It changes what every flight this
        // console opens will name, so a person glancing at the frame should
        // learn it without reading the rows.
        _repositoriesPane.Title = State.ChosenRepository is { Length: > 0 } chosen
            ? $"Repositories — flying against {chosen}"
            : "Repositories";

        // THE TRACKER IS IN THE TITLE, because a tenant may configure more than
        // one and a list of work items with no attribution is a list nobody can
        // act on. It is in the body too; a person reading either should not
        // have to look at the other.
        _browsePane.Title = State.Browse is { ProviderKey.Length: > 0 } listing
            ? $"Browse — {listing.ProviderKey}"
            : "Browse";

        // WHICH TAB IS SHOWING IS STILL THE MODEL'S, and the component is told
        // rather than asked. Tabs.Showing answers true for exactly one tab -
        // asserted over generated states rather than over pixels - and the sync
        // flag is what stops the assignment answering its own event.
        foreach (var (tab, pane) in _tabbed)
        {
            pane.Title = Tabs.Title(State, tab);
        }

        _syncing = true;
        try
        {
            var showing = _tabbed.First(t => Tabs.Showing(State, t.Tab)).Pane;
            if (!ReferenceEquals(_bar.Value, showing))
            {
                _bar.Value = showing;
            }
        }
        finally
        {
            _syncing = false;
        }

        _modal.Visible = State.Mode != UiMode.Normal;

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

        _flightBody.Visible = flight;
        _runnerBody.Visible = runner;
        _modalBody.Visible = !flight && !runner;

        if (flight)
        {
            RenderFlight();
        }
        else if (runner)
        {
            RenderRunner();
        }
        else
        {
            _modalBody.Text = PaneText.Modal(State);
        }

        // SIZED BY WHAT IS IN IT. A question with two answers wants a box a
        // person's eye can take in at once; a document wants the screen. The
        // help page is twenty-one keys and a flight's detail is its whole log,
        // and both were being drawn into fifty-two columns by twelve rows -
        // which is a scrollbar where a reader wanted a page.
        var document = PaneText.ModalIsADocument(State.Mode);

        _modal.Width = document ? Dim.Percent(92) : 52;
        _modal.Height = document ? Dim.Percent(88) : 12;

        _activity.Text = PaneText.Activity(State);
        _hints.Text = Keymap.Hints(Context());

        Focus();
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

    /// <summary>The frame changed width, so the lines have to be broken again.</summary>
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

        // MEASURED HERE, DECIDED THERE. How wide the detail column is depends
        // on the terminal, and what goes in it depends on the model; the view
        // owns exactly the first half. Viewport is zero before the first
        // layout, and a width of zero means wrap nothing.
        var width = _flightLog.Viewport.Width;
        var shown = Rows.Unwrapped(log, State.LogSelected, Rows.DetailWidth(log, width));

        var showing = (State.Story?.FlightId ?? "", shown.Count, State.LogSelected, width);

        if (_logShowing != showing)
        {
            _syncing = true;

            try
            {
                CollectionViews.Fill(
                    _flightLog,
                    shown.Count == 0
                        ? null
                        : new DataTableSource(CollectionViews.Rows(
                            Rows.LogColumns,
                            [.. shown.Select(r => new[] { r.Mark, r.Time, r.Attempt, r.Event })])));

                // ON THE ENTRY'S FIRST ROW. Filling replaces the source, which
                // resets the selection - and the model's cursor is an entry, so
                // where that entry STARTS is the row the highlight belongs on.
                if (shown.Count > 0)
                {
                    var at = Math.Max(0, IndexOfEntry(shown, State.LogSelected));

                    _flightLog.SetSelection(0, at, extendExistingSelection: false, null);
                    _flightLog.EnsureValidSelection();
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
    private void OnLogRowPointedAt(object? sender, ValueChangedEventArgs<TableSelection?> args)
    {
        if (_syncing || args.NewValue is not { } selection)
        {
            return;
        }

        var log = Rows.Log(State);
        var shown = Rows.Unwrapped(
            log, State.LogSelected, Rows.DetailWidth(log, _flightLog.Viewport.Width));

        var row = selection.SelectedCell.Y;

        if (row < 0 || row >= shown.Count)
        {
            return;
        }

        var pointed = Reducer.Pointed(State, shown[row].Entry);

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
        switch (FocusChange.Wanted(State.Mode, State.ActiveTab, _landed, _modal.HasFocus))
        {
            case FocusTarget.LeaveAlone:
                return;

            case FocusTarget.FlightLog:
                // THE TABLE WHEN IT HAS ROWS, THE FRAME WHEN IT HAS NONE - the
                // same fallback the tabs make, and for the same reason: focus
                // is what makes the arrows move a cursor a person can see, and
                // an empty log has none to move.
                (_flightLog.Visible ? _flightLog : (View)_modal).SetFocus();
                _landed = null;
                return;

            case FocusTarget.Modal:
                _modal.SetFocus();

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
        View landing = State.ActiveTab switch
        {
            TabId.Flights => _flightsTable.Visible ? _flightsTable : _flights,
            TabId.Evidence => _evidence,
            TabId.Live => _live,
            TabId.Browse => _browseTable.Visible ? _browseTable : _browse,
            TabId.Repositories => _repositoriesTable.Visible ? _repositoriesTable : _repositories,

            // THE TABLE, NOT THE BUTTON ABOVE IT. Terminal.Gui would pick the
            // button, because it is the first focusable child - and a tab whose
            // arrow keys do nothing until you press one to get off a button is
            // a tab that reads as broken.
            TabId.Runners => _runnersTable.Visible ? _runnersTable : _runners,
            TabId.Checklist => _checklist,
            TabId.Envelope => _envelope,

            // The queue tab is the one with two panes, and the list is the half
            // a person drives - the flight beside it is what the cursor means.
            _ => _queue,
        };

        landing.SetFocus();
        _landed = State.ActiveTab;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            KeyDown -= OnScreenKeyDown;
            _runnerStart.Accepting -= OnStartRunner;
            _runnerStart.KeyDown -= OnButtonKeyDown;
            _runnersTable.KeyDown -= OnTableKeyDown;

            // ALL FOUR, and three of them were missed. The file already let go
            // of the key handler and the queue's, so the convention was there
            // and the tables were outside it - which is how the fourth came to
            // be built without a subscription at all.
            _flightsTable.ValueChanged -= OnRowPointedAt;
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
