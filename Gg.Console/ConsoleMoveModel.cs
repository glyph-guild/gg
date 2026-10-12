using Gg.Client;

namespace Gg.Console;

/// <summary>
/// Moves the flight a person is reading to the model under the picker's
/// cursor (slice seventy-six).
/// </summary>
/// <remarks>
/// <para>
/// <b>The flight comes from the same function the modal renders</b>, for
/// <see cref="ConsoleGround"/>'s reason, and the model from the picker's own
/// cursor: two answers to "which" is how a pane came to act on a row nobody
/// was looking at.
/// </para>
/// <para>
/// <b>A composition-root function, like its neighbours.</b>
/// <see cref="ConsoleLoop"/> is handed something it can call and never a read
/// surface.
/// </para>
/// </remarks>
public static class ConsoleMoveModel
{
    public static AppState Move(ConsoleData data, AppState state)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(state);

        var flight = PaneText.Detailed(state);
        var model = ModelChoices.Chosen(state);
        var closed = state with { Mode = UiMode.Normal, ModelSelected = 0 };

        if (flight is null)
        {
            return closed with { LastMoved = "There is no flight on the screen to move." };
        }

        if (model is null)
        {
            return closed with
            {
                LastMoved = $"{flight.FlightNumber} was not moved: root lists no models: to choose from.",
            };
        }

        try
        {
            var moved = data.ModelAsync(flight.FlightNumber, model).GetAwaiter().GetResult();

            // THROUGH APPLY, which is rule 2, so the flight's summary - its model
            // in force - lands in the model the way every other read does.
            return ConsoleProjection.Apply(closed, moved) with
            {
                LastMoved = $"{flight.FlightNumber} was moved to {model}.",
            };
        }
        catch (Exception failure) when (failure is NotSignedInException
                                            or ProtocolTooOldException
                                            or FlightNotFoundException
                                            or DecisionRefusedException
                                            or HttpRequestException)
        {
            // ITS OWN FAILURE, said in the model. The control plane's refusal
            // names root's list or says the flight has ended, which is the
            // sentence a person needs.
            return closed with
            {
                LastMoved = $"{flight.FlightNumber} was not moved: {failure.Message}",
            };
        }
    }
}
