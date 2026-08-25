using Gg.Client;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// A flight that cannot start says so in the listing and the detail, with a
/// sentence DERIVED from the reason kind - and an unknown kind poisons the
/// render rather than blanking.
/// </summary>
/// <remarks>
/// Refusal at apply, waiting at flight: apply had an actor at the keyboard,
/// the queued flight has only whoever runs `gg flights`. The sentence is
/// Reason.Sentence's - one grammar, contract-side - so this render cannot
/// reword what a script asserted. Nothing renders when the member is null;
/// a kind this build does not know THROWS, because a renderer that shrugs
/// turns a governed refusal into silence.
/// </remarks>
public class WaitingKindRenderTests
{
    private static FlightSummary Flight(Reason? waiting) => new()
    {
        FlightId = "5a2b9f18-7e04-4c63-8d1a-b6f30e97c542",
        FlightNumber = "GG-42",
        Name = "payments",
        Intent = new FlightIntent { Kind = FlightIntentKinds.Text, Text = "fix it" },
        CreatedAt = new DateTimeOffset(2026, 8, 23, 9, 0, 0, TimeSpan.Zero),
        RunnerProtocolVersion = 1,
        FactVocabularyVersion = "1.0.0",
        ConstitutionVersion = "1.0.0",
        EnvelopeVersion = "v3",
        Attempts = 0,
        Facts = [],
        RequiredLabels = waiting is null ? [] : ["environment=aspire-payments"],
        Waiting = waiting,
    };

    private static Reason Waiting() =>
        Reason.For(ReasonKinds.NoRunnerAdvertises, ["environment=aspire-payments"]);

    private const string Sentence = "waiting: no runner advertises environment=aspire-payments";

    [Test]
    public async Task The_listing_derives_the_waiting_sentence_from_the_kind()
    {
        var text = VerbOutput.ToText(
            new VerbResult.Flights(new FlightList { Flights = [Flight(Waiting())] }));

        await Assert.That(text).Contains(Sentence);
    }

    [Test]
    public async Task The_detail_derives_the_waiting_sentence_from_the_kind()
    {
        var text = VerbOutput.ToText(new VerbResult.Flight(Flight(Waiting())));

        await Assert.That(text).Contains(Sentence);
    }

    [Test]
    public async Task A_flight_that_is_not_waiting_says_nothing_about_waiting()
    {
        var listing = VerbOutput.ToText(
            new VerbResult.Flights(new FlightList { Flights = [Flight(waiting: null)] }));
        var detail = VerbOutput.ToText(new VerbResult.Flight(Flight(waiting: null)));

        await Assert.That(listing).DoesNotContain("waiting");
        await Assert.That(detail).DoesNotContain("waiting");
    }

    [Test]
    public async Task An_unknown_kind_poisons_the_render_rather_than_blanking()
    {
        var flight = Flight(new Reason
        {
            Family = ReasonFamilies.Failed,
            Kind = "kind-this-build-does-not-know",
            Params = [],
        });

        await Assert.That(() => VerbOutput.ToText(new VerbResult.Flight(flight)))
            .Throws<InvalidOperationException>()
            .Because("a renderer that shrugs at a kind turns a governed refusal into "
                   + "silence, which reads as a healthy flight - Article XI's shape.");
    }
}
