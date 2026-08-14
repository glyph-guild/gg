using System.Text.Json;
using Gg.Contracts;
using Gg.Runner.Vcs;

namespace Gg.Runner.Facts;

/// <summary>
/// What changed between the base and the working tree, from git's own answer.
/// </summary>
/// <remarks>
/// <para>
/// Reading files to count lines happens HERE, on the runner's disk, and the
/// lines stay there. What comes out is paths, counts and hashes - and
/// <c>--numstat</c> is what produces it, so "how many lines changed" is git's
/// answer rather than a re-implementation of one.
/// </para>
/// <para>
/// <b>The head of this diff is the WORKING TREE, never a commit.</b> The runner's
/// order is materialize → invoke → extract → ship → land, and the commit happens
/// inside the destination adapter's push - so at this moment the agent's work is
/// uncommitted, and a diff between two commits describes a change nobody made. It
/// is the tree because the tree is where the work is, and it is measured from a
/// commit rather than from the tree's own HEAD because an agent that commits
/// half its work mid-loop has not thereby removed it from what is being proposed.
/// </para>
/// <para>
/// <b>A two-point diff, not a merge-base diff.</b> The clone is <c>--depth 1</c>,
/// so there is no common ancestor on this disk to find one from. The base is the
/// commit this flight checked out; a flight pinned to a branch already ahead of
/// its destination's base would need a merge base, and that has to be supplied
/// rather than computed here.
/// </para>
/// </remarks>
public static class ChangeExtractor
{
    /// <summary>
    /// The manifest. There is always one, because there is always a base.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Nullable in its return for the caller's sake and never null in fact. It
    /// used to return null when the tree had no base to measure from, which was a
    /// correct rule about a state that should not exist - and the state existed on
    /// every real flight, so the rule silently deleted the fact this whole slice
    /// reads. <see cref="Materialized.BaseCommit"/> is required now, so the case
    /// has no way to arise.
    /// </para>
    /// <para>
    /// An EMPTY manifest is a real answer and a different one: a loop that changed
    /// nothing produces one, and it says so rather than being absent.
    /// </para>
    /// </remarks>
    public static ChangeManifest? Extract(
        Materialized tree, IReadOnlyList<ClassificationRule> rules)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(rules);

        var baseCommit = tree.BaseCommit;

        // AN INDEX OF OUR OWN, and never the repository's. Untracked files - the
        // commonest shape of new work, and the one `when: touches migrations/**`
        // exists for - reach a diff only through an index, and staging into the
        // real one would leave a customer's working copy with somebody else's
        // staged changes in it. This tree is handed to a person when a flight does
        // not land, so measuring it has to change nothing in it.
        //
        // `add --all` is what excludes .gitignore'd files, which is the right
        // answer for the same reason: the destination adapter's own `git add
        // --all` would not stage them either, so a manifest naming one would
        // report as landing something that cannot land.
        var index = Path.Combine(
            Path.GetTempPath(), "gg-manifest-index", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(Path.GetDirectoryName(index)!);

        string numstat;
        string status;
        try
        {
            GitInvocation.InScratchIndex(index, "read-tree", baseCommit)
                .RunAsync(tree.Path).GetAwaiter().GetResult();
            GitInvocation.InScratchIndex(index, "add", "--all")
                .RunAsync(tree.Path).GetAwaiter().GetResult();

            // --no-renames, so a rename is a delete and an add. ChangeKinds has
            // three words and none of them is "renamed"; decomposing is TRUE at
            // this vocabulary's resolution, and it is the safer answer for both
            // readers - `in-scope` evaluates both paths and `touches` matches the
            // new one. A fourth kind is a contract move with a ledger entry.
            numstat = GitInvocation
                .InScratchIndex(index, "diff", "--cached", "--numstat", "--no-renames", "-z", baseCommit)
                .RunAsync(tree.Path).GetAwaiter().GetResult();

            status = GitInvocation
                .InScratchIndex(index, "diff", "--cached", "--name-status", "--no-renames", "-z", baseCommit)
                .RunAsync(tree.Path).GetAwaiter().GetResult();
        }
        finally
        {
            File.Delete(index);
        }

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
