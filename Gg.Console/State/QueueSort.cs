namespace Gg.Console;

/// <summary>
/// How the queue is ordered. A named, replaceable strategy from the first
/// commit.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is not the real answer and must not be mistaken for it.</b> "What
/// needs me soonest" means gate SLA, blocked dependents and budget exhaustion,
/// and none of those exist yet. The risk is shipping recency, calling it a
/// queue, and letting it calcify into the thing everybody works around.
/// </para>
/// <para>
/// So it is named after what it actually does rather than after what a queue
/// is for, and the seam is here from the start so replacing it is a new class
/// rather than an excavation.
/// </para>
/// </remarks>
public interface IQueueSort
{
    /// <summary>What this ordering is called, so a person can see which one they have.</summary>
    string Name { get; }

    IReadOnlyList<QueueRow> Order(IReadOnlyList<QueueRow> rows);
}

/// <summary>
/// Oldest first, then by reason, then by flight number.
/// </summary>
/// <remarks>
/// <para>
/// A placeholder that says so. Oldest-first is defensible - something that has
/// needed attention for two hours probably needs it more than something from
/// two minutes ago - but it is a proxy, and the moment a real urgency signal
/// exists this is wrong.
/// </para>
/// <para>
/// Total by construction: ties fall through to the flight number, so the order
/// never depends on the order rows happened to arrive in. A sort that is
/// unstable under equal keys makes the cursor appear to move on its own.
/// </para>
/// </remarks>
public sealed class OldestFirst : IQueueSort
{
    public string Name => "oldest first (placeholder: no urgency signal exists yet)";

    public IReadOnlyList<QueueRow> Order(IReadOnlyList<QueueRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        return
        [
            .. rows
                .OrderBy(r => r.Since)
                .ThenBy(r => r.Reason)
                .ThenBy(r => r.FlightNumber, StringComparer.Ordinal)
                .ThenBy(r => r.FlightId, StringComparer.Ordinal),
        ];
    }
}

/// <summary>The ordering in force.</summary>
public static class QueueSort
{
    /// <summary>
    /// One place to change it, so replacing the strategy is one edit rather
    /// than a search for every caller that happened to sort.
    /// </summary>
    public static IQueueSort Default { get; } = new OldestFirst();
}
