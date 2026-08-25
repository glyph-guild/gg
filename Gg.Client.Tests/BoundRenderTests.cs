using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// A decline never wears a gap's clothes on any surface: the waiting line,
/// the flight view and the plan all derive the bound's own sentence, with
/// its clearing and — for a schedule — when it opens.
/// </summary>
/// <remarks>
/// <b>Rendered, never computed</b> — the reason arrives from the control
/// plane already decided, and every surface derives the same sentence
/// through the one contract-side grammar. The poison twin is
/// <c>WaitingKindRenderTests</c>' precedent one param deeper: a known kind
/// with an unknown clearing poisons the render rather than blanking, because
/// a blank waiting line reads as health.
/// </remarks>
public class BoundRenderTests
{
    private static FlightSummary Flight(Reason waiting) => new()
    {
        FlightId = "5a2b9f18-7e04-4c63-8d1a-b6f30e97c542",
        FlightNumber = "GG-77",
        Name = "payments",
        Intent = new FlightIntent { Kind = FlightIntentKinds.Text, Text = "fix it" },
        CreatedAt = new DateTimeOffset(2026, 8, 25, 9, 0, 0, TimeSpan.Zero),
        RunnerProtocolVersion = 1,
        FactVocabularyVersion = "1.0.0",
        ConstitutionVersion = "1.0.0",
        EnvelopeVersion = "v3",
        Attempts = 0,
        Facts = [],
        RequiredLabels = ["environment=aspire-payments"],
        Waiting = waiting,
    };

    [Test]
    public async Task A_capacity_bound_renders_as_a_decline_and_never_as_a_gap()
    {
        var text = VerbOutput.ToText(new VerbResult.Flights(new FlightList
        {
            Flights = [Flight(Reason.For(
                ReasonKinds.BlockedByBound, ["pool-maximum", BoundClearings.Capacity]))],
        }));

        await Assert.That(text).Contains("declined by your own bound");
        await Assert.That(text).Contains("pool-maximum");
        await Assert.That(text).DoesNotContain("no runner advertises")
            .Because("the two are the same silence with opposite remedies, and this one's "
                   + "remedy is a number in a document the reader already owns.");
    }

    [Test]
    public async Task A_schedule_bound_renders_with_its_opening_time()
    {
        var text = VerbOutput.ToText(new VerbResult.Flight(Flight(Reason.For(
            ReasonKinds.BlockedByBound,
            ["active-hours", BoundClearings.Schedule, "08:00Z"]))));

        await Assert.That(text).Contains("opens 08:00Z");
    }

    [Test]
    public async Task A_warming_pool_renders_its_own_state_not_a_gap_and_not_a_bound()
    {
        var text = VerbOutput.ToText(new VerbResult.Flight(Flight(Reason.For(
            ReasonKinds.PoolWarming, ["payments-pool"]))));

        await Assert.That(text).Contains("payments-pool");
        await Assert.That(text).Contains("warming");
        await Assert.That(text).DoesNotContain("no runner advertises");
        await Assert.That(text).DoesNotContain("declined");
    }

    [Test]
    public async Task The_plan_renders_the_declined_satisfier_with_the_bounds_sentence()
    {
        var text = VerbOutput.ToText(new VerbResult.Plan(new Checklist
        {
            EnvelopeVersion = "v3",
            Environment = "aspire-payments",
            RequiredLabels = ["environment=aspire-payments"],
            Items =
            [
                new ChecklistItem
                {
                    Requirement = "environment=aspire-payments",
                    Verification = "a live runner's advertised labels contain it",
                    Satisfier = ChecklistSatisfiers.DeclinedByBound,
                    WhenUnmet = Reason.For(
                        ReasonKinds.BlockedByBound, ["pool-maximum", BoundClearings.Capacity]),
                    Disposition = LabelDispositions.Stated,
                },
            ],
        }));

        await Assert.That(text).Contains("declined by your own bound");
        await Assert.That(text).DoesNotContain("capability gap")
            .Because("the satisfier word is the checklist's own decline marker, not the "
                   + "gap's clothes.");
    }

    /// <summary>WaitingKindRenderTests' poison, one param deeper.</summary>
    [Test]
    public async Task An_unknown_clearing_poisons_the_render_rather_than_blanking()
    {
        var poisoned = Assert.Throws<InvalidOperationException>(() =>
            VerbOutput.ToText(new VerbResult.Flight(Flight(Reason.For(
                ReasonKinds.BlockedByBound, ["spend-ceiling", "authority"])))));

        await Assert.That(poisoned!.Message).Contains("authority");
    }
}
