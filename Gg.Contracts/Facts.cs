namespace Gg.Contracts;

/// <summary>
/// Marks a type as the payload of one fact kind.
/// </summary>
/// <remarks>
/// The anchor the build-time manifest guard walks. A fact has to be registered
/// four ways to cross the boundary - pinned id, entry in
/// <see cref="FactKinds"/>, declared JSON members, and a slot on
/// <see cref="FactEnvelope"/> to arrive in - and three out of four is a fact
/// that serializes to a digest and nothing.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class FactKindAttribute(string kind) : Attribute
{
    public string Kind { get; } = kind;
}

/// <summary>
/// The pinned fact vocabulary.
/// </summary>
/// <remarks>
/// A fact this list does not contain is rejected loudly rather than
/// accepted-and-ignored. Silently absent is indistinguishable from satisfied,
/// which is this system's most dangerous failure mode: governance that reports
/// success while enforcing nothing.
/// </remarks>
public static class FactKinds
{
    /// <summary>What ran, and where. The first real fact.</summary>
    public const string EnvironmentIdentity = "environment.identity";

    /// <summary>Which commit was examined, and whether it came from a fork.</summary>
    /// <remarks>
    /// Deliberately NOT <c>change.manifest</c>, which is step 7: there is no
    /// file list here and no diff. It exists because materializing a fork's
    /// head is only trustworthy if the fact set says that is what happened -
    /// a run that examined a fork and recorded the base is a false fact, which
    /// this design treats as unrecoverable.
    /// </remarks>
    public const string SourceProvenance = "source.provenance";

    /// <summary>Every kind that validates.</summary>
    public static IReadOnlyList<string> All { get; } = [EnvironmentIdentity, SourceProvenance];
}

/// <summary>How much evidence one fact may be.</summary>
/// <remarks>
/// Declared once and read by both sides. gg refuses an over-budget item where
/// it was produced, and ingress refuses it again - and neither truncates,
/// because a fact cut in half is a false fact rather than a small one.
/// </remarks>
public static class FactBudget
{
    /// <summary>The digest budget, per item.</summary>
    public const int MaxItemBytes = 16 * 1024;
}

/// <summary>Whether this environment was made for this flight or found.</summary>
/// <remarks>
/// A field, not a feature. Warm pools are years away; recording which of the
/// two happened costs nothing now and is unrecoverable later.
/// </remarks>
public static class EnvironmentProvenance
{
    public const string Fresh = "fresh";

    public const string Reused = "reused";

    public static IReadOnlyList<string> All { get; } = [Fresh, Reused];
}

/// <summary>One dependency lock file, as a path and a hash.</summary>
/// <remarks>
/// Never the file. The question a lock hash answers is whether two runs
/// resolved the same dependencies, and a hash answers it without carrying a
/// customer's dependency graph off their machine.
/// </remarks>
[PinnedId("3f8a1c05-6d92-4b47-8e13-9c5b0a7d2e64")]
public sealed record LockHash
{
    /// <summary>Relative to the materialized tree.</summary>
    public required string Path { get; init; }

    public required string Sha256 { get; init; }
}

/// <summary>One tool the runner used, and which version of it.</summary>
[PinnedId("c62e4b18-05a7-4d3f-91b6-8e2a7c0d5f39")]
public sealed record ToolVersion
{
    public required string Name { get; init; }

    public required string Version { get; init; }
}

/// <summary>
/// What ran, and where.
/// </summary>
/// <remarks>
/// <para>
/// <b>Tests passed is not a fact without the environment they passed in.</b> A
/// laptop is the least reproducible environment in the fleet, which makes this
/// more important locally rather than less - and it is the reason this belongs
/// in slice one despite warm pools being years away.
/// </para>
/// <para>
/// Paths, counts and hashes. There is no member here that could carry a file.
/// </para>
/// </remarks>
[PinnedId("9d1b7e34-2a80-4c56-b7f9-06e3a8d4c150")]
[FactKind(FactKinds.EnvironmentIdentity)]
public sealed record EnvironmentIdentity
{
    /// <summary>
    /// A hash of the stable facts about this machine.
    /// </summary>
    /// <remarks>
    /// Of the ENVIRONMENT rather than of the machine: operating system,
    /// architecture, processor count, and the image when there is one. The
    /// runner's label already identifies the box, and hashing a hostname would
    /// carry an identifier nobody needs into a fact about reproducibility.
    /// </remarks>
    public required string HostFingerprint { get; init; }

