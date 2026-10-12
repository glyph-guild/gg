namespace Gg.Console;

/// <summary>
/// The models a person may move a flight to, as root lists them, and which
/// one the picker's cursor is on (slice seventy-six).
/// </summary>
/// <remarks>
/// <b>Root's list and nothing else</b>, so the keystroke cannot name what the
/// door would refuse. Read off the envelope in the model, which the console
/// has whenever the Envelope tab has been read; until then the picker says so
/// rather than offering an empty list as an answer.
/// </remarks>
public static class ModelChoices
{
    /// <summary>Root's <c>models:</c>, or empty when root lists none or the envelope is unread.</summary>
    public static IReadOnlyList<string> Rows(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.Envelope?.Envelope.Models ?? [];
    }

    /// <summary>The model under the cursor while the picker is open, or null.</summary>
    /// <remarks>
    /// A cursor and not an answer: outside the picker nothing is chosen, which
    /// is what keeps the loop from sending a flight somewhere on a stale row.
    /// </remarks>
    public static string? Chosen(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Mode is not UiMode.ModelChoice)
        {
            return null;
        }

        var rows = Rows(state);
        return rows.Count == 0 ? null : rows[Math.Clamp(state.ModelSelected, 0, rows.Count - 1)];
    }
}
