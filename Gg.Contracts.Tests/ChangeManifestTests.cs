using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// <c>change.manifest</c>: what changed, at whichever resolution fits.
/// </summary>
/// <remarks>
/// <para>
/// The first fact that is ABOUT files, so the first where "paths and counts
/// cross, content does not" is a line rather than a slogan.
/// </para>
/// <para>
/// <b>Degrade resolution. Never degrade completeness, and never silently.</b>
/// A manifest too large for the budget becomes a per-directory rollup - a true
/// statement at lower resolution - rather than a truncated file list, which is
/// a false statement. And a manifest that withheld paths says how many, for the
/// same reason.
/// </para>
/// </remarks>
public class ChangeManifestTests
{
    private static ChangedPath APath(string path, string change = ChangeKinds.Modified) => new()
    {
        Path = path,
        Change = change,
        LinesAdded = 3,
        LinesRemoved = 1,
        Classification = Classifications.Internal,
    };

    private static ChangeManifest AManifest(params ChangedPath[] paths) => new()
    {
        BaseCommit = new string('a', 40),
        HeadCommit = new string('b', 40),
        Resolution = ChangeResolution.Files,
        Paths = paths,
        Directories = [],
        Languages = [new LanguageChange { Language = "csharp", Files = paths.Length, LinesAdded = 3, LinesRemoved = 1 }],
        FilesChanged = paths.Length,
        LinesAdded = 3 * paths.Length,
        LinesRemoved = paths.Length,
        PathsWithheld = 0,
    };

    [Test]
    public async Task A_file_resolution_manifest_validates()
    {
        await Assert.That(ChangeManifest.Validate(AManifest(APath("src/Program.cs")))).IsNull();
    }

    [Test]
    public async Task The_paths_it_carries_and_the_paths_it_withheld_account_for_every_file()
    {
        // The invariant that makes "never silently" checkable. A manifest whose
        // list is shorter than its own count, with nothing saying why, is a
        // false statement at full resolution - which is exactly the thing a
        // truncation would have been.
        var withheld = AManifest(APath("src/Program.cs")) with { FilesChanged = 4, PathsWithheld = 3 };
        await Assert.That(ChangeManifest.Validate(withheld)).IsNull();

        var unaccounted = AManifest(APath("src/Program.cs")) with { FilesChanged = 4, PathsWithheld = 0 };
        await Assert.That(ChangeManifest.Validate(unaccounted)).IsNotNull()
            .Because("three files vanished and the manifest says nothing about them.");
    }

    [Test]
    public async Task A_rollup_carries_directories_and_says_it_is_one()
    {
        // A consumer must never have to guess whether it is looking at
        // everything.
        var rollup = new ChangeManifest
        {
            BaseCommit = new string('a', 40),
            HeadCommit = new string('b', 40),
            Resolution = ChangeResolution.Directories,
            Paths = [],
            Directories = [new DirectoryChange { Directory = "src", Files = 900, LinesAdded = 12, LinesRemoved = 4 }],
            Languages = [],
            FilesChanged = 900,
            LinesAdded = 12,
            LinesRemoved = 4,
            PathsWithheld = 0,
        };

        await Assert.That(ChangeManifest.Validate(rollup)).IsNull();
        await Assert.That(rollup.Resolution).IsEqualTo(ChangeResolution.Directories);
        await Assert.That(rollup.FilesChanged).IsEqualTo(900)
            .Because("the rollup states what it summarises, or it is a smaller lie than a truncation "
                   + "rather than a truthful smaller thing.");
    }

    [Test]
    public async Task A_manifest_carrying_both_lists_is_refused()
    {
        // One resolution, and it says which. Two populated lists is a document
        // whose meaning depends on which reader looked first.
        var confused = AManifest(APath("src/Program.cs")) with
        {
            Directories = [new DirectoryChange { Directory = "src", Files = 1, LinesAdded = 3, LinesRemoved = 1 }],
        };

        await Assert.That(ChangeManifest.Validate(confused)).IsNotNull();
    }

