namespace Gg.Client;

/// <summary>
/// The edits a person makes to a draft by position, from the mux panel - by the rules the tool
/// server's own edits keep, so neither door leaves a draft the other would refuse. Slice
/// sixty-six.
/// </summary>
public static class PlanEdits
{
    /// <summary>The draft without the leg at <paramref name="at"/>, unless another leg waits for it.</summary>
    public static DraftChange Dropped(PlanDraft draft, int at)
    {
        ArgumentNullException.ThrowIfNull(draft);

        if (at < 0 || at >= draft.Legs.Count)
        {
            return DraftChange.Refuse("There is no leg there to drop.");
        }

        var subject = draft.Legs[at].Subject;
        var waiting = draft.Legs
            .Where((other, index) => index != at
                && string.Equals(other.After, subject, StringComparison.Ordinal))
            .Select(other => $"'{other.Subject}'")
            .ToList();

        return waiting.Count > 0
            ? DraftChange.Refuse(
                $"'{subject}' is not dropped: {string.Join(" and ", waiting)} come after it, and "
              + "would be left waiting for a leg that no longer exists. Revise their 'after' "
              + "first, or drop them.")
            : new DraftChange.Written(draft with { Legs = [.. draft.Legs.Where((_, index) => index != at)] });
    }

    /// <summary>The draft with the leg at <paramref name="at"/> moved <paramref name="by"/> places.</summary>
    /// <remarks>
    /// <b>Order is the person's to set</b>, and moving a leg never changes what it waits for:
    /// <c>after</c> is a dependency, the list is only the order they are read in.
    /// </remarks>
    public static DraftChange Moved(PlanDraft draft, int at, int by)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var to = at + by;
        if (at < 0 || at >= draft.Legs.Count || to < 0 || to >= draft.Legs.Count)
        {
            return DraftChange.Refuse("That leg is already at the end it would move past.");
        }

        var legs = draft.Legs.ToList();
        (legs[at], legs[to]) = (legs[to], legs[at]);
        return new DraftChange.Written(draft with { Legs = legs });
    }
}
