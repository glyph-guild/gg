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

    /// <summary>What changed between the base and the head that was examined.</summary>
    public const string ChangeManifest = "change.manifest";

    /// <summary>Every kind that validates.</summary>
    public static IReadOnlyList<string> All { get; } =
        [EnvironmentIdentity, SourceProvenance, ChangeManifest];
}

/// <summary>
/// The version of the pinned fact vocabulary, declared once.
/// </summary>
/// <remarks>
/// <para>
/// It travels on every request as <c>GG-Fact-Vocabulary</c> and it answers one
/// question: whether the facts a runner can produce are the ones this control
/// plane evaluates obligations against. A runner evaluating against a
/// vocabulary the control plane has moved past gives a silently wrong answer,
/// which is why the header exists at all.
/// </para>
/// <para>
/// <b>Held to account by a ledger</b>, in <c>fact-vocabulary.json</c>: a
/// fingerprint of every registered fact type is recorded against each version,
/// and moving the surface without moving this number fails the build. It was
/// three hand-typed constants until step 7, and it stayed at 0.1.0 through the
/// addition of a whole fact type - which is what a number nothing holds to
/// account does.
/// </para>
/// <para>
/// Declared HERE rather than in each half, so the client, the runner and the
/// control plane read one value. Three copies of a number that must agree is
/// how one of them stops agreeing.
/// </para>
/// </remarks>
public static class FactVocabulary
{
    /// <summary>
    /// 0.4.0: environment.identity, source.provenance, change.manifest with
    /// a diff basis.
    /// </summary>
    /// <remarks>
    /// 0.2.0 is recorded in the ledger and was never emitted by a released
    /// binary - it is the version source.provenance should have carried, and
    /// recording it is how the omission stays visible rather than being
    /// renumbered away.
    ///
    /// 0.4.0 adds no fact type. It adds a required member to change.manifest,
    /// which is exactly as much of a vocabulary change: a control plane
    /// reading 0.3.0 manifests cannot tell which diff they described, and the
    /// version is how it knows to stop guessing.
    /// </remarks>
    public const string Version = "0.4.0";
}

/// <summary>How much evidence one fact may be.</summary>
/// <remarks>
/// <para>
/// Declared once and read by both sides. gg refuses an over-budget item where
/// it was produced, and ingress refuses it again - and neither truncates,
/// because a fact cut in half is a false fact rather than a small one.
/// </para>
/// <para>
/// <b>This budget is not the gate budget, and conflating them was a mistake.</b>
/// The gate numbers - 8 KiB an item, 32 KiB a page - were sized for WHAT A
/// PERSON READS while deciding something. A digest is machine-comparable
/// history: nobody reads a hundred of them, something diffs them.
/// </para>
/// <para>
/// Sized at 16 KiB, a manifest of about 150 bytes a file ran out at roughly a
/// hundred files, so an ordinary pull request nearly filled it and the
/// per-directory rollup became the common case. That quietly breaks the claim
/// the hardening rests on - "enough to compare thirty flights" - because thirty
/// rollups cannot be compared with each other at all.
/// </para>
/// </remarks>
public static class FactBudget
{
    /// <summary>
    /// The digest budget, per item. Derived from the measurement.
    /// </summary>
    /// <remarks>
    /// <c>ManifestSizeTests</c> measures about 150 bytes a changed file at
    /// realistic path lengths. A thousand files is a very large pull request
    /// and a real one - a dependency bump, a formatter run, a rename - so the
    /// budget is sized to hold one at full resolution and the rollup goes back
    /// to being the exceptional case it was described as.
    ///
    /// The number is the measurement rounded up to a power of two, not a
    /// number somebody liked: 1000 files x 150 bytes is 150 KiB, and 192 KiB
    /// leaves the same proportional headroom 16 KiB left for a hundred.
    /// </remarks>
    public const int MaxItemBytes = 192 * 1024;

    /// <summary>
    /// Roughly what one changed file costs in a change manifest.
    /// </summary>
    /// <remarks>
    /// Recorded so the budget above can be re-derived rather than
    /// re-guessed, and asserted against a real manifest by
    /// <c>ManifestSizeTests</c> - a constant nothing checks is a comment.
    /// </remarks>
    public const int ManifestBytesPerFile = 150;

    /// <summary>How many files the digest budget holds at full resolution.</summary>
    public static int ManifestFilesWithinBudget => MaxItemBytes / ManifestBytesPerFile;
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

/// <summary>What happened to one path.</summary>
public static class ChangeKinds
{
    public const string Added = "added";

