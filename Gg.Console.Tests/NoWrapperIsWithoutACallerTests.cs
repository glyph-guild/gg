using Gg.Client;

namespace Gg.Console.Tests;

/// <summary>
/// Every read the console offers is one it makes.
/// </summary>
/// <remarks>
/// <para>
/// <b>Step 1 built the ratchet and step 6 empties its list.</b> A list with a
/// reason per entry is a good instrument and a bad resting place: each entry
/// names a read whose method exists, whose name tells the next reader a pane
/// uses it, and which nothing calls.
/// </para>
/// <para>
/// <b>Three of the four resolve by DELETION, and that is the finding.</b> The
/// slice assumed a dead wrapper meant a missing pane. Two of them were
/// duplicate reads - the same request under a second name - and one was a read
/// the boot's own list already answers. A wrapper whose data is already in the
/// model is not a pane waiting to be built; it is a second way to ask, and a
/// console with two ways to ask the same question makes two requests.
/// </para>
/// </remarks>
public class NoWrapperIsWithoutACallerTests
{
    [Test]
    public async Task The_exemption_list_is_empty_but_for_the_slice_that_owns_it()
    {
        // S28.6-01. The one entry left is slice twenty-nine's by agreement -
        // its author asked for this ratchet knowing it would fire on them.
        var parked = ConsoleDataReachTests.Exempt.Keys
            .Where(k => !string.Equals(k, "RepositoriesAsync", StringComparison.Ordinal))
            .ToList();

        await Assert.That(parked).IsEmpty()
            .Because("this slice's own entries are resolved - wired or deleted, and no third "
                   + "option. Found: " + string.Join(", ", parked));
    }

    [Test]
    public async Task The_console_asks_for_the_fleet_once()
    {
        // BOTH WRAPPERS CALLED ListRunnersAsync. `gg runners` and
        // `gg runner labels` are one request rendered two ways; the console
        // renders labels from what the boot already fetched, so a second
        // wrapper was a second way to make a request it had already made.
        await Assert.That(Offers("RunnerLabelsAsync")).IsFalse()
            .Because("the fleet's labels are in AppState.Runners, put there by RunnersAsync "
                   + "at boot. A wrapper that fetched them again would be a request for data "
                   + "the model is already holding.");
    }

    [Test]
    public async Task The_console_asks_for_a_flight_once()
    {
        // `gg show` answers one FlightSummary. The boot fetches every flight's
        // summary in one list to derive the queue and keeps it, so the detail
        // under the cursor is a lookup rather than a request - which is what
        // makes an arrow key free, and what makes this wrapper a second way to
        // pay for something already bought.
        await Assert.That(Offers("ShowAsync")).IsFalse()
            .Because("AppState.Flights holds every summary, and Reducer.Detail picks one out "
                   + "of it with no I/O at all.");
    }

    [Test]
    public async Task Nothing_offers_a_read_whose_answer_no_pane_shows()
    {
        // The topology - envelope names and their roles - has no pane and no
        // plan for one. `RepositoriesAsync` answers the question a person
        // browsing actually has, and slice twenty-nine owns the pane for it.
        await Assert.That(Offers("AirspaceAsync")).IsFalse()
            .Because("wired to a pane or deleted, and no third option. Nobody asked for the "
                   + "topology, so it is deleted rather than parked.");
    }

    private static bool Offers(string method) =>
        typeof(ConsoleData).GetMethod(method) is not null;
}
