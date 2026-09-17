using Gg.Local;
using Gg.Runner.Execution;

namespace Gg.Runner.Tests;

/// <summary>
/// What a sweep's executor nominated through gg is read out of what it did, and
/// every answered call is kept.
/// </summary>
/// <remarks>
/// <para>
/// <b>S39.3-03.</b> ADR-0023 § 2: a nomination passes through <c>gg</c> by
/// construction, so the record of what a sweep nominated is the answered calls
/// to that server - read from the transcript, as a flight's nomination is,
/// because a tool call is something the agent chose to make and a sentence is
/// something it can be told to write.
/// </para>
/// <para>
/// <b>Every answered call, in order</b>, where a flight keeps its last: a
/// sweep nominates each item worth a flight. A call the server refused is not
/// a nomination, and a call with no subject or version never was one.
/// </para>
/// </remarks>
public class TheSweepsCallsReachTheAttestationTests
{
    private static string Called(
        string id, string arguments, bool refused = false, bool answered = true)
    {
        var call =
            "{\"type\":\"assistant\",\"message\":{\"content\":[{\"type\":\"tool_use\","
          + "\"id\":\"" + id + "\",\"name\":\"" + NominationTool.Qualified + "\","
          + "\"input\":" + arguments + "}]}}";

        if (!answered)
        {
            return call;
        }

        var error = refused ? ",\"is_error\":true" : "";
        return call + "\n"
             + "{\"type\":\"user\",\"message\":{\"content\":[{\"type\":\"tool_result\","
             + "\"tool_use_id\":\"" + id + "\"" + error
             + ",\"content\":[{\"type\":\"text\",\"text\":\"Recorded\"}]}]}}";
    }

    private static string Item(string subject, string version, string? kind = null) =>
        "{\"subject\":\"" + subject + "\",\"version\":\"" + version + "\","
      + "\"intent_key\":\"https://t.example/" + subject + "\","
      + (kind is null ? "" : "\"work_kind\":\"" + kind + "\",")
      + "\"reason\":\"needs a person\",\"note\":\"look at the logs first\"}";

    [Test]
    public async Task Every_answered_nomination_is_kept_in_the_order_it_was_made()
    {
        var transcript = string.Join('\n',
            Called("a", Item("31", "12", kind: "review")),
            Called("b", Item("26", "7")));

        var nominated = TranscriptDigest.SweepNominations(transcript);

        await Assert.That(string.Join(',', nominated.Select(n => n.Subject))).IsEqualTo("31,26");

        var first = nominated[0];
        await Assert.That(first.Version).IsEqualTo("12");
        await Assert.That(first.IntentKey).IsEqualTo("https://t.example/31");
        await Assert.That(first.WorkKind).IsEqualTo("review");
        await Assert.That(first.Reason).IsEqualTo("needs a person");
        await Assert.That(first.Note).IsEqualTo("look at the logs first");

        await Assert.That(nominated[1].WorkKind).IsNull()
            .Because("left to the bound, which is the board's to apply.");
    }

    [Test]
    public async Task A_refused_or_unanswered_call_is_not_a_nomination()
    {
        var transcript = string.Join('\n',
            Called("a", Item("31", "12"), refused: true),
            Called("b", Item("26", "7"), answered: false),
            Called("c", Item("40", "3")));

        var nominated = TranscriptDigest.SweepNominations(transcript);

        await Assert.That(nominated.Select(n => n.Subject)).IsEquivalentTo((string[])["40"]);
    }

    [Test]
    public async Task A_call_with_no_subject_or_version_never_was_one()
    {
        var transcript = string.Join('\n',
            Called("a", "{\"version\":\"1\",\"reason\":\"why\"}"),
            Called("b", "{\"subject\":\"9\",\"reason\":\"why\"}"),
            Called("c", "{\"subject\":\"9\",\"version\":\"1\"}"));

        await Assert.That(TranscriptDigest.SweepNominations(transcript)).IsEmpty()
            .Because("the extractor may not invent the missing half of a nomination.");
    }

    [Test]
    public async Task What_is_kept_is_within_what_the_report_accepts()
    {
        var reason = new string('r', Gg.Contracts.FlightNomination.MaxReason + 50);
        var transcript = Called("a",
            "{\"subject\":\"9\",\"version\":\"1\",\"reason\":\"" + reason + "\"}");

        var kept = TranscriptDigest.SweepNominations(transcript).Single();

        await Assert.That(kept.Reason.Length).IsLessThanOrEqualTo(
            Gg.Contracts.FlightNomination.MaxReason)
            .Because("bounded on this machine before it crosses, as a flight's reason is - a "
                   + "report the contract refuses would lose every nomination in it.");
    }

    [Test]
    public async Task A_flights_one_nomination_is_not_read_as_a_sweeps()
    {
        // THE FLIGHT SHAPE - a kind and a reason, no subject - is ignored here,
        // so a flight's transcript can never be reported as a sweep's.
        var transcript = Called("a", "{\"work_kind\":\"review\",\"reason\":\"why\"}");

        await Assert.That(TranscriptDigest.SweepNominations(transcript)).IsEmpty();
    }
}