    public const string Modified = "modified";

    public const string Deleted = "deleted";

    /// <summary>Every kind that validates.</summary>
    public static IReadOnlyList<string> All { get; } = [Added, Modified, Deleted];
}

/// <summary>At what resolution a manifest describes its change.</summary>
/// <remarks>
/// <b>Degrade resolution. Never degrade completeness, and never silently.</b>
/// A per-directory rollup is a true statement at lower resolution; a truncated
/// file list is a false statement. This field is how a consumer tells which it
/// is holding, and it must never have to guess.
/// </remarks>
public static class ChangeResolution
{
    /// <summary>Every changed path, named.</summary>
    public const string Files = "files";

    /// <summary>A per-directory rollup, because the file list would not fit.</summary>
    public const string Directories = "directories";

    public static IReadOnlyList<string> All { get; } = [Files, Directories];
}

/// <summary>
/// Which diff a change manifest measured.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two-point over-reports a pull request.</b> It is the difference between
/// two commits; a pull request's change is the difference between the head and
/// the point the branch left the base. They diverge by everything anybody else
/// merged in the meantime, and on a busy repository that is most of it.
/// </para>
/// <para>
/// Recorded rather than corrected, deliberately and for now. A manifest that
/// says which basis it used can be read correctly today and re-read correctly
/// after somebody computes a real merge base; a manifest that stayed silent
/// would have to be re-interpreted retroactively, and there is no way to know
/// which of the old ones were affected.
/// </para>
/// </remarks>
public static class DiffBasis
{
    /// <summary>Base commit to head commit. What the runner computes today.</summary>
    public const string TwoPoint = "two-point";

    /// <summary>Merge base to head. What a pull request's change actually is.</summary>
    public const string MergeBase = "merge-base";

    public static IReadOnlyList<string> All { get; } = [TwoPoint, MergeBase];
}

/// <summary>One changed path: what it is, what happened, how much, how sensitive.</summary>
/// <remarks>
/// A path and three numbers. Reading the file to count its lines happens on the
/// runner and the lines stay there - there is no member here a line could
/// travel in, which is asserted over this type's shape.
/// </remarks>
[PinnedId("8c47b1e0-3d95-4a26-b8f7-1e05c9d3a742")]
public sealed record ChangedPath
{
    public required string Path { get; init; }

    /// <summary>One of <see cref="ChangeKinds"/>.</summary>
    public required string Change { get; init; }

    public required int LinesAdded { get; init; }

    public required int LinesRemoved { get; init; }

    /// <summary>
    /// What the runner's rules made of this path.
    /// </summary>
    /// <remarks>
    /// Carried so a disagreement with the control plane's own answer is
    /// visible, which is a genuinely interesting signal. It is emphatically
    /// NOT what the control plane checks: a patched runner would label
    /// everything public, and re-validation that read this would pass on every
    /// item it was built to catch.
    /// </remarks>
    public required string Classification { get; init; }
}

/// <summary>One directory's worth of change, when the file list would not fit.</summary>
[PinnedId("2b96d4f8-51a3-4c70-9e18-7f0a6b2c85d3")]
public sealed record DirectoryChange
{
    public required string Directory { get; init; }

    public required int Files { get; init; }

    public required int LinesAdded { get; init; }

    public required int LinesRemoved { get; init; }
}

/// <summary>How much of the change was in one language.</summary>
[PinnedId("6e83a25c-0f71-4b94-8d36-c15b90e7a4f2")]
public sealed record LanguageChange
{
    public required string Language { get; init; }

    public required int Files { get; init; }

    public required int LinesAdded { get; init; }

    public required int LinesRemoved { get; init; }
}

/// <summary>
/// What changed between the base and the head that was examined.
/// </summary>
/// <remarks>
/// <para>
/// The first fact that is ABOUT files, and so the first where "paths and counts
/// cross, content does not" is a line of code rather than a slogan.
/// </para>
/// <para>
/// <b>The list and the withheld count account for every file.</b> A manifest
/// whose paths are fewer than its own total, with nothing saying why, is a
/// false statement at full resolution - exactly what a truncation would have
/// been. Validation refuses it, which is what makes "never silently" checkable.
/// </para>
/// </remarks>
[PinnedId("f519c7a3-4e60-4d82-91b5-3a7d0c6e28b4")]
[FactKind(FactKinds.ChangeManifest)]
public sealed record ChangeManifest
{
    /// <summary>The commit the change is measured from.</summary>
    public required string BaseCommit { get; init; }

