using Gg.Contracts;
using Gg.Local;
using Gg.Runner.Execution;
using Gg.Runner.Facts;
using Gg.Runner.Vcs;

namespace Gg.Runner.Tests;

/// <summary>
/// What the agent asked its proposal be called, reaching the proposal.
/// </summary>
/// <remarks>
/// <para>
/// <b>Extraction, not summary</b>, on the digest's own terms: every value here
/// is something the stream says literally, read from an ANSWERED call. No model
/// is in this path, which is what keeps a transcript - a file that can contain
/// text addressed to a model - from being an injection surface into the thing a
/// reviewer reads first.
/// </para>
/// <para>
/// <b>Answered only, and the last one wins.</b> An unanswered call and an
/// errored one are both things the agent TRIED; neither is a thing the platform
/// took. And an agent that calls twice has changed its mind, which is the
/// nomination's rule rather than the proposal list's - there is one title.
/// </para>
/// </remarks>
public class AProposedLandingReachesTheProposalTests
{
    /// <summary>One tool call, as the stream really carries one.</summary>
    /// <remarks>
    /// Built rather than written as a raw literal: the shape is nested braces
    /// all the way down, and a fixture whose escaping is its own puzzle is one
    /// nobody reads for what it is asserting.
    /// </remarks>
    private static string Call(string id, string title, string? description = null)
    {
        var input = new System.Text.StringBuilder("{\"title\":")
            .Append(System.Text.Json.JsonSerializer.Serialize(title));

        if (description is not null)
        {
            input.Append(",\"description\":")
                .Append(System.Text.Json.JsonSerializer.Serialize(description));
        }

        input.Append('}');

        return "{\"message\":{\"content\":[{\"type\":\"tool_use\",\"id\":"
             + System.Text.Json.JsonSerializer.Serialize(id)
             + ",\"name\":" + System.Text.Json.JsonSerializer.Serialize(LandingProposalTool.Qualified)
             + ",\"input\":" + input + "}]}}";
    }

    private static string Answer(string id) =>
        "{\"message\":{\"content\":[{\"type\":\"tool_result\",\"tool_use_id\":"
      + System.Text.Json.JsonSerializer.Serialize(id) + "}]}}";

    [Test]
    public async Task An_answered_call_is_what_the_agent_asked_for()
    {
        var landing = TranscriptDigest.Landing(
            Call("t1", "Remove the residual explicit local", "It came in after the sweep.")
          + "\n" + Answer("t1") + "\n");

        await Assert.That(landing).IsNotNull();
        await Assert.That(landing!.Title).IsEqualTo("Remove the residual explicit local");
        await Assert.That(landing.Description).IsEqualTo("It came in after the sweep.");
    }

    [Test]
    public async Task A_call_nothing_answered_is_a_thing_the_agent_tried()
    {
        // The server refuses before it answers, so an unanswered call is one
        // the platform turned away - reading it would ship a title this
        // platform declined.
        await Assert.That(TranscriptDigest.Landing(Call("t1", "Never taken"))).IsNull();
    }

    [Test]
    public async Task Calling_twice_is_changing_your_mind()
    {
        // ONE TITLE, so the last answered call wins - the nomination's rule
        // rather than the proposal list's. A proposal list is a dozen asks a
        // person answers separately; a title is one thing, and two of them is
        // an agent that reconsidered.
        var landing = TranscriptDigest.Landing(
            Call("t1", "First thought") + "\n" + Answer("t1") + "\n"
          + Call("t2", "Second thought") + "\n" + Answer("t2") + "\n");

        await Assert.That(landing!.Title).IsEqualTo("Second thought");
    }

