using System.Text.Json;
using Gg.Contracts;
using Gg.Runner.Vcs;

namespace Gg.Runner.Facts;

/// <summary>
/// What changed between the base and the head, from git's own answer.
/// </summary>
/// <remarks>
/// <para>
/// Reading files to count lines happens HERE, on the runner's disk, and the
/// lines stay there. What comes out is paths, counts and hashes - and
/// <c>--numstat</c> is what produces it, so "how many lines changed" is git's
/// answer rather than a re-implementation of one.
/// </para>
/// <para>
/// <b>A two-point tree diff, not a merge-base diff.</b> The clone is shallow at
/// both refs, so there is no common ancestor on this disk to find one from.
/// That means a base which has moved on since the branch was cut shows up as
/// change - debt with a trigger, and the trigger is the first person who asks
/// why a manifest lists a file they did not touch. Fixing it needs history,
/// which needs a deeper fetch, which is a disk decision rather than a diff one.
/// </para>
/// </remarks>
public static class ChangeExtractor
{
    /// <summary>
    /// The manifest, or null when there is no base to measure from.
    /// </summary>
    /// <remarks>
    /// Null rather than a manifest against a guessed default branch. Article
    /// XI: a plausible manifest of the wrong change is a false fact, and this
    /// design treats those as unrecoverable.
    /// </remarks>
    public static ChangeManifest? Extract(
        Materialized tree, IReadOnlyList<ClassificationRule> rules)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(rules);

        if (tree.BaseCommit is not { Length: > 0 } baseCommit)
        {
            return null;
        }

        var numstat = GitInvocation
            .Plain("diff", "--numstat", "--no-renames", "-z", baseCommit, tree.HeadCommit)
            .RunAsync(tree.Path).GetAwaiter().GetResult();

        var status = GitInvocation
            .Plain("diff", "--name-status", "--no-renames", "-z", baseCommit, tree.HeadCommit)
            .RunAsync(tree.Path).GetAwaiter().GetResult();

        var kinds = ParseStatus(status);
        var paths = new List<ChangedPath>();

        foreach (var (path, added, removed) in ParseNumstat(numstat))
        {
            paths.Add(new ChangedPath
            {
                Path = path,
                Change = kinds.TryGetValue(path, out var kind) ? kind : ChangeKinds.Modified,
                LinesAdded = added,
                LinesRemoved = removed,
                Classification = ClassificationRules.Classify(path, rules),
            });
        }

        var manifest = AtFileResolution(baseCommit, tree.HeadCommit, tree.Basis, paths);

