using Gg.Cli;
using Gg.Client;
using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Cli.Tests;

/// <summary>
/// <c>gg show</c> on a classify flight says what it nominated, and where the
/// flight it opened can be found.
/// </summary>
/// <remarks>
/// <para>
/// <b>A pointer for a person is not a reference the system reads.</b> Neither
/// flight holds a field naming the other — asserted as an absence over the
/// command that opens one — so this rendering cannot print the opened flight's
/// number, and must not grow a field to make it able to. What it can do is name
/// the KIND that was nominated, the reason, and the query that groups the two:
/// correlation is the work item, and that is the whole of the linkage.
/// </para>
/// <para>
/// <b>The reason is the half worth reading.</b> "A flight was opened" is a
/// chore; <i>opened a research flight because the item names the failing
/// endpoint and the file</i> is a record. It arrives on the summary the verb
/// already returns, so the JSON and this rendering stay two views of one
/// document and there is no second fetch route.
/// </para>
/// <para>
/// <b>And the command it prints has to work.</b> A rendering that told somebody
/// to run <c>gg flights --intent</c> with a value that verb refuses would be
/// worse than saying nothing — so the parse is asserted here too, in the same
/// file, rather than trusted one layer down.
/// </para>
/// </remarks>
public class ShowNamesWhatItOpenedTests
{
    private const string Nominated = "research";

    private const string Because =
        "the item names the failing endpoint and the file, so the cause is already known";

    private static FlightSummary AClassifyFlight(FlightIntent intent) => new()
    {
        FlightId = "019fe815-6136-7518-bb57-b06d6d3f411a",
        FlightNumber = FlightRef.Format(41),
        Name = "what kind of work is acme#812",
        Intent = intent,
        CreatedAt = new DateTimeOffset(2026, 9, 4, 12, 0, 0, TimeSpan.Zero),
        RunnerProtocolVersion = 1,
        FactVocabularyVersion = "0.18.0",
        ConstitutionVersion = "1.0.0",
        EnvelopeVersion = "v1",
        Attempts = 1,
        Facts =
        [
            new FactEnvelope
            {
                IdempotencyKey = "nominated-once",
                Kind = FactKinds.FlightNomination,
                Digest = new string('b', 64),
                ObservedAt = new DateTimeOffset(2026, 9, 4, 12, 0, 1, TimeSpan.Zero),
                Nomination = new FlightNomination
                {
                    WorkKind = Nominated,
                    Reason = Because,
                },
            },
        ],
    };

    private static FlightIntent ATicket() => new()
    {
        Kind = FlightIntentKinds.Ticket,
        Provider = "acme",
        Id = "812",
    };

    private static string Shown(FlightIntent intent) =>
        VerbOutput.ToText(new VerbResult.Flight(AClassifyFlight(intent)));

    [Test]
    public async Task It_names_the_kind_it_nominated_and_why()
    {
        var text = Shown(ATicket());

        await Assert.That(text).Contains(Nominated)
            .Because("the nomination is what the flight was for, and a rendering that showed "
                   + "the fact's kind and not its content would say a decision happened "
                   + "without saying what was decided.");
        await Assert.That(text).Contains("failing endpoint")
            .Because("the reason is the half worth reading. 'A flight was opened' is a chore.");
    }

    [Test]
    public async Task It_points_at_the_work_item_rather_than_at_a_flight()
    {
        var text = Shown(ATicket());

        await Assert.That(text).Contains("gg flights --intent acme#812")
            .Because("correlation is the work item, so the pointer is a query somebody can "
                   + "run - and it is built from this flight's OWN intent, which is the "
                   + "only thing the two flights share.");

        // THE ABSENCE, and it is the rule rather than an omission. A number
        // here would need a field naming the other flight, which is exactly
        // what ADR-0019 § 1 forbids and what the command that opens the flight
        // is asserted not to carry.
        await Assert.That(text).DoesNotContain("GG-42")
            .Because("a rendering cannot name a flight nothing told it about, and the field "
                   + "that would tell it is the reference this design refuses.");
    }

    [Test]
    public async Task A_uri_intent_is_pointed_at_by_its_uri()
    {
        var text = Shown(new FlightIntent
        {
            Kind = FlightIntentKinds.Uri,
            Uri = "https://example.test/boards/1/items/812",
        });

        await Assert.That(text)
            .Contains("gg flights --intent https://example.test/boards/1/items/812")
            .Because("a flight opened from a link is correlated by the link, and the verb "
                   + "takes one now. Printing the work-item form here would be a command "
                   + "that answers about nothing.");
    }

    [Test]
    public async Task A_flight_that_nominated_nothing_says_nothing_about_it()
    {
        // THE LIVENESS TWIN, on the renderer's own axis. A branch that printed
        // its heading unconditionally would satisfy every assertion above and
        // would tell every ordinary flight it had nominated something.
        var ordinary = AClassifyFlight(ATicket()) with { Facts = [] };

        var text = VerbOutput.ToText(new VerbResult.Flight(ordinary));

        await Assert.That(text.Contains("nominated", StringComparison.OrdinalIgnoreCase)).IsFalse()
            .Because("an implement flight has no nomination, and a heading with nothing "
                   + "under it reads as a decision nobody can see.");
    }

    // ---- and the command it prints is one the verb accepts ----

    [Test]
    public async Task The_verb_takes_a_work_item_and_a_uri_and_refuses_neither_form()
    {
        await Assert.That(CliArgs.Parse(["flights", "--intent", "acme#812"]))
            .IsTypeOf<CliAction.Flights>()
            .Because("the form the rendering prints for a ticket intent.");

        await Assert.That(CliArgs.Parse(
                ["flights", "--intent", "https://example.test/boards/1/items/812"]))
            .IsTypeOf<CliAction.Flights>()
            .Because("and the form it prints for a uri intent. The control plane correlates "
                   + "on the uri as written; a verb that refused it would leave a surface "
                   + "nobody could reach.");

        var refused = CliArgs.Parse(["flights", "--intent", "4471"]);

        await Assert.That(refused).IsTypeOf<CliAction.Unknown>()
            .Because("an id alone does not say which tracker it is in, and a filter nobody "
                   + "could satisfy that answered with everything is a wide answer to a "
                   + "narrow question.");
        await Assert.That(((CliAction.Unknown)refused).Message).Contains("#");
        await Assert.That(((CliAction.Unknown)refused).Message
                .Contains("uri", StringComparison.OrdinalIgnoreCase)).IsTrue()
            .Because("the refusal names both forms, or it sends somebody who typed a link "
                   + "away believing links cannot be asked about.");
    }
}
