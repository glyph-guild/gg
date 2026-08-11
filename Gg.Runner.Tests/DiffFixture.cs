namespace Gg.Runner.Tests;

/// <summary>
/// A bare repository with two branches that genuinely differ.
/// </summary>
/// <remarks>
/// <para>
/// Built so the manifest has something of every shape to find: an add, a
/// modify, a delete, more than one language, and paths at more than one
/// classification. A fixture where every file looks the same makes "the filter
/// works" pass on a system with no filter, which is this codebase's recurring
/// defect.
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

    internal string Directory { get; }

    internal string BarePath { get; }

    internal string MainCommit { get; }

    internal string FeatureCommit { get; }

    /// <param name="wideFiles">
    /// How many extra files to change, for the case where the manifest cannot
    /// fit at file resolution. Zero for the ordinary case.
    /// </param>
    internal DiffFixture(int wideFiles = 0)
    {
        Directory = Path.Combine(Path.GetTempPath(), "gg-diff-fixture", Guid.NewGuid().ToString("n"));
        System.IO.Directory.CreateDirectory(Directory);

        BarePath = Path.Combine(Directory, "base.git");
        var work = Path.Combine(Directory, "work");

        GitFixture.Run(Directory, "init", "--bare", "--initial-branch=main", BarePath);
        GitFixture.Run(Directory, "clone", BarePath, work);

        Write(work, "src/Program.cs", "// original\n");
        Write(work, "src/Gone.cs", "// to be deleted\n");
        Write(work, "docs/notes.md", "notes\n");
        Write(work, "deploy/key.pem", "not a real key\n");
        GitFixture.Run(work, "add", ".");
        GitFixture.Run(work, "commit", "-m", "base");
        GitFixture.Run(work, "push", "origin", "main");
        MainCommit = GitFixture.Run(work, "rev-parse", "HEAD").Trim();

        // The change. The marker goes INSIDE a file, on a line the extractor
        // will count and must not carry.
        Write(work, "src/Program.cs", $"// original\n// {ContentMarker}\n// and more\n");
        Write(work, "src/Added.cs", "// brand new\n");
        Write(work, "docs/notes.md", "notes\nand more notes\n");
        Write(work, "deploy/key.pem", "still not a real key\nrotated\n");
        File.Delete(Path.Combine(work, "src", "Gone.cs"));

        for (var i = 0; i < wideFiles; i++)
        {
            // Spread across a few directories, so a rollup has something to
            // roll up rather than one bucket holding everything.
            Write(work, $"wide/area{i % 7}/file{i}.cs", $"// {i}\n");
        }

        GitFixture.Run(work, "add", "--all");
        GitFixture.Run(work, "commit", "-m", "the change");
        FeatureCommit = GitFixture.Run(work, "rev-parse", "HEAD").Trim();
        GitFixture.Run(work, "push", "origin", "HEAD:refs/heads/feature");
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
