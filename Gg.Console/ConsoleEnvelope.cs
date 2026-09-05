using Gg.Client;

namespace Gg.Console;

/// <summary>
/// The envelope read, for the tenant rather than for a row.
/// </summary>
/// <remarks>
/// A composition-root function for <see cref="ConsoleChecklist"/>'s reason:
/// <see cref="ConsoleLoop"/> is handed something it can call and never a read
/// surface.
/// </remarks>
public static class ConsoleEnvelope
{
    public static AppState Read(ConsoleData data, AppState state)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(state);

        try
        {
            return ConsoleProjection.Apply(
                state, data.EnvelopeAsync().GetAwaiter().GetResult());
        }
        catch (NoEnvelopeException)
        {
            // NOT A FAILURE, AND NOT A BLANK. A tenant with no envelope has
            // every flight ungoverned, which is the single most important thing
            // this pane can say - and it is exactly what an empty pane would
            // hide.
            return state with
            {
                Diagnosis = "No envelope has been applied, so nothing governs this tenant's "
                          + "flights. gg envelope apply is where the rules come from.",
            };
        }
        catch (Exception failure) when (failure is NotSignedInException
                                            or ProtocolTooOldException
                                            or EnvelopeUnreadableException
                                            or HttpRequestException)
        {
            return state with
            {
                Diagnosis = "The envelope could not be read: " + failure.Message,
            };
        }
    }
}
