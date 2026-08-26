using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// Retiring a name is applying a terminal version of it — never a delete.
/// </summary>
/// <remarks>
/// <para>
/// <b>ADR-0014 is explicit, and the alternative is what makes it explicit.</b>
/// Retirement by deleting a topology entry would be governance-critical change
/// wearing bookkeeping's clothes: the constraint stops attaching and no version
/// anywhere records that it did. So the only way a name stops applying is a final
/// version on its own stream — gated, attributed and versioned like every other
/// envelope change.
/// </para>
/// <para>
/// <b>Which is why it is a POST to the name's own stream rather than a DELETE.</b>
/// A delete verb would say the document is going away; what actually happens is
/// that one more version is written, and the topology reflects it rather than
/// deciding it. <i>Unreachable becomes derived.</i>
/// </para>
/// <para>
/// <b>And it always gates.</b> A document that stops applying removes every
/// constraint in it at once, so retirement is a widening by construction —
/// registration's rule in the other direction, where direction is a constant of
/// the act rather than something computed.
/// </para>
/// </remarks>
public class RetirementTests
{
    [Test]
    public async Task Retirement_is_a_write_to_the_names_own_stream()
    {
        var retire = ProtocolSurface.Endpoints
            .Single(e => e.Path == "/v1/airspace/envelopes/{name}/retirement");

        await Assert.That(retire.Method).IsEqualTo("POST")
            .Because("retiring writes one more version; DELETE would say the document is "
                   + "going away, and it is not - it is being ended, on the record.");
        await Assert.That(retire.Audience).IsEqualTo(Audience.Developer);
    }

    [Test]
    public async Task Retirement_always_defers_and_never_lands()
    {
        var retire = ProtocolSurface.Endpoints
            .Single(e => e.Path == "/v1/airspace/envelopes/{name}/retirement");

        await Assert.That(retire.Statuses).Contains(202)
            .Because("a document that stops applying removes every constraint in it at "
                   + "once, so retirement is a widening by construction and rides a gate.");
        await Assert.That(retire.Statuses).DoesNotContain(200)
            .Because("there is no say-so path: 200 would be a retirement that landed "
                   + "without anybody deciding it.");
        await Assert.That(retire.Statuses).Contains(409)
            .Because("it takes a precondition like any other apply - a working copy "
                   + "deleted a file it rendered from some version.");
    }

    [Test]
    public async Task No_endpoint_anywhere_deletes_a_document()
    {
        // THE SHAPE OF THE RULE, asserted over the whole surface. A DELETE under
        // the airspace prefix would be a second way for a name to stop applying,
        // and the second way is always the ungoverned one.
        var deletes = ProtocolSurface.Endpoints
            .Where(e => string.Equals(e.Method, "DELETE", StringComparison.Ordinal))
            .Where(e => e.Path.StartsWith("/v1/airspace", StringComparison.Ordinal)
                     || e.Path.StartsWith("/v1/envelope", StringComparison.Ordinal))
            .Select(e => e.Path)
            .ToList();

        await Assert.That(deletes).IsEmpty()
            .Because("retiring a name is a version, not a deletion, and a delete verb "
                   + "would make the topology decisive rather than reflective. Found: "
                   + string.Join(", ", deletes));
    }

    [Test]
    public async Task A_work_kind_with_a_flight_in_the_air_has_a_reason_of_its_own()
    {
        var sentence = Reason.Sentence(
            ReasonKinds.FlightsInTheAir, ["migrate-data", "GG-7, GG-9"]);

        await Assert.That(sentence).Contains("migrate-data");
        await Assert.That(sentence).Contains("GG-7, GG-9")
            .Because("naming the flights is the whole refusal - 'some flights are still "
                   + "running' sends a person to go and find out which.");
        await Assert.That(ReasonKinds.FamilyOf(ReasonKinds.FlightsInTheAir))
            .IsEqualTo(ReasonFamilies.Refused);
    }

    [Test]
    public async Task The_new_kind_is_in_the_closed_vocabulary()
    {
        // The lesson from 0.65.0, applied on the way in rather than after a
        // consumer found it: a kind missing from All is invisible to the guard
        // whose entire job is noticing a new value.
        await Assert.That(ReasonKinds.All).Contains(ReasonKinds.FlightsInTheAir);
    }
}
