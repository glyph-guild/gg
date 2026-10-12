namespace Gg.Console;

/// <summary>
/// The models a person may move a flight to, as root lists them, and which
/// one the picker's cursor is on (slice seventy-six).
/// </summary>
public static class ModelChoices
{
    /// <summary>Root's <c>models:</c>, or empty when root lists none or the envelope is unread.</summary>
    public static IReadOnlyList<string> Rows(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return [];
    }

    /// <summary>The model under the cursor while the picker is open, or null.</summary>
    public static string? Chosen(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return null;
    }
}
