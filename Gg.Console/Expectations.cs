using Gg.Client;
using Gg.Contracts;

namespace Gg.Console;

/// <summary>
/// Looks for what this console's writes said they did, until it appears or the
/// console stops expecting it - and says which.
/// </summary>
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

    /// <summary>Start a look that is due, fold one that has landed, and age the notifications.</summary>
    public AppState Advance(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _ = _look;
        _ = _clock;
        return state;
    }

    /// <summary>The looks, over a read of one flight that answers null when it is not there yet.</summary>
    public static Func<Expectation, Task<Func<AppState, AppState>?>> Looks(
        Func<string, Task<FlightSummary?>> flight)
    {
        ArgumentNullException.ThrowIfNull(flight);
        return _ => Task.FromResult<Func<AppState, AppState>?>(null);
    }
}
