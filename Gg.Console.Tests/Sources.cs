namespace Gg.Console.Tests;

/// <summary>Reads a source file out of the repository, for structural guards.</summary>
/// <remarks>
/// One implementation, because four test files had walked up to <c>Gg.sln</c>
/// themselves and a fifth copy is a fifth thing to get wrong.
/// </remarks>
internal static class Sources
{
    internal static string Read(string project, params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Gg.sln")))
        {
            dir = dir.Parent;
        }

        return File.ReadAllText(
            Path.Combine([dir!.FullName, project, .. parts]));
    }
}
