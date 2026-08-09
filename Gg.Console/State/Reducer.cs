namespace Gg.Console;

public static class Reducer
{
    public static AppState Reduce(AppState state, Command command) => command switch
    {
        Command.ToggleHelp => state with
        {
            Mode = state.Mode == UiMode.Help ? UiMode.Normal : UiMode.Help,
        },
        Command.FocusNextPane => state with
        {
            FocusedPane = state.FocusedPane == PaneId.Flights ? PaneId.Notes : PaneId.Flights,
        },
        // Quit and OpenEditor end the UI session; the shell handles them.
        Command.Quit or Command.OpenEditor => state,
        _ => state,
    };
}
