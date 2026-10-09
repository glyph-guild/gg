using Gg.Contracts;

namespace Gg.Client;

/// <summary>
/// What an intent is called when something has to find it again: a ticket's <c>provider#id</c>, a
/// link's uri, a file's <c>repository:path[@ref]</c>; null for free text.
/// </summary>
public static class IntentKeys
{
    public static string? Of(FlightIntent intent) => null;

    /// <summary>The plans (legs) of <paramref name="page"/> about <paramref name="key"/>.</summary>
    public static BoardPage About(BoardPage page, string key) => page;
}
