using Gg.Contracts;
using Gg.Contracts.Authoring;

namespace Gg.Client.Tests;

/// <summary>
/// <b>S63.1-01</b> - a draft written by the store is read back by
/// <see cref="EnvelopeYaml.ParseItinerary"/> as the same draft.
/// </summary>
/// <remarks>
/// <b>There is no second format</b> (slice sixty-three rule 1). A draft can always be checked
/// with <c>gg itinerary check</c>, committed, or flown by hand, because it is the plan file.
/// </remarks>
public class ADraftIsThePlanFileTests
{
    private static readonly FlightNomination[] Legs =
    [
        new()
        {
            Subject = "the icon",
            WorkKind = "implement",
            Reason = "the ticket names the icon fix on its own",
            Repository = "JDNext",
            Environment = "dev",
        },
        new()
        {
            Subject = "the padding: shared, and \"quoted\"",
            WorkKind = "implement",
            Reason = "a shared padding change,\nseparate from the icon",
            After = "the icon",
            Note = "1.10 is not a float",
        },
    ];

    public static IEnumerable<Func<FlightIntent>> Intents() =>
    [
        () => FlightIntent.Of("three findings in one bug"),
        () => FlightIntent.Of("line one\nline two: with a colon"),
        () => FlightIntent.Of(null, "https://example.invalid/issues/7"),
        () => FlightIntent.Of(null, null, "ado", "18291"),
        () => FlightIntent.ForFile("JDX/JDNext", "docs/plans/18291.md", "develop"),
        () => FlightIntent.ForFile("JDX/JDNext", "docs/plans/18291.md", null),
    ];

    [Test]
    [MethodDataSource(nameof(Intents))]
    public async Task What_the_store_writes_the_plan_parser_reads_back(FlightIntent intent)
    {
        var text = EnvelopeText.Render(new ItineraryDraft { Planner = "plan", Intent = intent, Legs = Legs });

        var parsed = EnvelopeYaml.ParseItinerary(text, defaultPlanner: "something-else");

        await Assert.That(parsed.Diagnosis).IsNull().Because(text);
        await Assert.That(parsed.Draft!.Planner).IsEqualTo("plan");
        await Assert.That(parsed.Draft.Intent).IsEqualTo(intent);
        await Assert.That(parsed.Draft.Legs).IsEquivalentTo(Legs);
    }

    [Test]
    public async Task An_unfinished_draft_is_read_without_being_judged()
    {
        // A DRAFT BEING BUILT HAS NO LEGS YET, and the plan parser refuses that - rightly, for a
        // plan somebody is about to check. The store reads the same text without that judgement,
        // so the first set_intent has somewhere to land.
        var text = EnvelopeText.Render(new ItineraryDraft
        {
            Planner = "plan",
            Intent = FlightIntent.Of("only an intent so far"),
            Legs = [],
        });

        var read = EnvelopeYaml.ReadItinerary(text, defaultPlanner: "plan");

        await Assert.That(read.Diagnosis).IsNull().Because(text);
        await Assert.That(read.Intent).IsEqualTo(FlightIntent.Of("only an intent so far"));
        await Assert.That(read.Legs).IsEmpty();
    }
}
