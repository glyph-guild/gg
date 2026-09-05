namespace Gg.Console;

/// <summary>
/// One tail per flight, and the only thing in the console that opens a file.
/// </summary>
/// <remarks>
/// <para>
/// <b>Shared by the loop and the session on purpose.</b> Between sessions the
/// loop advances the pane; during a session the screen advances it on a timer.
/// Both must resume from the same offset or the same lines arrive twice, so
/// there is one of these and both hold a reference to it.
/// </para>
/// <para>
/// <b>It is a collaborator, not state on the session.</b> <c>IUiSession</c> must
/// not RETAIN anything across calls, and it does not: this object outlives the
/// session, is owned by whoever composed the console, and the session merely
/// calls it. The distinction matters because the rule exists to stop a UI
/// lifetime accumulating things that should die with the terminal, and a file
/// offset that must survive a rebuild is precisely something that should not
/// live there.
/// </para>
/// </remarks>
public sealed class LiveTails(Func<string, ILiveSource> source)
{
    private readonly Dictionary<string, ILiveSource> _tails = new(StringComparer.Ordinal);

    /// <summary>How many times a read has thrown. Zero is the ordinary state.</summary>
    public int Faults { get; private set; }

    /// <summary>
    /// Folds whatever the watched flight has said into the state.
    /// </summary>
    /// <remarks>
    /// <b>It never throws.</b> A view that fails must not fail anything, and on
    /// this side that means it must not take a UI session down mid-render: the
    /// terminal would be left in a state nothing is holding. A failed read is
    /// counted and the pane says the tail stopped.
    /// </remarks>
    public AppState Advance(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (!state.LiveVisible || state.Selected is not { } row)
        {
            return state with { Silence = LiveSilence.NotAttached };
        }

        try
        {
            if (!_tails.TryGetValue(row.FlightId, out var tail))
            {
                tail = source(row.FlightId);
                _tails[row.FlightId] = tail;
            }

            foreach (var line in tail.Read())
            {
                state = Reducer.StreamArrived(state, line);
            }

            return state with
            {
                Silence = state.Live.Count > 0 ? LiveSilence.Speaking
                    : tail.Exists ? LiveSilence.NothingYet
                    : LiveSilence.NotStarted,
            };
        }
        catch (Exception)
        {
            // COUNTED, NOT THROWN, and said on the screen rather than swallowed.
            // A tail that dies quietly looks exactly like a flight that went
            // quiet, which is the one thing this pane must never be ambiguous
            // about.
            Faults++;
            return state with { Silence = LiveSilence.Stopped };
        }
    }
}