    /// <summary>
    /// The container image, when there is one.
    /// </summary>
    /// <remarks>
    /// Null means "not running in an image", which is a different fact from
    /// "running in an image nobody recorded" - and only one of them is true on
    /// a laptop.
    /// </remarks>
    public string? ImageDigest { get; init; }

    /// <summary>Dependency lock files found in the tree, as paths and hashes.</summary>
    public required IReadOnlyList<LockHash> Locks { get; init; }

    /// <summary>The tools that did the work, and their versions.</summary>
    public required IReadOnlyList<ToolVersion> Tools { get; init; }

    /// <summary>One of <see cref="EnvironmentProvenance"/>.</summary>
    public required string Provenance { get; init; }
}

/// <summary>
/// Which commit was examined, and where it came from.
/// </summary>
/// <remarks>
/// <para>
/// The fact that makes fork handling trustworthy. A pull request's head is
/// fetched from the BASE repository via <c>refs/pull/&lt;n&gt;/head</c>, which
/// works identically for forks and branches and needs no credential for the
/// fork - and the only way to know that happened correctly is for the fact set
/// to say which commit it actually got, and whose it was.
/// </para>
/// <para>
/// A naive clone of a fork either fails or succeeds against the base and
/// produces a manifest describing the wrong commit. The second is worse, and
/// this record is what makes it detectable.
/// </para>
/// </remarks>
[PinnedId("5e0c9a26-4718-4f83-a2d5-6b91e7c0348f")]
[FactKind(FactKinds.SourceProvenance)]
public sealed record SourceProvenance
{
    public required string Provider { get; init; }

    public required string Slug { get; init; }

    /// <summary>The ref the flight was pinned to.</summary>
    public required string RequestedRef { get; init; }

    /// <summary>What the adapter turned it into. Different for a pull request.</summary>
    public required string ResolvedRef { get; init; }

    /// <summary>The commit that was actually put on disk.</summary>
    public required string HeadCommit { get; init; }

    /// <summary>Whether the head belongs to a fork rather than to the base.</summary>
    public required bool HeadIsFork { get; init; }

    /// <summary>Whose fork, when it is one. Provenance a <c>when:</c> condition will want.</summary>
    public string? ForkSlug { get; init; }

    /// <summary>How many files the tree held. A count, never a list.</summary>
    public required int FileCount { get; init; }

    /// <summary>How much disk it took. The first resource we consume in somebody else's environment.</summary>
    public required long Bytes { get; init; }
}

/// <summary>
/// One fact, on its way to the control plane.
/// </summary>
/// <remarks>
/// <para>
/// One populated payload, chosen by <see cref="Kind"/> - the same shape
/// <see cref="FlightIntent"/> uses, for the same reason. A kind and a payload
/// that disagree is a document whose meaning depends on which reader saw it
/// first, so validation refuses it.
/// </para>
/// <para>
/// <see cref="Digest"/> is computed BEFORE the filter runs. Computing it after
/// would make every later analysis derive from already-redacted material, and
/// draw conclusions about a document nobody produced.
/// </para>
/// </remarks>
[PinnedId("a47d2f60-8b15-4e93-97c2-0d6a3e8b51c7")]
public sealed record FactEnvelope
{
    /// <summary>
    /// What makes a replay a duplicate rather than a second fact.
    /// </summary>
    /// <remarks>
    /// The runner mints it and the control plane dedupes on it. A retry after a
    /// timeout is the ordinary case, and without this it would append the same
    /// fact twice and make an evidence budget wrong.
    /// </remarks>
    public required string IdempotencyKey { get; init; }

    /// <summary>One of <see cref="FactKinds"/>.</summary>
    public required string Kind { get; init; }

    /// <summary>SHA-256 of the payload, lowercase hex. Computed before the filter.</summary>
    public required string Digest { get; init; }

    public required DateTimeOffset ObservedAt { get; init; }

