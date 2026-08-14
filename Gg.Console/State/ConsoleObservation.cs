using Gg.Contracts;

namespace Gg.Console;

/// <summary>
/// What the console saw while somebody answered a gate.
/// </summary>
/// <remarks>
/// SHELL. It reports nothing yet, which is what the observation tests are about to say is
/// wrong.
/// </remarks>
public static class ConsoleObservation
{
    public static DecisionObservations Of(AppState state, TimeSpan open) => new()
    {
        Interactive = false,
        EvidenceRendered = false,
        SecondsToDecide = null,
    };
}
