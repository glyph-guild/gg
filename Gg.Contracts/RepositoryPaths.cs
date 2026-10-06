namespace Gg.Contracts;

/// <summary>
/// Whether a path names a file inside a repository, said once for everything that reads one.
/// </summary>
/// <remarks>
/// <para>
/// <b>One rule for the wire and the reader.</b> A file intent is refused on the wire when its
/// path could leave its repository, and the runner refuses it again before fetching anything.
/// Two spellings of that check would agree until the first edit to either - and the one that
/// drifted would be the one that let a path out.
/// </para>
/// <para>
/// <b>Refused by shape, never resolved.</b> Nothing here normalises <c>a/../b</c> into
/// <c>b</c>: a path that needs resolving to be safe is one somebody wrote to see whether it
/// would be resolved.
/// </para>
/// </remarks>
public static class RepositoryPaths
{
    /// <summary>Why this path is not a path inside a repository, or null when it is.</summary>
    public static string? Refused(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var outside = $"'{path}' is not a path inside the repository";

        if (path.Contains('\\', StringComparison.Ordinal))
        {
            return outside + " - paths here are written with '/', and a backslash is either a "
                 + "Windows path or an escape.";
        }

        if (path.StartsWith('/'))
        {
            return outside + " - it starts at the root of a machine, not of the repository.";
        }

        if (path.Length > 1 && path[1] == ':')
        {
            return outside + " - it names a drive.";
        }

        if (path.Split('/').Any(segment => segment is ".." or "." or ""))
        {
            return outside + " - it has an empty, '.' or '..' segment, so where it ends up "
                 + "depends on resolving it.";
        }

        return null;
    }
}
