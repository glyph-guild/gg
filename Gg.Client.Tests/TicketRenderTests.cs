using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// A work item intent renders as a work item, on both surfaces that render one.
/// </summary>
/// <remarks>
/// <para>
/// <b>The fall-through is the defect this catches.</b> Both renderers switch on
/// the kind with <c>uri</c> as the only named arm and everything else falling
/// through to <c>Text</c> — so a ticket, whose <c>Text</c> is null by
/// construction, renders as an empty cell in the queue every person reads. A
/// new value in a closed vocabulary is exactly where a sensible default arm
/// stops being sensible and starts being a hole.
/// </para>
/// <para>
/// <b>The queue does not render the intent at all, and writing this found
/// that out.</b> <c>gg flights</c> shows the flight's NAME, which for a ticket
/// flight defaults to <c>provider#id</c> and so happens to read correctly — but
/// it reads correctly for a reason that has nothing to do with the intent, and
/// a test that asserted it there would have been proving the default name.
/// The two surfaces that really render an intent are <c>gg show</c> and the
/// console's flight pane, and the pane is one assembly over.
/// </para>
/// </remarks>
public class TicketRenderTests
{
    private static FlightSummary Flight(FlightIntent intent) => new()
    {
        FlightId = "5a2b9f18-7e04-4c63-8d1a-b6f30e97c542",
        FlightNumber = "GG-77",
        Name = "from-a-work-item",
        Intent = intent,
        CreatedAt = new DateTimeOffset(2026, 8, 30, 9, 0, 0, TimeSpan.Zero),
        RunnerProtocolVersion = 1,
        FactVocabularyVersion = "1.0.0",
        ConstitutionVersion = "1.0.0",
        EnvelopeVersion = "none",
        Attempts = 0,
        Facts = [],
    };

    private static FlightIntent Ticket => new()
    {
        Kind = FlightIntentKinds.Ticket,
        Provider = "a-tracker",
        Id = "4471",
    };

    private static string Shown(FlightIntent intent) =>
        VerbOutput.ToText(new VerbResult.Flight(Flight(intent)));

    [Test]
    public async Task A_shown_flight_renders_a_work_item_rather_than_a_blank()
    {
        var text = Shown(Ticket);

        await Assert.That(text).Contains("4471")
            .Because("a ticket whose Text is null by construction falls through to an empty "
                   + "cell, and an empty cell reads as a flight with no intent at all.");
        await Assert.That(text).Contains("a-tracker")
            .Because("the id alone is ambiguous across trackers, and the provider is the half "
                   + "that says which 4471 this is.");
    }

    [Test]
    public async Task The_two_kinds_that_already_rendered_still_do()
    {
        // The regression half, and the one a new switch arm threatens.
        await Assert.That(Shown(new FlightIntent
        {
            Kind = FlightIntentKinds.Text,
            Text = "fix the login bug",
        })).Contains("fix the login bug");

        await Assert.That(Shown(new FlightIntent
        {
            Kind = FlightIntentKinds.Uri,
            Uri = "https://example.invalid/issues/7",
        })).Contains("example.invalid");
    }

    [Test]
    public async Task Every_advertised_kind_renders_its_own_payload()
    {
        // THE TOTALITY ASSERTION, which is what stops the NEXT kind shipping
        // blank. A kind advertised in the vocabulary and rendered as nothing is
        // this slice's own subject arriving at a surface: registered, and
        // reached by nobody who could tell.
        foreach (var kind in FlightIntentKinds.All)
        {
            var (intent, needle) = kind switch
            {
                FlightIntentKinds.Uri => (
                    new FlightIntent { Kind = kind, Uri = "https://example.invalid/needle-uri" },
                    "needle-uri"),
                FlightIntentKinds.Ticket => (
                    new FlightIntent { Kind = kind, Provider = "tracker", Id = "needle-ticket" },
                    "needle-ticket"),
                _ => (new FlightIntent { Kind = kind, Text = "needle-text" }, "needle-text"),
            };

            await Assert.That(Shown(intent)).Contains(needle)
                .Because($"'{kind}' is advertised as a kind, so its payload must reach the page.");
        }
    }

    [Test]
    public async Task The_queue_shows_the_name_and_never_claimed_to_show_the_intent()
    {
        // WRITTEN BECAUSE THE FIRST VERSION OF THIS FILE ASSERTED OTHERWISE and
        // was wrong. A ticket flight's default name IS provider#id, so a queue
        // assertion would have passed for a reason that has nothing to do with
        // the intent - and would then have "covered" the render path while
        // measuring the naming default. Recorded as a test rather than as a
        // deleted assumption, so nobody re-adds it.
        var queue = VerbOutput.ToText(new VerbResult.Flights(
            new FlightList { Flights = [Flight(Ticket)] }));

        await Assert.That(queue).Contains("from-a-work-item");
        await Assert.That(queue).DoesNotContain("4471")
            .Because("the queue renders name, number, opened and state - and adding the intent "
                   + "to it is a surface decision nobody has made.");
    }
}
