namespace Gg.Console;

/// <summary>What a completed UI session hands back: why it exited, and the model as it stood.</summary>
public sealed record UiOutcome(Command Exit, AppState State);

/// <summary>
/// One complete UI lifetime: build views FROM the given state, run, tear
/// everything down, give the terminal back. Implementations must not retain
/// anything across calls — the returned state is the only survivor.
/// </summary>
public interface IUiSession
{
    UiOutcome Run(AppState state);
}
