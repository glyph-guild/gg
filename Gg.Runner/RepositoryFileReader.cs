using System.Security.Cryptography;
using System.Text;
using Gg.Contracts;
using Gg.Runner.Vcs;

namespace Gg.Runner;

/// <summary>What reading a file from a repository concluded.</summary>
public abstract record FileRead
{
    private FileRead()
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
    public sealed record Read(RepositoryFile File, string Commit) : FileRead;

    /// <summary>Why it could not be read, in a sentence the sweep attests.</summary>
    public sealed record Unreadable(string Diagnosis) : FileRead;
}

/// <summary>
/// Reads one file from a repository at a ref it resolves, once per commit - a watch's skill,
/// or a flight's intent.
/// </summary>
/// <remarks>
/// <para>
/// <b>One reader for every file a runner is handed by name.</b> Slice sixty-two made a flight's
/// intent able to be a file, and the reader it needed was this one: the same path rule, the same
/// cache, the same object id. It was <c>SkillReader</c>; a second reader for intents would have
/// drifted from the first on exactly the checks that keep a path inside its repository.
/// </para>
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
public sealed class RepositoryFileReader(
    IReadOnlyList<IVcsAdapter> adapters,
    string cacheRoot,
    Func<RepoTarget, Task<string?>> secretFor)
{
    private readonly IReadOnlyList<IVcsAdapter> _adapters = adapters;
    private readonly string _cacheRoot = cacheRoot;
    private readonly Func<RepoTarget, Task<string?>> _secretFor = secretFor;

    /// <summary>
    /// The same reader - its adapters and its cache - finding credentials another way.
    /// </summary>
    /// <remarks>
    /// A sweep's skill is read with this machine's credential; a flight's intent with the ones its
    /// lease resolved. The cache is shared because it is keyed by commit, and a commit's bytes
    /// are the same whoever read them.
    /// </remarks>
    public RepositoryFileReader WithSecrets(Func<RepoTarget, Task<string?>> secretFor) =>
        new(_adapters, _cacheRoot, secretFor);

    /// <param name="where">The repository, with the pinned commit as its <see cref="RepoTarget.PinnedRef"/>.</param>
    /// <param name="path">The skill's path in that repository.</param>
    /// <param name="what">What is being read, as the sentences name it: "this watch's skill", "this flight's intent".</param>
    public async Task<FileRead> ReadAsync(
        RepoTarget where, string path, string what, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(where);

        // THE CONTRACT'S RULE, never a second one. The wire refuses a path that could leave
        // its repository and this refuses it again before anything is fetched; one function
        // means the two cannot disagree, and the stricter of the two rules is the one kept.
        if (string.IsNullOrWhiteSpace(path))
        {
            return new FileRead.Unreadable($"Nothing names a path for {what}, so nothing was fetched.");
        }

        if (RepositoryPaths.Refused(path) is { } outside)
        {
            return new FileRead.Unreadable(outside + " Nothing was fetched.");
        }

        if (_adapters.FirstOrDefault(a =>
                string.Equals(a.Provider, where.Provider, StringComparison.Ordinal)) is not { } adapter)
        {
            return new FileRead.Unreadable(
                $"This runner serves no repositories from '{where.Provider}', so it cannot read "
              + $"{path} from {where.Slug} for {what}.");
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
            return new FileRead.Unreadable(
                $"'{where.PinnedRef}' could not be resolved in {where.Slug}, so {what} was not read.");
        }

        if (commit is not { Length: > 0 })
        {
            return new FileRead.Unreadable(
                $"'{where.PinnedRef}' does not resolve to a commit in {where.Slug}, so there is "
              + $"no version of {what} to read.");
        }

        var entry = Path.Combine(_cacheRoot, Key(where, commit, path));
        if (Cached(entry) is { } cached)
        {
            return new FileRead.Read(cached, commit);
        }

        RepositoryFile? file;
        var scratch = Path.Combine(
            Path.GetTempPath(), "gg-file-read", Guid.NewGuid().ToString("n"));

        try
        {
            file = await adapter.ReadFileAsync(
                where, commit, path, scratch, await _secretFor(where), cancellationToken);
        }
        catch (Exception failed) when (failed is InvalidOperationException
                                          or VcsCapabilityException
                                          or IOException)
        {
            return new FileRead.Unreadable(
                $"{path} could not be fetched from {where.Slug} at {commit}, so {what} was not read.");
        }

        if (file is null)
        {
            return new FileRead.Unreadable(
                $"{where.Slug} has no {path} at {commit}, so {what} was not read.");
        }

        Keep(entry, file);
        return new FileRead.Read(file, commit);
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
        // ".skill" still, though the reader reads more than skills: renaming it would orphan
        // every entry already cached on a runner, for a word nobody reads.
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
