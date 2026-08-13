namespace Gg.Contracts;

/// <summary>Something the loop hit, and what produced it.</summary>
/// <remarks>
/// Named by its tool rather than counted. "Three errors" sends somebody
/// looking; "Bash, and it could not find pytest" is the thing they needed.
/// </remarks>
[PinnedId("6d3c8a51-04e7-4f92-b8d6-1c5079ae3b24")]
public sealed record DigestError
{
    /// <summary>Which tool produced it.</summary>
    public required string Source { get; init; }

    /// <summary>What it said, stripped and bounded.</summary>
    public required string Detail { get; init; }
}

/// <summary>
/// What a loop did, extracted from its stream so a person can pick the work up
/// without the transcript.
/// </summary>
/// <remarks>
/// <para>
/// <b>The transcript does not cross.</b> It is a machine-local reference, which
/// is the disposition ADR-0006 chose for it, and that choice only holds if
/// something else carries enough to act on. This is that something else.
/// </para>
/// <para>
/// <b>Mechanically extracted, never model-generated.</b> A model's summary is a
/// claim rather than a fact; it would be non-deterministic, so digests would not
/// be comparable across flights, and comparison across flights is the whole of
/// Article XIII's hardening; and a summariser reading the transcript turns the
/// one artifact that crosses into an injection surface, because the transcript
/// can contain text addressed to a model. Step 3 read the manifest from the tree
/// rather than from the agent's account of its edits, and this is the same rule
/// one artifact further along.
/// </para>
/// <para>
/// <b><see cref="FilesReadNotEdited"/> is the field this fact exists for.</b>
/// A diff already says what changed. This says what the loop opened and left
/// alone, which is the closest thing the stream holds to <i>considered and ruled
/// out</i> - and the alternative, asking the agent what it rejected, is exactly
/// the account of itself that must not be trusted.
/// </para>
/// </remarks>
[FactKind(FactKinds.LoopDigest)]
[PinnedId("3f81b2c7-59da-4e08-9a63-d740be1c8562")]
public sealed record LoopDigest
{
    /// <summary>Which loop, by its id in the envelope.</summary>
    public required string LoopId { get; init; }

    /// <summary>
    /// Files it opened and did not change.
    /// </summary>
    /// <remarks>
    /// In first-read order, deduplicated. The order is part of the value: it is
    /// the sequence in which the loop looked at things, and sorting would throw
    /// that away for a tidiness nobody asked for.
    /// </remarks>
    public required IReadOnlyList<string> FilesReadNotEdited { get; init; }

    /// <summary>Files it changed, which the manifest also measures from the tree.</summary>
    public required IReadOnlyList<string> FilesEdited { get; init; }

    /// <summary>What it went looking for.</summary>
    public required IReadOnlyList<string> Searches { get; init; }

    /// <summary>What it hit.</summary>
    public required IReadOnlyList<DigestError> Errors { get; init; }

    /// <summary>
    /// Tools the agent USED that the envelope's moves did not declare.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The name is wrong and is kept deliberately.</b> Nothing refused these: the
    /// allow-list this runner passes to its executor does not bind, measured both
    /// directions in <c>EnforcesMovesTests</c>. So this is where the agent worked
    /// outside what the envelope declared - which is still exactly the signal
    /// somebody wants, and is a different claim from "we stopped it".
    /// </para>
    /// <para>
    /// Renaming a member on a pinned fact type is a vocabulary event, so the wire
    /// spelling stays and every place a person READS it says the true thing. Worth
    /// renaming the day something else moves this fact.
    /// </para>
    /// </remarks>
    public required IReadOnlyList<string> RefusedMoves { get; init; }

    /// <summary>Turns taken.</summary>
    public required int Attempts { get; init; }

    /// <summary>
    /// Where it stopped, from <see cref="LoopOutcomes"/>.
    /// </summary>
    /// <remarks>
    /// Carried here as well as on <c>loop.outcome</c>, deliberately. This fact
    /// has to stand alone: a person reading it must not have to go and find
    /// another one to learn whether the work finished.
    /// </remarks>
    public required string StopReason { get; init; }

    /// <summary>
    /// Value equality over the lists, because that is what this fact is for.
    /// </summary>
    /// <remarks>
    /// A record compares <see cref="IReadOnlyList{T}"/> members by reference, so
    /// two digests extracted from the same stream would come out unequal - which
    /// would quietly defeat the comparison across flights that Article XIII's
    /// hardening is entirely made of, and would do it in a way that looks like
    /// non-determinism in the extractor.
    /// </remarks>
    public bool Equals(LoopDigest? other) =>
        other is not null
        && string.Equals(LoopId, other.LoopId, StringComparison.Ordinal)
        && string.Equals(StopReason, other.StopReason, StringComparison.Ordinal)
        && Attempts == other.Attempts
        && FilesReadNotEdited.SequenceEqual(other.FilesReadNotEdited, StringComparer.Ordinal)
        && FilesEdited.SequenceEqual(other.FilesEdited, StringComparer.Ordinal)
        && Searches.SequenceEqual(other.Searches, StringComparer.Ordinal)
        && RefusedMoves.SequenceEqual(other.RefusedMoves, StringComparer.Ordinal)
        && Errors.SequenceEqual(other.Errors);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(LoopId, StringComparer.Ordinal);
        hash.Add(StopReason, StringComparer.Ordinal);
        hash.Add(Attempts);

        foreach (var value in FilesReadNotEdited.Concat(FilesEdited).Concat(Searches).Concat(RefusedMoves))
        {
            hash.Add(value, StringComparer.Ordinal);
        }

        foreach (var error in Errors)
        {
            hash.Add(error);
        }

        return hash.ToHashCode();
    }

    /// <summary>The diagnosis, or null when there is nothing wrong.</summary>
    public static string? Validate(LoopDigest digest)
    {
        ArgumentNullException.ThrowIfNull(digest);

        if (string.IsNullOrWhiteSpace(digest.LoopId))
        {
            return "A loop digest names the loop it summarises.";
        }

        if (!LoopOutcomes.All.Contains(digest.StopReason, StringComparer.Ordinal))
        {
            return $"Unknown loop outcome '{digest.StopReason}'. Expected one of: "
                 + string.Join(", ", LoopOutcomes.All) + ".";
        }

        foreach (var path in digest.FilesReadNotEdited.Concat(digest.FilesEdited))
        {
            if (path.StartsWith('/') || path.Contains(':'))
            {
                // An absolute path is a machine detail crossing a boundary, and
                // a digest carrying one is not comparable with a digest from
                // another machine - which ends the cross-flight comparison this
                // fact exists for.
                return $"'{path}' is not relative to the tree. A digest carries paths a person can "
                     + "compare across flights, not paths on one machine.";
            }
        }

        return digest.Attempts < 0
            ? "A loop digest carries the turns it took, and this one is negative."
            : null;
    }
}
