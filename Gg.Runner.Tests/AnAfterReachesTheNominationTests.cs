using Gg.Local;
using Gg.Runner.Execution;

namespace Gg.Runner.Tests;

/// <summary>
/// S57.1-02's missing half: an `after` an agent wrote reaches the fact.
/// </summary>
/// <remarks>
/// <para>
/// <b>FOUND ON THE SLICE FIFTY-SEVEN WALK, 2026-10-02, and it had made the
/// whole feature inert.</b> A `plan` flight proposed two legs and the agent
/// passed `after` correctly — the transcript holds
/// <c>"after":"preview-probe: serve flight-survival page (flight A)"</c>, byte
/// for byte the other leg's subject. The fact that reached the control plane
/// carried <c>"after": null</c>. So no edge was stored, nothing held the
/// follower back, and the queue handed out the SECOND leg five seconds after
/// opening while the first sat unclaimed for six minutes.
/// </para>
/// <para>
/// <b>Everything below the runner was correct and unreachable.</b> The board
/// row, the resolution at approval, the predicate on the claim and the sentence
/// <c>gg why</c> renders were all built, tested and deployed; the value never
/// left the machine that produced it.
/// </para>
/// <para>
/// <b>This is <see cref="ASubjectReachesTheNominationTests"/>'s lesson,
/// repeated.</b> That class was written for `subject` in slice fifty-three and
/// says it in as many words: <i>"the server may offer a subject and the
/// contract may carry one, and neither matters if the extractor does not read it
/// back out of the transcript"</i>. `after` was added to the tool and to the
/// contract and not to the extractor, and the slice recorded S57.1-02 as proven
/// because the tool DECLARED the argument. Declaring and reading are two
/// places.
/// </para>
/// <para>
/// <b>Bounded rather than dropped, which is the subject's rule.</b> A value cut
/// short is one the control plane can still refuse by name; one dropped
/// silently is an order nobody can see was lost — exactly what happened here.
/// </para>
/// </remarks>
public class AnAfterReachesTheNominationTests
{
    /// <summary>
    /// One answered <c>propose_nomination</c> call, as the transcript holds it.
    /// </summary>
    /// <remarks>
    /// The same shape <see cref="ASubjectReachesTheNominationTests"/> builds,
    /// because the subject is what `after` names and a fixture that disagreed
    /// about the call would be measuring a different tool.
    /// </remarks>
    private static string Called(
        string id, string workKind, string reason,
        string? subject = null, string? after = null)
    {
        var extra =
            (subject is null ? "" : ",\"" + NominationTool.Subject + "\":\"" + subject + "\"")
          + (after is null ? "" : ",\"" + NominationTool.After + "\":\"" + after + "\"");

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
    public async Task An_after_the_agent_wrote_reaches_the_fact()
    {
        // THE ASSERTION THE WALK NEEDED AND NOBODY HAD MADE. Every other part of
        // slice fifty-seven was green against this member being null.
        var nominations = TranscriptDigest.Nominations(
            Called("a", "preview-probe", "it reads what the first leg produced",
                subject: "the page", after: "the schema"));

        await Assert.That(nominations.Count).IsEqualTo(1);
        await Assert.That(nominations[0].After).IsEqualTo("the schema")
            .Because("the tool offers the argument and the contract carries the member, and "
                   + "neither matters if what the agent wrote never leaves the transcript - "
                   + "which is how a two-leg plan ran its legs backwards on a live stack.");
    }

    [Test]
    public async Task A_call_that_named_no_after_carries_none()
    {
        // THE POISON TWIN, and it is every nomination in the field. Silence is
        // not a claim to anything: an invented `after` would be an order nobody
        // declared, and the claim would hold a leg behind a flight the agent
        // never named.
        var nominations = TranscriptDigest.Nominations(
            Called("a", "implement", "the parser needs its own flight", subject: "the parser"));

        await Assert.That(nominations.Count).IsEqualTo(1);
        await Assert.That(nominations[0].After).IsNull()
            .Because("absent and null are one answer, and that is what every nomination this "
                   + "fleet has ever shipped carries.");
    }

    [Test]
    public async Task An_after_beside_no_subject_is_still_carried_and_refused_by_the_contract()
    {
        // NOT THE VERSION'S RULE, and the difference is worth stating. A version
        // of nothing is meaningless, so the extractor refuses to invent the
        // subject it would be a version OF. An `after` with no subject of its
        // own is a leg the board cannot key - and the CONTRACT already refuses
        // it by name, so carrying it reaches a refusal that says which leg was
        // wrong. Dropping it here would turn that into silence.
        var nominations = TranscriptDigest.Nominations(
            Called("a", "implement", "a reason", after: "the schema"));

        await Assert.That(nominations.Count).IsEqualTo(1);
        await Assert.That(nominations[0].After).IsEqualTo("the schema")
            .Because("the contract refuses an `after` beside a null subject and names it; an "
                   + "extractor that dropped it would make the agent's mistake unreportable.");

        await Assert.That(Gg.Contracts.FlightNomination.Validate(nominations[0])).IsNotNull()
            .Because("and that refusal has to actually fire, or this test is an argument for "
                   + "carrying a value nothing checks.");
    }

    [Test]
    public async Task An_after_past_the_bound_is_cut_rather_than_dropped()
    {
        // THE SUBJECT'S RULE, applied to the member that names one. A value cut
        // short is still one the control plane can refuse by name; one dropped
        // silently is the defect this class exists for, arrived at from the
        // other side.
        var nominations = TranscriptDigest.Nominations(
            Called("a", "implement", "a reason",
                subject: "the parser",
                after: new string('x', Gg.Contracts.FlightNomination.MaxSubject + 50)));

        await Assert.That(nominations.Count).IsEqualTo(1);
        await Assert.That(nominations[0].After).IsNotNull();
        await Assert.That(nominations[0].After!.Length)
            .IsLessThanOrEqualTo(Gg.Contracts.FlightNomination.MaxSubject)
            .Because("the contract bounds it, so a longer one is refused whole - and losing "
                   + "the nomination over the tail of a subject throws away the part that "
                   + "decides something.");
    }
}
