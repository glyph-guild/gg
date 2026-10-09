using Gg.Contracts;

namespace Gg.Console;

/// <summary>What came of an intent: the flights about it, and the plans.</summary>
public static class ConsoleIntents
{
    /// <remarks>
    /// <b>Two reads, by the item's key</b>: the flights the control plane finds for it
    /// (<c>?intent=</c>), and the plans whose legs carry it. A plan's legs keep the ticket they were
    /// about, so its flights are already in the first list - the second says which plans they were.
    /// A failed read says so on the tab rather than looking like nothing came of the item.
    /// </remarks>
    public static Func<AppState, AppState> CameOfPatch(
        Func<string, IReadOnlyList<FlightSummary>?> flown, Func<BoardPage?> plans, AppState state)
    {
        ArgumentNullException.ThrowIfNull(flown);
        ArgumentNullException.ThrowIfNull(plans);
        ArgumentNullException.ThrowIfNull(state);

        if (WorkItemDetails.Key(state) is not { } key)
        {
            return current => current;
        }

        try
        {
            var flights = flown(key) ?? [];
            IReadOnlyList<string> planned = plans() is { } page
                ? [.. Gg.Client.IntentKeys.About(page, key).Nominations
                    .Select(n => n.ItineraryNumber)
                    .OfType<string>()
                    .Distinct(StringComparer.Ordinal)]
                : [];

            return current => WorkItemDetails.Key(current) == key
                ? current with
                {
                    WorkItemFlights = flights,
                    WorkItemPlans = planned,
                    WorkItemFlightsSaid = null,
                    WorkItemFlightSelected = 0,
                }
                : current;
        }
        catch (Exception unread) when (unread is HttpRequestException or InvalidOperationException
                                          or Gg.Client.NotSignedInException or TaskCanceledException)
        {
            return current => current with
            {
                WorkItemFlights = [],
                WorkItemPlans = [],
                WorkItemFlightsSaid = $"What came of {key} could not be read: {unread.Message}",
            };
        }
    }
}
