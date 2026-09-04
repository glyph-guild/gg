namespace Gg.Contracts;

/// <summary>
/// What granting a move means: the grant of a recallable capability, or the
/// act itself.
/// </summary>
/// <remarks>
/// <para>
/// ADR-0014's third resolution: <b>enforce a move whose use is itself the
/// outward act; record-only a move whose product a destination still
/// gates.</b> An edit is recallable - the manifest measures it, the gates ask
/// about it, and nothing has left the tree until a destination admits it. A
/// <c>send</c> is not: using it IS the outward act, and no gate downstream of
/// the act can unsend it. Article VI is the axis that will populate the
/// enforced set - <c>send</c>, and <c>power-on</c>/<c>power-off</c> when the
/// maintenance verbs arrive.
/// </para>
/// <para>
/// <b>The enforced set is correctly empty today</b>, and the refusal below is
/// what keeps that from being a mechanism waiting for a member: a move
/// classified as an outward act, whose enforcement nothing this product has
/// lets a probe confirm, is refused at authoring - the lock installed before
/// the door. The probe confirms WITHHOLDING of tools that were not granted;
/// an outward move would need its bound to hold WHILE GRANTED, which nothing
/// today can measure, so the confirmable-outward set is empty and the
/// classification alone drives the refusal. The commit that ships the first
/// confirmable enforcement is the commit that adds the second input.
/// </para>
/// <para>
/// Contract-fingerprinted rather than fact-fingerprinted, deliberately: the
/// kind never crosses in a fact - it changes what Validate accepts, which is
/// contract surface. <see cref="LoopMoves"/> itself stays on the fact
/// fingerprint, because its VALUES do cross.
/// </para>
/// </remarks>
[VocabularyOf(VocabularyFingerprints.Contract)]
public static class MoveKinds
{
    /// <summary>Using it is itself the outward act, and nothing can take it back.</summary>
    public const string OutwardAct = "outward-act";

    /// <summary>Its product is still gated at a destination before anything leaves.</summary>
    public const string RecordOnly = "record-only";

    public static IReadOnlyList<string> All { get; } = [OutwardAct, RecordOnly];

    /// <summary>
    /// Every declared move's kind. A dictionary rather than a switch, so
    /// totality is a testable property instead of a compiler default.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Table { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // Each produces something a destination gate still faces: read and
            // search produce knowledge, edit and write produce tree state the
            // manifest measures, run-tests produces outcomes the verdicts read.
            [LoopMoves.Read] = RecordOnly,
            [LoopMoves.Edit] = RecordOnly,
            [LoopMoves.RunTests] = RecordOnly,
            [LoopMoves.Search] = RecordOnly,
            [LoopMoves.Write] = RecordOnly,

            // NOMINATING PRODUCES A FACT, and whether that fact becomes a
            // flight is a destination's answer - so nothing has left, nobody
            // has been messaged, and admission can refuse it. That is the whole
            // difference between declaring a value and acting on one.
            [LoopMoves.Propose] = RecordOnly,
        };

    /// <summary>The kind of a declared move. THROWS on one nobody classified.</summary>
    /// <remarks>
    /// Article XI's poison, the Reason.Sentence shape: a sixth move added
    /// without a classification fails a build or a validate, never an audit -
    /// because the unclassified default would otherwise be record-only, and
    /// record-only is the answer that lets an unrecallable act be granted.
    /// </remarks>
    public static string Of(string move) =>
        Table.TryGetValue(move, out var kind)
            ? kind
            : throw new InvalidOperationException(
                $"'{move}' is not a move anybody classified. Decide one of: "
              + $"{OutwardAct}, {RecordOnly} - whether using it is itself the outward act "
              + "is the decision, and defaulting it would grant what nothing can recall.");
}
