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

    /// <summary>The skill, and the commit the runner resolved and read it at.</summary>
    /// <remarks>
    /// <b>The commit is carried because nothing else knows it.</b> Rule 16 had
    /// the control plane resolve the ref and hand a commit over; since the
    /// owner amended that on 2026-09-17 this machine decides which commit a
    /// watch's ref names, so the attestation's record of what ran can only come
    /// from here.
    /// </remarks>
    public sealed record Read(RepositoryFile Skill, string Commit) : SkillRead;

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

        if (_adapters.FirstOrDefault(a =>
                string.Equals(a.Provider, where.Provider, StringComparison.Ordinal)) is not { } adapter)
        {
            return new SkillRead.Unreadable(
                $"This runner serves no repositories from '{where.Provider}', so it cannot read "
              + $"{path} from {where.Slug}.");
        }

        // RESOLVE FIRST, THEN LOOK IN THE CACHE - the owner's call of
        // 2026-09-17, and the order is the whole of it. Rule 16 used to hand
        // this machine a commit; it hands a ref now, and a ref MOVES. Keyed by
        // the ref, a watch whose skill was updated would run the first words
        // this machine ever read, forever, and nothing would say so. Keyed by
        // what the ref resolved to, the cache is sound for the reason it was
        // added: commits still do not change.
        //
        // `ls-remote` rather than a fetch, so a ref that has not moved costs
        // one question and no objects - which is the saving the cache exists
        // for, and it disappears if resolving means fetching.
        string? commit;

        try
        {
            commit = await adapter.ResolveRemoteAsync(
                where, where.PinnedRef, await _secretFor(where), cancellationToken);
        }
        catch (Exception failed) when (failed is InvalidOperationException
                                          or VcsCapabilityException
                                          or IOException)
        {
            return new SkillRead.Unreadable(
                $"'{where.PinnedRef}' could not be resolved in {where.Slug}.");
        }

        if (commit is not { Length: > 0 })
        {
            return new SkillRead.Unreadable(
                $"'{where.PinnedRef}' does not resolve to a commit in {where.Slug}, so there is "
              + "no version of this watch's skill to run.");
        }

        var entry = Path.Combine(_cacheRoot, Key(where, commit, path));
        if (Cached(entry) is { } cached)
        {
            return new SkillRead.Read(cached, commit);
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
        return new SkillRead.Read(file, commit);
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
    /// <summary>One file name per repository, commit and path.</summary>
    /// <summary>
    /// The cache key: the repository, the COMMIT, and the path.
    /// </summary>
    /// <remarks>
    /// <b>The commit rather than <c>PinnedRef</c>, which is what was asked
    /// for.</b> Since a sweep may be handed a ref, the two are different
    /// questions - and keying on the question means one answer is kept under
    /// a name that will later mean something else. It also makes a ref and the
    /// commit it points at one entry rather than two.
    /// </remarks>
    private static string Key(RepoTarget where, string commit, string path) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{where.Provider}\n{where.Slug}\n{commit}\n{path}")));

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
