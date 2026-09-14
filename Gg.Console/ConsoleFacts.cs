using Gg.Client;
using Gg.Contracts;

namespace Gg.Console;

/// <summary>
/// Reading what a flight recorded, beside the console rather than instead of it.
/// </summary>
/// <remarks>
/// <para>
/// <b><c>ConsoleRepositories</c>' shape, for the same reason.</b> A pane that is
/// opened wants filling, and filling it used to mean the console going away and
/// coming back. This returns a PATCH rather than a model, so the fold happens on
/// a tick inside <c>Invoke</c> and the session never waits.
/// </para>
/// <para>
/// <b>Asked for on the keypress, not at boot.</b> Every flight's log is held
/// because the boot already fetches one per flight for its own reasons; facts
/// are large and rarely wanted, so a request per flight at launch would be paid
/// by everybody for something almost nobody opens.
/// </para>
/// <para>
/// <b>A failed read lands as no patch at all.</b> The pane says which absence it
/// is - still coming, never asked for, or genuinely nothing recorded - and the
/// difference between the first and the last is the one a reader must not have
/// to guess: a pending read drawn as "recorded nothing" would say a flight
/// wrote no evidence when nobody has looked yet.
/// </para>
/// </remarks>
public static class ConsoleFacts
{
    /// <summary>What to fold, once the read has landed.</summary>
    public static Func<AppState, AppState> Patch(ConsoleData data, AppState state)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(state);

        var read = Read(data, state);

        return current => current with
        {
            FlightFacts = read.FlightFacts,
            Diagnosis = read.Diagnosis,
        };
    }

    /// <summary>The read itself, so a test can hold it without a screen.</summary>
    /// <remarks>
    /// <b>The flight under the cursor, and nothing if there is none.</b> A facts
    /// read needs a flight to be about, and asking for one the console does not
    /// have selected would be a request whose answer nothing could caption.
    /// </remarks>
    public static AppState Read(ConsoleData data, AppState state)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(state);

        if (PaneText.Detailed(state) is not { } flight)
        {
            return state;
        }

        try
        {
            // THROUGH THE ONE PROJECTION, so this does not keep a second copy of
            // how a result becomes state. ConsoleData.Apply has the arm; a
            // module that unwrapped the result itself would be the parity its
            // ratchet exists to hold.
            return ConsoleProjection.Apply(
                state, data.FactsAsync(flight.FlightNumber).GetAwaiter().GetResult());
        }
        catch (Exception failed)
        {
            // SAID BY THE PANE, never by a throw. This runs on a task beside a
            // console somebody is using, and the absence already has a sentence
            // for it - what this adds is why, for the one case where the reason
            // is not "nobody asked yet".
            return state with { Diagnosis = failed.Message };
        }
    }
}
