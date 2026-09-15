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
/// Oldest first, then by reason, then by what a person types, then by key.
/// </summary>
/// <remarks>
/// <para>
/// A placeholder that says so. Oldest-first is defensible - something that has
/// needed attention for two hours probably needs it more than something from
/// two minutes ago - but it is a proxy, and the moment a real urgency signal
/// exists this is wrong.
/// </para>
/// <para>
/// Total by construction: ties fall through to the reference and then to the
/// key, so the order never depends on the order rows happened to arrive in. A
/// sort that is unstable under equal keys makes the cursor appear to move on
/// its own.
/// </para>
/// <para>
/// <b>AND IT DOES NOT KNOW WHAT KIND OF ROW IT IS LOOKING AT.</b> This broke
/// its ties on the flight number and then the flight id, which stopped being
/// members every row has - so a row without them would have sorted against
/// null, which is not an ordering. The reference and the key are what every
/// row has. If ordering the queue needed a case per kind, the promise above -
/// that replacing this is a new class rather than an excavation - would be
/// spent on the first real urgency signal it was left for, and a test reads
/// this file and refuses to find a kind named in it.
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
                .ThenBy(r => r.Reference, StringComparer.Ordinal)
                .ThenBy(r => r.Key, StringComparer.Ordinal),
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
