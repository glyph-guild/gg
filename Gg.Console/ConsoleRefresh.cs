using Gg.Client;

namespace Gg.Console;

/// <summary>
/// What one tab needs re-read, and nothing else.
/// </summary>
/// <remarks>
/// <para>
/// <b>A patch, not a model.</b> Each of these answers with a function that is
/// applied to whatever is on screen when it lands - so a person who moved the
/// cursor while the request was in the air keeps their cursor, and nothing here
/// has to list which fields are the read plane.
/// </para>
/// <para>
/// <b>A composition-root function, like its neighbours.</b>
/// <see cref="AutoRefresh"/> is handed something it can call and never a read
/// surface; a <see cref="ConsoleData"/> inside its type would be one step from a
/// read surface inside a UI session, which is the rule this whole arrangement
/// is built around.
/// </para>
/// <para>
/// <b>Every failure is one refresh's worth.</b> Rule 5's third sentence: the
/// rest of the model is still true, and emptying it because one read failed is
/// the shape that rule exists to stop. A refresh that cannot reach anybody says
/// so in the diagnosis and leaves the screen alone.
/// </para>
/// </remarks>
public static class ConsoleRefresh
{
    /// <summary>How many logs to have in the air at once, as at boot.</summary>
    private const int LogsAtOnce = 8;

    /// <summary>
    /// As many rows as are on screen, and never fewer than a page.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Because a refresh must not take away what somebody scrolled to.</b>
    /// A person three pages into a list has three pages' worth of rows held;
    /// a tick that asked for one page would replace them with a hundred rows
    /// and send the cursor back to the top of what is left. So the tick asks
    /// for what is there.
    /// </para>
    /// <para>
    /// <b>Never fewer than a page, even when fewer are held.</b> A short list
    /// asked for its own length can never grow: the rows created since would
    /// need a scroll to reach, on a list whose cursor said it was complete.
    /// </para>
    /// <para>
    /// <b>And never more than the contract allows.</b> Somebody who has
    /// scrolled past a thousand rows loses the tail on the next tick, which is
    /// a worse answer than a refused request - <c>Paging.Validate</c> is what
    /// the other side would answer with, and it refuses.
    /// </para>
    /// </remarks>
    private static int AsManyAsAreShown(int held) =>
        Math.Clamp(held, Gg.Contracts.Paging.DefaultLimit, Gg.Contracts.Paging.MaxLimit);

    /// <param name="on">
    /// What is on screen, which decides how much of it to ask for again.
    /// </param>
    public static async Task<Func<AppState, AppState>> ForTabAsync(
        ConsoleData data, TabId tab, AppState on, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(on);

        // WHAT THIS TAB COST, when somebody set GG_TIMING. Around the whole
        // switch so a tab that turns out to be cheap is recorded as cheap -
        // a diagnostic that only measured the branch already suspected would
        // confirm whatever it was pointed at.
        using var measured = Timings.Active.Measure($"refresh.{tab}");

        try
        {
            return tab switch
            {
                TabId.Queue or TabId.Flights => await TheFleetAndItsWorkAsync(
                    data, AsManyAsAreShown(on.Flights?.Flights.Count ?? 0), cancellationToken),
                TabId.Runners => await TheFleetAndWhatItHasLeftAsync(data, cancellationToken),
                // TWO READS, AND THE PANE WAITS FOR BOTH. Nominations and the
                // watches that make them are separate routes; folding one in
                // while the other is still null would draw a board that looks
                // complete and is half a story.
                TabId.Board => await TheBoardAndItsWatchesAsync(
                    data,
                    AsManyAsAreShown(on.Board?.Nominations.Count ?? 0),
                    cancellationToken),
                TabId.Repositories => Apply(await data.RepositoriesAsync(cancellationToken)),
                TabId.Envelope => Apply(await data.EnvelopeAsync(cancellationToken)),

                _ => Nothing,
            };
        }
        catch (Exception failure) when (failure is NotSignedInException
                                            or ProtocolTooOldException
                                            or FlightNotFoundException
                                            or NoEnvelopeException
                                            or HttpRequestException)
        {
            return state => state with
            {
                Diagnosis = "The last refresh did not finish: " + failure.Message,
            };
        }
    }

    /// <summary>Nothing to ask anybody: the tab reads a local file or a child.</summary>
    private static AppState Nothing(AppState state) => state;

    private static Func<AppState, AppState> Apply(VerbResult result) =>
        state => ConsoleProjection.Apply(state, result);

    /// <summary>
    /// The board, and the watches whose sweeps put rows on it.
    /// </summary>
    /// <remarks>
    /// <b>Two reads because they are two questions</b>, joined in the pane
    /// rather than on the wire - the arrangement the fleet and its allowances
    /// already have. A board drawn without its watches can look quiet while
    /// nothing is sweeping, which is the state slice thirty-nine's rule 11
    /// exists to make impossible.
    /// </remarks>
    private static async Task<Func<AppState, AppState>> TheBoardAndItsWatchesAsync(
        ConsoleData data, int page, CancellationToken cancellationToken)
    {
        // ENDED ROWS TOO. The queue already shows what is standing; what this
        // pane adds is what happened to the rest, and a board that dropped
        // every answered row would be the queue with a second name.
        var board = Apply(await data.BoardAsync(ended: true, cancellationToken, limit: page));
        var watches = await data.WatchesAsync(cancellationToken);

        return state => ConsoleProjection.Apply(board(state), watches);
    }

