using Gg.Contracts;

namespace Gg.Client;

/// <summary>
/// What an intent is called when something has to find it again: a ticket's <c>provider#id</c>, a
/// link's uri, a file's <c>repository:path[@ref]</c>; null for free text.
/// </summary>
public static class IntentKeys
{
    /// <remarks>
    /// <b>The control plane's <c>RecordedIntent.KeyOf</c>, spelled once on this side</b>: the
    /// itinerary rows carry it as <c>IntentKey</c> and <c>?intent=</c> reads it, so the console and
    /// the command line must say it the same way or they find nothing.
    /// </remarks>
    public static string? Of(FlightIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);

        if (!string.IsNullOrWhiteSpace(intent.Uri))
        {
            return intent.Uri;
        }

        if (!string.IsNullOrWhiteSpace(intent.Repository) && !string.IsNullOrWhiteSpace(intent.Path))
        {
            return string.IsNullOrWhiteSpace(intent.Ref)
                ? $"{intent.Repository}:{intent.Path}"
                : $"{intent.Repository}:{intent.Path}@{intent.Ref}";
        }

        return !string.IsNullOrWhiteSpace(intent.Provider) && !string.IsNullOrWhiteSpace(intent.Id)
            ? Ticket(intent.Provider, intent.Id)
            : null;
    }

    /// <summary>A work item's key.</summary>
    public static string Ticket(string provider, string id) => $"{provider}#{id}";

    /// <summary>The plans (legs) of <paramref name="page"/> about <paramref name="key"/>.</summary>
    public static BoardPage About(BoardPage page, string key)
    {
        ArgumentNullException.ThrowIfNull(page);

        return page with
        {
            Nominations = [.. page.Nominations.Where(n => string.Equals(n.IntentKey, key, StringComparison.Ordinal))],
        };
    }
}