    /// <summary>Populated when <see cref="Kind"/> is <see cref="FactKinds.EnvironmentIdentity"/>.</summary>
    public EnvironmentIdentity? Environment { get; init; }

    /// <summary>Populated when <see cref="Kind"/> is <see cref="FactKinds.SourceProvenance"/>.</summary>
    public SourceProvenance? Source { get; init; }

    /// <summary>The diagnosis, or null when there is nothing wrong.</summary>
    /// <remarks>
    /// A sentence rather than a bool, for the same reason every other Validate
    /// here returns one: Article XI asks for a diagnosis, and "invalid fact"
    /// sends whoever hit it to read their own code.
    /// </remarks>
    public static string? Validate(FactEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        if (string.IsNullOrWhiteSpace(envelope.IdempotencyKey))
        {
            return "A fact carries an idempotency key. Without one a retry appends the same fact twice.";
        }

        if (!FactKinds.All.Contains(envelope.Kind))
        {
            return $"Unknown fact kind '{envelope.Kind}'. Expected one of: "
                 + string.Join(", ", FactKinds.All) + ".";
        }

        if (!IsSha256(envelope.Digest))
        {
            return $"'{envelope.Digest}' is not a sha256. A digest that cannot be compared is a "
                 + "budget nobody can enforce.";
        }

        // Exactly one payload, and it is the one the kind named. Counting
        // rather than checking the named field alone, so a second payload
        // travelling alongside is refused too.
        var carried = new (string Kind, bool Present)[]
        {
            (FactKinds.EnvironmentIdentity, envelope.Environment is not null),
            (FactKinds.SourceProvenance, envelope.Source is not null),
        };

        var present = carried.Where(c => c.Present).ToList();
        if (present.Count != 1)
        {
            return $"A fact carries exactly one payload; this one carries {present.Count}.";
        }

        if (present[0].Kind != envelope.Kind)
        {
            return $"This fact says it is '{envelope.Kind}' and carries a '{present[0].Kind}' payload.";
        }

        if (envelope.Environment is { } environment
            && !EnvironmentProvenance.All.Contains(environment.Provenance))
        {
            return $"Unknown environment provenance '{environment.Provenance}'. Expected one of: "
                 + string.Join(", ", EnvironmentProvenance.All) + ".";
        }

        return null;
    }

    private static bool IsSha256(string? value) =>
        value is { Length: 64 } && value.All(c => char.IsAsciiDigit(c) || (c >= 'a' && c <= 'f'));
}

/// <summary>
/// A batch of facts from the runner holding this lease.
/// </summary>
/// <remarks>
/// Against a LEASE rather than a flight: the lease is the authorisation, and
/// the generation is the fence. A runner that lost its flight must not still be
/// able to assert facts about it.
/// </remarks>
[PinnedId("7b3f18d5-0c64-4a29-85e7-3d90a6b2f4e8")]
public sealed record FactBatch
{
    /// <summary>The generation the runner believes it holds.</summary>
    public required int Generation { get; init; }

    public required IReadOnlyList<FactEnvelope> Facts { get; init; }
}

/// <summary>One fact the control plane would not take, and why.</summary>
/// <remarks>
/// Named individually rather than counted. "3 of 5 rejected" sends somebody
/// looking; naming the key and the reason ends the search.
/// </remarks>
[PinnedId("e18c5074-9a3b-4d62-b0f5-72c4e6a91d38")]
public sealed record FactRejection
{
    public required string IdempotencyKey { get; init; }

    public required string Reason { get; init; }
}

/// <summary>
/// What the control plane did with a batch.
/// </summary>
/// <remarks>
/// Three numbers rather than one, because they mean different things to a
/// runner. Duplicates are the expected result of a retry and not a problem;
/// rejections are, and they are named.
/// </remarks>
[PinnedId("41a9e7b2-38c0-4f65-9d1a-5e70b8c34962")]
public sealed record FactBatchAccepted
{
    public required int Accepted { get; init; }

    /// <summary>Already recorded under the same idempotency key. A replay changed nothing.</summary>
    public required int Duplicates { get; init; }

    public required IReadOnlyList<FactRejection> Rejected { get; init; }
}