    /// <summary>
    /// The fleet, and what each machine's allowance has left.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Two reads because they are two questions</b>, joined in the pane
    /// rather than on the wire: the runners list is per machine and an
    /// allowance is per subscription, which two machines may share.
    /// </para>
    /// <para>
    /// <b>The allowance read is allowed to fail on its own.</b> A control
    /// plane one version behind serves no such route, and the pane a person
    /// opens to see their fleet must not go blank — or worse, report itself
    /// disconnected — over a column that is extra. The runners read has no
    /// such indulgence: if THAT fails there is nothing to draw, and the
    /// caller's diagnosis is the right answer.
    /// </para>
    /// </remarks>
    private static async Task<Func<AppState, AppState>> TheFleetAndWhatItHasLeftAsync(
        ConsoleData data, CancellationToken cancellationToken)
    {
        var fleet = Apply(await data.RunnersAsync(cancellationToken));

        // AND WHAT THE MODAL WILL WANT. Opening a runner is how a person
        // reaches the environments view, and the chart it is keyed on is
        // in no other read - so it arrives with the fleet rather than on a
        // key of its own, the way the allowances below already do.

        var joined = new List<VerbResult>();

        foreach (var read in (Func<CancellationToken, Task<VerbResult>>[])
                 [data.AllowancesAsync, data.EnvironmentsAsync,
                  data.StrategiesAsync, data.PoolsAsync])
        {
            try
            {
                joined.Add(await read(cancellationToken));
            }
            catch (HttpRequestException)
            {
            }
            catch (ProtocolTooOldException)
            {
            }
        }

        return state => joined.Aggregate(fleet(state), ConsoleProjection.Apply);
    }


    /// <summary>
    /// The queue and the flights list, which are the same four reads.
    /// </summary>
    /// <remarks>
    /// <b>The queue is derived, so its inputs travel together.</b> Folding a
    /// new flight list without the runners and the gates it was ranked against
    /// would leave rows explained by an answer that has moved.
    /// </remarks>
    private static async Task<Func<AppState, AppState>> TheFleetAndItsWorkAsync(
        ConsoleData data, int page, CancellationToken cancellationToken)
    {
        var listing = data.ListAsync(cancellationToken, limit: page);
        var fleet = data.RunnersAsync(cancellationToken);
        var waiting = data.GatesAsync(cancellationToken);
        // AND WHAT HAS NOT STARTED, in the same round as the three above it.
        // The queue is derived and its inputs travel together: folding a new
        // board against a flight list from a different moment would leave rows
        // explained by an answer that has moved, which is this method's own
        // rule one read over.
        var nominated = data.BoardAsync(cancellationToken: cancellationToken);

        using (Timings.Active.Measure("refresh.lists", reads: 4))
        {
            await Task.WhenAll(listing, fleet, waiting, nominated);
        }

        var flights = (VerbResult.Flights)await listing;
        var runners = (VerbResult.Runners)await fleet;
        var gates = await waiting is VerbResult.Gates open ? open.Value : null;
        var board = await nominated is VerbResult.Board standing ? standing.Value : null;

        // A LOG FOR EVERY FLIGHT STILL FLYING, as at boot and for the same
        // reason: those are the only ones whose log can put a row in the queue.
        using var room = new SemaphoreSlim(LogsAtOnce);

        // ONE PER OPEN FLIGHT, AND THE COUNT IS THE POINT. This is the only
        // phase in the console whose cost grows with the tenant, so a duration
        // without the number beside it could not say whether it was slow or
        // simply asked a lot.
        using var logged = Timings.Active.Measure(
            "refresh.logs",
            reads: Timings.Active.Asked
                ? flights.Value.Flights.Count(
                    f => f.State == Gg.Contracts.FlightStates.Open)
                : null);

        var reading = flights.Value.Flights
            .Where(flight => flight.State == Gg.Contracts.FlightStates.Open)
            .Select(async flight =>
            {
                await room.WaitAsync(cancellationToken);
                try
                {
                    return (flight.FlightId, Answer: await data.LogAsync(
                        flight.FlightId, cancellationToken));
                }
                finally
                {
                    room.Release();
                }
            })
            .ToList();

        var fetched = await Task.WhenAll(reading);

        return state =>
        {
            var logs = new Dictionary<string, Gg.Contracts.FlightLog>(
                state.Logs, StringComparer.Ordinal);

            foreach (var (flightId, answer) in fetched)
            {
                if (answer is VerbResult.Log log)
                {
                    logs[flightId] = log.Value;
                }
            }

            var folded = ConsoleProjection.Apply(state, flights);
            folded = ConsoleProjection.Apply(folded, runners);

            // AND THE CORNER, LAST. A gate that opened since the previous tick
            // is news; the ones that were already waiting are not. Announcements
            // folds after Gates is set, because it reads them - and this is the
            // tick half of the rule: ConsoleStart's fold arms it, every one of
            // these is what somebody is actually watching happen.
            return Announcements.Folded(Reducer.Detail(folded with
            {
                Queue = ConsoleProjection.Queue(
                    flights.Value, logs, runners.Value, gates, board),
                Gates = gates,
                Logs = logs,
            }));
        };
    }
}
