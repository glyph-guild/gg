using Gg.Console;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// A flight whose intent is a link can be opened, wherever that link came from.
/// </summary>
/// <remarks>
/// <para>
/// <b>Reported from the live console: GG-153 was opened by a watch and had
/// nowhere to go.</b> A sweep nominates a work item by its url and the flight
/// carries <c>kind: uri</c> - <c>provider</c> and <c>id</c> are null - so the
/// ticket key, which needs both and a reader here, was never offered. The
/// flight was about a work item a person could see in a browser, and the
/// console's answer was nothing at all.
/// </para>
/// <para>
/// <b>One key, and the flight decides which it means.</b> A ticket a reader
/// here can read opens the item in a modal, because that is the answer without
/// leaving the console; anything else with a link opens the browser, which is
/// the console's existing way out to a page. The hint says which, so nothing is
/// advertised that would not happen.
/// </para>
/// <para>
/// <b>Not a second way to decide what a flight is about.</b> Matching the url
/// against a declared tracker to recover a provider and an id would be one -
/// and pulling an id out of a url is a shape per forge. The link is what the
/// flight carries, so the link is what opens.
/// </para>
/// </remarks>
public class AFlightCanOpenItsLinkTests
{
    private static FlightSummary Flight(FlightIntent intent) => new()
    {
        FlightId = "0e18955f-0919-7820-8c4d-aa50a43e1861",
        FlightNumber = FlightRef.Format(153),
        Name = "a work item a sweep found",
        Intent = intent,
        CreatedAt = DateTimeOffset.Parse("2026-09-18T04:27:10Z"),
        RunnerProtocolVersion = 1,
        FactVocabularyVersion = "0.31.0",
        ConstitutionVersion = "1.0.0",
        EnvelopeVersion = "v7",
        Attempts = 1,
        State = FlightStates.Landed,
        Facts = [],
    };

    /// <summary>GG-153's own intent, as the board opened it.</summary>
    private static FlightIntent ALink() => new()
    {
        Kind = FlightIntentKinds.Uri,
        Uri = "https://dev.azure.com/HRTMS/JDX/_workitems/edit/18490",
    };

    private static AppState Reading(FlightIntent intent, params string[] readers) => new()
    {
        ActiveTab = TabId.Flights,
        Mode = UiMode.FlightDetail,
        Flights = new FlightList { Flights = [Flight(intent)] },
        ReaderKeys = readers,
    };

    [Test]
    public async Task A_flight_opened_from_a_link_offers_the_key()
    {
        var context = KeymapContext.For(Reading(ALink()));

        await Assert.That(Keymap.Resolve(KeyStroke.Char('t'), context))
            .IsEqualTo(Command.OpenTheLink)
            .Because("GG-153 was about a page somebody could open, and the console offered "
                   + "nothing.");
    }

    [Test]
    public async Task And_says_so_on_the_hint_line()
    {
        var hints = Keymap.Hints(KeymapContext.For(Reading(ALink())));

        await Assert.That(hints).Contains("t the link")
            .Because("the same key means the ticket where there is one, so the line has to "
                   + "say which this is.");

        await Assert.That(hints).DoesNotContain("t the ticket");
    }

    [Test]
    public async Task A_ticket_a_reader_can_read_still_wins_the_key()
    {
        // THE MODAL BEATS THE BROWSER where both are possible: reading the item
        // without leaving the console is the better answer, and the browser is
        // still a keystroke away from there.
        var ticket = new FlightIntent
        {
            Kind = FlightIntentKinds.Ticket,
            Provider = "a-tracker",
            Id = "17864",
        };

        var context = KeymapContext.For(Reading(ticket, "a-tracker"));

        await Assert.That(Keymap.Resolve(KeyStroke.Char('t'), context))
            .IsEqualTo(Command.OpenTheTicket);
        await Assert.That(Keymap.Hints(context)).Contains("t the ticket");
    }

    [Test]
    public async Task A_flight_opened_from_words_still_offers_nothing()
    {
        // NOT DISABLED - ABSENT, and that half of the rule is unchanged: there
        // is nowhere to go from a sentence.
        var words = new FlightIntent { Kind = FlightIntentKinds.Text, Text = "fix the login bug" };
        var context = KeymapContext.For(Reading(words, "a-tracker"));

        await Assert.That(Keymap.Resolve(KeyStroke.Char('t'), context)).IsNull();
        await Assert.That(Keymap.Hints(context)).DoesNotContain("the link");
    }

    [Test]
    public async Task The_link_it_opens_is_the_flights_own()
    {
        await Assert.That(FlightDetails.LinkHere(Reading(ALink())))
            .IsEqualTo("https://dev.azure.com/HRTMS/JDX/_workitems/edit/18490");

        await Assert.That(FlightDetails.LinkHere(Reading(
                new FlightIntent { Kind = FlightIntentKinds.Text, Text = "words" })))
            .IsNull();
    }

    [Test]
    public async Task A_console_that_cannot_open_a_browser_says_so_rather_than_blinking()
    {
        // THE NULL PORT, which is a real deployment: the same answer the work
        // item's own key gives, because a keypress that does nothing reads as a
        // broken console.
        var said = ConsoleLoop.OpenedTheLink(Reading(ALink()), openUri: null);

        await Assert.That(said.LastRunner ?? "").Contains("browser");
    }

    [Test]
    public async Task And_hands_the_port_the_link_when_there_is_one()
    {
        var handed = "";
        var state = ConsoleLoop.OpenedTheLink(
            Reading(ALink()),
            openUri: (s, uri) =>
            {
                handed = uri;
                return s;
            });

        await Assert.That(handed)
            .IsEqualTo("https://dev.azure.com/HRTMS/JDX/_workitems/edit/18490");
        await Assert.That(state.LastRunner ?? "").DoesNotContain("browser");
    }
}
