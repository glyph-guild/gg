namespace Gg.Contracts.Tests;

using Gg.Contracts.Description;

/// <summary>
/// Every fact a flight recorded is readable, and says which budget held it.
/// </summary>
/// <remarks>
/// <para>
/// <b>A flight records nine facts and shows one.</b> Two of the thirteen kinds
/// reach a story - the loop's outcome and its question - and the outcome's
/// reason is cut to 280 characters where it is produced. So the machine's
/// account of a flight exists, is hashed, is what admission reads, and has no
/// reader.
/// </para>
/// <para>
/// <b>The sharpest form of it: a claim and a measurement read the same.</b> A
/// scoring flight's one visible line is the agent's own prose - <i>"Scored
/// 18119 and proposed Custom.HAL = 4"</i> - while the recorded proposal that
/// either backs it or does not is unreachable. Keeping those apart is the whole
/// job; a surface that shows only the first is the one place this system cannot
/// afford to be quiet.
/// </para>
/// <para>
/// <b>THE STORED ENVELOPE CROSSES, NOT A SUMMARY OF IT.</b>
/// <see cref="FactEnvelope"/> is already pinned and in the vocabulary because a
/// runner ships it, so this read adds a container and nothing else. A second
/// projection would be two computations of one question, and the one a reader
/// wants - which paths a manifest names, which fields a proposal sets - is
/// exactly what a summary drops.
/// </para>
/// <para>
/// <b>And the disposition travels with it</b>, because it is the answer to why
/// one row has no content. Inline, digest and reference are the control plane's
/// classification of what budget an item is held against;
/// <c>EvidenceDisposition</c> is not reachable from here, and a reader deriving
/// it would be keeping a second copy of that table.
/// </para>
/// </remarks>
public class AFlightsFactsAreReadableTests
{
    [Test]
    public async Task The_wire_declares_a_facts_read_beside_the_log_and_the_story()
    {
        var route = ProtocolSurface.Endpoints.SingleOrDefault(
            r => string.Equals(r.Path, "/v1/flights/{ref}/facts", StringComparison.Ordinal));

        await Assert.That(route).IsNotNull()
            .Because("the log and the story are declared here and this is the third "
                   + "question about one flight: what it actually recorded.");

        await Assert.That(route!.Method).IsEqualTo("GET");
        await Assert.That(route.Response).IsEqualTo(typeof(FlightFacts));
        await Assert.That(route.Audience).IsEqualTo(Audience.Developer)
            .Because("a flight's evidence is the tenant's, read by the person who flew it - "
                   + "the same audience the log and the story already have.");
    }

    [Test]
    public async Task A_recorded_fact_carries_the_envelope_and_the_budget_that_held_it()
    {
        // DECLARED, NOT DERIVED, on the rule this file's siblings follow: if each
        // side computed these from its own serializer they would agree with
        // themselves and prove nothing.
        await Assert.That(ProtocolSurface.JsonMembers[typeof(RecordedFact)])
            .IsEquivalentTo((string[])["fact", "disposition", "recordedAt"]);

        await Assert.That(ProtocolSurface.JsonMembers[typeof(FlightFacts)])
            .IsEquivalentTo((string[])["flightNumber", "facts"]);
    }

    [Test]
    public async Task The_envelope_is_reused_rather_than_summarised()
    {
        // THE POINT OF THE SHAPE. A reader that wanted the paths a manifest names
        // or the fields a proposal sets would be told to go and ask the runner,
        // which is the thing this read exists to stop being the answer.
        var fact = typeof(RecordedFact).GetProperty("Fact");

        await Assert.That(fact).IsNotNull();
        await Assert.That(fact!.PropertyType).IsEqualTo(typeof(FactEnvelope))
            .Because("the ledger stores the payload whole either way - disposition "
                   + "decides the budget, not what is kept - so the envelope is what "
                   + "there is to hand over.");
    }

    [Test]
    public async Task A_flight_that_recorded_nothing_answers_with_an_empty_list()
    {
        // ABSENT IS NOT EMPTY, and the difference is a flight nobody has flown
        // yet versus one whose runner shipped nothing. Both are real and the
        // list says so without a second field to disagree with.
        var none = new FlightFacts { FlightNumber = "GG-1", Facts = [] };

        await Assert.That(none.Facts).IsEmpty();
        await Assert.That(none.FlightNumber).IsEqualTo("GG-1");
    }
}