        // Degrade resolution rather than completeness. A per-directory rollup
        // is a true statement at lower resolution; a truncated file list is a
        // false one, and ingress already draws exactly that distinction about
        // facts cut in half.
        return Fits(manifest) ? manifest : AtDirectoryResolution(manifest);
    }

    private static ChangeManifest AtFileResolution(
        string baseCommit, string headCommit, string basis, IReadOnlyList<ChangedPath> paths) =>
        new()
        {
            BaseCommit = baseCommit,
            HeadCommit = headCommit,
            Resolution = ChangeResolution.Files,
            // What this actually is, said out loud. The runner diffs two
            // commits; a pull request's change is head against the point the
            // branch left the base, and the two differ by everything anybody
            // else merged since. Labelling it is not fixing it - it is making
            // the gap legible to whoever reads the numbers, and making the day
            // somebody computes a real merge base a label change rather than a
            // silent reinterpretation of every fact already recorded.
            // CARRIED, not chosen here. Whoever decided which commit to measure from
            // decided what kind of diff this is, and computing it twice is how a label
            // ends up describing a base it does not have.
            DiffBasis = basis,
            Paths = paths,
            Directories = [],
            Languages = Languages(paths),
            FilesChanged = paths.Count,
            LinesAdded = paths.Sum(p => p.LinesAdded),
            LinesRemoved = paths.Sum(p => p.LinesRemoved),
            PathsWithheld = 0,
        };

    /// <summary>
    /// The same change, summarised by directory.
    /// </summary>
    /// <remarks>
    /// The TOTALS are carried through unchanged. A rollup whose numbers differ
    /// from the file list's is a different statement rather than a coarser one,
    /// and the whole justification for rolling up is that it stays true.
    /// </remarks>
    private static ChangeManifest AtDirectoryResolution(ChangeManifest files)
    {
        var directories = files.Paths
            .GroupBy(p => DirectoryOf(p.Path), StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new DirectoryChange
            {
                Directory = g.Key,
                Files = g.Count(),
                LinesAdded = g.Sum(p => p.LinesAdded),
                LinesRemoved = g.Sum(p => p.LinesRemoved),
            })
            .ToList();

        return files with
        {
            Resolution = ChangeResolution.Directories,
            Paths = [],
            Directories = directories,
        };
    }

    /// <summary>Whether this manifest would survive the digest budget.</summary>
    /// <remarks>
    /// Measured over the envelope the pipeline will build, because that is what
    /// ingress will measure. Two different answers to "how big is this" is how
    /// a runner ships something it believed would fit.
    /// </remarks>
    private static bool Fits(ChangeManifest manifest) =>
        !FactPipeline.OverBudget(new FactEnvelope
        {
            IdempotencyKey = new string('k', 80),
            Kind = FactKinds.ChangeManifest,
            Digest = new string('0', 64),
            ObservedAt = DateTimeOffset.UnixEpoch,
            Change = manifest,
        });

    /// <summary>The immediate directory, or <c>(root)</c> for a file at the top.</summary>
    private static string DirectoryOf(string path)
    {
        var slash = path.LastIndexOf('/');
        return slash < 0 ? "(root)" : path[..slash];
    }

    /// <summary>
    /// What language a file is, by extension.
    /// </summary>
    /// <remarks>
    /// By extension and nothing cleverer. Sniffing content to decide would
    /// mean reading files for a reason the manifest does not need, and the
    /// answer would still be wrong for the files where it matters.
    /// </remarks>
    private static IReadOnlyList<LanguageChange> Languages(IReadOnlyList<ChangedPath> paths) =>
        [.. paths
            .GroupBy(p => LanguageOf(p.Path), StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new LanguageChange
            {
                Language = g.Key,
                Files = g.Count(),
                LinesAdded = g.Sum(p => p.LinesAdded),
                LinesRemoved = g.Sum(p => p.LinesRemoved),
            })];

    private static string LanguageOf(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".cs" => "csharp",
            ".fs" => "fsharp",
            ".ts" or ".tsx" => "typescript",
            ".js" or ".jsx" or ".mjs" or ".cjs" => "javascript",
            ".py" => "python",
            ".go" => "go",
            ".rs" => "rust",
            ".rb" => "ruby",
            ".java" => "java",
            ".kt" or ".kts" => "kotlin",
            ".swift" => "swift",
            ".sql" => "sql",
            ".sh" or ".bash" => "shell",
            ".md" => "markdown",
            ".json" or ".yaml" or ".yml" or ".toml" or ".xml" => "config",
            // Said out loud rather than dropped. A file the breakdown ignored
            // would make the per-language counts disagree with the total, and
            // somebody would spend an afternoon on the difference.
            _ => "other",
        };

    /// <summary>
    /// <c>--numstat -z</c>: added, removed, path, NUL-separated.
    /// </summary>
    /// <remarks>
    /// NUL-separated because a path may contain anything a filesystem allows,
    /// including a newline. Parsing the human format would work until the first
    /// customer who has one.
    /// </remarks>
    private static IEnumerable<(string Path, int Added, int Removed)> ParseNumstat(string output)
    {
        foreach (var record in output.Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = record.Split('\t');
            if (parts.Length < 3)
            {
                continue;
            }

            // "-" where git could not count: a binary file. Zero is the honest
            // answer for a line count that does not exist.
            var added = int.TryParse(parts[0], out var a) ? a : 0;
            var removed = int.TryParse(parts[1], out var r) ? r : 0;

            yield return (parts[2], added, removed);
        }
    }

    /// <summary><c>--name-status -z</c>: a status letter and a path, alternating.</summary>
    private static Dictionary<string, string> ParseStatus(string output)
    {
        var kinds = new Dictionary<string, string>(StringComparer.Ordinal);
        var fields = output.Split('\0', StringSplitOptions.RemoveEmptyEntries);

        for (var i = 0; i + 1 < fields.Length; i += 2)
        {
            kinds[fields[i + 1]] = fields[i] switch
            {
                "A" => ChangeKinds.Added,
                "D" => ChangeKinds.Deleted,
                _ => ChangeKinds.Modified,
            };
        }

        return kinds;
    }
}
