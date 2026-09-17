namespace Gg.Contracts.Tests;

/// <summary>
/// A runner that manages a pool and never beats reads as <c>maintaining</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Asked from the fleet.</b> <c>vmlinux001:maintain</c> read
/// <c>offline, never seen</c> while it reported on its pool every five seconds,
/// because state was derived from heartbeats alone and a maintainer sends none.
/// A heartbeat is an offer to take work, so having it beat would have made it
/// read <c>idle</c> - a promise it never keeps.
/// </para>
/// <para>
/// <b>A state, where parking was not.</b> <c>ARunnerCanBeParkedTests</c> keeps
/// parking beside the state because state says what a runner IS DOING and
/// parking says what policy allows. Maintaining a pool is what this runner is
/// doing, so it is a state - the choice was put to a person and made that way.
/// </para>
/// </remarks>
public class AMaintainerIsAStateOfItsOwnTests
{
    [Test]
    public async Task Maintaining_is_in_the_vocabulary()
    {
        await Assert.That(RunnerStates.All).Contains("maintaining");
    }

    [Test]
    public async Task It_is_none_of_the_other_three()
    {
        var others = RunnerStates.All.Where(s => s != "maintaining").ToList();

        await Assert.That(others).IsEquivalentTo(
            new List<string> { RunnerStates.Offline, RunnerStates.Busy, RunnerStates.Idle })
            .Because("the vocabulary grows by exactly this word; parking still does not join it.");
    }
}
