namespace Gg.Console.Tests;

/// <summary>
/// The itineraries tab sits second on the bar, straight after the queue (owner's call,
/// 2026-10-07): a plan is where the queue's work comes from.
/// </summary>
public class TheItinerariesTabFollowsTheQueueTests
{
    [Test]
    public async Task Itineraries_is_the_tab_after_the_queue()
    {
        await Assert.That(Tabs.All.Take(2)).IsEquivalentTo((TabId[])[TabId.Queue, TabId.Itineraries]);
        await Assert.That(Tabs.All[1]).IsEqualTo(TabId.Itineraries);
    }

    [Test]
    public async Task The_rest_keep_their_order()
    {
        await Assert.That(string.Join(" ", Tabs.All.Skip(2))).IsEqualTo(
            "Board Flights Runners Browse Credentials Envelope Allowances");
    }
}
