namespace Gg.Runner.Tests;

/// <summary>
/// <b>S62.2-01</b> - sweeps and intents read files through one reader, and the sweep path's
/// behaviour is unchanged.
/// </summary>
/// <remarks>
/// <b>Held by shape, because the behaviour is already held.</b> Every test that drove
/// <c>SkillReader</c> drives the reader it became, unchanged in what it asserts - that is the
/// "unchanged" half. What only a shape can hold is that there is ONE: a second reader for
/// intents would drift from the first on exactly the checks that keep a path inside its
/// repository.
/// </remarks>
public class OneReaderReadsRepositoryFilesTests
{
    private static string Root()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);
        while (at is not null && !File.Exists(Path.Combine(at.FullName, "Gg.sln")))
        {
            at = at.Parent;
        }

        return at!.FullName;
    }

    private static IEnumerable<string> RunnerSources() =>
        Directory.EnumerateFiles(Path.Combine(Root(), "Gg.Runner"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    [Test]
    public async Task There_is_one_reader_and_it_is_not_a_skill_reader()
    {
        var declaring = RunnerSources()
            .Where(f => File.ReadAllText(f).Contains("class RepositoryFileReader", StringComparison.Ordinal))
            .ToList();

        await Assert.That(declaring.Count).IsEqualTo(1);
        await Assert.That(RunnerSources().Any(f =>
                File.ReadAllText(f).Contains("class SkillReader", StringComparison.Ordinal)))
            .IsFalse()
            .Because("a skill is one of the files the reader reads, not the reader.");
    }

    [Test]
    public async Task Sweeps_read_through_it()
    {
        var sweep = File.ReadAllText(Path.Combine(Root(), "Gg.Runner", "Sweeps", "SweepLoop.cs"));

        await Assert.That(sweep).Contains("RepositoryFileReader");
    }

    [Test]
    public async Task Its_path_rule_is_the_contracts()
    {
        var reader = RunnerSources()
            .Single(f => File.ReadAllText(f).Contains("class RepositoryFileReader", StringComparison.Ordinal));

        await Assert.That(File.ReadAllText(reader)).Contains("RepositoryPaths.Refused(")
            .Because("the wire refuses a path that could leave the repository, and the reader "
                   + "refuses it again; one function means they cannot disagree.");
    }
}
