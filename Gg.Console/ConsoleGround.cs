using Gg.Client;

namespace Gg.Console;

/// <summary>
/// Stops the flight a person is reading, once they have said why.
/// </summary>
/// <remarks>
/// <para>
/// <b>The flight comes from the same function the modal renders.</b> Two
/// answers to "which flight" is how a pane came to show one row's detail under
/// another row's name, and doing it here for an ENDING would stop a flight
/// somebody was not looking at.
/// </para>
/// <para>
/// <b>Nothing typed grounds nothing.</b> The wire refuses a blank reason, so
/// this could send it and let the refusal come back - but a person who opened
/// the editor and closed it has not decided to stop anything, and finding out
/// by asking the control plane would be this console deciding for them.
/// </para>
/// <para>
/// <b>A composition-root function, like its neighbours.</b>
/// <see cref="ConsoleLoop"/> is handed something it can call and never a read
/// surface.
/// </para>
/// </remarks>
public static class ConsoleGround
{
    public static AppState Ground(ConsoleData data, AppState state, Func<string> ask)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(ask);

        if (PaneText.Detailed(state) is not { } flight)
        {
            return state with
            {
                LastGrounded = "There is no flight on the screen to ground.",
            };
        }

        var because = ask().Trim();

        if (because.Length == 0)
        {
            return state with
            {
                LastGrounded =
                    $"{flight.FlightNumber} was not grounded: nothing was written to say why. "
                  + "The reason is the only thing that survives to tell a later reader why "
                  + "work that could have been done was not.",
            };
        }

        try
        {
            var grounded = data.GroundAsync(flight.FlightNumber, because).GetAwaiter().GetResult();

            // THROUGH APPLY, WHICH IS RULE 2, so the flight's new state lands in
            // the model the same way every other read does - and the modal
            // behind this redraws showing the ending rather than the flight as
            // it was a moment ago.
            return ConsoleProjection.Apply(state, grounded) with
            {
                LastGrounded = $"{flight.FlightNumber} was grounded: {because}",
            };
        }
        catch (Exception failure) when (failure is NotSignedInException
                                            or ProtocolTooOldException
                                            or FlightNotFoundException
                                            or DecisionRefusedException
                                            or HttpRequestException)
        {
            // ITS OWN FAILURE, said in the model. A flight that had already
            // ended comes back through here as the control plane's refusal,
            // which is the sentence a person needs: it says the flight is over
            // and not that this console is broken.
            return state with
            {
                LastGrounded = $"{flight.FlightNumber} was not grounded: {failure.Message}",
            };
        }
    }
}