    /// <summary>The commit that was actually examined.</summary>
    public required string HeadCommit { get; init; }

    /// <summary>One of <see cref="ChangeResolution"/>. Says which list is populated.</summary>
    public required string Resolution { get; init; }

    /// <summary>
    /// One of <see cref="DiffBasis"/>. Says which diff these numbers are.
    /// </summary>
    /// <remarks>
    /// Required, not defaulted. A default would let a producer that has never
    /// heard of this member emit manifests labelled with a basis it did not
    /// use, which is the failure this member exists to end.
    /// </remarks>
    public required string DiffBasis { get; init; }

    /// <summary>Populated at file resolution.</summary>
    public required IReadOnlyList<ChangedPath> Paths { get; init; }

    /// <summary>Populated at directory resolution.</summary>
    public required IReadOnlyList<DirectoryChange> Directories { get; init; }

    public required IReadOnlyList<LanguageChange> Languages { get; init; }

    /// <summary>How many files changed in total, whatever the resolution.</summary>
    /// <remarks>
    /// Always the truth about the change. At directory resolution this is what
    /// the rollup states it summarises; at file resolution it is the list
    /// length plus whatever was withheld.
    /// </remarks>
    public required int FilesChanged { get; init; }

    public required int LinesAdded { get; init; }

    public required int LinesRemoved { get; init; }

    /// <summary>
    /// How many paths the filter withheld for being above the ceiling.
    /// </summary>
    /// <remarks>
    /// Never silently. Without this a filtered manifest is indistinguishable
    /// from a smaller change, and a tenant reading it would draw a conclusion
    /// about a flight that examined more than it said.
    /// </remarks>
    public required int PathsWithheld { get; init; }

    /// <summary>The diagnosis, or null when there is nothing wrong.</summary>
    public static string? Validate(ChangeManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        if (!Gg.Contracts.DiffBasis.All.Contains(manifest.DiffBasis))
        {
            return $"Unknown diff basis '{manifest.DiffBasis}'. Expected one of: "
                 + string.Join(", ", Gg.Contracts.DiffBasis.All)
                 + ". A manifest that cannot say which diff it measured cannot be compared with one "
                 + "that can.";
        }

        if (!ChangeResolution.All.Contains(manifest.Resolution))
        {
            return $"Unknown change resolution '{manifest.Resolution}'. Expected one of: "
                 + string.Join(", ", ChangeResolution.All) + ".";
        }

        var files = manifest.Resolution == ChangeResolution.Files;

        if (files && manifest.Directories.Count > 0)
        {
            return "A manifest at file resolution carries no directory rollup. Two populated lists "
                 + "is a document whose meaning depends on which reader looked first.";
        }

        if (!files && manifest.Paths.Count > 0)
        {
            return "A manifest at directory resolution carries no path list, for the same reason.";
        }

        if (!files && manifest.Directories.Count == 0 && manifest.FilesChanged > 0)
        {
            return "A rollup that summarises nothing is not a rollup.";
        }

        if (files && manifest.Paths.Count + manifest.PathsWithheld != manifest.FilesChanged)
        {
            return $"This manifest counts {manifest.FilesChanged} changed file(s), carries "
                 + $"{manifest.Paths.Count} and admits withholding {manifest.PathsWithheld}. A list "
                 + "shorter than its own total with nothing accounting for the difference is a "
                 + "false statement at full resolution.";
        }

        foreach (var path in manifest.Paths)
        {
            if (!ChangeKinds.All.Contains(path.Change))
            {
                return $"'{path.Path}' says it was '{path.Change}'. Expected one of: "
                     + string.Join(", ", ChangeKinds.All) + ".";
            }

            if (Classifications.RankOf(path.Classification) is null)
            {
                return $"'{path.Path}' is classified '{path.Classification}', which is not a "
                     + "classification.";
            }
        }

        return null;
    }
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

    /// <summary>Populated when <see cref="Kind"/> is <see cref="FactKinds.ChangeManifest"/>.</summary>
    public ChangeManifest? Change { get; init; }

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
            (FactKinds.ChangeManifest, envelope.Change is not null),
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

        return envelope.Change is { } change ? ChangeManifest.Validate(change) : null;
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
