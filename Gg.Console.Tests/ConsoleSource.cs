namespace Gg.Console.Tests;

/// <summary>
/// The console's own source, for the ratchets that read it.
/// </summary>
/// <remarks>
/// <b>Source rather than reflection, and only where reflection cannot answer.</b>
/// Three of the four parity ratchets ask questions about CODE - is there an arm
/// for this, is this field ever assigned, does anything call this - and a
/// compiled assembly has no arms, no assignments and no call sites it will
/// admit to without a decompiler. What reflection answers well is *what exists*;
/// what the source answers is *what is wired*.
/// </remarks>
internal static class ConsoleSource
{
    /// <summary>The repository root, found by walking up to the console project.</summary>
    internal static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "Gg.Console")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName
            ?? throw new InvalidOperationException(
                "no Gg.Console beside this test, so every ratchet that reads the source would "
              + "assert nothing. That is a broken scan, never a clean console.");
    }

    /// <summary>Every production file in a project, generated output excluded.</summary>
    internal static IReadOnlyList<string> In(params string[] projects)
    {
        var files = new List<string>();

        foreach (var project in projects)
        {
            var at = Path.Combine(Root(), project);
            if (!Directory.Exists(at))
            {
                continue;
            }

            files.AddRange(Directory
                .EnumerateFiles(at, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                                        StringComparison.Ordinal)
                         && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                                        StringComparison.Ordinal)));
        }

        return files;
    }

    internal static string Text(string project, string file) =>
        File.ReadAllText(Path.Combine(Root(), project, file));
}
