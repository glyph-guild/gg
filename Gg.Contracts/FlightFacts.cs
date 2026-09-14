namespace Gg.Contracts;

/// <summary>
/// What a flight actually recorded, as the ledger holds it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Because a flight records nine facts and a story shows one.</b> Two fact
/// kinds reach a story - a loop's outcome and its question - and the outcome's
/// reason is cut where it is produced, to the first paragraph or 280 characters,
/// on the argument that the row somebody reads first has to stay short. That
/// argument is right and it left the rest with no reader at all: the manifests,
/// the provenance of each tree, the digest extracted from the transcript, and
/// the proposal a triage or scoring flight exists to make.
/// </para>
/// <para>
/// <b>A claim and a measurement must not read the same.</b> The one visible line
/// of a scoring flight is the agent's own prose - <i>"Scored 18119 and proposed
/// Custom.HAL = 4"</i> - and the recorded proposal that either backs it or does
/// not was unreachable. A flight that said it proposed and proposed nothing read
/// identically to one that did. Everything else in this contract works to keep
/// what an agent SAYS apart from what a machine MEASURED; a surface showing only
/// the first is the one place that separation cannot afford to be quiet.
/// </para>
/// <para>
/// <b>Not a story, and not a log.</b> The log is what the control plane did to a
/// flight - leases granted, renewed, released. The story folds that into
/// something a person reads. This is neither: it is what the RUNNER shipped, and
/// the only one of the three whose contents a customer audits.
/// </para>
/// </remarks>
[PinnedId("f1d2a7b4-3c05-4e91-8a6d-72b0e9c41538")]
public sealed record FlightFacts
{
    /// <summary>What a person types. <c>GG-42</c>.</summary>
    public required string FlightNumber { get; init; }

    /// <summary>
    /// Every fact this flight has shipped, in the order the ledger holds them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>EMPTY IS AN ANSWER.</b> A flight nobody has flown yet and a flight
    /// whose runner shipped nothing are both real, and the list says so without
    /// a second field for a reader to disagree with.
    /// </para>
    /// <para>
    /// <b>AND IT IS NOT NECESSARILY FINAL.</b> A landing ships its own fact
    /// AFTER the batch a loop produced - on one measured flight the loop's facts
    /// landed at 17:09:53 and the destination's write at 17:09:55 - so a read
    /// taken while a flight is still landing is correct and incomplete. Nothing
    /// here claims otherwise, and a reader that rendered this as settled would
    /// be making a claim this list does not.
    /// </para>
    /// </remarks>
    public required IReadOnlyList<RecordedFact> Facts { get; init; }
}

/// <summary>
/// One fact, and the budget the control plane held it against.
/// </summary>
/// <remarks>
/// <para>
/// <b>The stored envelope, not a summary of it.</b>
/// <see cref="FactEnvelope"/> is already pinned and in the vocabulary because a
/// runner ships it, so this read adds a container and nothing else. A summary
/// would be a second projection of one thing - and the half a reader actually
/// wants is what a summary drops: which paths a manifest names, which fields a
/// proposal sets, which commit a tree was at.
/// </para>
/// <para>
/// <b>Nothing is withheld by the disposition, which is why this can hand over
/// the envelope at all.</b> The control plane's own note says it: disposition
/// decides the budget an item is held against, not what is stored - the payload
/// is serialized whole either way. The exception is the transcript, which is a
/// REFERENCE by kind: hash, size, media type and a locator, and the bytes stay
/// on the machine that produced them. Dereferencing is a capability this
/// platform does not have and this read does not add one.
/// </para>
/// </remarks>
[PinnedId("6b83e0c2-9d41-47af-b5e8-0a1c36d9f274")]
public sealed record RecordedFact
{
    /// <summary>The fact, exactly as the runner shipped it.</summary>
    public required FactEnvelope Fact { get; init; }

    /// <summary>
    /// Which budget held it: one of the control plane's evidence dispositions.
    /// </summary>
    /// <remarks>
    /// <b>CARRIED, NEVER DERIVED.</b> The table mapping a fact kind to inline,
    /// digest or reference is the control plane's, and it is not reachable from
    /// this assembly; a reader inferring it would be keeping a second copy of a
    /// decision that has one owner. It travels because it is the answer to why
    /// one row has no content - a transcript rendered empty without saying
    /// <i>reference</i> reads as a defect rather than as a boundary.
    /// </remarks>
    public required string Disposition { get; init; }

    /// <summary>When the control plane recorded it, which is not when it happened.</summary>
    /// <remarks>
    /// The fact carries its own <c>ObservedAt</c> - what the runner saw, on the
    /// runner's clock. This is the ledger's, and the two differ by however long
    /// the shipping took. Both are kept because a gap between them is a real
    /// thing to be able to see.
    /// </remarks>
    public required DateTimeOffset RecordedAt { get; init; }
}
