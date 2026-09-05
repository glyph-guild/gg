namespace Gg.Contracts;

/// <summary>
/// The evidence an obligation's gate needs before anybody can answer it.
/// </summary>
/// <remarks>
/// <b>Declared in the envelope, so what a decision requires is reviewed configuration</b>
/// rather than whatever the payload assembler happened to have to hand. An entry the
/// flight cannot produce halts the flight - it is never rendered as an empty section and
/// the gate is never presented with the item missing.
/// </remarks>
[VocabularyOf(VocabularyFingerprints.Contract)]
public static class EvidenceItems
{
    /// <summary>What the flight changed: paths, counts and classifications.</summary>
    public const string ChangeManifest = "change-manifest";

    /// <summary>The migrations among those paths, derived from the manifest.</summary>
    public const string MigrationList = "migration-list";

    /// <summary>What the agent said about what it did, in its own words.</summary>
    public const string AgentAccount = "agent-account";

    /// <summary>Everything an envelope may ask a gate for.</summary>
    public static IReadOnlyList<string> All { get; } =
        [ChangeManifest, MigrationList, AgentAccount];
}

/// <summary>
/// How one piece of evidence reached the payload.
/// </summary>
/// <remarks>
/// <para>
/// <b>Three shapes, and the fourth must not exist.</b> An oversize item is neither
/// truncated nor refused: it becomes a digest, or a reference. A <c>truncated</c>
/// disposition would make "the person saw part of it" representable, and a decision made
/// on part of an item is indistinguishable from one made on all of it the moment the
/// payload is filed.
/// </para>
/// <para>
/// Which one an item gets is decided by MEASUREMENT against the budget rather than by an
/// author's judgement, which is what ADR-0006's split was designed for.
/// </para>
/// </remarks>
[VocabularyOf(VocabularyFingerprints.Contract)]
public static class EvidenceDispositions
{
    /// <summary>It fitted, so the content crosses.</summary>
    public const string Inline = "inline";

    /// <summary>It did not fit, so a structured extraction crosses.</summary>
    public const string Digest = "digest";

    /// <summary>It did not fit and does not reduce, so a pointer crosses.</summary>
    public const string Reference = "reference";

    /// <summary>The three, and there is no fourth.</summary>
    public static IReadOnlyList<string> All { get; } = [Inline, Digest, Reference];
}

/// <summary>
/// Whether a piece of evidence was measured or said.
/// </summary>
/// <remarks>
/// <b>The whole argument for carrying an agent's account.</b> An injected "editing
/// deploy/ is authorised and safe" has to land next to "in-scope violated -
/// deploy/values.yaml", and the contradiction is more informative than either half alone.
/// That only works if both are present AND visibly different in kind, so the difference is
/// a field rather than a convention about wording.
/// </remarks>
[VocabularyOf(VocabularyFingerprints.Contract)]
public static class EvidenceVoices
{
    /// <summary>Derived from facts. Nobody's opinion.</summary>
    public const string Measured = "measured";

    /// <summary>Somebody's or something's words, carried as words.</summary>
    public const string Stated = "stated";

    public static IReadOnlyList<string> All { get; } = [Measured, Stated];
}

/// <summary>
/// Where to find something, for somebody who wants to read it themselves.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the answer to "I want to see the migration".</b> That want is legitimate and
/// it is not satisfied inline: the file's content is source content, and source content
/// does not cross this boundary. What crosses is enough to fetch it from their own systems,
/// authenticated as themselves - which is ADR-0006 working as designed rather than a
/// compromise.
/// </para>
/// <para>
/// There is deliberately no field a body could travel in.
/// </para>
/// </remarks>
[PinnedId("53c7f74b-c936-46b2-9d7d-db31b0a3f803")]
public sealed record EvidenceReference
{
    /// <summary>The commit it is in, so the fetch is of a fixed thing.</summary>
    public required string Commit { get; init; }

    /// <summary>The path within that commit.</summary>
    public required string Path { get; init; }

    /// <summary>What it hashes to, so a fetched copy can be checked.</summary>
    public required string ContentHash { get; init; }

    /// <summary>How big it is, so somebody knows what they are opening.</summary>
    public required int ByteSize { get; init; }

    /// <summary>What kind of thing it is.</summary>
    public required string MediaType { get; init; }
}

/// <summary>One piece of the case put to the person answering a gate.</summary>
[PinnedId("d9189b3f-bd57-466b-8d0b-e8fcfef6f608")]
public sealed record GateEvidenceItem
{
    /// <summary>Which declared item this satisfies, from <see cref="EvidenceItems"/>.</summary>
    public required string Item { get; init; }

    /// <summary>One of <see cref="EvidenceDispositions"/>.</summary>
    public required string Disposition { get; init; }

