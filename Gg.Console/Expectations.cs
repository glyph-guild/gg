using Gg.Client;
using Gg.Contracts;

namespace Gg.Console;

/// <summary>
/// Looks for what this console's writes said they did, until it appears or the
/// console stops expecting it - and says which.
/// </summary>
/// <remarks>
/// <para>
/// <b><see cref="AutoRefresh"/>'s shape, for <see cref="AutoRefresh"/>'s reason.</b>
/// A look runs on a task owned outside every UI lifetime and the tick asks only
/// whether it has finished, so the session folds an answer that has already
/// arrived and never waits for one. What comes back is a patch, applied to
/// whatever is on the screen when it lands.
/// </para>
/// <para>
/// <b>The question is the model's; the waiting is this.</b> What is expected is
/// in <see cref="AppState.Expecting"/>, because it is on the screen until it is
/// answered and has to survive a session rebuild. When it was first expected,
/// when to look next and when the notifications go are times - and a time in
/// the model is one a dump compares against a different now - so they live
/// here, the way the refresh's due time lives on <see cref="AutoRefresh"/>.
/// </para>
/// <para>
/// <b>Soon, then less often, then said.</b> A flight the door accepted is most
/// likely a moment away, so the first look is immediate and the next a quarter
/// of a second later; the gap doubles to two seconds and stays there. Thirty
/// seconds without it is a notification saying so, and no more looks - a
/// console that went quiet about a flight somebody is waiting for would be the
/// one answer worse than admitting it has not appeared.
/// </para>
/// </remarks>
public sealed class Expectations(
    Func<Expectation, Task<Func<AppState, AppState>?>> look, IClock clock)
{
    /// <summary>How long a named flight is looked for before the console says so.</summary>
    public static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    /// <summary>The wait after the first look that found nothing.</summary>
    public static readonly TimeSpan FirstGap = TimeSpan.FromMilliseconds(250);

    /// <summary>The longest wait between two looks.</summary>
    public static readonly TimeSpan LongestGap = TimeSpan.FromSeconds(2);

    /// <summary>How long notifications stay after the last one arrived, while nobody is reading them.</summary>
    public static readonly TimeSpan NotificationsLast = TimeSpan.FromSeconds(15);

    private readonly Func<Expectation, Task<Func<AppState, AppState>?>> _look = look;
    private readonly IClock _clock = clock;

    /// <summary>What is being waited for, and where each wait has got to.</summary>
    /// <remarks>
    /// Keyed on the expectation itself - a record, so two with the same kind and
    /// id are one question - and forgotten the moment the model stops asking it.
    /// </remarks>
    private readonly Dictionary<Expectation, Waiting> _waiting = [];

    /// <summary>When the notifications go, or null while nothing is counting down.</summary>
    private DateTimeOffset? _notificationsUntil;

    private sealed class Waiting(DateTimeOffset since)
    {
        public DateTimeOffset Since { get; } = since;

        public DateTimeOffset Due { get; set; } = since;

        public int Looks { get; set; }

        public Task<Func<AppState, AppState>?>? Running { get; set; }
    }

    /// <summary>Every look that is waiting, due now.</summary>
    public void Hurry()
    {
    }

    /// <summary>
    /// Start a look that is due, fold one that has landed, and age the
    /// notifications.
    /// </summary>
    /// <remarks>
    /// <b>It never waits and never throws.</b> A look that failed is a look that
    /// found nothing, and the next one is still worth making - an unreachable
    /// control plane is exactly when a person most needs to know whether their
    /// flight exists.
    /// </remarks>
    public AppState Advance(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var now = _clock.UtcNow;
        var raised = false;

        // FORGOTTEN WITH THE QUESTION. The model is the authority on what is
        // asked; a wait kept for something nobody is asking about any more would
        // go on looking for it.
        foreach (var gone in _waiting.Keys.Where(k => !state.Expecting.Contains(k)).ToList())
        {
            _waiting.Remove(gone);
        }

        foreach (var expected in state.Expecting.ToList())
        {
            if (!_waiting.TryGetValue(expected, out var waiting))
            {
                waiting = new Waiting(now);
                _waiting[expected] = waiting;
            }

            if (waiting.Running is null && now - waiting.Since >= Patience)
            {
                state = Raised(Unasked(state, expected), About(state, expected, seen: false));
                _waiting.Remove(expected);
                raised = true;
                continue;
            }

            if (waiting.Running is null && now >= waiting.Due)
            {
                waiting.Running = Started(expected);
            }

            // FOLDED IN THE SAME TICK IT STARTED, when it has already answered.
            // The gap is measured from the look, not from whichever tick
            // happened to notice it had finished.
            if (waiting.Running is not { IsCompleted: true } landed)
            {
                continue;
            }

            waiting.Running = null;
            waiting.Looks++;

            if (landed.IsCompletedSuccessfully && landed.Result is { } patch)
            {
                state = Seen(patch(state), expected);
                _waiting.Remove(expected);
                raised = true;
                continue;
            }

            waiting.Due = now + Gap(waiting.Looks);
        }

        return Aged(state, now, raised);
    }

    /// <summary>
    /// The looks, over a read of one flight that answers null when it is not
    /// there yet.
    /// </summary>
    /// <remarks>
    /// <b>No task of its own.</b> A read that has already answered answers here
    /// synchronously, which is what lets the tests hold time still; putting the
    /// request on a task outside the UI thread is the composition root's, as it
    /// is for every other read this console makes.
    /// </remarks>
    /// <param name="gates">
    /// The gate list, for a gate this console answered. A console composed
    /// without one never finds a gate closed, and says so when the patience runs
    /// out rather than claiming it did.
    /// </param>
    public static Func<Expectation, Task<Func<AppState, AppState>?>> Looks(
        Func<string, Task<FlightSummary?>> flight,
        Func<Task<GateList?>>? gates = null)
    {
        ArgumentNullException.ThrowIfNull(flight);

        return expected => LookAsync(flight, gates, expected);
    }

    /// <summary>
    /// The gate list as it now is, and the queue folded from it.
    /// </summary>
    /// <remarks>
    /// <b>The queue is re-derived, not edited.</b> It is what needs somebody,
    /// computed from flights, logs, runners, gates and the board together; a row
    /// taken out by hand would be a second opinion about which rows those make.
    /// And the detail under the cursor is re-read the way a refresh re-reads it,
    /// because the row it was about may be the one that left.
    /// </remarks>
    public static AppState GatesNow(AppState state, GateList gates)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(gates);

        return Reducer.Detail(state with
        {
            Gates = gates,
            Queue = state.Flights is { } flights && state.Runners is { } runners
                ? ConsoleProjection.Queue(flights, state.Logs, runners, gates, state.Board)
                : state.Queue,
        });
    }

    /// <summary>
    /// The flight on the list, in place of any copy already there.
    /// </summary>
    /// <remarks>
    /// <b>And the cursor on the flight it was on.</b> The flights cursor is an
    /// index into the list as shown, newest first, so a flight arriving at the
    /// top moves every row down one - and a cursor left where it was would be on
    /// a different flight from the one somebody was about to open.
    /// </remarks>
    public static AppState Listed(AppState state, FlightSummary flight)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(flight);

        var shown = PaneText.Shown(state.Flights);
        var under = shown.Count > 0
            ? shown[Math.Clamp(state.FlightSelected, 0, shown.Count - 1)].FlightId
            : null;

        FlightSummary[] flights =
        [
            flight,
            .. (state.Flights?.Flights ?? []).Where(f => f.FlightId != flight.FlightId),
        ];

        var listed = state with
        {
            Flights = state.Flights is { } list
                ? list with { Flights = flights }
                : new FlightList { Flights = flights },
        };

        var now = PaneText.Shown(listed.Flights);
        var at = under is null ? -1 : IndexOf(now, under);

        return at < 0 ? listed : listed with { FlightSelected = at };
    }

    private static int IndexOf(IReadOnlyList<FlightSummary> flights, string id)
    {
        for (var i = 0; i < flights.Count; i++)
        {
            if (string.Equals(flights[i].FlightId, id, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private static async Task<Func<AppState, AppState>?> LookAsync(
        Func<string, Task<FlightSummary?>> flight,
        Func<Task<GateList?>>? gates,
        Expectation expected)
    {
        switch (expected.Kind)
        {
            case ExpectationKind.FlightAppears:
                var found = await flight(expected.Id);
                return found is null ? null : state => Listed(state, found);

            case ExpectationKind.GateAnswered:
                // NOTHING TO ASK WITH IS NOT AN ANSWER. Without a gate list
                // there is no way to see it close, so nothing is folded and the
                // patience says so when it runs out.
                if (gates is null || await gates() is not { } listed)
                {
                    return null;
                }

                var still = listed.Gates.Any(g =>
                    string.Equals(g.FlightNumber, expected.Id, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(g.ObligationId, expected.Obligation, StringComparison.Ordinal));

                return still ? null : state => GatesNow(state, listed);

            default:
                return null;
        }
    }

    /// <summary>A look, never a throw: a delegate that throws is a look that failed.</summary>
    private Task<Func<AppState, AppState>?> Started(Expectation expected)
    {
        try
        {
            return _look(expected);
        }
        catch (Exception failure)
        {
            return Task.FromException<Func<AppState, AppState>?>(failure);
        }
    }

    /// <summary>A quarter of a second, doubling, and never more than two.</summary>
    private static TimeSpan Gap(int looks)
    {
        var doubled = FirstGap * Math.Pow(2, Math.Min(looks - 1, 8));
        return doubled < LongestGap ? doubled : LongestGap;
    }

    private static AppState Unasked(AppState state, Expectation expected) => state with
    {
        Expecting = [.. state.Expecting.Where(e => e != expected)],
    };

    /// <summary>What was expected, seen - and said, with what the door could not give.</summary>
    private static AppState Seen(AppState state, Expectation expected) =>
        Raised(Unasked(state, expected), About(state, expected, seen: true));

    /// <summary>What a notification about an expectation names.</summary>
    /// <remarks>
    /// <b>By the flight's id wherever the list has it</b>, because that is what
    /// going to it looks for. A gate names its flight by number, so the id is
    /// found through the list, and the number stands in only where the list does
    /// not have the flight yet.
    /// </remarks>
    private static Notification About(AppState state, Expectation expected, bool seen)
    {
        var flights = state.Flights?.Flights ?? [];

        return expected.Kind switch
        {
            ExpectationKind.GateAnswered => new Notification
            {
                Kind = seen ? NotificationKind.GateAnswered : NotificationKind.GateStillWaiting,
                FlightId = flights.FirstOrDefault(f => string.Equals(
                        f.FlightNumber, expected.Id, StringComparison.OrdinalIgnoreCase))?.FlightId
                    ?? expected.Id,
                FlightNumber = expected.Id,
                Name = expected.Obligation,
            },

            _ when seen && flights.FirstOrDefault(f => f.FlightId == expected.Id) is var flight =>
                new Notification
                {
                    Kind = NotificationKind.FlightOpened,
                    FlightId = expected.Id,
                    FlightNumber = flight?.FlightNumber,
                    Name = flight?.Name,
                },

            _ => new Notification { Kind = NotificationKind.NotListedYet, FlightId = expected.Id },
        };
    }

    /// <summary>
    /// A notification added to the stack, and showing unless somebody is reading.
    /// </summary>
    /// <remarks>
    /// <b>The page does not move under a person reading it</b>; only the count
    /// does. Unfocused, the newest is the one worth drawing.
    /// </remarks>
    private static AppState Raised(AppState state, Notification notification) => state with
    {
        Notifications = [.. state.Notifications, notification],
        NotificationAt = state.Mode == UiMode.Notifications
            ? state.NotificationAt
            : state.Notifications.Count,
    };

    /// <summary>
    /// Gone when the corner has been quiet for long enough - and never while
    /// somebody is reading them.
    /// </summary>
    /// <remarks>
    /// <b>The clock starts again when they are put down</b>, not when they
    /// arrived: taking a notification away from under the person reading it is
    /// the popup's version of focus being taken away, and so is taking it away a
    /// moment after they looked up from it.
    /// </remarks>
    private AppState Aged(AppState state, DateTimeOffset now, bool raised)
    {
        if (state.Notifications.Count == 0 || state.Mode == UiMode.Notifications)
        {
            _notificationsUntil = null;
            return state;
        }

        if (raised || _notificationsUntil is null)
        {
            _notificationsUntil = now + NotificationsLast;
            return state;
        }

        if (now < _notificationsUntil)
        {
            return state;
        }

        _notificationsUntil = null;
        return state with { Notifications = [], NotificationAt = 0 };
    }
}
