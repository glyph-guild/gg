using Gg.Contracts;

namespace Gg.Runner.Tests;

/// <summary>
/// A runner whose turn is over and whose work landed nowhere says so.
/// </summary>
/// <remarks>
/// <para>
/// <b>The fleet's half of the pair.</b> <see cref="AttendedReturnTests"/> covers
/// the hand-flown side, where a person said they were finished and a gate they
/// opened was not answered. This is the same state reached without a person in
/// it: the control plane cleared the push and admitted nothing, because a
/// requirement is a human decision somebody has not made.
/// </para>
/// <para>
/// <b>It released <c>completed</c>, unconditionally, and that is what recorded a
/// landing on a flight that landed nowhere.</b> The fleet path chose its
/// disposition from whether the flight was attended and never from what the
/// landing said, so a push with no admission — the whole point of a gate — read
/// as a conclusion. <c>completed</c> maps to <c>landed</c>, the exit claim is
/// first-writer-wins, and nothing corrects it afterwards.
/// </para>
/// <para>
/// <b>Why not <c>abandoned</c>, which records no ending either.</b> Because it is
/// also what puts the flight back on the queue. A flight waiting on a person
/// handed to another runner is flown into the same refusal, asks the same
/// question again, and opens a second gate against the first one's work.
/// <c>outstanding</c> is terminal without being a conclusion, which is the pair
/// neither of the other two could say.
/// </para>
/// </remarks>
public class OutstandingReleaseTests
{
    [Test]
    public async Task A_pushed_flight_nobody_admitted_is_not_reported_as_completed()
    {
        using var fixture = new GitFixture();

        var (_, trees, protocol, _) = await PreserveFixture.RunAsync(fixture, new BranchPush
        {
            Branch = "gg/GG-1042",
            BaseRef = "refs/heads/main",
            Slug = "acme/widgets",
            Reason = "cleared to push",
        });
        trees.Dispose();

        await Assert.That(AttendedReturnTests.ReleasedWith(protocol))
            .IsEqualTo(RunnerDisposition.Outstanding)
            .Because("the push was granted and the proposal was not, which is a decision "
                   + "somebody has not made. Reporting `completed` records a landing on a "
                   + "flight that landed nowhere, and the exit claim is first-writer-wins.");
    }

    [Test]
    public async Task A_preserved_push_is_the_same_state_and_says_the_same_thing()
    {
        // A HANDOFF BRANCH IS NOT A DIFFERENT ANSWER. Preservation and a gated
        // push are both work on the remote that was admitted nowhere; what
        // separates them is who is expected to look at it, which the pushed
        // fact already records. Reading the branch name to choose a disposition
        // would put that decision in two places.
        using var fixture = new GitFixture();

        var (_, trees, protocol, _) = await PreserveFixture.RunAsync(fixture, new BranchPush
        {
            Branch = "gg/handoff/GG-1042",
            BaseRef = "refs/heads/main",
            Slug = "acme/widgets",
            Reason = "kept so somebody can take it over",
        });
        trees.Dispose();

        await Assert.That(AttendedReturnTests.ReleasedWith(protocol))
            .IsEqualTo(RunnerDisposition.Outstanding);
    }
}
