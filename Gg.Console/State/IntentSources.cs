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
    /// <remarks>
    /// <b>One tracker source per declared reader</b>, in the order they were declared - the reader
    /// is what makes a tracker readable here at all. A directory of documents will be a second
    /// kind beside these.
    /// </remarks>
    public static IReadOnlyList<IntentSource> All(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return [.. state.ReaderKeys.Select(key => new IntentSource(key, IntentSourceKind.Tracker, key))];
    }

    /// <summary>The source the tab shows: the chosen one, or the first offered; null for none.</summary>
    public static IntentSource? Shown(AppState state)
    {
        var all = All(state);

        return all.FirstOrDefault(source => string.Equals(source.Key, state.IntentSource, StringComparison.Ordinal))
               ?? all.FirstOrDefault();
    }

    /// <summary>The key of the source just before <paramref name="key"/>, wrapping.</summary>
    public static string? Before(AppState state, string key)
    {
        var all = All(state);
        var at = all.Select((source, index) => (source, index))
            .FirstOrDefault(pair => string.Equals(pair.source.Key, key, StringComparison.Ordinal)).index;

        return all.Count == 0 ? null : all[(at - 1 + all.Count) % all.Count].Key;
    }

    /// <summary>The source after the shown one, wrapping; null when there is none.</summary>
    public static IntentSource? Next(AppState state)
    {
        var all = All(state);
        if (all.Count == 0)
        {
            return null;
        }

        var at = Shown(state) is { } shown ? all.ToList().IndexOf(shown) : -1;
        return all[(at + 1) % all.Count];
    }

    /// <summary>What the column on the intents tab's left draws: each source's label, and which is shown.</summary>
    public static (IReadOnlyList<string> Labels, int Shown) Column(AppState state)
    {
        var all = All(state);
        var shown = Shown(state);

        return ([.. all.Select(source => source.Label)], shown is null ? -1 : all.ToList().IndexOf(shown));
    }
}
