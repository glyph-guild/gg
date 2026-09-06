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
    private readonly FrameView _modal;
    private readonly Label _modalBody;
    private readonly Label _hints;
    private readonly Label _activity;

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
        _queue = new ListView { Width = Dim.Fill(), Height = Dim.Fill() };
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
        _flightsPane.Add(_flights);

        _hints = new Label { X = 0, Y = Pos.AnchorEnd(1), Width = Dim.Fill() };

        // ABOVE THE HINTS, on a line of its own. A write a person cannot see is
        // indistinguishable from a key that does nothing.
        _activity = new Label { X = 0, Y = Pos.AnchorEnd(2), Width = Dim.Fill() };

        _modal = new FrameView
        {
            X = Pos.Center(),
            Y = Pos.Center(),
            Width = 52,
            Height = 12,
            Visible = false,
        };
        _modalBody = new Label { Width = Dim.Fill(), Height = Dim.Fill() };
        _modal.Add(_modalBody);

        Add(_queuePane, _flightPane, _flightsPane, _evidencePane, _livePane, _browsePane, _repositoriesPane,
            _checklistPane, _envelopePane, _activity, _hints, _modal);

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
        _checklist.Text = PaneText.Checklist(State);
        _envelope.Text = PaneText.Envelope(State);

        // ONE FUNCTION DECIDES WHAT IS ON SCREEN, and it is pure. Tabs.Showing
        // answers true for exactly one tab, which is what "a view takes over
        // all the panes" means concretely - and it is asserted over states
        // rather than over pixels.
        _flights.Text = PaneText.Flights(State);
        _flightsPane.Visible = Tabs.Showing(State, TabId.Flights);
        _evidencePane.Visible = Tabs.Showing(State, TabId.Evidence);
        _livePane.Visible = Tabs.Showing(State, TabId.Live);
        _livePane.Title = State.Frozen ? "Live (frozen — f to resume)" : "Live";
        _browsePane.Visible = Tabs.Showing(State, TabId.Browse);
        _checklistPane.Visible = Tabs.Showing(State, TabId.Checklist);
        _envelopePane.Visible = Tabs.Showing(State, TabId.Envelope);
        _repositoriesPane.Visible = Tabs.Showing(State, TabId.Repositories);
        _repositories.Text = PaneText.Repositories(State);

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

        // THE QUEUE TAB IS TWO PANES, and they come and go together: the flight
        // detail is what the selected row means, and a person moving the cursor
        // is reading both.
        _queuePane.Visible = Tabs.Showing(State, TabId.Queue);
        _flightPane.Visible = Tabs.Showing(State, TabId.Queue);

        // THE TABS ARE ON THE TITLE LINE, which is the line a person reads
        // without being asked to. Empty while only the queue is open, because a
        // bar with one tab on it is decoration.
        Title = Tabs.Bar(State) is { Length: > 0 } bar ? $"Good Grief   {bar}" : "Good Grief";

        _modal.Visible = State.Mode != UiMode.Normal;
        _modal.Title = State.Mode.ToString();
        _modalBody.Text = PaneText.Modal(State);

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
                _flights.SetFocus();
                break;
            case TabId.Evidence:
                _evidence.SetFocus();
                break;
            case TabId.Live:
                _live.SetFocus();
                break;
            case TabId.Browse:
                _browse.SetFocus();
                break;
            case TabId.Repositories:
                _repositories.SetFocus();
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
            _queue.ValueChanged -= OnQueueSelectionChanged;
        }
        base.Dispose(disposing);
    }
}
