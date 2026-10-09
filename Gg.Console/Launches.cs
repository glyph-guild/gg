namespace Gg.Console;

/// <summary>
/// Asking for a flight, sending it, and folding what the door said - three
/// steps, so the first can be on the screen before the second has started.
/// </summary>
/// <remarks>
/// <para>
/// <b>Asked for 2026-10-09: when we fly something, the notification should come
/// up immediately, animated while it is submitted.</b> Flying was the shell's:
/// the key ended the session, the loop made the request with the screen gone,
/// and a new session was built over the answer - and the corner said nothing
/// until the flight had been accepted AND projected, up to thirty seconds later.
/// </para>
/// <para>
/// <b>So the press is pure and the request is not the session's.</b>
/// <see cref="Asked"/> puts the launch and its corner entry in the model, which
/// the screen draws on the same keypress. <see cref="Launcher"/> sends it on a
/// task owned outside every UI lifetime - <c>Expectations</c>' arrangement, and
/// <c>BackgroundReads</c>' argument: the session does not write, it folds an
/// answer that has arrived. Not through <c>BackgroundReads</c> itself, which
/// holds one read and abandons it for the next; a flight is a write, and a tab
/// switch must never be what loses the answer to one.
/// </para>
/// <para>
/// <b>One corner entry for the whole opening.</b> Submitting becomes accepted,
/// and accepted becomes the number or "not listed yet", in place - three
/// notifications stacked for one flight would be a count that means nothing.
/// </para>
/// </remarks>
public static class Launches
{
    /// <summary>What the door said, as the fold needs it.</summary>
    public abstract record Answer;

    /// <summary>This item has flown before; a person decides whether again.</summary>
    public sealed record Asks(string Why) : Answer;

    /// <summary>The door's answer: a flight named, or the sentence saying why not.</summary>
    public sealed record Answered(Opening Opening) : Answer;

    /// <summary>A work item to fly, captured at the press.</summary>
    public static Launch Ticket(
        string provider, string id, string title, AppState state, bool check)
    {
        ArgumentNullException.ThrowIfNull(state);

        return new Launch
        {
            Token = Guid.NewGuid(),
            Kind = LaunchKind.Ticket,
            Title = title,
            Provider = provider,
            Id = id,
            Against = state.Against,
            WorkKind = WorkKinds.Picked(state),
            Check = check,
        };
    }

    /// <summary>Words somebody wrote, captured when they finished writing.</summary>
    public static Launch Composed(
        string intent, IReadOnlyList<string> against, string? workKind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(intent);

        return new Launch
        {
            Token = Guid.NewGuid(),
            Kind = LaunchKind.Intent,
            // THE FIRST LINE, because it is what a person wrote first and the
            // corner has one line for it.
            Title = intent.Split('\n', 2)[0].Trim(),
            Intent = intent,
            Against = against,
            WorkKind = workKind,
        };
    }

    /// <summary>
    /// The launch in the model, and its corner entry up - nothing sent yet.
    /// </summary>
    public static AppState Asked(AppState state, Launch launch)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(launch);

