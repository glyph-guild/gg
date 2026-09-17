namespace Gg.Runner.Vcs;

/// <summary>
/// Puts one ref on disk, and leaves nothing that could push back.
/// </summary>
/// <remarks>
/// <para>
/// Shared by every adapter, because the sequence is the same wherever the url
/// points and only the url and the credential differ. Init, fetch exactly the
/// pinned ref at depth one, check out what came back, and remove the remote.
/// </para>
/// <para>
/// <b>No remote is ever configured.</b> The url is an argument to the fetch
/// rather than something written into the tree's config, so the tree that comes
/// out does not know where it came from and has nowhere to push. The port
/// having no write method is the assertion that matters; this closes the gap
/// between "our code cannot write" and "nothing in this directory can".
/// </para>
/// <para>
/// It also means the credential helper does not outlive the fetch: it was
/// passed with <c>-c</c> for one command and was never persisted.
/// </para>
/// </remarks>
internal static class GitWorkingTree
{
    internal static async Task<CloneOutcome> FetchAsync(
        string url, string resolvedRef, string intoDirectory, string? secret,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(intoDirectory);

        await GitInvocation.Plain("init", "--quiet").RunAsync(intoDirectory, cancellationToken);

        await GitInvocation.Fetch(url, resolvedRef, secret).RunAsync(intoDirectory, cancellationToken);

        // FETCH_HEAD, not a branch: the pinned ref may be a pull-request head
        // that is on no branch of the base repository, which is exactly the
        // case this whole design turns on.
        await GitInvocation.Plain("checkout", "--quiet", "--detach", "FETCH_HEAD")
            .RunAsync(intoDirectory, cancellationToken);

        var head = (await GitInvocation.Plain("rev-parse", "HEAD")
            .RunAsync(intoDirectory, cancellationToken)).Trim();

        var (files, bytes) = Measure(intoDirectory);

        return new CloneOutcome { HeadCommit = head, FileCount = files, Bytes = bytes };
    }

    /// <summary>
    /// One file at one commit, fetched as objects into a scratch repository that
    /// is gone before this returns.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>No working copy, and no blobless fetch either - measured, not
    /// assumed.</b> A fetch with <c>--filter=blob:none</c> needs the source
    /// configured as the repository's promisor remote, and this runner never
    /// writes a remote: the url is an argument so nothing on disk knows where it
    /// came from. So the commit is fetched at depth one into a bare scratch
    /// repository, the file is found by <c>rev-parse</c> and printed by
    /// <c>cat-file</c>, and the scratch is removed. The caller caches the result,
    /// so this runs once per commit.
    /// </para>
    /// <para>
    /// <b>Null means the commit has no such file</b>; a fetch that fails throws,
    /// because an unreachable forge is not an absent skill.
    /// </para>
    /// </remarks>
    internal static async Task<RepositoryFile?> ReadFileAsync(
        string url, string commit, string path, string scratchDirectory, string? secret,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(scratchDirectory);

        try
        {
            await GitInvocation.Plain("init", "--quiet", "--bare")
                .RunAsync(scratchDirectory, cancellationToken);

            await GitInvocation.Fetch(url, commit, secret)
                .RunAsync(scratchDirectory, cancellationToken);

            string blob;
            try
            {
                blob = (await GitInvocation.Plain("rev-parse", "--verify", $"{commit}:{path}")
                    .RunAsync(scratchDirectory, cancellationToken)).Trim();
            }
            catch (InvalidOperationException)
            {
                // rev-parse exits non-zero when the path is not in the commit,
                // which is an answer rather than a failure.
                return null;
            }

            var type = (await GitInvocation.Plain("cat-file", "-t", blob)
                .RunAsync(scratchDirectory, cancellationToken)).Trim();

            if (!string.Equals(type, "blob", StringComparison.Ordinal))
            {
                // A directory is not a skill.
                return null;
            }

            var content = await GitInvocation.Plain("cat-file", "blob", blob)
                .RunAsync(scratchDirectory, cancellationToken);

            return new RepositoryFile(content, blob);
        }
        finally
        {
            try
            {
                Directory.Delete(scratchDirectory, recursive: true);
            }
            catch (IOException)
            {
                // A scratch that will not delete is left for the operating
                // system's temp sweep; it holds objects, not a working copy.
            }
        }
    }

    /// <summary>
    /// Brings one more ref into an existing tree, without disturbing it.
    /// </summary>
    /// <remarks>
    /// No checkout. The working tree stays at the head that was materialized;
    /// this only puts the base's objects on the same disk so a diff has two
    /// points to compare.
    /// </remarks>
    internal static async Task<string> FetchAlsoAsync(
        string url, string resolvedRef, string intoDirectory, string? secret,
        CancellationToken cancellationToken = default)
    {
        await GitInvocation.Fetch(url, resolvedRef, secret).RunAsync(intoDirectory, cancellationToken);

        // CHECKED OUT, not only fetched, and the difference is what the agent sees.
        // This is only ever called for a continuation - the commit a prior attempt
        // pushed - and an attempt that continues works ON that tree: the feedback it
        // is acting on references files in it, its next commit has to sit on top of
        // it so the push fast-forwards, and its manifest measures what THIS attempt
        // did from there. Fetch-without-checkout gave the manifest its base and gave
        // the agent a tree from before the work existed.
        //
        // Detached HEAD, deliberately: the push path creates its branch with
        // `checkout -b` when it commits, so nothing here needs a name. And no
        // --force, also deliberately: this runs against a tree the materializer
        // just built, which is clean by construction - and the runner carries a
        // structural guard that refuses the word, because a flag that can rewrite
        // work must not exist anywhere a refactor could move it in front of a push.
        await GitInvocation.Plain("checkout", "FETCH_HEAD")
            .RunAsync(intoDirectory, cancellationToken);

        return (await GitInvocation.Plain("rev-parse", "FETCH_HEAD")
            .RunAsync(intoDirectory, cancellationToken)).Trim();
    }

    /// <summary>
    /// How much disk this took, including git's own objects.
    /// </summary>
    /// <remarks>
    /// <c>.git</c> is counted deliberately. Disk is the first resource this
    /// product consumes in a customer's environment, and a number that
    /// excluded the object store would understate it by most of the total on
    /// any repository with history.
    /// </remarks>
    private static (int Files, long Bytes) Measure(string directory)
    {
        var files = 0;
        var bytes = 0L;

        foreach (var path in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            files++;
            bytes += new FileInfo(path).Length;
        }

        return (files, bytes);
    }
}
