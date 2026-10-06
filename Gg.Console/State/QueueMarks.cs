using System.Collections.Immutable;

namespace Gg.Console;

/// <summary>
/// The queue's marks: which nominations are marked, and the moves that change
/// that.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nominations only.</b> A flight in the queue is answered through its gate,
/// and a gate is not something a person declines in bulk - every one asks its
/// own question. A nomination's only answers are open and decline, and decline
/// is the one that can be said once about many rows.
/// </para>
/// <para>
/// <b>Everything reads <see cref="Live"/>, nothing reads the raw set.</b> The
/// raw set can hold a row that has since left the queue, and a mark on a row
/// nobody can see must not be declined, counted or advertised.
/// </para>
/// </remarks>
public static class QueueMarks
{
    /// <summary>The marked nominations that are still in the queue.</summary>
    public static IReadOnlySet<Guid> Live(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.Marked.IsEmpty)
        {
            return state.Marked;
        }

        return state.Queue
            .Where(row => row.NominationId is { } id && state.Marked.Contains(id))
            .Select(row => row.NominationId!.Value)
            .ToImmutableHashSet();
    }

    /// <summary>The nomination under the queue's cursor, or null.</summary>
    public static Guid? Under(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return state.Selected?.NominationId;
    }

    /// <summary>Every row a watch stood about its own failure.</summary>
    public static IReadOnlyList<Guid> Failures(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return
        [
            .. state.Queue
                .Where(row => row.Reason == QueueReason.WatchFailing)
                .Select(row => row.NominationId)
                .OfType<Guid>(),
        ];
    }

    /// <summary>Marks the row under the cursor, or takes its mark off.</summary>
    public static AppState Toggled(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return Under(state) is not { } id
            ? state
            : state with
            {
                Marked = Live(state).Contains(id)
                    ? state.Marked.Remove(id)
                    : state.Marked.Add(id),
            };
    }

    /// <summary>
    /// Marks every watch failure, or - when every one already is - unmarks
    /// them.
    /// </summary>
    /// <remarks>
    /// <b>A toggle, so the same key undoes it.</b> "All of them, unless they all
    /// are" is the shape every file manager's select-all has, and it means a
    /// stray press costs one more press rather than a hunt for which rows it
    /// took.
    /// </remarks>
    public static AppState FailuresToggled(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var failures = Failures(state);
        var live = Live(state);

        return failures.Count == 0
            ? state
            : state with
            {
                Marked = failures.All(live.Contains)
                    ? state.Marked.Except(failures)
                    : state.Marked.Union(failures),
            };
    }
}
