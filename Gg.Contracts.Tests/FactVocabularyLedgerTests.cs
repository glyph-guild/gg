using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// <c>GG-Fact-Vocabulary</c> means something about the fact vocabulary, and
/// this is what makes that true.
/// </summary>
/// <remarks>
/// <para>
/// It did not, last step. <c>source.provenance</c> was added and the version
/// stayed <c>0.1.0</c> - immediately after a note calling the first added fact
/// "the versioning mechanism doing its job on the first opportunity". The
/// mechanism was not doing its job because there was no mechanism: the number
/// was three hand-typed constants and nothing held them to anything.
/// </para>
/// <para>
/// So it gets the one the contract package already has. A fingerprint of the
/// registered fact types is recorded against each published version, and moving
/// the surface without moving the version fails the build. The next omission is
/// impossible rather than merely unlikely, which is the difference this file is
/// for.
/// </para>
/// <para>
/// Scoped to FACT types rather than the whole wire surface, deliberately. They
/// version separately because they answer different questions: the contract
/// version tells a consumer whether their client still compiles, and this one
/// tells a runner whether the facts it can produce are the ones this control
/// plane evaluates obligations against. A runner evaluating against a
/// vocabulary the control plane has moved past gives a silently wrong answer.
/// </para>
/// </remarks>
public class FactVocabularyLedgerTests
{
    private static DirectoryInfo RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Gg.sln")))
        {
            dir = dir.Parent;
        }
        return dir ?? throw new InvalidOperationException("Gg.sln not found above " + AppContext.BaseDirectory);
    }

    private static string LedgerPath =>
        Path.Combine(RepoRoot().FullName, "Gg.Contracts", "fact-vocabulary.json");

    private sealed record LedgerEntry(string Version, string Surface, string Kinds);

    private static List<LedgerEntry> Ledger() =>
        JsonSerializer.Deserialize<List<LedgerEntry>>(
            File.ReadAllText(LedgerPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
        ?? throw new InvalidOperationException("fact-vocabulary.json is empty");

    /// <summary>
    /// A fingerprint of every registered fact type: kind, pinned id, and shape.
    /// </summary>
    /// <remarks>
    /// The SHAPE matters as much as the list. A fact type that silently grew a
    /// member is a fact a runner can produce and an older control plane cannot
    /// read, which is the same failure as an unregistered kind wearing a
    /// familiar name.
    /// </remarks>
    internal static string Fingerprint(IEnumerable<Type> factTypes)
    {
        var lines = new List<string>();

        foreach (var type in factTypes.OrderBy(
                     t => t.GetCustomAttribute<FactKindAttribute>()!.Kind, StringComparer.Ordinal))
        {
            var kind = type.GetCustomAttribute<FactKindAttribute>()!.Kind;
            var pinned = type.GetCustomAttribute<PinnedIdAttribute>()!.Id;
            lines.Add($"fact {kind} {pinned}");

            // The same stable naming the wire surface uses. Both ledgers had
            // the same leak and both are fixed by the same function.
            lines.AddRange(SurfaceNaming.PropertyLines(type));
        }

        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', lines)))).ToLowerInvariant();
    }

    /// <summary>
    /// This build's fact vocabulary: every fact type's shape, AND the closed
    /// vocabularies whose values travel on them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The values are here and not in <see cref="Fingerprint"/>, deliberately.</b>
    /// That function reconstructs what a historical version would have recorded, and it
    /// is only defensible because those types' shapes have not changed since they
    /// shipped. Folding today's vocabularies into it would re-derive history a second
    /// time - which the comment on that reconstruction explicitly forbids.
    /// </para>
    /// <para>
    /// <b>Why values belong in the fingerprint at all.</b> Contracts says a member may be
    /// added freely and a value may not, because the only safe response to an unknown
    /// value is to halt. Until this line existed the rule had no mechanism: a third
    /// DiffBasis value moved neither ledger, so the guard that exists to force the
    /// conversation could not see the change that most needs one.
    /// </para>
    /// <para>
    /// <b>Over-inclusive on purpose.</b> A vocabulary that never travels on a fact moves
    /// this version too. A false alarm is the direction this project takes every time
    /// over a silent break.
    /// </para>
    /// </remarks>
    private static string Current()
    {
        var shape = Fingerprint(FactManifest.FactTypesIn(typeof(FactKinds).Assembly));

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            shape + '\n' + string.Join('\n', ClosedVocabularies.Lines(VocabularyFingerprints.Fact))))).ToLowerInvariant();
    }

    [Test]
    public async Task The_declared_vocabulary_version_has_an_entry_in_the_ledger()
    {
        await Assert.That(Ledger().Select(e => e.Version)).Contains(FactVocabulary.Version)
            .Because($$"""
                The fact vocabulary declares {{FactVocabulary.Version}} and the ledger has no entry.

                If a fact type was added or changed, add this to
                Gg.Contracts/fact-vocabulary.json:

                  { "version": "…", "surface": "{{Current()}}", "kinds": "…" }

                Adding it is meant to be a deliberate, reviewable act - that is the
                whole mechanism, and it is the one that was missing when
                source.provenance shipped under 0.1.0.
                """);
    }

    [Test]
    public async Task The_vocabulary_matches_what_the_declared_version_recorded()
    {
        var recorded = Ledger().SingleOrDefault(e => e.Version == FactVocabulary.Version);
        if (recorded is null)
        {
            return; // The test above owns that failure; do not report it twice.
        }

        await Assert.That(Current()).IsEqualTo(recorded.Surface)
            .Because($"""
                The fact vocabulary changed but GG-Fact-Vocabulary did not.

                Version {FactVocabulary.Version} was published with surface {recorded.Surface};
                this build computes {Current()}.

                Bump FactVocabulary.Version and add a new ledger entry. Editing the
                recorded surface of a version that already shipped would silently
                change the vocabulary under every runner pinned to it.
                """);
    }

    [Test]
    public async Task Every_ledger_entry_names_the_kinds_it_covers()
    {
        // The list is not what the fingerprint is OF - the shape is in there
        // too - but it is what makes the ledger readable as a history. An entry
        // nobody can interpret is one nobody checks.
        foreach (var entry in Ledger())
        {
            await Assert.That(entry.Kinds).IsNotEmpty()
                .Because($"{entry.Version} says nothing about what it contains.");
        }

        await Assert.That(Ledger().Single(e => e.Version == FactVocabulary.Version).Kinds)
            .IsEqualTo(string.Join(", ", FactKinds.All.OrderBy(k => k, StringComparer.Ordinal)));
    }

    [Test]
    public async Task Published_vocabularies_are_unique_and_ascending()
    {
        var versions = Ledger().Select(e => e.Version).ToList();

        await Assert.That(versions.Distinct().Count()).IsEqualTo(versions.Count)
            .Because("two entries for one version means one of them is a lie about what shipped.");

        var parsed = versions.Select(Version.Parse).ToList();
        await Assert.That(parsed.SequenceEqual(parsed.OrderBy(v => v))).IsTrue();
    }

    [Test]
    public async Task Every_recorded_surface_is_a_sha256()
    {
        var malformed = Ledger()
            .Where(e => e.Surface.Length != 64
                     || !e.Surface.All(c => char.IsAsciiDigit(c) || (c >= 'a' && c <= 'f')))
            .Select(e => e.Version)
            .ToList();

        await Assert.That(malformed).IsEmpty();
    }

    [Test]
    public async Task The_version_that_shipped_two_facts_is_recorded_as_the_one_that_should_have()
    {
        // The correction, written down rather than quietly renumbered.
        //
        // 0.1.0 was worn by two different vocabularies: no facts at all before
        // step 6, and environment.identity plus source.provenance after it. The
        // ledger records what each version MEANS, so 0.1.0 is the environment
        // fact alone and 0.2.0 is the one source.provenance should have carried.
        //
        // Those two fingerprints were reconstructed when this ledger was
        // introduced, which is only defensible because neither type's SHAPE has
        // changed since it shipped - the fingerprint over the subset today is
        // the fingerprint that version would have recorded. No future entry may
        // be reconstructed; once a version ships, its recorded value stands.
        //
        // RE-DERIVED ONCE, at the fingerprint normalisation. Both were always
        // reconstructions and neither was ever emitted by a released binary,
        // so re-deriving them under the new naming records the same claim in
        // the new spelling rather than editing history. 0.3.0 and 0.4.0 were
        // NOT touched: they shipped, so their recorded values stand and the
        // normalisation simply gets a new version of its own.
        var all = FactManifest.FactTypesIn(typeof(FactKinds).Assembly).ToList();
        var byKind = all.ToDictionary(t => t.GetCustomAttribute<FactKindAttribute>()!.Kind);

        await Assert.That(Ledger().Single(e => e.Version == "0.1.0").Surface)
            .IsEqualTo(Fingerprint([byKind[FactKinds.EnvironmentIdentity]]));

        await Assert.That(Ledger().Single(e => e.Version == "0.2.0").Surface)
            .IsEqualTo(Fingerprint([byKind[FactKinds.EnvironmentIdentity], byKind[FactKinds.SourceProvenance]]));
    }

    [Test]
    public async Task Both_halves_of_gg_read_one_version_rather_than_holding_their_own()
    {
        // The other half of the failure. The number was three hand-typed
        // constants - the client, the runner and the control plane - and three
        // copies of a number that must agree is how one of them stops agreeing.
        await Assert.That(Gg.Contracts.FactVocabulary.Version).IsNotEmpty();
        await Assert.That(Version.TryParse(FactVocabulary.Version, out _)).IsTrue();
    }

    // ---- the twins ----

    [Test]
    public async Task The_fingerprint_notices_a_fact_type_arriving()
    {
        // The exact omission this file exists for: a new fact type, and a
        // version that did not move.
        var all = FactManifest.FactTypesIn(typeof(FactKinds).Assembly).ToList();
        var withoutOne = all.Where(t =>
            t.GetCustomAttribute<FactKindAttribute>()!.Kind != FactKinds.SourceProvenance);

        await Assert.That(Fingerprint(all)).IsNotEqualTo(Fingerprint(withoutOne))
            .Because("if adding a fact does not move the fingerprint, the ledger cannot catch the "
                   + "thing it was built to catch.");
    }

    [Test]
    public async Task The_fingerprint_notices_a_fact_type_changing_shape()
    {
        // The subtler half. A fact that grew a member is one a runner can
        // produce and an older control plane cannot read, and the kind list
        // alone would say nothing had happened.
        var baseline = Digest(["fact a.b 0000", "  One System.String required"]);
        var widened = Digest(["fact a.b 0000", "  One System.String required", "  Two System.Int32 required"]);

        await Assert.That(baseline).IsNotEqualTo(widened);

        static string Digest(IEnumerable<string> lines) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', lines))))
                .ToLowerInvariant();
    }

    [Test]
    public async Task The_ledger_records_the_versions_that_actually_shipped()
    {
        await Assert.That(File.Exists(LedgerPath)).IsTrue();
        await Assert.That(Ledger()).IsNotEmpty();
        await Assert.That(Ledger().Count).IsGreaterThanOrEqualTo(3)
            .Because("0.1.0, the one source.provenance should have had, and this step's.");
    }
}
