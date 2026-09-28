using Gg.Local;
using Gg.Runner.Execution;

namespace Gg.Runner.Tests;

/// <summary>
/// S53.4-04. A subject an agent named reaches the fact, and a call that named
/// none arrives exactly as it does today.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE OTHER END OF THE TOOL, and the end that was losing the work.</b> The
/// server may offer a subject and the contract may carry one, and neither
/// matters if the extractor does not read it back out of the transcript - the
/// fact would ship with the member empty and a pass's three legs would collapse
/// onto one row exactly as before.
/// </para>
/// <para>
/// <b>And the calls themselves were being thrown away.</b>
/// <c>TranscriptDigest</c> recorded every answered call and returned the last
/// one; a pass proposing three pieces of work shipped one fact and lost two
/// without a word. That is what this class holds now.
/// </para>
/// </remarks>
public class ASubjectReachesTheNominationTests
{
    private static string Called(
        string id, string workKind, string reason,
        string? subject = null, string? version = null, bool failed = false)
    {
        var extra =
            (subject is null ? "" : ",\"" + NominationTool.Subject + "\":\"" + subject + "\"")
          + (version is null ? "" : ",\"" + NominationTool.Version + "\":\"" + version + "\"");

        var call =
            "{\"type\":\"assistant\",\"message\":{\"content\":[{\"type\":\"tool_use\","
          + "\"id\":\"" + id + "\",\"name\":\"" + NominationTool.Qualified + "\","
          + "\"input\":{\"" + NominationTool.WorkKindArgument + "\":\"" + workKind + "\",\""
          + NominationTool.ReasonArgument + "\":\"" + reason + "\"" + extra + "}}]}}";

        var error = failed ? ",\"is_error\":true" : "";

        return call + "\n"
          + "{\"type\":\"user\",\"message\":{\"content\":[{\"type\":\"tool_result\","
          + "\"tool_use_id\":\"" + id + "\"" + error
          + ",\"content\":[{\"type\":\"text\",\"text\":\"Recorded\"}]}]}}";
    }

    [Test]
    public async Task A_subject_the_agent_named_reaches_the_fact()
    {
        var nominations = TranscriptDigest.Nominations(
            Called("a", "implement", "the parser needs its own flight", subject: "the parser"));

        await Assert.That(nominations.Count).IsEqualTo(1);
        await Assert.That(nominations[0].Subject).IsEqualTo("the parser")
            .Because("the server offers the argument and the contract carries the member, and "
                   + "neither matters if what the agent wrote never leaves the transcript.");
    }

    [Test]
    public async Task A_version_reaches_it_too_and_only_beside_a_subject()
    {
        var withBoth = TranscriptDigest.Nominations(
            Called("a", "implement", "a reason", subject: "the parser", version: "rev-2"));

        await Assert.That(withBoth[0].Version).IsEqualTo("rev-2");

        // A VERSION OF NOTHING. The contract refuses it and the server refuses
        // it; the extractor may not invent the subject it would be a version
        // of, so it carries neither rather than half a claim.
        var orphan = TranscriptDigest.Nominations(
            Called("b", "implement", "a reason", version: "rev-2"));

        await Assert.That(orphan[0].Subject).IsNull();
        await Assert.That(orphan[0].Version).IsNull()
            .Because("a version with no subject is a version of something nobody named, and "
                   + "the extractor may not decide what.");
    }

    [Test]
    public async Task A_call_that_named_no_subject_arrives_exactly_as_it_does_today()
    {
        // EVERY CLASSIFIER RUNNING RIGHT NOW. Absent means what it has always
        // meant - this nomination is about the flight it came from - and a
        // change that gave it a default would reclassify every one of them.
        var nominations = TranscriptDigest.Nominations(
            Called("a", "research", "the item does not name a cause"));

        await Assert.That(nominations.Count).IsEqualTo(1);
        await Assert.That(nominations[0].WorkKind).IsEqualTo("research");
        await Assert.That(nominations[0].Subject).IsNull();
        await Assert.That(nominations[0].Version).IsNull();
    }

    [Test]
    public async Task Three_pieces_of_work_are_three_nominations()
    {
        // THE WHOLE POINT, and it failed silently before: the extractor kept
        // the LAST answered call and dropped the rest, so a pass proposing
        // three legs shipped one fact.
        var nominations = TranscriptDigest.Nominations(string.Join('\n',
        [
            Called("a", "implement", "the parser", subject: "the parser"),
            Called("b", "implement", "the renderer", subject: "the renderer"),
            Called("c", "research", "the walk", subject: "the walk"),
        ]));

        await Assert.That(nominations.Select(n => n.Subject)).IsEquivalentTo((string?[])
            ["the parser", "the renderer", "the walk"]);

        await Assert.That(nominations.Select(n => n.WorkKind)).IsEquivalentTo((string[])
            ["implement", "implement", "research"])
            .Because("two legs may name one kind against different pieces of work, and they "
                   + "are two legs - the pair is what makes them the same or not.");
    }

    [Test]
    public async Task A_refused_call_among_several_costs_only_itself()
    {
        // The rule the singular extractor already had, and it still holds one
        // leg at a time: the tool recorded nothing for a refused call, so
        // shipping a fact for it would be the runner inventing one.
        var nominations = TranscriptDigest.Nominations(string.Join('\n',
        [
            Called("a", "implement", "the parser", subject: "the parser"),
            Called("b", "implement", "the renderer", subject: "the renderer", failed: true),
            Called("c", "research", "the walk", subject: "the walk"),
        ]));

        await Assert.That(nominations.Select(n => n.Subject)).IsEquivalentTo((string?[])
            ["the parser", "the walk"]);
    }

    [Test]
    public async Task A_subject_past_its_bound_is_bounded_rather_than_dropped()
    {
        // The note's rule, on the new member: what crosses is bounded where it
        // is produced, and a value cut in half is still a value the control
        // plane can refuse - where dropping it silently is not.
        var nominations = TranscriptDigest.Nominations(
            Called("a", "implement", "a reason", subject: new string('s', 4000)));

        await Assert.That(nominations[0].Subject!.Length)
            .IsLessThanOrEqualTo(Gg.Contracts.FlightNomination.MaxSubject);
    }
}
