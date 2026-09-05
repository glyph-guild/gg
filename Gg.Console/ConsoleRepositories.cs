using Gg.Client;

namespace Gg.Console;

/// <summary>
/// Reading what this tenant can fly against.
/// </summary>
/// <remarks>
/// <para>
/// <b><c>ConsoleChecklist</c>'s shape, for the same reason.</b> Showing this
/// pane is a read and a UI session may not make one, so the session ends, the
/// loop asks, and the next session renders it. The answer goes through
/// <c>ConsoleProjection.Apply</c> rather than a bespoke reducer, so there is
/// one path from a verb result into the model.
/// </para>
/// <para>
/// <b>A refusal leaves the list null and says why.</b> Recording an empty list
/// instead would render as "this tenant has nothing registered", which sends a
/// person to register a repository they already have.
/// </para>
/// </remarks>
public static class ConsoleRepositories
{
    public static AppState Read(ConsoleData data, AppState state)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(state);

        try
        {
            return ConsoleProjection.Apply(
                state, data.RepositoriesAsync().GetAwaiter().GetResult());
        }
        catch (Exception failure) when (failure is NotSignedInException
                                            or ProtocolTooOldException
                                            or HttpRequestException)
        {
            return state with
            {
                Diagnosis = "Could not read what this tenant can fly against: " + failure.Message,
            };
        }
    }
}
