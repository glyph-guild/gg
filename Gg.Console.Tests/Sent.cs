namespace Gg.Console.Tests;

/// <summary>
/// Every launch a press asked for, sent and folded here and now.
/// </summary>
/// <remarks>
/// <b>The loop's own inline path, for tests that call a fly function
/// directly.</b> Asking for a flight no longer sends it - <c>Launcher</c> does,
/// beside the screen - so a test about what the door was asked with sends what
/// was asked for through the same <c>Perform</c> and <c>Landed</c> the
/// launcher uses. Nothing here is a second implementation.
/// </remarks>
internal static class Sent
{
    internal static AppState Inline(AppState state, IConsoleActions? actions)
    {
        if (actions is null)
        {
            return state;
        }

        foreach (var launch in state.Launches)
        {
            state = Launches.Landed(state, launch, Launches.Perform(launch, actions));
        }

        return state;
    }
}