        return state with
        {
            Launches = [.. state.Launches, launch],
            Notifications =
            [
                .. state.Notifications,
                new Notification
                {
                    Kind = NotificationKind.Submitting,
                    FlightId = "",
                    Launch = launch.Token,
                    Said = launch.Title,
                },
            ],

            // THE NEW ONE SHOWING, unless somebody is reading the corner - the
            // rule Expectations raises by, so a launch does not pull the
            // corner out from under a person paging through it.
            NotificationAt = state.Mode == UiMode.Notifications
                ? state.NotificationAt
                : state.Notifications.Count,
        };
    }

    /// <summary>Asks the door. Blocking, and never on the UI thread.</summary>
    public static Answer Perform(Launch launch, IConsoleActions actions)
    {
        ArgumentNullException.ThrowIfNull(launch);
        ArgumentNullException.ThrowIfNull(actions);

        return launch.Kind switch
        {
            LaunchKind.Ticket when launch.Check
                && actions.AlreadyFlown(launch.Provider!, launch.Id!) is { Length: > 0 } why
                => new Asks(why),
            LaunchKind.Ticket => new Answered(actions.FlyTicket(
                launch.Provider!, launch.Id!, launch.Against, launch.WorkKind)),
            LaunchKind.Intent => new Answered(actions.Fly(
                launch.Intent!, launch.Against, launch.WorkKind)),
            _ => throw new ArgumentOutOfRangeException(
                nameof(launch), launch.Kind, "unknown launch"),
        };
    }

    /// <summary>What the door said, folded onto whatever is on screen now.</summary>
    public static AppState Landed(AppState state, Launch launch, Answer answer)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(launch);
        ArgumentNullException.ThrowIfNull(answer);

        var done = state with
        {
            Launches = [.. state.Launches.Where(l => l.Token != launch.Token)],
        };

        switch (answer)
        {
            case Asks asks:
                // THE QUESTION ONLY OVER A SCREEN THAT CAN TAKE ONE. Somebody who
                // opened a modal while the check was out is reading something
                // else; the corner says why nothing was sent instead.
                if (done.Mode is not UiMode.Normal)
                {
                    return Replaced(done, launch, new Notification
                    {
                        Kind = NotificationKind.NotOpened,
                        FlightId = "",
                        Said = $"Not sent: {asks.Why}",
                    });
                }

                return Dropped(done, launch) with
                {
                    Mode = UiMode.ConfirmFlight,
                    PendingFlight = new PendingFlight
                    {
                        Provider = launch.Provider!,
                        Id = launch.Id!,
                        Why = asks.Why,
                    },
                };

            case Answered { Opening: var opening }:
                var said = launch is { Kind: LaunchKind.Ticket, Check: true }
                    ? opening.Said + " " + PaneText.ComposedBy(ComposingFor.WorkItem)
                    : opening.Said;

                var expecting = ConsoleLoop.Expect(done with { LastFlightOpened = said }, opening);

                // NO FLIGHT NAMED, AND STILL READ AGAIN. A console cannot tell
                // from a sentence whether anything changed: a POST that reached
                // the control plane and failed on the way back opens a flight
                // and reports that nothing was opened. So the refresh is asked
                // for - the loop's old rule, carried to where the answer lands.
                if (opening.FlightId is not { Length: > 0 })
                {
                    expecting = expecting with
                    {
                        Refresh = expecting.Refresh with { Wanted = true },
                    };
                }

                return Replaced(expecting, launch, opening.FlightId is { Length: > 0 } id
                    ? new Notification
                    {
                        Kind = NotificationKind.Accepted,
                        FlightId = id,
                        Said = launch.Title,
                    }
                    : new Notification
                    {
                        Kind = NotificationKind.NotOpened,
                        FlightId = "",
                        Said = opening.Said,
                    });

            default:
                throw new ArgumentOutOfRangeException(nameof(answer), answer, "unknown answer");
        }
    }

    /// <summary>Whether a notification is still in motion, and so animates.</summary>
    public static bool InMotion(Notification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);

        return notification.Kind is NotificationKind.Submitting or NotificationKind.Accepted;
    }

    /// <summary>The launch's corner entry, replaced where it stands.</summary>
    /// <remarks>
    /// <b>Appended if it is gone</b> - aged out, or dismissed. Somebody who
    /// cleared the corner while a flight was out still wants to know how it
    /// went.
    /// </remarks>
    private static AppState Replaced(AppState state, Launch launch, Notification with)
    {
        var at = IndexOf(state, launch);

        if (at < 0)
        {
            return state with
            {
                Notifications = [.. state.Notifications, with],
                NotificationAt = state.Mode == UiMode.Notifications
                    ? state.NotificationAt
                    : state.Notifications.Count,
            };
        }

        var replaced = state.Notifications.ToList();
        replaced[at] = with;
        return state with { Notifications = replaced };
    }

    /// <summary>The launch's corner entry, gone, because a question replaced it.</summary>
    private static AppState Dropped(AppState state, Launch launch)
    {
        var at = IndexOf(state, launch);

        if (at < 0)
        {
            return state;
        }

        var left = state.Notifications.Where((_, i) => i != at).ToList();

        return state with
        {
            Notifications = left,
            NotificationAt = left.Count == 0 ? 0 : Math.Clamp(state.NotificationAt, 0, left.Count - 1),
        };
    }

    private static int IndexOf(AppState state, Launch launch)
    {
        for (var i = 0; i < state.Notifications.Count; i++)
        {
            if (state.Notifications[i].Launch == launch.Token)
            {
                return i;
            }
        }

        return -1;
    }
}

/// <summary>
/// Sends each launch once, on a task, and folds the answer when it lands.
/// </summary>
/// <remarks>
/// <para>
/// <b>Owned outside every UI lifetime</b>, like <c>Expectations</c>: a launch
/// asked for in one session may be answered in the next, and the task is keyed
/// by the launch's token so a rebuilt session neither loses it nor sends it
/// again.
/// </para>
/// <para>
/// <b>As many as are asked for.</b> Two flights flown in a row are two
/// requests, both answered - the opposite of the reads' single slot.
/// </para>
/// </remarks>
public sealed class Launcher(Func<Launch, Launches.Answer> perform)
{
    private readonly Func<Launch, Launches.Answer> _perform = perform;
    private readonly Dictionary<Guid, Task<Launches.Answer>> _running = [];

    /// <summary>The launcher the console runs, over the actions it was given.</summary>
    public static Launcher Over(IConsoleActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        return new Launcher(launch => Launches.Perform(launch, actions));
    }

    /// <summary>Starts every launch not already on its way. The shell's.</summary>
    /// <remarks>
    /// <b>The shell starts, the session folds</b> - CLAUDE.md's rule that a UI
    /// session may not start anything. Called by <c>ConsoleLoop</c> between
    /// sessions; <see cref="Fold"/> is all the screen calls.
    /// </remarks>
    public void Start(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        foreach (var launch in state.Launches)
        {
            if (!_running.ContainsKey(launch.Token))
            {
                _running[launch.Token] = Task.Run(() => _perform(launch));
            }
        }
    }

    /// <summary>Whether any send has landed and is waiting to be folded.</summary>
    public bool Landed => _running.Values.Any(t => t.IsCompleted);

    /// <summary>Folds every send that has landed onto what is on screen now.</summary>
    public AppState Fold(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        foreach (var (token, sending) in _running.Where(r => r.Value.IsCompleted).ToList())
        {
            _running.Remove(token);

            if (state.Launches.FirstOrDefault(l => l.Token == token) is not { } launch)
            {
                continue;
            }

            // A THROW IS A SENTENCE, never a crash. The actions already turn the
            // refusals they expect into sentences; this is the one they did not,
            // on a task beside a console somebody is using.
            var answer = sending.IsCompletedSuccessfully
                ? sending.Result
                : new Launches.Answered(new Opening(
                    "Nothing was opened — "
                  + (sending.Exception?.GetBaseException().Message ?? "the request did not finish")));

            state = Launches.Landed(state, launch, answer);
        }

        return state;
    }
}
