using System.Collections.ObjectModel;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Gg.Console.Views;

/// <summary>
/// The whole screen, built FROM an <see cref="AppState"/>. Views here render
/// the model and forward input; they are never the source of truth. When the
/// session ends, <see cref="State"/> is everything worth keeping.
/// </summary>
public sealed class ConsoleScreen : Window
{
    private readonly IApplication _app;
    private readonly ListView _flights;
    private readonly Label _notes;
    private readonly FrameView _flightsPane;
    private readonly FrameView _notesPane;
    private readonly FrameView _helpOverlay;
    private readonly Label _hints;

    public AppState State { get; private set; }

    public Command ExitCommand { get; private set; } = Command.Quit;

    public ConsoleScreen(IApplication app, AppState state)
    {
        _app = app;
        State = state;
        Title = "Good Grief";

        _flightsPane = new FrameView
        {
            Title = "Flights",
            X = 0,
            Y = 0,
            Width = Dim.Percent(40),
            Height = Dim.Fill(1),
        };
        _flights = new ListView { Width = Dim.Fill(), Height = Dim.Fill() };
        _flights.SetSource(new ObservableCollection<string>(State.Flights));
        _flightsPane.Add(_flights);

        _notesPane = new FrameView
        {
            Title = "Notes",
            X = Pos.Right(_flightsPane),
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(1),
        };
        _notes = new Label { Width = Dim.Fill(), Height = Dim.Fill(), CanFocus = true };
        _notesPane.Add(_notes);

        _hints = new Label { X = 0, Y = Pos.AnchorEnd(1), Width = Dim.Fill() };

        _helpOverlay = new FrameView
        {
            Title = "Help",
            X = Pos.Center(),
            Y = Pos.Center(),
            Width = 44,
            Height = 8,
            Visible = false,
        };
        _helpOverlay.Add(new Label
        {
            Text = "q        quit\n?        toggle this help\ne        edit notes in $EDITOR\ntab      switch pane\nctrl+c   quit from anywhere",
        });

        Add(_flightsPane, _notesPane, _hints, _helpOverlay);

        KeyDown += OnScreenKeyDown;
        _flights.ValueChanged += OnFlightSelectionChanged;

        Render();
    }

    private void OnScreenKeyDown(object? sender, Key key)
    {
        var (input, keyInfo) = KeyTranslator.Translate(key);
        var command = Keymap.Resolve(input, keyInfo, new KeymapContext(State.Mode));
        if (command is null)
        {
            return;
        }

        key.Handled = true;
        if (command is Command.Quit or Command.OpenEditor)
        {
            ExitCommand = command.Value;
            _app.RequestStop(this);
            return;
        }

        State = Reducer.Reduce(State, command.Value);
        Render();
    }

    private void OnFlightSelectionChanged(object? sender, ValueChangedEventArgs<int?> args)
    {
        State = State with { SelectedFlight = args.NewValue ?? 0 };
    }

    /// <summary>One-way: model in, pixels out.</summary>
    private void Render()
    {
        if (State.Flights.Count > 0)
        {
            _flights.SelectedItem = Math.Clamp(State.SelectedFlight, 0, State.Flights.Count - 1);
        }
        _notes.Text = State.Notes;
        _helpOverlay.Visible = State.Mode == UiMode.Help;
        _hints.Text = Keymap.Hints(new KeymapContext(State.Mode));

        if (State.Mode == UiMode.Help)
        {
            _helpOverlay.SetFocus();
        }
        else if (State.FocusedPane == PaneId.Flights)
        {
            _flights.SetFocus();
        }
        else
        {
            _notes.SetFocus();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            KeyDown -= OnScreenKeyDown;
            _flights.ValueChanged -= OnFlightSelectionChanged;
        }
        base.Dispose(disposing);
    }
}
