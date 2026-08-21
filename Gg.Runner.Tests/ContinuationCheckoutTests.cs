using Gg.Runner.Vcs;

namespace Gg.Runner.Tests;

/// <summary>
/// An attempt that continues works ON the prior attempt's tree, not beside it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Found by tracing what a resumed agent would actually see.</b>
/// <c>ContinuesFrom</c> was fetched and used as the manifest's diff base - and
/// nothing checked it out, so the working tree stayed at the pinned ref. The agent
/// on attempt two read a rejection about "the migration missing a down step" in a
/// tree where the migration did not exist: the feedback referenced work the tree
/// did not contain.
/// </para>
/// <para>
/// <b>Why the fixture never saw it.</b> <c>AttemptFixture</c> pins the WORK BRANCH
/// itself, so the clone happened to contain the prior attempt and the diff-base
/// fetch looked sufficient. In production the pinned ref is the flight's base -
/// <c>refs/heads/main</c> from the intent uri - and the prior work lives only on
/// the pushed branch.
/// </para>
/// <para>
/// <b>What checking it out also fixes.</b> Attempt two commits on top of the
/// continuation, so its push to the flight's branch is a fast-forward. Committing
/// beside it produced a second root whose push the remote refuses - and a manifest
/// measured from the prior commit against a tree without the prior work reported
/// every one of attempt one's files as DELETED, which reads as an agent undoing
/// reviewed work.
/// </para>
/// </remarks>
public class ContinuationCheckoutTests
{
    /// <summary>
    /// Attempt one's commit, pushed to the flight's branch and to no pinned ref.
    /// </summary>
    /// <remarks>
    /// Staged the way production stages it: the work is on <c>gg/GG-7</c> and the
    /// pinned ref stays <c>main</c>. The fixture that hid this defect pinned the
    /// work branch itself, so its clone happened to contain the prior attempt.
    /// </remarks>
    private static string PriorAttempt(GitFixture fixture)
    {
        var work = Path.Combine(fixture.Directory, "work");

        GitFixture.Run(work, "checkout", "-b", "attempt-one", fixture.BranchCommit);
        Directory.CreateDirectory(Path.Combine(work, "src", "Billing"));
        File.WriteAllText(
            Path.Combine(work, "src", "Billing", "Converters.cs"), "// migrated\n");
        GitFixture.Run(work, "add", "--all");
        GitFixture.Run(work, "commit", "-m", "first attempt");
        GitFixture.Run(work, "push", "origin", "HEAD:refs/heads/gg/GG-7");

        return GitFixture.Run(work, "rev-parse", "HEAD").Trim();
    }

    private static Materializer AMaterializer(GitFixture fixture) => new(
        new LocalVcsAdapter(fixture.Directory),
        new WorkingTreeRoot(Path.Combine(fixture.Directory, "trees")));

    [Test]
    public async Task The_materialized_tree_contains_the_prior_attempts_work()
    {
        using var fixture = new GitFixture();
        var prior = PriorAttempt(fixture);
        var materializer = AMaterializer(fixture);

        var tree = await materializer.MaterializeAsync(
            "flight-7",
            new RepoTarget
            {
                Provider = LocalVcsAdapter.ProviderKey,
                Slug = fixture.BarePath,
                PinnedRef = "refs/heads/main",
                ContinuesFrom = prior,
            },
            secret: null);

        await Assert.That(File.Exists(Path.Combine(tree.Path, "src/Billing/Converters.cs")))
            .IsTrue()
            .Because("the rejection the agent is acting on references this file, and feedback "
                   + "about work the tree does not contain sends the loop to redo the attempt "
                   + "rather than continue it.");

        await Assert.That(tree.HeadCommit).IsEqualTo(prior)
            .Because("the tree IS the prior attempt now, so the commit this attempt builds on "
                   + "and the commit the manifest measures from are the same thing.");

        await Assert.That(tree.BaseCommit).IsEqualTo(prior)
            .Because("and the manifest describes what THIS attempt did from there.");
    }

    [Test]
    public async Task A_first_attempt_still_materializes_the_pinned_ref()
    {
        // The twin. A materializer that always chased a branch would break every
        // first attempt, and null has to keep meaning "start from the base".
        using var fixture = new GitFixture();

        var materializer = AMaterializer(fixture);

        var tree = await materializer.MaterializeAsync(
            "flight-8",
            new RepoTarget
            {
                Provider = LocalVcsAdapter.ProviderKey,
                Slug = fixture.BarePath,
                PinnedRef = "refs/heads/main",
                ContinuesFrom = null,
            },
            secret: null);

        await Assert.That(File.Exists(Path.Combine(tree.Path, "README.md"))).IsTrue();
        await Assert.That(tree.BaseCommit).IsEqualTo(tree.HeadCommit)
            .Because("a first attempt measures from where the branch was cut.");
    }

    [Test]
    public async Task The_continued_trees_next_commit_fast_forwards_the_flights_branch()
    {
        // The push half of the same defect. An attempt committing beside the prior
        // work produces a second root, and pushing it to the flight's branch is a
        // non-fast-forward the remote refuses - so attempt two's work was lost at
        // the exact moment it was finished.
        using var fixture = new GitFixture();
        var prior = PriorAttempt(fixture);
        var materializer = AMaterializer(fixture);

        var tree = await materializer.MaterializeAsync(
            "flight-7",
            new RepoTarget
            {
                Provider = LocalVcsAdapter.ProviderKey,
                Slug = fixture.BarePath,
                PinnedRef = "refs/heads/main",
                ContinuesFrom = prior,
            },
            secret: null);

        // The agent edits, and the runner's own push path commits and pushes.
        File.WriteAllText(Path.Combine(tree.Path, "src/Billing/More.cs"), "// attempt two\n");

        GitFixture.Run(tree.Path, "add", "--all");
        GitFixture.Run(tree.Path, "-c", "user.name=gg", "-c", "user.email=gg@localhost",
            "commit", "-m", "second attempt");
        GitFixture.Run(tree.Path, "push", "origin", "HEAD:refs/heads/gg/GG-7");

        var remoteHead = GitFixture.Run(
            fixture.BarePath, "rev-parse", "refs/heads/gg/GG-7").Trim();
        var parent = GitFixture.Run(
            fixture.BarePath, "rev-parse", "refs/heads/gg/GG-7~1").Trim();

        await Assert.That(parent).IsEqualTo(prior)
            .Because("attempt two's commit sits ON attempt one's, which is what makes the push a "
                   + "fast-forward and the branch a history rather than a fight.");
        await Assert.That(remoteHead).IsNotEqualTo(prior);
    }
}
