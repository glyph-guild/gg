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
    private readonly FrameView _queuePane;
    private readonly FrameView _flightPane;
    private readonly FrameView _evidencePane;
    private readonly FrameView _livePane;
    private readonly FrameView _modal;
    private readonly Label _modalBody;
    private readonly Label _hints;
    private readonly Label _activity;

    public AppState State { get; private set; }

    public Command ExitCommand { get; private set; } = Command.Quit;

    public ConsoleScreen(IApplication app, AppState state)
    {
        _app = app;
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

        _evidencePane = new FrameView
        {
            Title = "Evidence",
            X = Pos.Right(_queuePane),
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Percent(50),
            Visible = false,
        };
        _evidence = new Label { Width = Dim.Fill(), Height = Dim.Fill(), CanFocus = true };
        _evidencePane.Add(_evidence);

        _livePane = new FrameView
        {
            Title = "Live",
            X = Pos.Right(_queuePane),
            Y = Pos.Bottom(_evidencePane),
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

        Add(_queuePane, _flightPane, _evidencePane, _livePane, _activity, _hints, _modal);

        KeyDown += OnScreenKeyDown;
        _queue.ValueChanged += OnQueueSelectionChanged;

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
        // decides what is selected and the view reports what was clicked.
        var wanted = args.NewValue ?? 0;
        if (wanted != State.SelectedRow)
        {
            State = Reducer.Reduce(State, wanted > State.SelectedRow ? Command.SelectNext : Command.SelectPrevious);
            Render();
        }
    }

    private KeymapContext Context() => new(State.Mode, State.LiveVisible, State.Frozen);

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

        _evidencePane.Visible = State.EvidenceVisible;
        _livePane.Visible = State.LiveVisible;
        _livePane.Title = State.Frozen ? "Live (frozen — f to resume)" : "Live";
        // The flight pane gives up its space rather than being covered.
        _flightPane.Visible = !State.EvidenceVisible && !State.LiveVisible;

        _modal.Visible = State.Mode != UiMode.Normal;
        _modal.Title = State.Mode.ToString();
        _modalBody.Text = PaneText.Modal(State);

        _activity.Text = PaneText.Activity(State);
        _hints.Text = Keymap.Hints(Context());

        Focus();
    }

    private void Focus()
    {
        if (State.Mode != UiMode.Normal)
        {
            _modal.SetFocus();
            return;
        }

        switch (State.FocusedPane)
        {
            case PaneId.Evidence:
                _evidence.SetFocus();
                break;
            case PaneId.Live:
                _live.SetFocus();
                break;
            case PaneId.Flight:
                _flight.SetFocus();
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
