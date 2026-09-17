using System.Security.Cryptography;
using System.Text;
using Gg.Runner.Vcs;

namespace Gg.Runner.Sweeps;

/// <summary>What reading a sweep's skill concluded.</summary>
public abstract record SkillRead
{
    private SkillRead()
    {
    }

    /// <summary>The skill, as the pinned commit holds it.</summary>
    public sealed record Read(RepositoryFile Skill) : SkillRead;

    /// <summary>Why it could not be read, in a sentence the sweep attests.</summary>
    public sealed record Unreadable(string Diagnosis) : SkillRead;
}

/// <summary>
/// Reads a watch's skill at the commit the control plane pinned, once per
/// commit.
/// </summary>
/// <remarks>
/// <para>
/// <b>The runner reads it, and the owner decided so on 2026-09-16</b>: the
/// control plane reads nothing from a repository but a narrowings directory, so
/// it pins the commit and the runner fetches the words with the customer's
/// credential, on the customer's machine.
/// </para>
/// <para>
/// <b>Cached by commit, because <i>"commits won't change"</i></b> - the owner's
/// words. The key is the repository, the commit and the path together, so a
/// second commit or a second file is never answered from the first; the cached
/// entry is the file's text and its blob id, and nothing else from the
/// repository is kept.
/// </para>
/// <para>
/// <b>A path is refused before anything is fetched</b> when it could leave the
/// repository, and a pin that is not a commit is refused because the cache key
/// must not move. Every failure is a sentence of this class's - an exception's
/// own text is never repeated, because it can name a url.
/// </para>
/// </remarks>
/// <param name="adapters">This runner's VCS adapters, keyed by the provider each serves.</param>
/// <param name="cacheRoot">Where read skills are kept, normally <c>LocalPaths.Skills()</c>.</param>
/// <param name="secretFor">The credential for a repository, or null where it needs none.</param>
public sealed class SkillReader(
    IReadOnlyList<IVcsAdapter> adapters,
    string cacheRoot,
    Func<RepoTarget, Task<string?>> secretFor)
{
    private readonly IReadOnlyList<IVcsAdapter> _adapters = adapters;
    private readonly string _cacheRoot = cacheRoot;
    private readonly Func<RepoTarget, Task<string?>> _secretFor = secretFor;

    /// <param name="where">The repository, with the pinned commit as its <see cref="RepoTarget.PinnedRef"/>.</param>
    /// <param name="path">The skill's path in that repository.</param>
    public async Task<SkillRead> ReadAsync(
        RepoTarget where, string path, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(where);

        if (Unsafe(path) is { } unsafePath)
        {
            return new SkillRead.Unreadable(unsafePath);
        }

        var commit = where.PinnedRef;
        if (!IsCommit(commit))
        {
            return new SkillRead.Unreadable(
                $"'{commit}' is not a commit, so the skill has no pin to be read at.");
        }

        var entry = Path.Combine(_cacheRoot, Key(where, path));
        if (Cached(entry) is { } cached)
        {
            return new SkillRead.Read(cached);
        }

        if (_adapters.FirstOrDefault(a =>
                string.Equals(a.Provider, where.Provider, StringComparison.Ordinal)) is not { } adapter)
        {
            return new SkillRead.Unreadable(
                $"This runner serves no repositories from '{where.Provider}', so it cannot read "
              + $"{path} from {where.Slug}.");
        }

        RepositoryFile? file;
        var scratch = Path.Combine(
            Path.GetTempPath(), "gg-skill-read", Guid.NewGuid().ToString("n"));

        try
        {
            file = await adapter.ReadFileAsync(
                where, commit, path, scratch, await _secretFor(where), cancellationToken);
        }
        catch (Exception failed) when (failed is InvalidOperationException
                                          or VcsCapabilityException
                                          or IOException)
        {
            return new SkillRead.Unreadable(
                $"{path} could not be fetched from {where.Slug} at {commit}.");
        }

        if (file is null)
        {
            return new SkillRead.Unreadable(
                $"{where.Slug} has no {path} at {commit}.");
        }

        Keep(entry, file);
        return new SkillRead.Read(file);
    }

    /// <summary>Why this path may not be read, or null.</summary>
    private static string? Unsafe(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "The watch names no skill path.";
        }

        if (path.StartsWith('/')
            || path.Contains('\\', StringComparison.Ordinal)
            || path.Split('/').Any(part => part is ".." or "")
            || path.Any(char.IsControl))
        {
            return $"'{path}' is not a path inside the repository, so nothing was fetched.";
        }

        return null;
    }

    /// <summary>Forty or sixty-four hex digits - a commit, and never a name that moves.</summary>
    private static bool IsCommit(string value) =>
        value is { Length: 40 or 64 } && value.All(Uri.IsHexDigit);

    /// <summary>One file name per repository, commit and path.</summary>
    private static string Key(RepoTarget where, string path) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{where.Provider}\n{where.Slug}\n{where.PinnedRef}\n{path}")));

    private static RepositoryFile? Cached(string entry)
    {
        var content = entry + ".skill";
        var sha = entry + ".sha";

        return File.Exists(content) && File.Exists(sha)
            ? new RepositoryFile(File.ReadAllText(content), File.ReadAllText(sha).Trim())
            : null;
    }

    /// <summary>
    /// Writes the entry, digest last, so a half-written one is never read as whole.
    /// </summary>
    private static void Keep(string entry, RepositoryFile file)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(entry)!);
        File.WriteAllText(entry + ".skill", file.Content);
        File.WriteAllText(entry + ".sha", file.BlobSha);
    }
}
