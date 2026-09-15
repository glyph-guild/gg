using Gg.Console;
using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Console.Tests;

/// <summary>
/// A flight opened against a ticket can show that ticket, and one that was not
/// does not offer to.
/// </summary>
/// <remarks>
/// <para>
/// <b>REPORTED FROM USE: "it would also be good to be able to go directly to
/// the relevant browse modal ticket from a flight (not all flights are
/// associated with a ticket)".</b> The flight modal names the intent - it shows
/// <c>provider#id</c> - and getting from there to what that item actually says
/// meant leaving the modal, opening Browse, and finding the row by eye. The
/// item is already named; the console just would not go there.
/// </para>
/// <para>
/// <b>Offered only where it leads somewhere, which is the owner's choice.</b>
/// Two things have to be true: the flight's intent is a ticket, and a reader is
/// declared for that ticket's provider. A key advertised on a flight opened
/// from words would be a key that cannot do anything, and one advertised
/// against an undeclared provider would open a modal that can only say it
/// could not ask - which is Article XI's dead key either way. So it is not on
/// the hint line at all in either case.
/// </para>
/// <para>
/// <b>And the listing is left alone.</b> The item a flight names is usually not
/// on the page somebody last browsed, so this modal is about an item held
/// beside the listing rather than a row in it. A person who browsed, filtered,
/// then opened a flight's ticket still has their filtered page when they go
/// back.
/// </para>
/// </remarks>
public class OpenTheFlightsTicketTests
{
    private static FlightSummary Flight(FlightIntent intent) => new()
    {
        FlightId = "019fe815-6136-7518-bb57-b06d6d3f411a",
        FlightNumber = FlightRef.Format(118),
        Name = "the login form loses focus",
        Intent = intent,
        CreatedAt = DateTimeOffset.Parse("2026-09-14T10:00:00Z"),
        RunnerProtocolVersion = 1,
        FactVocabularyVersion = "0.31.0",
        ConstitutionVersion = "1.0.0",
        EnvelopeVersion = "v7",
        Attempts = 1,
        State = FlightStates.Open,
        Facts = [],
    };

    private static FlightIntent Ticket(string provider = "a-tracker") => new()
    {
        Kind = FlightIntentKinds.Ticket,
        Provider = provider,
        Id = "17864",
    };

    private static AppState Reading(FlightIntent intent, params string[] readers) => new()
    {
        ActiveTab = TabId.Flights,
        Mode = UiMode.FlightDetail,
        Flights = new FlightList { Flights = [Flight(intent)] },
        ReaderKeys = readers,
    };

    [Test]
    public async Task The_key_is_there_when_the_flight_names_a_ticket_a_reader_can_read()
    {
        var context = KeymapContext.For(Reading(Ticket(), "a-tracker"));

        await Assert.That(Keymap.Resolve(KeyStroke.Char('t'), context))
            .IsEqualTo(Command.OpenTheTicket);
    }

    [Test]
    public async Task And_it_is_on_the_hint_line_where_it_is_bound()
    {
        // THE HINTS COME FROM THE SAME CONTEXT DISPATCH DOES, which is what
        // stops this advertising a key it would refuse.
        var hints = Keymap.Hints(KeymapContext.For(Reading(Ticket(), "a-tracker")));

        // THE WHOLE HINT, NOT THE LETTER. "t " alone is in "ground it ·" and
        // in half the other descriptions on this line.
        await Assert.That(hints).Contains("t the ticket");
    }

    [Test]
    public async Task A_flight_opened_from_words_does_not_offer_it()
    {
        // NOT DISABLED - ABSENT. There is no ticket to go to, so the key is not
        // a thing that fails; it is a thing that was never offered.
        var words = new FlightIntent { Kind = FlightIntentKinds.Text, Text = "fix the login bug" };
        var context = KeymapContext.For(Reading(words, "a-tracker"));

        await Assert.That(Keymap.Resolve(KeyStroke.Char('t'), context)).IsNull();
        await Assert.That(Keymap.Hints(context)).DoesNotContain("the ticket");
    }

