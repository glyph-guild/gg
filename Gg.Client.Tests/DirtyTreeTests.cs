using Gg.Client;

namespace Gg.Client.Tests;

/// <summary>
/// Pull refuses a dirty tree, and names the files.
/// </summary>
/// <remarks>
/// <para>
/// <b>Overwriting canonical formatting and overwriting somebody's unfinished edit
/// are different acts, and only one is intended.</b> Pull writes canonical
/// renderings, which is the point — a person's formatting is not the record. But
/// an edit nobody committed is work, and a tool that eats it is a tool people stop
/// running.
/// </para>
/// <para>
/// <b>The answer is the one ADR-0016's zero-magic commitment already implies: this
/// is a git repository, so behave like git.</b> No merge strategy, no stash, no
/// cleverness — refuse, name the files, and let the person commit or discard
/// exactly as they would for anything else in that directory.
/// </para>
/// <para>
/// <b>Which is why git enters <c>Gg.Client</c>, and it is a deliberate
/// crossing.</b> One read-only invocation, no network, no credentials, no writes.
/// It is the only way to tell a committed edit from an uncommitted one, and that
/// distinction is the whole refusal.
/// </para>
/// </remarks>
[Category("RealGit")]
public class DirtyTreeTests
{
    private static string Repository()
    {
        var path = Path.Combine(Path.GetTempPath(), $"gg-dirty-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        Git.Run(path, "init", "-q");
        Git.Run(path, "config", "user.email", "estate@example.invalid");
        Git.Run(path, "config", "user.name", "the estate walk");
        return path;
    }

    private static void Commit(string repo)
    {
        Git.Run(repo, "add", "-A");
        Git.Run(repo, "commit", "-q", "-m", "the estate as pulled");
    }

    [Test]
    public async Task An_uncommitted_edit_stops_the_pull_and_is_named()
    {
        var repo = Repository();
        try
        {
            _ = AirspaceTree.Write(repo, PullTests.Estate());
            Commit(repo);

            var edited = Path.Combine(repo, "airspace", "narrowings", "pci.yaml");
            await File.WriteAllTextAsync(edited, "obligations:\n  - id: mine\n");

            var refusal = AirspaceTree.Dirty(repo);

            await Assert.That(refusal).Contains("airspace/narrowings/pci.yaml")
                .Because("naming the file is the whole refusal - 'the tree is dirty' sends "
                       + "a person to run git status themselves.");
        }
        finally
        {
            Directory.Delete(repo, recursive: true);
        }
    }

    [Test]
    public async Task A_clean_tree_is_not_refused()
    {
        // THE POISON TWIN. A refusal that refused everything would satisfy the
        // assertion above and make pull unusable, which is the failure mode a
        // one-sided test cannot see.
        var repo = Repository();
        try
        {
            _ = AirspaceTree.Write(repo, PullTests.Estate());
            Commit(repo);

            await Assert.That(AirspaceTree.Dirty(repo)).IsEmpty();
        }
        finally
        {
            Directory.Delete(repo, recursive: true);
        }
    }

    [Test]
    public async Task An_untracked_document_counts_as_dirty()
    {
        // A file somebody wrote and has not committed is an edit, whether or not
        // git is tracking it yet. Treating untracked as clean would let pull
        // overwrite a document a person had just authored by hand.
        var repo = Repository();
        try
        {
            _ = AirspaceTree.Write(repo, PullTests.Estate());
            Commit(repo);

            await File.WriteAllTextAsync(
                Path.Combine(repo, "airspace", "narrowings", "sox.yaml"),
                "obligations:\n  - id: sox\n");

            await Assert.That(AirspaceTree.Dirty(repo)).Contains("airspace/narrowings/sox.yaml");
        }
        finally
        {
            Directory.Delete(repo, recursive: true);
        }
    }

    [Test]
    public async Task An_edit_outside_the_estate_does_not_stop_the_pull()
    {
        // Pull owns the documents it renders. Somebody mid-edit on a README has
        // nothing to do with whether the estate can be re-rendered, and refusing
        // on it would make the tool's business the whole repository's business.
        var repo = Repository();
        try
        {
            _ = AirspaceTree.Write(repo, PullTests.Estate());
            await File.WriteAllTextAsync(Path.Combine(repo, "README.md"), "hello\n");
            Commit(repo);

            await File.WriteAllTextAsync(Path.Combine(repo, "README.md"), "hello again\n");

            await Assert.That(AirspaceTree.Dirty(repo)).IsEmpty();
        }
        finally
        {
            Directory.Delete(repo, recursive: true);
        }
    }

    [Test]
    public async Task A_directory_that_is_not_a_repository_is_not_called_dirty()
    {
        // Nothing forces the working copy to be a git repository - the ADR says
        // the repository is convenience and the stream is law. A plain directory
        // gets no git opinion, rather than a refusal it cannot act on.
        var plain = Path.Combine(Path.GetTempPath(), $"gg-plain-{Guid.NewGuid():N}");
        Directory.CreateDirectory(plain);
        try
        {
            _ = AirspaceTree.Write(plain, PullTests.Estate());

            await Assert.That(AirspaceTree.Dirty(plain)).IsEmpty();
        }
        finally
        {
            Directory.Delete(plain, recursive: true);
        }
    }
}
