namespace Gg.Runner.Tests;

/// <summary>
/// One forge's path segment stays in the adapters named for that shape.
/// </summary>
/// <remarks>
/// <para>
/// <b>The vendor half of this rule is already enforced, and better.</b>
/// <c>ProviderNeutralityTests</c> scans the whole solution for provider names,
/// with a matcher that has its own self-test and a comment recording that two
/// more names were added at slice twenty <i>because the guard did not catch
/// what it exists to catch</i>. A first draft of this file re-implemented that
/// scan and was caught by it twice — first for listing the vendors in order to
/// ban them, then for merely quoting the list in this remark. Both are correct
/// catches: the guard reads comments too, because a name in a comment is still
/// a name a customer reads. Nothing here duplicates it.
/// </para>
/// <para>
/// <b>What is left is the part no guard covers: a path segment.</b>
/// <c>_git</c> names no vendor, so it passes provider neutrality cleanly, and
/// it is exactly what leaks when somebody makes a failing deployment work by
/// building a url in a shared place instead of in the adapter that owns the
/// spelling. <c>PathScopedGitVcsAdapter</c> says the segment <i>"belongs to
/// this provider's spelling"</i> and <i>"stays in here"</i>. This is that
/// sentence, checkable.
/// </para>
/// </remarks>
public class NoForgeIsNamedTests
{
    /// <summary>Where one forge's path spelling is allowed to be known.</summary>
    private static readonly string[] _spellsTheConvention =
    [
        "PathScopedGitVcsAdapter.cs",
        "RefNamedDestinationAdapter.cs",
    ];

    private static IEnumerable<string> ShippedSource() =>
        new[] { "Gg.Runner", "Gg.Cli", "Gg.Client", "Gg.Contracts" }
            .Select(project => Path.Combine(RepoRoot(), project))
            .Where(Directory.Exists)
            .SelectMany(dir => Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories))
            .Where(file =>
                !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
             && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    [Test]
    public async Task The_scan_reaches_the_source_it_claims_to()
    {
        // The assertion below is an ABSENCE, and an absence over an empty set
        // passes forever. This is the liveness anchor: it fails if the walk
        // stops finding files, which is how a guard quietly stops guarding.
        var scanned = ShippedSource().ToList();

        await Assert.That(scanned.Count).IsGreaterThan(100)
            .Because("a project layout change that emptied this walk would leave the guard green "
                   + "and enforcing nothing.");
        await Assert.That(scanned.Any(f => Path.GetFileName(f) == "PathScopedGitVcsAdapter.cs")).IsTrue()
            .Because("the one file allowed to know the convention must be inside the scan, or its "
                   + "exemption is describing a file nobody looks at.");
    }

    [Test]
    public async Task The_path_segment_of_one_convention_stays_in_the_adapters_named_for_it()
    {
        var offenders = ShippedSource()
            .Where(file => !_spellsTheConvention.Contains(Path.GetFileName(file), StringComparer.Ordinal))
            .Where(file => File.ReadAllText(file).Contains("/_git/", StringComparison.Ordinal))
            .Select(file => Path.GetRelativePath(RepoRoot(), file))
            .Order(StringComparer.Ordinal)
            .ToList();

        await Assert.That(offenders).IsEmpty()
            .Because("`/_git/` belongs to one forge's path convention. The adapters named for that "
                   + "convention are where it may be known; a default, a config reader or a url "
                   + "built anywhere else is the seam leaking - and it leaks without naming a "
                   + "vendor, so provider neutrality would pass it. Found in: "
                   + string.Join(", ", offenders));
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null
            && !File.Exists(Path.Combine(directory.FullName, "Gg.Contracts", "fact-vocabulary.json")))
        {
            directory = directory.Parent;
        }

        return (directory ?? throw new InvalidOperationException("repository root not found")).FullName;
    }
}