    [Test]
    public async Task And_neither_does_a_ticket_no_reader_here_can_read()
    {
        // THE SECOND HALF OF THE OWNER'S RULE. The flight names a tracker this
        // machine has no reader for - a colleague's, or one nobody has
        // configured yet. Opening a modal to say "no reader was declared" is a
        // key that leads to a sentence, and the sentence is already on the
        // flight: the intent reads `someone-elses#17864`.
        var context = KeymapContext.For(Reading(Ticket("someone-elses"), "a-tracker"));

        await Assert.That(Keymap.Resolve(KeyStroke.Char('t'), context)).IsNull();
    }

    [Test]
    public async Task With_no_reader_at_all_it_is_not_offered_either()
    {
        var context = KeymapContext.For(Reading(Ticket()));

        await Assert.That(Keymap.Resolve(KeyStroke.Char('t'), context)).IsNull();
    }

    [Test]
    public async Task Taking_it_opens_the_modal_about_that_items_id()
    {
        var after = Reducer.Reduce(Reading(Ticket(), "a-tracker"), Command.OpenTheTicket);

        await Assert.That(after.Mode).IsEqualTo(UiMode.WorkItemDetail);
        await Assert.That(after.WorkItemId).IsEqualTo("17864");
        await Assert.That(WorkItemDetails.Item(after)!.Id).IsEqualTo("17864")
            .Because("the modal is about the item the flight named, and every pane in it reads "
                   + "the item through this.");
    }

    [Test]
    public async Task The_page_somebody_was_browsing_is_still_there_afterwards()
    {
        // THE COST THIS AVOIDS. Seeding the listing with one row would have
        // been the short way to give the modal an item, and it would have
        // thrown away a filtered page somebody assembled - silently, and only
        // noticed the next time they pressed `b`.
        var browsing = Reading(Ticket(), "a-tracker") with
        {
            Browse = new BrowseListing
            {
                ProviderKey = "a-tracker",
                Items =
                [
                    new BrowseRow { Id = "11111", Title = "Something else", State = "New" },
                ],
            },
            BrowseSelected = 0,
        };

        var after = Reducer.Reduce(browsing, Command.OpenTheTicket);

        await Assert.That(after.Browse!.Items).Count().IsEqualTo(1);
        await Assert.That(after.Browse!.Items[0].Id).IsEqualTo("11111");
        await Assert.That(WorkItemDetails.Item(after)!.Id).IsEqualTo("17864")
            .Because("the modal is about the flight's ticket and not about the row the browse "
                   + "cursor happens to be on.");
    }

    [Test]
    public async Task Closing_it_puts_the_cursor_back_where_the_listing_had_it()
    {
        var after = Reducer.Reduce(
            Reducer.Reduce(Reading(Ticket(), "a-tracker"), Command.OpenTheTicket),
            Command.CloseModal);

        await Assert.That(after.WorkItemRow).IsNull()
            .Because("an item held beside the listing outliving the modal would make the next "
                   + "browse modal open about it instead of the row under the cursor.");
    }

    [Test]
    public async Task The_inventory_of_an_item_opened_this_way_is_the_trackers_own_names()
    {
        // THE SEVEN ARE A LISTING'S COLUMNS, and this item did not come from a
        // listing. Emitting them anyway would put `title` on screen as a blank
        // row directly above the tracker's own System.Title with the title in
        // it, which reads as a console that lost them.
        var opened = Reducer.Reduce(Reading(Ticket(), "a-tracker"), Command.OpenTheTicket) with
        {
            WorkItemFields =
            [
                new("System.Title", "Sonar cleanup: correctness risks"),
                new("Microsoft.VSTS.Scheduling.StoryPoints", "5"),
            ],
        };

        var names = WorkItemDetails.AllFields(opened).Select(r => r.Name).ToList();

        await Assert.That(names).Contains("System.Title");
        await Assert.That(names).DoesNotContain("areaPath")
            .Because("a blank row for a column this item was never listed under is an absence "
                   + "the console invented.");
    }
}