    [Test]
    public async Task A_manifest_whose_list_does_not_match_its_resolution_is_refused()
    {
        await Assert.That(ChangeManifest.Validate(
            AManifest(APath("a.cs")) with { Resolution = ChangeResolution.Directories })).IsNotNull();
    }

    [Test]
    public async Task A_resolution_nobody_declared_is_refused()
    {
        await Assert.That(ChangeManifest.Validate(
            AManifest(APath("a.cs")) with { Resolution = "summary" })).IsNotNull();
    }

    [Test]
    public async Task A_change_kind_nobody_declared_is_refused()
    {
        // Article XI. An unrecognised kind mapped to "modified" would record a
        // deletion as an edit.
        await Assert.That(ChangeManifest.Validate(AManifest(APath("a.cs", "renamed-ish")))).IsNotNull();
        foreach (var kind in ChangeKinds.All)
        {
            await Assert.That(ChangeManifest.Validate(AManifest(APath("a.cs", kind)))).IsNull();
        }
    }

    [Test]
    public async Task A_path_classified_at_a_level_nobody_declared_is_refused()
    {
        await Assert.That(ChangeManifest.Validate(
            AManifest(APath("a.cs") with { Classification = "spicy" }))).IsNotNull();
    }

    [Test]
    public async Task No_member_of_the_manifest_could_carry_a_line_of_a_file()
    {
        // The cheap build-time half of the claim. The control plane's own scan
        // is the real proof; a member named for content is one that will hold
        // it, and this is the fact where that stops being hypothetical.
        string[] contentWords = ["content", "body", "blob", "text", "diff", "patch", "data", "payload", "snippet"];

        var offenders = (Type[])
            [typeof(ChangeManifest), typeof(ChangedPath), typeof(DirectoryChange), typeof(LanguageChange)];

        var found = offenders
            .SelectMany(t => t.GetProperties().Select(p => (Type: t, Property: p)))
            .Where(m => contentWords.Any(w => m.Property.Name.Contains(w, StringComparison.OrdinalIgnoreCase)))
            .Select(m => $"{m.Type.Name}.{m.Property.Name}")
            .ToList();

        await Assert.That(found).IsEmpty()
            .Because("Found: " + string.Join(", ", found));
    }

    [Test]
    public async Task The_manifest_is_a_registered_fact_with_a_slot_to_arrive_in()
    {
        await Assert.That(FactKinds.All).Contains(FactKinds.ChangeManifest);
        await Assert.That(FactManifest.Unregistered([typeof(ChangeManifest)])).IsEmpty();

        var envelope = new FactEnvelope
        {
            IdempotencyKey = "k",
            Kind = FactKinds.ChangeManifest,
            Digest = new string('c', 64),
            ObservedAt = DateTimeOffset.UnixEpoch,
            Change = AManifest(APath("src/Program.cs")),
        };

        await Assert.That(FactEnvelope.Validate(envelope)).IsNull();
    }

    [Test]
    public async Task An_envelope_naming_the_manifest_and_carrying_something_else_is_refused()
    {
        var confused = new FactEnvelope
        {
            IdempotencyKey = "k",
            Kind = FactKinds.ChangeManifest,
            Digest = new string('c', 64),
            ObservedAt = DateTimeOffset.UnixEpoch,
            Source = new SourceProvenance
            {
                Provider = "local", Slug = "acme/widgets",
                RequestedRef = "refs/heads/main", ResolvedRef = "refs/heads/main",
                HeadCommit = new string('d', 40), HeadIsFork = false, ForkSlug = null,
                FileCount = 1, Bytes = 1,
            },
        };

        await Assert.That(FactEnvelope.Validate(confused)).IsNotNull();
    }

    [Test]
    public async Task The_declared_json_members_carry_no_content_either()
    {
        // The wire spelling, not the C# name: a [JsonPropertyName] could add a
        // member the shape assertion above never sees.
        await Assert.That(ProtocolSurface.JsonMembers[typeof(ChangedPath)])
            .IsEquivalentTo((string[])["path", "change", "linesAdded", "linesRemoved", "classification"]);
    }
}