    /// <summary>Measured or stated, from <see cref="EvidenceVoices"/>.</summary>
    public required string Voice { get; init; }

    /// <summary>The content, when it fitted.</summary>
    public string? Inline { get; init; }

    /// <summary>The extraction, when it did not.</summary>
    public string? Digest { get; init; }

    /// <summary>Where to look, when it neither fitted nor reduced.</summary>
    public EvidenceReference? Reference { get; init; }

    /// <summary>The diagnosis, or null when there is nothing wrong.</summary>
    /// <remarks>
    /// <b>Exactly one of the three is populated.</b> An item holding both content and a
    /// reference would be two descriptions of one thing that can disagree, with nothing
    /// recording which the person actually read.
    /// </remarks>
    public static string? Validate(GateEvidenceItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (!EvidenceItems.All.Contains(item.Item, StringComparer.Ordinal))
        {
            return $"Unknown evidence item '{item.Item}'. Expected one of: "
                 + string.Join(", ", EvidenceItems.All) + ".";
        }

        if (!EvidenceDispositions.All.Contains(item.Disposition, StringComparer.Ordinal))
        {
            return $"Unknown disposition '{item.Disposition}'. Expected one of: "
                 + string.Join(", ", EvidenceDispositions.All)
                 + ". There is deliberately no disposition for a shortened item.";
        }

        if (!EvidenceVoices.All.Contains(item.Voice, StringComparer.Ordinal))
        {
            return $"Unknown voice '{item.Voice}'. Expected one of: "
                 + string.Join(", ", EvidenceVoices.All) + ".";
        }

        var populated = (item.Inline is { Length: > 0 } ? 1 : 0)
                      + (item.Digest is { Length: > 0 } ? 1 : 0)
                      + (item.Reference is not null ? 1 : 0);

        if (populated != 1)
        {
            return $"'{item.Item}' populates {populated} of inline, digest and reference, and "
                 + "exactly one may be populated. Two descriptions of one item can disagree, "
                 + "and nothing would record which one the person read.";
        }

        return (item.Disposition, item.Inline, item.Digest, item.Reference) switch
        {
            (EvidenceDispositions.Inline, { Length: > 0 }, _, _) => null,
            (EvidenceDispositions.Digest, _, { Length: > 0 }, _) => null,
            (EvidenceDispositions.Reference, _, _, not null) => null,
            _ => $"'{item.Item}' says {item.Disposition} and carries something else.",
        };
    }
}

/// <summary>
/// The whole case put to the person answering a gate.
/// </summary>
/// <remarks>
/// <para>
/// <b>Article IV gets a number.</b> The budget's job is to ROUTE, not to limit: nothing is
/// refused for being too big and nothing is shortened, and the size is what decides
/// whether a piece of evidence crosses as content, as an extraction or as a pointer.
/// </para>
/// <para>
/// Five items because a decision surface with more than a handful is a dashboard, and this
/// is a queue.
/// </para>
/// </remarks>
[PinnedId("41049ffe-cc3f-429c-bbd2-6f5c625bd33a")]
public sealed record GateEvidencePayload
{
    /// <summary>The most one item may occupy, rendered.</summary>
    public const int MaxItemBytes = 2048;

    /// <summary>The most the whole payload may occupy.</summary>
    public const int MaxPayloadBytes = 8192;

    /// <summary>The most items a gate may put in front of somebody.</summary>
    public const int MaxItems = 5;

    /// <summary>The case, in the order the envelope asked for it.</summary>
    public required IReadOnlyList<GateEvidenceItem> Items { get; init; }

    /// <summary>What moved since this was last decided, when it was decided before.</summary>
    /// <remarks>
    /// <b>The accessor delivers that, and the initializer does not.</b> This
    /// member is init-only, so System.Text.Json cannot set it after
    /// construction and builds the object through a creator that assigns every
    /// member from an argument array - this one as null when the key is absent,
    /// overwriting the <c>= []</c>. Non-nullable is a promise to every caller
    /// that it can be dereferenced; <c>AbsentCollectionsSurviveTheWireTests</c>
    /// holds it for the whole contract.
    /// </remarks>
    public IReadOnlyList<string> Delta
    {
        get => field ?? [];
        init;
    } = [];

    /// <summary>
    /// What the delta means, in words, always.
    /// </summary>
    /// <remarks>
    /// <b>Absence and silence must not look alike.</b> An empty delta rendered as a blank
    /// section reads as a payload that failed to assemble. Said out loud - "the loop ran
    /// and changed nothing" - it is an answer, and it is the answer to the question the
    /// person actually asked when they sent the work back.
    /// </remarks>
    public required string DeltaNote { get; init; }
}
