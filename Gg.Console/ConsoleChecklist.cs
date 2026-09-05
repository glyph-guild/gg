using Gg.Client;

namespace Gg.Console;

/// <summary>
/// The checklist read, for the row the cursor is on.
/// </summary>
/// <remarks>
/// <para>
/// <b>A composition-root function, not a method on the loop.</b>
/// <see cref="ConsoleLoop"/> is handed something it can call and never a read
/// surface - a <see cref="ConsoleData"/> inside its type would be one step from
/// a read surface inside a UI session, which is rule 3.
/// </para>
/// <para>
/// <b>Through <c>Apply</c>, which is rule 2.</b> Assigning the field here would
/// be a second projection one layer down and harder to see.
/// </para>
/// </remarks>
public static class ConsoleChecklist
{
    public static AppState Read(ConsoleData data, AppState state)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(state);

        if (state.Selected is not { } row)
        {
            // NOTHING SELECTED READS NOTHING, rather than answering for the
            // envelope with no flight. `gg plan` will answer that, and it is a
            // different question from the one this pane is asking.
            return state;
        }

        try
        {
            return ConsoleProjection.Apply(
                state, data.PlanAsync(row.FlightNumber).GetAwaiter().GetResult());
        }
        catch (Exception failure) when (failure is NotSignedInException
                                            or ProtocolTooOldException
                                            or FlightNotFoundException
                                            or HttpRequestException)
        {
            // ITS OWN FAILURE, said in the pane. The rest of the model is still
            // true, and emptying it because one read failed is the shape rule 5
            // exists to stop.
            return state with
            {
                Diagnosis = "The checklist could not be read: " + failure.Message,
            };
        }
    }
}
