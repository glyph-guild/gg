using Gg.Local;
using Gg.Runner.Execution;

namespace Gg.Runner.Tests;

/// <summary>
/// An itinerary an agent named reaches the fact, so a plan can be revised.
/// </summary>
/// <remarks>
/// <para>
/// <b>The third time this file's lesson has had to be written.</b>
/// <see cref="ASubjectReachesTheNominationTests"/> was written for `subject` in
/// slice fifty-three and <see cref="AnAfterReachesTheNominationTests"/> for
/// `after` in fifty-seven, both because a member was added to the tool and to
/// the contract and not to the extractor. `itinerary` was never added to any of
/// the three: the contract has carried the member since 0.245.0,
/// <c>FlightNomination.Validate</c> refuses an unparseable reference by name,
/// and the control plane implements <i>absent mints, present revises</i> on one
/// code path with four passing test classes behind it -
/// <c>ARevisionIsIdempotentTests</c>,
/// <c>ARevisionChangesWhatHasNotHappenedTests</c>,
/// <c>ARevisionIsGatedByItsOwnPassTests</c> and the `Dropped` ending. The only
/// assignment to the member anywhere in this repository was in a contract test.
/// </para>
/// <para>
/// <b>So the whole of revision was unreachable</b>, and so was an unplanned
/// flight joining a plan - both of which are the same argument.
/// </para>
/// <para>
/// <b>BOUNDED, AND THE BOUND IS SAFE TO PROVE RATHER THAN ASSUME.</b> Cutting
/// prose short loses words; cutting a REFERENCE short could in principle name a
/// different itinerary - revising somebody else's plan - which would be worse
/// than refusing. It cannot happen here, and the reason is arithmetic:
/// <c>ItineraryRef.TryParse</c> reads `ITN-` plus digits through
/// <c>int.TryParse</c> with <c>NumberStyles.None</c>, which overflows past ten
/// digits, and otherwise requires <c>Guid.TryParseExact</c> "D", which is
/// exactly 36 characters. Any cut at a bound above 37 is therefore too long to
/// be an id and too long to be a number, so a truncated value is always
/// REFUSED and never resolves. The last test here is that proof.
/// </para>
/// </remarks>
public class AnItineraryReachesTheNominationTests
{
    /// <summary>
    /// One answered <c>propose_nomination</c> call, as the transcript holds it.
    /// </summary>
    /// <remarks>
    /// <see cref="AnAfterReachesTheNominationTests"/>'s shape, member for
    /// member, because a fixture that disagreed about the call would be
    /// measuring a different tool.
    /// </remarks>
    private static string Called(
        string id, string workKind, string reason,
        string? subject = null, string? itinerary = null)
    {
        var extra =
            (subject is null ? "" : ",\"" + NominationTool.Subject + "\":\"" + subject + "\"")
          + (itinerary is null ? "" : ",\"" + NominationTool.Itinerary + "\":\"" + itinerary + "\"");

        var call =
            "{\"type\":\"assistant\",\"message\":{\"content\":[{\"type\":\"tool_use\","
          + "\"id\":\"" + id + "\",\"name\":\"" + NominationTool.Qualified + "\","
          + "\"input\":{\"" + NominationTool.WorkKindArgument + "\":\"" + workKind + "\",\""
          + NominationTool.ReasonArgument + "\":\"" + reason + "\"" + extra + "}}]}}";

        return call + "\n"
          + "{\"type\":\"user\",\"message\":{\"content\":[{\"type\":\"tool_result\","
          + "\"tool_use_id\":\"" + id + "\""
          + ",\"content\":[{\"type\":\"text\",\"text\":\"Recorded\"}]}]}}";
    }

    [Test]
    public async Task A_number_a_person_typed_reaches_the_fact()
    {
        // THE FORM AN AGENT ACTUALLY HAS. ITN-7 is what a prompt shows it, and
        // the contract resolves either spelling on purpose so that a read
        // surface accepting only uuids would not make the number decorative.
        var nominations = TranscriptDigest.Nominations(
            Called("a", "implement", "the parser still needs doing",
                subject: "the parser", itinerary: "ITN-7"));

        await Assert.That(nominations.Count).IsEqualTo(1);
        await Assert.That(nominations[0].Itinerary).IsEqualTo("ITN-7")
            .Because("absent mints and present revises, so a pass that named a plan and had the "
                   + "name dropped here does not revise that plan - it silently mints a SECOND "
                   + "one, which is the outcome the contract's own refusal exists to prevent.");
    }

    [Test]
    public async Task An_id_reaches_the_fact_as_written()
    {
        var id = "01a10f37-08e2-7628-8782-11623248f946";

        var nominations = TranscriptDigest.Nominations(
            Called("a", "implement", "a reason", subject: "the parser", itinerary: id));

        await Assert.That(nominations[0].Itinerary).IsEqualTo(id)
            .Because("an agent holding the id names that, and the reference reads both forms.");
    }

    [Test]
    public async Task A_call_that_named_no_itinerary_carries_none()
    {
        // THE POISON TWIN, and it is every nomination this fleet has ever
        // shipped. Absent is not nothing: it is the instruction to MINT, so an
        // invented value here would revise a plan the agent never named.
        var nominations = TranscriptDigest.Nominations(
            Called("a", "implement", "a reason", subject: "the parser"));

        await Assert.That(nominations[0].Itinerary).IsNull()
            .Because("absent means mint a new itinerary, which is what a first plan wants.");
    }

    [Test]
    public async Task An_itinerary_beside_no_subject_is_carried()
    {
        // NOT THE VERSION'S RULE, which refuses to invent the subject it would
        // be a version OF. This combination is the one rule 4 is about: an
        // UNPLANNED FLIGHT joins a plan by nominating under it and has no
        // subject of its own, so dropping the itinerary here would silently
        // turn joining into minting a plan of one.
        var nominations = TranscriptDigest.Nominations(
            Called("a", "implement", "this has to be reflown", itinerary: "ITN-7"));

        await Assert.That(nominations[0].Itinerary).IsEqualTo("ITN-7")
            .Because("the contract declares this legal in as many words - an unplanned flight "
                   + "joins by nominating under the itinerary - so the extractor may not be the "
                   + "thing that makes it impossible.");

        await Assert.That(Gg.Contracts.FlightNomination.Validate(nominations[0])).IsNull()
            .Because("and the contract has to actually accept it, or this test is an argument "
                   + "for carrying a value that gets refused later.");
    }

    [Test]
    public async Task A_reference_past_the_bound_is_cut_into_one_nothing_resolves()
    {
        // THE PROOF THE BOUND IS SAFE. A cut reference that still PARSED would
        // revise some other plan; the arithmetic in this class's remarks says it
        // cannot, and this is where that claim is measured rather than argued.
        var nominations = TranscriptDigest.Nominations(
            Called("a", "implement", "a reason",
                subject: "the parser",
                itinerary: "ITN-" + new string('7', 400)));

        await Assert.That(nominations[0].Itinerary).IsNotNull()
            .Because("dropped silently is the defect this class exists for; refused by name is "
                   + "an answer an agent can act on.");

        await Assert.That(Gg.Contracts.Description.ItineraryRef.TryParse(
            nominations[0].Itinerary, out _)).IsFalse()
            .Because("a truncated reference must never resolve, or a cut would revise a plan "
                   + "the agent did not name.");

        await Assert.That(Gg.Contracts.FlightNomination.Validate(nominations[0])).IsNotNull()
            .Because("and the contract refuses it, so the pass is told which value was wrong.");
    }
}
