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

    public ConsoleScreen(IApplication app, AppState state, LiveTails? tails = null)
    {
        _app = app;
        _tails = tails;
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
        _modal.Add(_modalBody);

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
            (TabId.Evidence, Tabbed(_evidencePane)),
            (TabId.Live, Tabbed(_livePane)),
            (TabId.Browse, Tabbed(_browsePane)),
            (TabId.Repositories, Tabbed(_repositoriesPane)),
            (TabId.Runners, Tabbed(_runnersPane)),
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
        Muted(_envelope, _checklist, _evidence, _live, _flight, _modalBody, _runners);

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
        if (_tails is null)
        {
            return;
        }

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
                r => [r.Here, r.Runner, r.State, r.Work, r.Heard]);

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
        _modal.Title = PaneText.ModalTitle(State.Mode);
        _modalBody.Text = PaneText.Modal(State);

        // SIZED BY WHAT IS IN IT. A question with two answers wants a box a
        // person's eye can take in at once; a document wants the screen. The
        // help page is twenty-one keys and a flight's detail is its whole log,
        // and both were being drawn into fifty-two columns by twelve rows -
        // which is a scrollbar where a reader wanted a page.
        var document = State.Mode is UiMode.Help or UiMode.FlightDetail;

        _modal.Width = document ? Dim.Percent(92) : 52;
        _modal.Height = document ? Dim.Percent(88) : 12;

        _activity.Text = PaneText.Activity(State);
        _hints.Text = Keymap.Hints(Context());

        Focus();
    }

    /// <summary>
    /// Focus follows the tab, because the tab is the only thing on screen.
    /// </summary>
    /// <remarks>
    /// It used to follow a <c>FocusedPane</c> that tab cycled independently of
    /// what was visible. With one view on the screen there is nothing to choose
    /// between: the queue tab focuses its list, because that is the pane a
    /// person drives, and every other tab focuses the one pane it has.
    /// </remarks>
    private void Focus()
    {
        if (State.Mode != UiMode.Normal)
        {
            _modal.SetFocus();
            return;
        }

        switch (State.ActiveTab)
        {
            case TabId.Flights:
                // THE TABLE, WHEN THERE IS ONE. Focus is what makes the arrow
                // keys move the cursor a person can see, and the label is only
                // on screen when there is nothing to point at.
                (_flightsTable.Visible ? (View)_flightsTable : _flights).SetFocus();
                break;
            case TabId.Evidence:
                _evidence.SetFocus();
                break;
            case TabId.Live:
                _live.SetFocus();
                break;
            case TabId.Browse:
                (_browseTable.Visible ? (View)_browseTable : _browse).SetFocus();
                break;
            case TabId.Repositories:
                (_repositoriesTable.Visible ? (View)_repositoriesTable : _repositories).SetFocus();
                break;
            case TabId.Runners:
                (_runnersTable.Visible ? (View)_runnersTable : _runners).SetFocus();
                break;
            case TabId.Checklist:
                _checklist.SetFocus();
                break;
            case TabId.Envelope:
                _envelope.SetFocus();
                break;
            default:
                _queue.SetFocus();
                break;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            KeyDown -= OnScreenKeyDown;
            _runnerStart.Accepting -= OnStartRunner;

            // ALL FOUR, and three of them were missed. The file already let go
            // of the key handler and the queue's, so the convention was there
            // and the tables were outside it - which is how the fourth came to
            // be built without a subscription at all.
            _flightsTable.ValueChanged -= OnRowPointedAt;
            _browseTable.ValueChanged -= OnRowPointedAt;
            _repositoriesTable.ValueChanged -= OnRowPointedAt;
            _runnersTable.ValueChanged -= OnRowPointedAt;
            _queue.ValueChanged -= OnQueueSelectionChanged;
        }
        base.Dispose(disposing);
    }
}
