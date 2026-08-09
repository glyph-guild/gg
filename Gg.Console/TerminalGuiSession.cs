using Gg.Console.Views;
using Terminal.Gui.App;

namespace Gg.Console;

/// <summary>
/// One complete Terminal.Gui lifetime per call: create an application, build
/// views from the model, run, tear the whole thing down. After Run returns,
/// the terminal is fully released — nothing of the UI survives except the
/// returned state.
/// </summary>
public sealed class TerminalGuiSession : IUiSession
{
    public UiOutcome Run(AppState state)
    {
        using var app = Application.Create();
        app.Init();
        using var screen = new ConsoleScreen(app, state);
        app.Run(screen);
        return new UiOutcome(screen.ExitCommand, screen.State);
    }
}
