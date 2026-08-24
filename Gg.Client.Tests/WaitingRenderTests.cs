using Gg.Client;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// A flight that cannot start says so in the listing and the detail - and a
/// flight that can says nothing.
/// </summary>
/// <remarks>
/// Refusal at apply, waiting at flight: apply had an actor at the keyboard,
/// the queued flight has only whoever runs `gg flights`. The sentence names
/// the labels no live runner advertises, because a name is what somebody can
/// act on. Nothing renders when the member is null - a "waiting: no" column
/// on every healthy flight would be noise somebody learns to skip.
/// </remarks>
public class WaitingRenderTests
{
    private static FlightSummary Flight(string? waiting) => new()
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

    private const string Sentence = "waiting: no runner advertises environment=aspire-payments";

    [Test]
    public async Task The_listing_carries_the_waiting_sentence()
    {
        var text = VerbOutput.ToText(
            new VerbResult.Flights(new FlightList { Flights = [Flight(Sentence)] }));

        await Assert.That(text).Contains(Sentence);
    }

    [Test]
    public async Task The_detail_carries_the_waiting_sentence()
    {
        var text = VerbOutput.ToText(new VerbResult.Flight(Flight(Sentence)));

        await Assert.That(text).Contains(Sentence);
    }

    [Test]
    public async Task A_flight_that_is_not_waiting_says_nothing_about_waiting()
    {
        var listing = VerbOutput.ToText(
            new VerbResult.Flights(new FlightList { Flights = [Flight(waiting: null)] }));
        var detail = VerbOutput.ToText(new VerbResult.Flight(Flight(waiting: null)));

        await Assert.That(listing.Contains("waiting", StringComparison.OrdinalIgnoreCase)).IsFalse();
        await Assert.That(detail.Contains("waiting", StringComparison.OrdinalIgnoreCase)).IsFalse()
            .Because("a waiting column on every healthy flight is noise somebody learns to skip.");
    }

    [Test]
    public async Task Json_carries_the_member()
    {
        var json = VerbOutput.ToJson(new VerbResult.Flight(Flight(Sentence)));

        await Assert.That(json).Contains("\"waiting\"");
        await Assert.That(json).Contains(Sentence);
    }
}
