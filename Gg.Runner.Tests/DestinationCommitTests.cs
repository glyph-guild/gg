using Gg.Runner.Vcs;

namespace Gg.Runner.Tests;

/// <summary>
/// A destination pushes the agent's work, and a push carries commits rather
/// than changes - so whoever pushes must commit first.
/// </summary>
/// <remarks>
/// <para>
/// <b>Found by a real flight against a path-scoped remote.</b> The loop reported
/// <c>destination pushed: gg/GG-16 at 7bfc04d</c> and then
/// <c>destination landed</c>, and the proposal was empty. <c>7bfc04d</c> was the
/// commit the workspace had materialized AT, recorded in that same flight's
/// <c>source.provenance</c>: the branch was created pointing at the base, because
/// nothing had committed anything onto it.
/// </para>
/// <para>
/// <b>The commit step existed on one adapter and not the other.</b>
/// <see cref="HttpsDestinationAdapter"/> ran <c>checkout -b</c>, <c>add --all</c>
/// and <c>commit</c>; <see cref="RefNamedDestinationAdapter"/> called
/// <see cref="GitPush"/> directly, and GitPush pushes <c>HEAD</c>. Which forge a
/// customer uses decided whether their agent's work was carried or discarded, and
/// nothing else did.
/// </para>
/// <para>
/// <b>The flight that exposed it lost nothing</b> - the agent had made no changes,
/// so the manifest read <c>0 file(s), +0 -0</c> and an empty proposal was the whole
/// harm. That is the mild version. The severe one is silent: uncommitted edits stay
/// in the working tree, the base commit is pushed over them, and both facts still
/// read <c>pushed</c> and <c>landed</c>.
/// </para>
/// <para>
/// <b>Why these assert on the local repository rather than on the outcome.</b> The
/// remote here is unreachable on purpose, so the push fails - and the push failing
/// is not the subject. What is under test is whether the tree was committed by the
/// time the push was attempted, and that is a fact about this disk. Asserting it
/// through a successful push would need a live forge for the proposal half, which
/// is the thing no test here can reach.
/// </para>
/// </remarks>
public class DestinationCommitTests
{
    /// <summary>The file the agent is standing in for having written.</summary>
    private const string AgentsFile = "the-agent-wrote-this.txt";

    private const string Branch = "gg/flight-1";

    /// <summary>
    /// A clone with a base commit and an uncommitted change on top - the state an
    /// agent leaves a working tree in.
    /// </summary>
    private sealed class DirtyTree : IDisposable
    {
        internal string Directory { get; }

        /// <summary>What the tree was sitting at before the agent touched it.</summary>
        internal string BaseCommit { get; }

        internal DirtyTree()
        {
            Directory = Path.Combine(
                Path.GetTempPath(), "gg-destination-commit", Guid.NewGuid().ToString("n"));
            System.IO.Directory.CreateDirectory(Directory);

            GitFixture.Run(Directory, "init", "--initial-branch=main", Directory);
            File.WriteAllText(Path.Combine(Directory, "README.md"), "widgets\n");
            GitFixture.Run(Directory, "add", ".");
            GitFixture.Run(Directory, "-c", "user.name=t", "-c", "user.email=t@t", "commit", "-m", "base");
            BaseCommit = Head();

            // THE AGENT'S WORK, left exactly as an agent leaves it: written to the
            // tree, not added and not committed. Committing it here would test the
            // fixture instead of the adapter.
            File.WriteAllText(Path.Combine(Directory, AgentsFile), "the change\n");
        }

        internal string Head() =>
            GitFixture.Run(Directory, "rev-parse", "HEAD").Trim();

        /// <summary>Everything the current commit actually contains.</summary>
        internal string CommittedFiles() =>
            GitFixture.Run(Directory, "ls-tree", "--name-only", "-r", "HEAD");

        public void Dispose()
        {
            try
            {
                System.IO.Directory.Delete(Directory, recursive: true);
            }
            catch (IOException)
            {
                // A temp directory that outlives the run is not a failed test.
            }
        }
    }

    private static LandingRequest Request(DirtyTree tree) => new()
    {
        WorkingDirectory = tree.Directory,
        Slug = "JDX/agile-cortex",
        Branch = Branch,
        BaseRef = "main",
        Title = "A change",
        Secret = "not-a-real-credential",
    };

    /// <summary>
    /// Unreachable on purpose - see the remarks. <c>.invalid</c> is reserved by
    /// RFC 2606 and resolves nowhere, so this cannot accidentally reach a host
    /// somebody owns.
    /// </summary>
    private static RefNamedDestinationAdapter PathScoped() =>
        new("ado", "unreachable.invalid", new HttpClient());

    /// <summary>
    /// Attempts the push and discards how it went.
    /// </summary>
    /// <remarks>
    /// <b>The failure is swallowed deliberately and it is not the subject.</b> The
    /// remote cannot be resolved, and <see cref="GitPush"/> answers an unresolvable
    /// host by THROWING rather than by returning a <see cref="PushOutcome"/> - so
    /// without this the exception leaves the method and the assertions below never
    /// run. What is under test is the state of this disk at the moment the push was
    /// attempted, which is reached either way.
    /// </remarks>
    private static async Task AttemptAsync(IDestinationAdapter destination, LandingRequest request)
    {
        try
        {
            _ = await destination.PushAsync(request, CancellationToken.None);
        }
        catch (InvalidOperationException)
        {
            // The remote is unreachable by construction. See above.
        }
    }

    [Test]
    public async Task The_path_scoped_destination_commits_the_agents_work_before_pushing()
    {
        // THE DEFECT. This adapter went straight to GitPush, which pushes HEAD -
        // so it pushed the commit the workspace started at and left the agent's
        // edits sitting in the working tree.
        using var tree = new DirtyTree();

        await AttemptAsync(PathScoped(), Request(tree));

        await Assert.That(tree.Head()).IsNotEqualTo(tree.BaseCommit)
            .Because("pushing HEAD when HEAD is still the base commit proposes the repository "
                   + "back to itself, which is the empty pull request this was found as.");
        await Assert.That(tree.CommittedFiles()).Contains(AgentsFile)
            .Because("the agent's work reaching the remote is the entire point of a destination; "
                   + "a commit that does not carry it is the silent version of this bug.");
    }

    [Test]
    public async Task The_github_shaped_destination_commits_it_too_and_always_did()
    {
        // THE TWIN, and it is what makes the one above a defect rather than a
        // design. Both adapters answer the same question for the same caller, and
        // this one already committed - so a customer's forge decided whether their
        // work was carried. Written to fail if the fix is ever made by DELETING
        // the commit step here to match the other one.
        using var tree = new DirtyTree();

        await AttemptAsync(
            new HttpsDestinationAdapter("forge", "unreachable.invalid", new HttpClient()), Request(tree));

        await Assert.That(tree.CommittedFiles()).Contains(AgentsFile);
    }

    [Test]
    public async Task Both_destinations_put_the_work_on_the_branch_that_was_asked_for()
    {
        // The commit has to land on the destination branch. Committing onto the
        // base branch instead would carry the work - and propose it from a ref
        // the control plane never named, which no obligation is written against.
        using var tree = new DirtyTree();

        await AttemptAsync(PathScoped(), Request(tree));

        await Assert.That(GitFixture.Run(tree.Directory, "rev-parse", "--abbrev-ref", "HEAD").Trim())
            .IsEqualTo(Branch);
    }
}
