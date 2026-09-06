using Gg.Console.Views;
using Terminal.Gui.App;

namespace Gg.Console;

/// <summary>
/// One complete Terminal.Gui lifetime per call: create an application, build
/// views from the model, run, tear the whole thing down. After Run returns,
/// the terminal is fully released — nothing of the UI survives except the
/// returned state.
/// </summary>
public sealed class TerminalGuiSession(LiveTails? tails = null) : IUiSession
{
    public UiOutcome Run(AppState state)
    {
        // BEFORE THE APPLICATION EXISTS. Views read their schemes from the
        // static facades as they are constructed, so a theme switched after
        // Init would leave whatever was built first on the old one.
        ConsoleTheme.Apply();

        using var app = Application.Create();
        app.Init();
        using var screen = new ConsoleScreen(app, state, tails);
        app.Run(screen);
        return new UiOutcome(screen.ExitCommand, screen.State);
    }
}
