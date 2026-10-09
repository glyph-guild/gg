namespace Gg.Console;

/// <summary>What kind of place an intent comes from.</summary>
public enum IntentSourceKind
{
    /// <summary>A tracker's work items, read through a declared reader (ADO today).</summary>
    Tracker,
}

/// <summary>One place intents come from, as the intents tab's left strip lists it.</summary>
/// <param name="Key">The source's key: a tracker's reader key, such as <c>ado</c>.</param>
/// <param name="Kind">What kind of source it is.</param>
/// <param name="Label">What the strip calls it.</param>
public sealed record IntentSource(string Key, IntentSourceKind Kind, string Label);

/// <summary>The intent sources this console offers.</summary>
public static class IntentSources
{
    public static IReadOnlyList<IntentSource> All(AppState state) => [];

    /// <summary>The source the tab shows: the chosen one, or the first offered; null for none.</summary>
    public static IntentSource? Shown(AppState state) => null;
}
