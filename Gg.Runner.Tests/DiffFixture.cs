namespace Gg.Runner.Tests;

/// <summary>
/// A repository with one commit, and a working tree an agent has just finished
/// with.
/// </summary>
/// <remarks>
/// <para>
/// <b>The change is UNCOMMITTED, and that is the whole point of this fixture.</b>
/// It used to build two commits and diff them, which is not the state the extractor
/// is ever handed: the runner's order is materialize → invoke → extract → ship →
/// land, and the commit happens inside the destination adapter's push, after the
/// facts have gone. A fixture that commits first agrees with a broken instrument.
/// </para>
/// <para>
/// Built so the manifest has something of every shape to find: a modify, an add
/// git has never seen, a delete, a rename, a file <c>.gitignore</c> excludes, more
/// than one language, and paths at more than one classification. A fixture where
/// every file looks the same makes "the filter works" pass on a system with no
/// filter, which is this codebase's recurring defect.
/// </para>
/// <para>
/// <b>The marker is in file CONTENT and never in a path.</b> That is what makes
/// the absence assertions mean something: a manifest that leaked a line would
/// carry it, and a manifest that correctly carries the path would not.
/// </para>
/// </remarks>
internal sealed class DiffFixture : IDisposable
{
    /// <summary>Inside a file. Must never cross.</summary>
    internal const string ContentMarker = "MARKER-INSIDE-A-FILE";

    /// <summary>Ignored by <c>.gitignore</c>. Must never appear in a manifest.</summary>
    internal const string IgnoredPath = "build/artifact.o";

    /// <summary>Renamed by the agent. Both halves must appear.</summary>
    internal const string RenamedFrom = "src/Moved.cs";

    internal const string RenamedTo = "src/MovedElsewhere.cs";

    internal string Directory { get; }

    internal string BarePath { get; }

    /// <summary>The one commit. Everything the agent did is measured from it.</summary>
    internal string MainCommit { get; }

    /// <param name="wideFiles">
    /// How many extra files to change, for the case where the manifest cannot
    /// fit at file resolution. Zero for the ordinary case.
    /// </param>
    /// <param name="agentWorks">
    /// False for the positive control: a flight whose loop changed nothing, where
    /// an empty manifest is the correct answer rather than a broken one.
    /// </param>
    internal DiffFixture(int wideFiles = 0, bool agentWorks = true)
    {
        Directory = Path.Combine(Path.GetTempPath(), "gg-diff-fixture", Guid.NewGuid().ToString("n"));
        System.IO.Directory.CreateDirectory(Directory);

        BarePath = Path.Combine(Directory, "base.git");
        var work = Path.Combine(Directory, "work");

        GitFixture.Run(Directory, "init", "--bare", "--initial-branch=main", BarePath);
        GitFixture.Run(Directory, "clone", BarePath, work);

        Write(work, "src/Program.cs", "// original\n");
        Write(work, "src/Gone.cs", "// to be deleted\n");
        Write(work, RenamedFrom, "// moved from here\n");
        Write(work, "docs/notes.md", "notes\n");
        Write(work, "deploy/key.pem", "not a real key\n");
        Write(work, ".gitignore", "build/\n");
        GitFixture.Run(work, "add", ".");
        GitFixture.Run(work, "commit", "-m", "base");
        GitFixture.Run(work, "push", "origin", "main");
        MainCommit = GitFixture.Run(work, "rev-parse", "HEAD").Trim();

        // Everything below happens in the TREE THE RUNNER CLONES, not here, so
        // the fixture's own clone stays clean and the agent's work is applied to
        // whatever tree a test materializes.
        _wideFiles = wideFiles;
        _agentWorks = agentWorks;
    }

    private readonly int _wideFiles;

    private readonly bool _agentWorks;

    /// <summary>
    /// What the agent does, in the runner's own working tree, and stops there.
    /// </summary>
    /// <remarks>
    /// No <c>git add</c> and no commit, because the agent does neither - it is
    /// told not to, in as many words, by the executor's prompt.
    /// </remarks>
    internal void TheAgentWorks(string tree)
    {
        if (!_agentWorks)
        {
            return;
        }

        // The marker goes INSIDE a file, on a line the extractor will count and
        // must not carry.
        Write(tree, "src/Program.cs", $"// original\n// {ContentMarker}\n// and more\n");
        Write(tree, "src/Added.cs", "// brand new\n");
        Write(tree, "docs/notes.md", "notes\nand more notes\n");
        Write(tree, "deploy/key.pem", "still not a real key\nrotated\n");
        File.Delete(Path.Combine(tree, "src", "Gone.cs"));

        // A rename, done the way an agent does it: a write and a delete, with no
        // index anywhere in the story.
        Write(tree, RenamedTo, File.ReadAllText(Path.Combine(tree, RenamedFrom.Replace('/', Path.DirectorySeparatorChar))));
        File.Delete(Path.Combine(tree, RenamedFrom.Replace('/', Path.DirectorySeparatorChar)));

        // Ignored, and therefore not part of the change being proposed: the
        // destination adapter's `git add --all` would not stage it either.
        Write(tree, IgnoredPath, "not source\n");

        for (var i = 0; i < _wideFiles; i++)
        {
            // Spread across a few directories, so a rollup has something to
            // roll up rather than one bucket holding everything.
            Write(tree, $"wide/area{i % 7}/file{i}.cs", $"// {i}\n");
        }
    }

    private static void Write(string root, string relative, string content)
    {
        var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    public void Dispose()
    {
        if (System.IO.Directory.Exists(Directory))
        {
            System.IO.Directory.Delete(Directory, recursive: true);
        }
    }
}
