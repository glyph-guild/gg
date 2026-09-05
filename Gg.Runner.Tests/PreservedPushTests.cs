using Gg.Contracts.Description;
using Gg.Contracts;
using Gg.Runner.Execution;
using Gg.Runner.Vcs;

namespace Gg.Runner.Tests;

/// <summary>
/// Work kept so somebody can take the flight over, and what the runner says it did.
/// </summary>
/// <remarks>
/// <para>
/// <b>The control plane decides; the runner reports.</b> Whether unadmitted work may
/// reach the forge is the envelope's answer, computed control-plane side and
/// travelling as a branch name - so nothing here derives a governance answer.
/// <c>DestinationBranch.IsHandoff</c> exists precisely so this reports which KIND of
/// push it made without matching a prefix at the call site.
/// </para>
/// <para>
/// <b>And the tree stays.</b> A pushed branch is normally the end of the tree's
/// usefulness - the work is somewhere a person can fetch, so the tree is released.
/// A preservation is different: the transcript is on THIS machine and cannot cross,
/// so releasing the tree would destroy the one artifact the seed can only point at.
/// </para>
/// </remarks>
public class PreservedPushTests
{
    /// <summary>A push the control plane granted for HANDOFF rather than for landing.</summary>
    private static BranchPush APreservation(GitFixture fixture) => new()
    {
        // gg/handoff/GG-1042. The control plane chose it, because a flight preserved
        // for handoff and the same flight later admitted must not fight over one ref.
        Branch = DestinationBranch.ForHandoff(FlightRef.Format(1042)),
        BaseRef = "main",
        Slug = fixture.BarePath,
        Reason = "This flight violates 'scope-respected'. The envelope permits unadmitted work to "
               + "be kept, so the work is pushed to a handoff branch and nobody is asked.",
    };

    /// <summary>An ordinary push under a pending human gate.</summary>
    private static BranchPush AGatedPush(GitFixture fixture) => new()
    {
        Branch = DestinationBranch.For(FlightRef.Format(1042)),
        BaseRef = "main",
        Slug = fixture.BarePath,
        Reason = "no machine obligation is violated, so the work may be preserved on the remote",
    };

    [Test]
    public async Task A_preserved_push_says_so_on_the_fact_that_names_it()
    {
        // A gg/ branch with no pull request is not a proposal, and a reader counting
        // this platform's branches has to be able to tell work that was admitted
        // from work that was merely kept - they mean opposite things about whether
        // anybody is expected to review it.
        using var fixture = new GitFixture();
        var (_, trees, protocol, _) = await PreserveFixture.RunAsync(
            fixture, APreservation(fixture));
        using var _t = trees;

        var pushed = protocol.ShippedFacts
            .SelectMany(b => b.Items)
            .Where(f => f.Kind == FactKinds.DestinationPushed)
            .Select(f => f.Pushed!)
            .ToList();

        await Assert.That(pushed.Count).IsEqualTo(1);
        await Assert.That(pushed[0].Preserved).IsTrue()
            .Because("nothing else in the record distinguishes a branch kept for a takeover from "
                   + "one waiting on a decision, and the two ask different things of a reader.");
        await Assert.That(pushed[0].Branch).IsEqualTo(DestinationBranch.ForHandoff(FlightRef.Format(1042)));
    }

    [Test]
    public async Task An_ordinary_gated_push_does_not_claim_to_be_preserved()
    {
        // THE POISON TWIN. A runner that marked every push preserved would satisfy
        // the assertion above and make the distinction worthless - and it would do
        // it in the misleading direction, reporting work awaiting review as work
        // nobody is expected to look at.
        using var fixture = new GitFixture();
        var (_, trees, protocol, _) = await PreserveFixture.RunAsync(fixture, AGatedPush(fixture));
        using var _t = trees;

        var pushed = protocol.ShippedFacts
            .SelectMany(b => b.Items)
            .Where(f => f.Kind == FactKinds.DestinationPushed)
            .Select(f => f.Pushed!)
            .Single();

        await Assert.That(pushed.Preserved is true).IsFalse()
            .Because("a push on the ordinary path is not a preservation, and absent means what it "
                   + "always meant.");
    }

    [Test]
    public async Task A_preserved_push_keeps_the_tree_because_the_transcript_is_only_here()
    {
        // THE REASON THE TREE STAYS. A pushed branch normally ends the tree's
        // usefulness: the work is somewhere a person can fetch. A preservation is
        // different - the transcript is on THIS machine, ArtifactScopes has one
        // value, and the seed can only ever point at it. Releasing the tree would
        // destroy the one artifact a person taking the flight over might walk to
        // another machine for.
        using var fixture = new GitFixture();
        var (destination, trees, _, observer) = await PreserveFixture.RunAsync(
            fixture, APreservation(fixture));
        using var _t = trees;

        await Assert.That(destination.Calls.Any(c => c.StartsWith("push", StringComparison.Ordinal)))
            .IsTrue()
            .Because("the push happened, or this test is about a flight that pushed nothing.");

        await Assert.That(trees.Handoff.Held().Count).IsEqualTo(1)
            .Because("the branch is authoritative for the CODE and the tree is still the only place "
                   + "the transcript is.");
    }

    [Test]
    public async Task An_ordinary_gated_push_releases_the_tree_as_it_always_did()
    {
        // The twin, and the one that stops the change above becoming "hold
        // everything". A gated flight's tree is released because its work reached
        // the remote and a person decides from the commit - holding every gated
        // flight's tree would fill a runner's disk with copies of work nobody needs
        // locally.
        using var fixture = new GitFixture();
        var (_, trees, _, _) = await PreserveFixture.RunAsync(fixture, AGatedPush(fixture));
        using var _t = trees;

        await Assert.That(trees.Handoff.Held()).IsEmpty()
            .Because("its work is on the remote and a decision is about the commit, so the tree is "
                   + "finished with - which is what it has always done.");
    }

    [Test]
    public async Task The_runner_says_the_held_tree_is_a_cache_and_the_branch_is_authoritative()
    {
        // THE FIRST DANGER THIS SLICE NAMED: two trees, one branch, and the loser is
        // silent. After a portable handoff this machine still holds a tree, and
        // nothing told it the work has moved somewhere anybody can fetch.
        //
        // Said in the runner's own narration rather than kept as state, because
        // there is no reader for a staleness flag yet and a value nothing consults
        // is not a warning. A person reading this runner's output is the reader.
        using var fixture = new GitFixture();
        var (_, trees, _, observer) = await PreserveFixture.RunAsync(
            fixture, APreservation(fixture));
        using var _t = trees;

        var held = observer.Events.Single(e => e.StartsWith("held:", StringComparison.Ordinal));

        await Assert.That(held).Contains("preserved")
            .Because("a held tree after a preservation is a CACHE - the branch has the code - and a "
                   + "person who reuses it without knowing that is the second failure mode of this "
                   + "slice: two people confidently editing divergent copies.");
    }
}