    [Test]
    public async Task An_answered_call_the_contract_refuses_stops_the_flight()
    {
        // The proposal's rule exactly: the server refuses a malformed payload
        // BEFORE answering, so an answered call carrying one is a transcript
        // that did not come from this server. Quietly dropping it would ship a
        // landing named by whatever came next.
        await Assert.That(() => TranscriptDigest.Landing(
                Call("t1", "") + "\n" + Answer("t1") + "\n"))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task A_transcript_with_no_call_at_all_asks_for_nothing()
    {
        // Every flight before this one, and every flight whose envelope does
        // not grant the move. Null rather than a default: a title nobody
        // proposed must fall through to what the landing composes.
        await Assert.That(TranscriptDigest.Landing("")).IsNull();
    }

    [Test]
    public async Task It_survives_the_pipeline_that_ships_it()
    {
        // THE FALL-THROUGH THIS REPOSITORY HAS ALREADY BEEN BITTEN BY. Every
        // payload passes a hygiene switch with a throwing default - deliberately,
        // because a compile error is not available for a switch over a hierarchy
        // - and a fact type added without an arm crashes the runner mid-flight
        // rather than failing a build. That happened with the work-item proposal
        // and cost a flight and a diagnosis; nothing catches it but a test that
        // puts the payload through.
        var batch = FactPipeline.Digest(
            FactHygiene.Clean(new GatheredFacts(
                [new FactPayload.ProposedLanding(new LandingProposal
                {
                    Title = "Remove the residual explicit local",
                    Description = "It came in after the sweep.",
                })])),
            flightId: "01a0a21e-edcb-70ea-b971-03228c1a296f",
            observedAt: DateTimeOffset.UnixEpoch);

        var shipped = batch.Items.Single();

        await Assert.That(shipped.Kind).IsEqualTo(FactKinds.LandingProposal);
        await Assert.That(shipped.Landing!.Title)
            .IsEqualTo("Remove the residual explicit local");
        await Assert.That(shipped.Landing!.Description).IsEqualTo("It came in after the sweep.");
        await Assert.That(FactEnvelope.Validate(shipped)).IsNull();
    }

    [Test]
    public async Task A_control_sequence_does_not_reach_the_record()
    {
        // WHAT THE HYGIENE PASS IS FOR. A title is written by an agent and read
        // by a person, in a terminal, out of a record somebody pastes into a
        // ticket. Stripping is not deleting - what was written survives and the
        // escape does not.
        var batch = FactPipeline.Digest(
            FactHygiene.Clean(new GatheredFacts(
                [new FactPayload.ProposedLanding(new LandingProposal
                {
                    Title = "Remove \u001b[31mthe\u001b[0m residual",
                })])),
            flightId: "01a0a21e-edcb-70ea-b971-03228c1a296f",
            observedAt: DateTimeOffset.UnixEpoch);

        await Assert.That(batch.Items.Single().Landing!.Title).DoesNotContain("\u001b");
    }

    [Test]
    public async Task What_the_agent_asked_for_beats_what_it_happened_to_write_first()
    {
        // THE WHOLE POINT. The account is a fallback and the file that holds it
        // says so; a title the agent chose is the tier above it.
        var title = LandingTitle.For(
            "GG-118",
            runReason: "Found the single residual in the unit tests.",
            fallback: "Destination 'pull-request' requires 'in-scope', and it holds.",
            proposed: new LandingProposal { Title = "Remove the residual explicit local" });

        await Assert.That(title).IsEqualTo("GG-118: Remove the residual explicit local");
    }

    [Test]
    public async Task Nothing_proposed_falls_through_to_the_account()
    {
        // The cascade below the top tier is unchanged, which is what makes
        // granting the move optional rather than a flag day.
        var title = LandingTitle.For(
            "GG-118",
            runReason: "Found the single residual in the unit tests.",
            fallback: "Destination 'pull-request' requires 'in-scope', and it holds.",
            proposed: null);

        await Assert.That(title).IsEqualTo("GG-118: Found the single residual in the unit tests.");
    }

    [Test]
    public async Task The_flight_number_still_leads_a_title_the_agent_wrote()
    {
        // A proposal nobody can trace back to a flight is a branch nobody will
        // ever delete - and an agent asked for wording, not for the record.
        await Assert.That(LandingTitle.For(
                "GG-118", null, "a verdict",
                new LandingProposal { Title = "Remove the residual" }))
            .StartsWith("GG-118: ");
    }
}
