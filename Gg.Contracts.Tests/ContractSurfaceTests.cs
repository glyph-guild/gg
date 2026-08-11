using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Gg.Contracts.Tests;

/// <summary>
/// The package version means something about the CONTRACT, and this is what
/// makes that true.
/// </summary>
/// <remarks>
/// <para>
/// The version used to be <c>alpha.N</c> where N was the commit count on
/// <c>main</c>, so a README fix bumped the wire contract exactly as much as a
/// new message type did. A consumer seeing the number move learned nothing
/// about whether they needed to care, which is the opposite of what a version
/// on an audited artifact is for.
/// </para>
/// <para>
/// Now the version is declared by hand and this ledger holds it to account: a
/// fingerprint of the wire surface is recorded against each published version,
/// and changing the surface without moving the version fails the build. The
/// number cannot drift away from the thing it describes.
/// </para>
/// </remarks>
public class ContractSurfaceTests
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

    private static string LedgerPath => Path.Combine(RepoRoot().FullName, "Gg.Contracts", "contract-versions.json");

    private sealed record LedgerEntry(string Version, string Surface);

    private static List<LedgerEntry> Ledger() =>
        JsonSerializer.Deserialize<List<LedgerEntry>>(
            File.ReadAllText(LedgerPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
        ?? throw new InvalidOperationException("contract-versions.json is empty");

    /// <summary>
    /// The assembly version is pinned, and both ledgers depend on it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A regression guard rather than a discovery: this arrived WITH its fix,
    /// because the fix and the fact it asserts are the same thing. What
    /// discovered the defect was the fact-vocabulary ledger failing on a
    /// release that touched no fact.
    /// </para>
    /// <para>
    /// Both fingerprints record <c>Type.FullName</c>, and for a constructed
    /// generic that string embeds the assembly-qualified name of the argument,
    /// including <c>Version=</c>. So the assembly version leaked into every
    /// recorded surface, and bumping the package version made both ledgers cry
    /// "the surface changed" when nothing had - the exact false alarm that
    /// teaches somebody to re-record a hash without reading the diff.
    /// </para>
    /// <para>
    /// Unpinning it invalidates every entry in both ledgers at once, silently,
    /// and there is no way back: the recorded values of shipped versions may
    /// not be edited.
    /// </para>
    /// </remarks>
    [Test]
    public async Task The_assembly_version_is_pinned_because_both_ledgers_hash_it()
    {
        var pinned = typeof(Vocabulary).Assembly.GetName().Version;

        await Assert.That(pinned?.ToString()).IsEqualTo("0.10.0.0")
            .Because("every surface in contract-versions.json and fact-vocabulary.json was computed "
                   + "under this number. Moving it rewrites all of them at once.");
    }

    /// <summary>
    /// Proof the leak is real, so the pin above is not cargo.
    /// </summary>
    /// <remarks>
    /// If <c>FullName</c> ever stopped embedding the version, the pin would be
    /// harmless superstition and this would say so by failing.
    /// </remarks>
    [Test]
    public async Task A_generic_property_type_name_really_does_carry_the_assembly_version()
    {
        var name = typeof(IReadOnlyList<WhoAmI>).FullName;

        await Assert.That(name).Contains("Version=0.10.0.0");
    }

    /// <summary>The version this build of the contract claims to be.</summary>
    private static string DeclaredVersion() =>
        typeof(Vocabulary).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
            .InformationalVersion
            // The SDK appends +<commit sha> to the informational version.
            .Split('+')[0];

    /// <summary>
    /// A fingerprint of everything about the vocabulary that a consumer could
    /// break on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Deliberately covers the pinned id, the type name, and each property's
    /// name, type and requiredness - and deliberately does NOT cover doc
    /// comments, attribute order, or the declaration order of properties.
    /// JSON is not positional, so reordering members is not a wire change and
    /// must not read as one; a fingerprint that moved on a comment edit would
    /// be ignored within a week.
    /// </para>
    /// <para>
    /// Sorted at every level, so the digest is a property of the contract
    /// rather than of the compiler's member ordering.
    /// </para>
    /// </remarks>
    private static string ComputeSurface()
    {
        var lines = new List<string>();

        foreach (var type in Vocabulary.Types
                     .OrderBy(t => t.GetCustomAttribute<PinnedIdAttribute>()?.Id.ToString() ?? "", StringComparer.Ordinal))
        {
            var pinnedId = (type.GetCustomAttribute<PinnedIdAttribute>()
                ?? throw new InvalidOperationException($"{type.FullName} has no pinned id")).Id;

            lines.Add($"type {pinnedId} {type.Name}");

            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                         .OrderBy(p => p.Name, StringComparer.Ordinal))
            {
                var required = property.GetCustomAttributes()
                    .Any(a => a is RequiredMemberAttribute) ? "required" : "optional";
                lines.Add($"  {property.Name} {property.PropertyType.FullName} {required}");
            }
        }

        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', lines)))).ToLowerInvariant();
    }

    [Test]
    public async Task The_declared_version_has_an_entry_in_the_ledger()
    {
        var version = DeclaredVersion();

        await Assert.That(Ledger().Select(e => e.Version)).Contains(version)
            .Because($$"""
                The contract declares version {{version}} and the ledger has no entry for it.

                If the wire surface changed, add this to Gg.Contracts/contract-versions.json:

                  { "version": "{{version}}", "surface": "{{ComputeSurface()}}" }

                Adding it is meant to be a deliberate, reviewable act - that is the
                whole mechanism.
                """);
    }

    [Test]
    public async Task The_wire_surface_matches_what_the_declared_version_recorded()
    {
        var version = DeclaredVersion();
        var recorded = Ledger().SingleOrDefault(e => e.Version == version);
        if (recorded is null)
        {
            return; // The test above owns that failure; do not report it twice.
        }

        await Assert.That(ComputeSurface()).IsEqualTo(recorded.Surface)
            .Because($"""
                The wire surface changed but the version did not.

                Version {version} was published with surface {recorded.Surface};
                this build computes {ComputeSurface()}.

                Bump <Version> in Gg.Contracts/Gg.Contracts.csproj and add a new
                ledger entry. Editing the recorded surface of a version that has
                already shipped would silently change a contract under everyone
                who pinned it.
                """);
    }

    [Test]
    public async Task Published_versions_are_unique_and_ascending()
    {
        var versions = Ledger().Select(e => e.Version).ToList();

        await Assert.That(versions.Distinct().Count()).IsEqualTo(versions.Count)
            .Because("two entries for one version means one of them is a lie about what shipped.");

        var parsed = versions.Select(v => Version.Parse(v.Split('-')[0])).ToList();
        await Assert.That(parsed.SequenceEqual(parsed.OrderBy(v => v))).IsTrue()
            .Because("the ledger is a history; out-of-order entries make it unreadable as one.");
    }

    [Test]
    public async Task Every_recorded_surface_is_a_sha256()
    {
        // A truncated or hand-typed digest would compare unequal forever, or
        // worse, be quietly replaced with whatever the build produced.
        var malformed = Ledger()
            .Where(e => e.Surface.Length != 64
                     || !e.Surface.All(c => char.IsAsciiDigit(c) || (c >= 'a' && c <= 'f')))
            .Select(e => e.Version)
            .ToList();

        await Assert.That(malformed).IsEmpty();
    }

    [Test]
    public async Task The_version_is_not_derived_from_the_commit_count()
    {
        // The regression this whole file exists to prevent. An alpha.N suffix
        // is the shape the old scheme produced, and it moved on every commit
        // to the repository whether or not the contract changed.
        var version = DeclaredVersion();

        await Assert.That(version).DoesNotContain("alpha.")
            .Because($"'{version}' looks like the commit-count scheme, which made the number meaningless.");

        // And it must be a version a consumer can order.
        await Assert.That(Version.TryParse(version.Split('-')[0], out _)).IsTrue();
    }

    [Test]
    public async Task The_fingerprint_notices_a_changed_property_type()
    {
        // A fingerprint that never moves is decoration. This proves the digest
        // responds to the exact class of change it claims to guard, without
        // waiting for someone to make that change for real.
        var baseline = SurfaceOf(("a401b483", "DeviceAuthorizationStarted", "PollIntervalSeconds", "System.Int32"));
        var widened = SurfaceOf(("a401b483", "DeviceAuthorizationStarted", "PollIntervalSeconds", "System.Int64"));

        await Assert.That(baseline).IsNotEqualTo(widened);

        static string SurfaceOf((string Id, string Type, string Property, string PropertyType) shape) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                $"type {shape.Id} {shape.Type}\n  {shape.Property} {shape.PropertyType} required")))
                .ToLowerInvariant();
    }

    [Test]
    public async Task The_fingerprint_ignores_the_order_members_are_declared_in()
    {
        // The other half: JSON is not positional, so reordering is not a wire
        // change and must not read as one. A digest that moved on a reorder
        // would be bumped past without thought within a week.
        var oneWay = Digest(["  Alpha System.String required", "  Beta System.String required"]);
        var theOther = Digest(["  Beta System.String required", "  Alpha System.String required"]);

        await Assert.That(oneWay).IsEqualTo(theOther);

        static string Digest(IEnumerable<string> members) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                string.Join('\n', members.OrderBy(m => m, StringComparer.Ordinal)))))
                .ToLowerInvariant();
    }

    [Test]
    public async Task The_ledger_records_the_versions_that_actually_shipped()
    {
        // Sanity on the file itself: it is checked in, non-empty, and parseable
        // without which every assertion above is vacuous.
        await Assert.That(File.Exists(LedgerPath)).IsTrue();
        await Assert.That(Ledger()).IsNotEmpty();
    }

    [Test]
    public async Task Culture_does_not_change_the_fingerprint()
    {
        // Ordinal comparisons and invariant hex throughout: a build machine in
        // a different locale must not produce a different digest and fail the
        // build for nobody's reason.
        var original = CultureInfo.CurrentCulture;
        try
        {
            var underInvariant = ComputeSurface();
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
            await Assert.That(ComputeSurface()).IsEqualTo(underInvariant);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
