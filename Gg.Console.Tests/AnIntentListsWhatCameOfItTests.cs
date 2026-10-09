using Gg.Client;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// From a work item to everything that came of it, and back: the modal's <b>Flights</b> tab lists
/// the flights about the item and the plans whose legs are, <c>f</c> goes to a flight, and the
/// flight's <c>t</c> comes back (owner, 2026-10-08: "proper traceability into flights so we can
/// still jump back to the ticket").
/// </summary>
public class AnIntentListsWhatCameOfItTests
{
    private static FlightSummary Flight(int n, string number, string name) =>
        new AConsolePlane().AFlight(n) with
        {
            FlightNumber = number,
            Name = name,
            Intent = FlightIntent.Of(null, provider: "ado", id: "18678"),
        };

    private static readonly FlightSummary Upgrade = Flight(991, "GG-991", "ITN-63 · Storybook 10 upgrade");
    private static readonly FlightSummary Check = Flight(990, "GG-990", "ITN-63 · Storybook 10 visual check");

    private static AppState OpenOn18678(WorkItemTab tab = WorkItemTab.Flights) => new()
    {
        Mode = UiMode.WorkItemDetail,
        ActiveTab = TabId.Intents,
        BrowseVisible = true,
        ReaderKeys = ["ado"],
        WorkItemTab = tab,
        Browse = new BrowseListing
        {
            ProviderKey = "ado",
            Items = [new BrowseRow { Id = "18678", Title = "Storybook", State = "Active" }],
        },
    };

    [Test]
    public async Task Intent_keys_have_one_spelling()
    {
        await Assert.That(IntentKeys.Of(FlightIntent.Of(null, provider: "ado", id: "18678"))).IsEqualTo("ado#18678");
        await Assert.That(IntentKeys.Of(FlightIntent.Of(null, uri: "https://example.test/x"))).IsEqualTo("https://example.test/x");
        await Assert.That(IntentKeys.Of(FlightIntent.ForFile("JDX/JDNext", "docs/a.md", "main"))).IsEqualTo("JDX/JDNext:docs/a.md@main");
        await Assert.That(IntentKeys.Of(FlightIntent.ForFile("JDX/JDNext", "docs/a.md", null))).IsEqualTo("JDX/JDNext:docs/a.md");
        await Assert.That(IntentKeys.Of(FlightIntent.Of("a sentence"))).IsNull();
    }

    [Test]
    public async Task The_modal_has_a_flights_tab_after_fields()
    {
        var at = OpenOn18678(WorkItemTab.Fields);
        at = Reducer.Reduce(at, Command.NextWorkItemTab);
        await Assert.That(at.WorkItemTab).IsEqualTo(WorkItemTab.Flights);
        await Assert.That(Reducer.Reduce(at, Command.NextWorkItemTab).WorkItemTab).IsEqualTo(WorkItemTab.Actions);
    }

    [Test]
    public async Task Opening_an_item_reads_what_came_of_it()
    {
        var asked = new List<string>();
        var plans = new BoardPage
        {
            IncludedEnded = true,
            Nominations =
            [
                new NominationSummary
                {
                    NominationId = Guid.NewGuid(), Nominator = "itinerary:a", Subject = "leg:x", Version = "v",
                    WorkKind = "implement", Mode = "auto", State = "opened", ItineraryNumber = "ITN-63",
                    IntentKey = "ado#18678", MadeAt = DateTimeOffset.UnixEpoch,
                },
                new NominationSummary
                {
                    NominationId = Guid.NewGuid(), Nominator = "itinerary:b", Subject = "leg:y", Version = "v",
                    WorkKind = "implement", Mode = "auto", State = "opened", ItineraryNumber = "ITN-12",
                    IntentKey = "ado#1", MadeAt = DateTimeOffset.UnixEpoch,
                },
            ],
        };

        var patched = ConsoleIntents.CameOfPatch(
            key => { asked.Add(key); return [Upgrade, Check]; },
            () => plans,
            OpenOn18678())(OpenOn18678());

        await Assert.That(asked).IsEquivalentTo(["ado#18678"]);
        await Assert.That(patched.WorkItemFlights!.Select(f => f.FlightNumber)).IsEquivalentTo(["GG-991", "GG-990"]);
        await Assert.That(patched.WorkItemPlans).IsEquivalentTo(["ITN-63"])
            .Because("only the plans whose legs are about this item.");
    }

    [Test]
    public async Task The_cursor_moves_on_the_flights_tab_and_f_goes_to_the_flight()
    {
        var state = OpenOn18678() with
        {
            WorkItemFlights = [Upgrade, Check],
            Flights = new FlightList { Flights = [Check, Upgrade] },
        };

        var moved = Reducer.Reduce(state, Command.SelectNext);
        await Assert.That(moved.WorkItemFlightSelected).IsEqualTo(1);
        await Assert.That(WorkItemDetails.FlightInTheList(moved)).IsEqualTo(Check.FlightId);

        await Assert.That(Keymap.Resolve(KeyStroke.Char('f'), KeymapContext.For(moved)))
            .IsEqualTo(Command.GoToTheFlight);

        var gone = Reducer.Reduce(moved, Command.GoToTheFlight);
        await Assert.That(gone.Mode).IsEqualTo(UiMode.Normal);
        await Assert.That(gone.ActiveTab).IsEqualTo(TabId.Flights);
        await Assert.That(Rows.Flights(gone)[gone.FlightSelected].FlightId).IsEqualTo(Check.FlightId);
    }

    [Test]
    public async Task The_tab_says_what_came_of_it_and_when_nothing_has()
    {
        var some = PaneText.WorkItemCameOf(OpenOn18678() with { WorkItemFlights = [Upgrade], WorkItemPlans = ["ITN-63"] });
        await Assert.That(some).Contains("GG-991");
        await Assert.That(some).Contains("ITN-63");

        var none = PaneText.WorkItemCameOf(OpenOn18678() with { WorkItemFlights = [] });
        await Assert.That(none).Contains("Nothing has been flown or planned from ado#18678 yet");
    }
}
