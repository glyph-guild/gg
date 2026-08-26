namespace Gg.Client;

/// <summary>
/// The order a changeset lands in, so no intermediate state is one nobody wrote.
/// </summary>
/// <remarks>
/// <para>
/// <b>Apply is per document, so a changeset is a sequence — and a sequence visits
/// instants.</b> ADR-0014 proved order cannot matter for COMPOSING documents,
/// because every lower-layer operation is a meet. This is the place order does
/// matter: applying them. Land a work kind that grants a move before the
/// narrowing that constrains it, and the estate spends the interval between two
/// gates holding a capability nothing governs.
/// </para>
/// <para>
/// <b>Tightenings first, so every intermediate is at or below both endpoints</b>
/// — never looser than where the estate started, never looser than where it is
/// going. The safe order falls out of the same operators the composer uses, which
/// is why it needs no separate table and no declaration.
/// </para>
/// <para>
/// <b>Ordering rather than atomicity, and the failure mode is what decides
/// it.</b> A gate rejected mid-changeset leaves the estate stricter than
/// intended: flights may be blocked and will say so by name, and nothing is
/// ungoverned. Atomicity across per-name streams would buy a rollback protocol —
/// multi-repo atomicity at its full price — to prevent a failure that is already
/// safe in the only direction that matters.
/// </para>
/// </remarks>
public static class Changeset
{
    /// <summary>A change that only ever constrains. Safe at any point.</summary>
    public const string Tightening = "tightening";

    /// <summary>A change that could allow more. Must not precede a tightening.</summary>
    public const string Widening = "widening";

    /// <summary>
    /// A document that stops applying entirely.
    /// </summary>
    /// <remarks>
    /// Last of all, and not arbitrarily: a retirement removes every constraint in
    /// its document at once, so it is the widest change in any changeset it
    /// appears in.
    /// </remarks>
    public const string Retirement = "retirement";

    /// <summary>Puts a changeset in the order it is safe to apply in.</summary>
    /// <remarks>
    /// <b>Within a direction the order is free, so it is by name.</b> Two applies
    /// at the same direction cannot make each other unsafe — but a free order
    /// should be the same one every time, or two people running one changeset get
    /// two flight sequences and neither can be reviewed against the other.
    /// </remarks>
    public static IReadOnlyList<DocumentChange> InSafeOrder(
        IReadOnlyList<DocumentChange> changes)
    {
        ArgumentNullException.ThrowIfNull(changes);

        return
        [
            .. changes
                .OrderBy(Rank)
                .ThenBy(c => c.Name, StringComparer.Ordinal),
        ];
    }

    /// <summary>
    /// How late a direction must land. Throws on one nobody declared.
    /// </summary>
    /// <remarks>
    /// <b>Unknown is not neutral</b> — the rule this codebase applies to
    /// direction everywhere. A change whose direction this build does not know
    /// cannot be placed safely, and guessing a position would put an unreviewed
    /// change into an interval nobody authored, which is the exact thing the
    /// ordering exists to prevent.
    /// </remarks>
    private static int Rank(DocumentChange change) => change.Direction switch
    {
        Tightening => 0,
        Widening => 1,
        Retirement => 2,
        _ => throw new InvalidOperationException(
            $"'{change.Direction}' is not a direction this build can order. A change it "
          + "cannot place cannot be applied safely, and guessing would put it in an "
          + "interval nobody authored."),
    };
}
