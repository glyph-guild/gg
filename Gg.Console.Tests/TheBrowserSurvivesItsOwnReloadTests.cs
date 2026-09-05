using Gg.Console;
using Gg.Local;

namespace Gg.Console.Tests;

/// <summary>
/// Flying from the browser does not close the browser.
/// </summary>
/// <remarks>
/// <para>
/// <b>S29.5-04's other half.</b> Slice twenty-eight covers that a flight opened
/// from a browsed row grows the queue. Nobody covered the reverse: that the
/// pane which triggered the reload is still there afterwards. It very nearly
/// was not — <c>Reloaded</c> carried six named fields over a boot-shaped state,
/// so <c>BrowseVisible</c>, <c>Browse</c> and <c>BrowseSelected</c> all reset,
/// and flying from the browser closed the browser.
/// </para>
/// <para>
/// <b>THE DOUBLE THREADS, AND THAT IS THE POINT OF THE TEST.</b> A reload that
/// returns a whole state is a BOOT, and a boot passes every assertion about the
/// queue while emptying every pane in production - which is exactly the shape
/// the real one had. So the double here assigns onto the state it was handed,
/// the way <c>ConsoleStart.LoadAsync</c> now does. A future double that stops
/// threading makes this file go red, which is the only reason it can be trusted.
/// </para>
/// </remarks>
public class TheBrowserSurvivesItsOwnReloadTests
{
    private sealed class Opens : IConsoleActions
    {
        public string Decide(string flight, string obligation, bool approved, string? reason) => "";

        public string Fly(string intent) => "opened";

        public string FlyTicket(string provider, string id) => $"Opened a flight for {provider}#{id}.";

        public string? AlreadyFlown(string provider, string id) => null;

        public string AddCredential() => "";

        public string ForgetCredential() => "";

        public string Invite() => "";
    }

    private sealed class Browses : IWorkBrowser
    {
        public string? Key => "a-tracker";

        public Task<BrowseOutcome> BrowseAsync(string? cursor, int limit, CancellationToken token) =>
            Task.FromResult<BrowseOutcome>(new BrowseOutcome.Listed(new WorkItemPage(
                [
                    new WorkItemSummary("18398", "A draft job fails", "New", "", null),
                    new WorkItemSummary("18471", "Story 3", "Active", "", null),
                ],
                null)));
    }

    /// <summary>
    /// What the read plane answers, threaded onto what it was handed.
    /// </summary>
    /// <remarks>
    /// The read plane's fields are assigned; nothing else is named, so nothing
    /// else can be lost by forgetting to name it. This mirrors the loader
    /// rather than inventing a second shape.
    /// </remarks>
    private static AppState Reload(AppState current) => current with
    {
        Queue =
        [
            new QueueRow
            {
                FlightId = "f-1",
                FlightNumber = "gg-1",
                Name = "something the boot found",
                Reason = QueueReason.AwaitingDecision,
                Since = DateTimeOffset.UnixEpoch,
            },
        ],
    };

    private static AppState AfterFlyingFromTheBrowser() =>
        new ConsoleLoop(
            new ConsoleDoubles.TypesKeys(Command.ToggleBrowse, Command.SelectNext, Command.FlyPicked),
            new ConsoleDoubles.NoEditor(),
            actions: new Opens(),
            browser: new Browses(),
            reload: Reload)
        .Run(new AppState());

    [Test]
    public async Task The_browser_is_still_open_after_flying_from_it()
    {
        // THE ONE A PERSON WOULD NOTICE FIRST. Pressing fly and watching the
        // list you were reading disappear is not a subtle regression.
        await Assert.That(AfterFlyingFromTheBrowser().BrowseVisible).IsTrue();
    }

    [Test]
    public async Task The_listing_is_still_there_after_flying_from_it()
    {
        // Re-reading costs a whole session rebuild on this path, so losing the
        // listing is not just a redraw - it is a tracker round trip a person
        // did not ask for.
        var after = AfterFlyingFromTheBrowser();

        await Assert.That(after.Browse).IsNotNull();
        await Assert.That(after.Browse!.Items).Count().IsEqualTo(2);
        await Assert.That(after.Browse.ProviderKey).IsEqualTo("a-tracker");
    }

    [Test]
    public async Task The_row_the_person_picked_is_still_picked()
    {
        var after = AfterFlyingFromTheBrowser();

        await Assert.That(after.BrowseSelected).IsEqualTo(1)
            .Because("a cursor reset to the top after flying makes the next fly the wrong item.");
    }

    [Test]
    public async Task What_the_flight_did_is_still_on_the_screen()
    {
        // A write whose receipt is wiped by the read it triggered is a write
        // that tells the person nothing.
        var after = AfterFlyingFromTheBrowser();

        await Assert.That(after.LastFlightOpened).IsNotNull();
        await Assert.That(after.LastFlightOpened!).Contains("18471");
    }

    [Test]
    public async Task And_the_reload_actually_happened()
    {
        // THE LIVENESS ANCHOR, and this file needs it more than most: every
        // assertion above passes trivially against a reload that never ran, and
        // a reload that never ran is precisely the bug in the neighbourhood.
        var after = AfterFlyingFromTheBrowser();

        await Assert.That(after.Queue).Count().IsEqualTo(1);
        await Assert.That(after.Queue[0].FlightNumber).IsEqualTo("gg-1")
            .Because("if the queue is empty the reload was never called and this file "
                   + "is asserting nothing at all.");
    }
}
