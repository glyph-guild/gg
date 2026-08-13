using System.Text.RegularExpressions;
using Gg.Runner.Vcs;

namespace Gg.Runner.Tests;

/// <summary>
/// Never overwrite a lifecycle - in somebody else's storage.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is a git property, not a forge property.</b> A local bare repository is a real
/// remote for git's purposes: push, force-push and commit resolution all behave
/// identically. So the claim this step turns on runs in CI, unlike the rows
/// that need a real forge for app authentication and pull requests. The two must not get
/// conflated - a forge is needed to PROPOSE, and not to prove this.
/// </para>
/// <para>
/// <b>What is asserted is our behaviour, not the remote's refusal.</b> A bare repository
/// will happily accept a force-push; so will most of a customer's. The property is
/// therefore observed as attempt one's commit still being reachable after attempt two
/// pushes - not as the remote having said no. That is the right target anyway: the remote
/// is the customer's and its configuration is not ours to rely on.
/// </para>
/// <para>
/// <b>Reachability rather than object existence.</b> A force-pushed commit lingers as a
/// dangling object until garbage collection, so <c>cat-file -e</c> would keep answering
/// yes for a commit that is on its way to being deleted. What a customer means by "my
/// commit is still there" is that a ref leads to it, and that is what evidence references
/// depend on.
/// </para>
/// </remarks>
public class SecondPushTests
{
    // ---- attempt two adds ----

    [Test]
    public async Task Attempt_ones_commit_is_still_reachable_after_attempt_two_pushes()
    {
        using var remote = new PushFixture();

        var one = remote.CommitAndPush("first attempt", "src/greet.py", "print('hello')\n");
        var two = remote.CommitAndPush("second attempt", "src/greet.py", "print('hello, world')\n");

        // ASK WHY IT PASSES. "The old commit is still reachable" passes trivially if the
        // second push never happened, so the second push is asserted to have landed
        // before anything is concluded from the first one surviving.
        await Assert.That(remote.RemoteTip()).IsEqualTo(two)
            .Because("attempt two reached the remote, or the rest of this test is about "
                   + "nothing.");
        await Assert.That(two).IsNotEqualTo(one)
            .Because("and it is a different commit, so there was something to overwrite.");

        await Assert.That(remote.ReachableFromBranch(one)).IsTrue()
            .Because("attempt one's commit is what destination.pushed recorded and what every "
                   + "evidence reference for that attempt resolves through. A push that "
                   + "rewrote it would dangle those references inside a customer's "
                   + "repository, where we cannot see it happen and they cannot undo it.");
    }

    [Test]
    public async Task A_deliberate_force_push_makes_the_old_commit_unreachable()
    {
        // THE LIVENESS TWIN. An absence test that has never seen presence is not looking:
        // if the assertion above could not fail, it would be measuring nothing. This does
        // the overwrite deliberately, with raw git rather than through anything of ours,
        // and watches the commit stop being reachable.
        using var remote = new PushFixture();

        var one = remote.CommitAndPush("first attempt", "src/greet.py", "print('hello')\n");

        await Assert.That(remote.ReachableFromBranch(one)).IsTrue()
            .Because("reachable to begin with, so the change below is the force-push and not "
                   + "the starting state.");

        remote.ForcePushADivergentHistory();

        await Assert.That(remote.ReachableFromBranch(one)).IsFalse()
            .Because("this is what the property protects against, and it is real: the commit "
                   + "attempt one's evidence points at is no longer led to by any ref, and "
                   + "the next garbage collection removes it.");
    }

    [Test]
    public async Task No_code_path_in_the_runner_can_rewrite_a_commit_on_a_remote()
    {
        // The structural half. The two tests above show what happens on the path they
        // drive; this one says there is no other path - a force-push added anywhere in the
        // runner would be found here rather than by a customer.
        var forcing = new Regex(
            @"""--force|""--force-with-lease|""\+refs|""\+HEAD|:\s*\+refs", RegexOptions.Compiled);

        var offenders = RunnerSources()
            .Where(f => forcing.IsMatch(File.ReadAllText(f)))
            .Select(f => Path.GetFileName(f)!)
            .ToList();

        await Assert.That(offenders).IsEmpty()
            .Because("nothing in this binary overwrites a commit in somebody else's "
                   + "repository. Found: " + string.Join(", ", offenders));

        await Assert.That(forcing.IsMatch("GitInvocation.Plain(\"push\", \"--force\", url)")).IsTrue()
            .Because("the scan can see one, so the emptiness above means something.");
    }

    // ---- and a push that would not fast-forward ----

    [Test]
    public async Task A_push_that_would_not_fast_forward_is_refused_rather_than_reported_pushed()
    {
        // SOMEBODY PUSHED TO THAT BRANCH BY HAND. A developer fixing it themselves between
        // attempts is a real case, and the branch has moved somewhere our commit does not
        // build on.
        //
        // Reporting this as pushed would be worse than failing: destination.pushed would
        // record a commit that is not on the remote at all, and a person would decide about
        // work they cannot fetch.
        using var remote = new PushFixture();

        remote.CommitAndPush("first attempt", "src/greet.py", "print('hello')\n");
        var byHand = remote.SomebodyElsePushes("src/greet.py", "print('fixed it myself')\n");

        var outcome = await remote.AttemptTwoPushes("src/greet.py", "print('attempt two')\n");

        await Assert.That(outcome).IsTypeOf<PushOutcome.Refused>()
            .Because("the branch moved under us, and the only ways forward are to rewrite "
                   + "somebody's work or to stop.");

        var refused = (PushOutcome.Refused)outcome;

        await Assert.That(refused.Diagnosis).Contains("moved")
            .Because("what it found, rather than what it wanted.");
        await Assert.That(refused.Diagnosis).Contains("refusing to rewrite")
            .Because("and what it did about it, so nobody has to guess whether the branch is "
                   + "in a half-written state.");

        await Assert.That(remote.RemoteTip()).IsEqualTo(byHand)
            .Because("and the person's commit is untouched, which is the point.");
    }

    [Test]
    public async Task A_branch_that_is_already_at_our_commit_is_not_an_error()
    {
        // The crash-recovery case, which must stay distinguishable from the one above: a
        // runner that pushed, died and came back finds its own commit on the remote.
        // Nothing has moved and nothing needs rewriting.
        using var remote = new PushFixture();

        var one = remote.CommitAndPush("first attempt", "src/greet.py", "print('hello')\n");
        var again = await remote.PushTheSameCommitAgain();

        await Assert.That(again).IsNotTypeOf<PushOutcome.Refused>()
            .Because("there is nothing wrong with a branch that is already where we were "
                   + "going to put it.");

        var commit = again switch
        {
            PushOutcome.Pushed(_, var sha) => sha,
            PushOutcome.AlreadyThere(_, var sha) => sha,
            _ => null,
        };

        await Assert.That(commit).IsEqualTo(one);
        await Assert.That(commit).IsEqualTo(remote.RemoteTip())
            .Because("and the commit it reports is the one on the remote. Reporting a local "
                   + "head that never got there would put a commit nobody can fetch into the "
                   + "evidence record.");
    }

    private static IEnumerable<string> RunnerSources()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Gg.sln")))
        {
            dir = dir.Parent;
        }

        var root = dir ?? throw new InvalidOperationException("Gg.sln not found");

        return Directory.EnumerateFiles(
                Path.Combine(root.FullName, "Gg.Runner"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                            StringComparison.Ordinal)
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                            StringComparison.Ordinal));
    }
}
