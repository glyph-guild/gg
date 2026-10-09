using Gg.Contracts;

namespace Gg.Console;

/// <summary>What came of an intent: the flights about it, and the plans.</summary>
public static class ConsoleIntents
{
    public static Func<AppState, AppState> CameOfPatch(
        Func<string, IReadOnlyList<FlightSummary>?> flown, Func<BoardPage?> plans, AppState state) =>
        current => current;
}
