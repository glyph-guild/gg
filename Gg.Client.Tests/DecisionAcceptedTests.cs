using Gg.Client;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// The decision endpoint stops answering inline, and `gg decide` does not notice.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is what step 1 was built for.</b> The verb already submits and then
/// observes; the synchronous answer was carried beside the observation and
/// consulted by nothing. So the write becoming a command empties one field and
/// changes no shape - and these tests are the proof of that claim rather than a
/// restatement of it.
/// </para>
/// <para>
/// <b>The client accepts both answers, deliberately.</b> A 200 with a record and a
/// 202 with none are the same event to a caller that observes, and tolerating both
/// is what lets the two repositories land this independently rather than in one
/// coordinated release neither side can test alone. It is not permanent: when the
/// control plane only ever answers 202, the 200 branch is dead and deleting it is
/// a separate change with its own reason.
/// </para>
/// </remarks>
public class DecisionAcceptedTests
{
    private static StoredSession SignedIn { get; } = new()
    {
        SessionToken = "stub-session",
        ExpiresAt = new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero),
        TenantId = "stub-tenant",
        PrincipalDisplay = "someone@example.test",
    };

    private static SubmitAndObserve Instant()
    {
        var now = new DateTimeOffset(2026, 8, 15, 12, 0, 0, TimeSpan.Zero);
        return new SubmitAndObserve(
            (span, _) => { now = now.Add(span); return Task.CompletedTask; },
            () => now);
    }

    private static async Task<DecisionReport> DecideAsync(StubControlPlane stub)
    {
        using var http = new HttpClient { BaseAddress = new Uri(stub.BaseAddress) };
        var commands = new FlightCommands(
            new ControlPlaneClient(http), new HeldSessionStore(SignedIn));

        var result = await commands.DecideAsync(
            "GG-42", "reversibility-plan", DecisionOutcomes.Approved,
            new DecisionObservations
            {
                Interactive = false, EvidenceRendered = false, SecondsToDecide = null,
            },
            loop: Instant());

        return ((VerbResult.Decided)result).Value;
    }

    [Test]
    public async Task An_accepted_submission_with_no_record_is_still_decided()
    {
        // THE SHAPE STEP 2 PRODUCES. The control plane took the decision, wrote
        // nothing back, and the verb learned what happened by looking - which is
        // the entire point of having done step 1 first.
        await using var stub = new StubControlPlane { AcceptsWithoutRecording = true };

        var report = await DecideAsync(stub);

        await Assert.That(report.Observation.State).IsEqualTo(ObservationStates.Decided);
        await Assert.That(report.Observation.Outcome).IsEqualTo(ObligationOutcomes.Satisfied);
        await Assert.That(report.Decision).IsNull()
            .Because("null here is a fact rather than a gap: the control plane no longer answers "
                   + "inline, which is the whole direction of ADR-0012.");
    }

    [Test]
    public async Task It_renders_without_the_record_rather_than_printing_a_gap()
    {
        // A person still has to be able to read this. The observation is the
        // headline and always was; what disappears is the block underneath, and it
        // disappears rather than rendering blank fields.
        await using var stub = new StubControlPlane { AcceptsWithoutRecording = true };

        var text = VerbOutput.ToText(new VerbResult.Decided(await DecideAsync(stub)));

        await Assert.That(text).Contains("decided:");
        await Assert.That(text).Contains("looked:");
        await Assert.That(text).DoesNotContain("by:       ")
            .Because("there is nobody to name from this response, and a blank `by:` would claim "
                   + "the record carries something it does not.");
    }

    [Test]
    public async Task The_json_a_script_reads_keeps_its_shape()
    {
        // THE PROPERTY THAT MAKES THIS SAFE. A script written against step 1's
        // output still parses: `observation` is where it always was, and
        // `decision` is null rather than absent.
        await using var stub = new StubControlPlane { AcceptsWithoutRecording = true };

        var json = VerbOutput.ToJson(new VerbResult.Decided(await DecideAsync(stub)));

        await Assert.That(json).Contains("\"observation\"");
        await Assert.That(json).Contains("\"decision\"");
        await Assert.That(json).Contains("null");
    }

    [Test]
    public async Task The_old_answer_is_still_accepted_while_it_still_exists()
    {
        // TOLERATED ON PURPOSE, so the two repositories can land this in either
        // order. Not permanent: when the control plane only ever answers 202 this
        // branch is dead, and deleting it is a change with its own reason.
        await using var stub = new StubControlPlane();

        var report = await DecideAsync(stub);

        await Assert.That(report.Observation.State).IsEqualTo(ObservationStates.Decided);
        await Assert.That(report.Decision).IsNotNull();
    }

    [Test]
    public async Task The_declared_statuses_say_the_answer_may_be_accepted_rather_than_returned()
    {
        var decisions = Gg.Contracts.Description.ProtocolSurface.Endpoints
            .Single(e => e.Path == "/v1/flights/{ref}/decisions" && e.Method == "POST");

        await Assert.That(decisions.Statuses).Contains(202)
            .Because("the write is a command now, and a declared surface that still promised 200 "
                   + "with a body would be a contract nobody serves.");
        await Assert.That(decisions.Statuses).Contains(409)
            .Because("and refusing a decision made against work that moved is still synchronous - "
                   + "that check happens before anything is dispatched, so it can still answer.");
    }
}
