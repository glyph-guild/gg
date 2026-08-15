using Gg.Client;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// `gg decide` submits, and then learns what happened by looking.
/// </summary>
/// <remarks>
/// <para>
/// <b>The differential in this file exists only in this window.</b> The control
/// plane still writes the decision inline and still answers with it, so there are
/// two independent accounts of the same fact - the synchronous response, and what
/// a read surface says a moment later. Asserting they agree is a check against a
/// known-good answer that <b>disappears the moment the write becomes a command</b>,
/// which is the next step. It is written now because now is the only time it can
/// be.
/// </para>
/// <para>
/// <b>The two accounts are in different vocabularies</b>, and that is the part
/// worth stating. The response says what was DECIDED - <c>approved</c> or
/// <c>rejected</c>. The read surface says what the obligation now IS -
/// <c>satisfied</c> or <c>violated</c>. The mapping between them is the Engine's,
/// not the client's, and the wire declares no closed vocabulary for the second
/// pair - so it is asserted here rather than assumed anywhere.
/// </para>
/// </remarks>
public class DecideObservationTests
{
    /// <summary>A session that is signed in, so the verb reaches the transport.</summary>
    private static StoredSession SignedIn { get; } = new()
    {
        SessionToken = "stub-session",
        ExpiresAt = new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero),
        TenantId = "stub-tenant",
        PrincipalDisplay = "someone@example.test",
    };

    private static DecisionObservations Observed() => new()
    {
        Interactive = false,
        EvidenceRendered = false,
        SecondsToDecide = null,
    };

    /// <summary>A loop whose waiting costs no wall-clock time.</summary>
    private static SubmitAndObserve Instant()
    {
        var now = new DateTimeOffset(2026, 8, 15, 12, 0, 0, TimeSpan.Zero);
        return new SubmitAndObserve(
            (span, _) => { now = now.Add(span); return Task.CompletedTask; },
            () => now);
    }

    private static async Task<DecisionReport> DecideAsync(
        StubControlPlane stub, string outcome = DecisionOutcomes.Approved, string? reason = null,
        double boundSeconds = 30)
    {
        using var http = new HttpClient { BaseAddress = new Uri(stub.BaseAddress) };
        var commands = new FlightCommands(
            new ControlPlaneClient(http), new HeldSessionStore(SignedIn));

        var result = await commands.DecideAsync(
            "GG-42", "reversibility-plan", outcome, Observed(), reason,
            new ObservationBound
            {
                Wait = TimeSpan.FromSeconds(boundSeconds),
                FirstDelay = TimeSpan.FromMilliseconds(200),
                MaxDelay = TimeSpan.FromSeconds(2),
            },
            Instant());

        return ((VerbResult.Decided)result).Value;
    }

    // ---- the differential, which is gone next step ----

    [Test]
    public async Task What_was_observed_agrees_with_what_the_synchronous_write_answered()
    {
        // THE ASSERTION THAT ONLY EXISTS IN THIS WINDOW. Two independent accounts
        // of one fact: the response the control plane still returns, and a read of
        // the surface a person would read. Step 2 removes the first one, so this
        // is the only step in which they can be compared at all.
        await using var stub = new StubControlPlane();

        var report = await DecideAsync(stub);

        await Assert.That(report.Decision).IsNotNull()
            .Because("while the write is still synchronous there IS a second account, and a "
                   + "differential with one side missing is not one.");
        await Assert.That(report.Observation.State).IsEqualTo(ObservationStates.Decided);

        // The mapping, stated rather than assumed: approved satisfies the
        // obligation, and that is the Engine's rule rather than the client's.
        await Assert.That(report.Observation.Outcome).IsEqualTo("satisfied");
        await Assert.That(report.Decision!.Outcome).IsEqualTo(DecisionOutcomes.Approved);
    }

    [Test]
    public async Task The_differential_holds_for_a_rejection_too()
    {
        // THE OTHER HALF OF THE MAPPING, and the one nobody would notice breaking:
        // every test that reaches this path approves, so a rejection that observed
        // as satisfied would read as correct for as long as nobody rejected
        // anything. That failure has happened here once already.
        await using var stub = new StubControlPlane();

        var report = await DecideAsync(
            stub, DecisionOutcomes.Rejected, reason: "the migration is not reversible");

        await Assert.That(report.Decision!.Outcome).IsEqualTo(DecisionOutcomes.Rejected);
        await Assert.That(report.Observation.Outcome).IsEqualTo("violated");
        await Assert.That(report.Observation.State).IsEqualTo(ObservationStates.Decided)
            .Because("a rejection is a decision that was recorded. `refused` is what the "
                   + "control plane says about the SUBMISSION, and conflating the two would "
                   + "make every rejection look like a failed request.");
    }

    // ---- the observation is a real read, not the posted value handed back ----

    [Test]
    public async Task It_waits_when_the_write_is_not_visible_yet()
    {
        // STEP 2 ARRIVING EARLY. The stub holds the write back for three
        // observations, which is what an asynchronous write looks like from here -
        // and the verb reports decided anyway, because it waited.
        await using var stub = new StubControlPlane { VisibleAfterPolls = 3 };

        var report = await DecideAsync(stub);

        await Assert.That(report.Observation.State).IsEqualTo(ObservationStates.Decided);
        await Assert.That(report.Observation.Polls).IsGreaterThan(1)
            .Because("if it answered on the first look, it read something other than the "
                   + "surface the write has not reached yet.");
    }

    [Test]
    public async Task A_write_that_never_becomes_visible_is_not_yet_visible_and_not_a_refusal()
    {
        await using var stub = new StubControlPlane { VisibleAfterPolls = 10_000 };

        var report = await DecideAsync(stub, boundSeconds: 2);

        await Assert.That(report.Observation.State).IsEqualTo(ObservationStates.NotYetVisible);
        await Assert.That(report.Observation.State).IsNotEqualTo(ObservationStates.Refused);
        await Assert.That(report.Observation.Because).Contains("does NOT mean it was refused");
        await Assert.That(report.Decision).IsNotNull()
            .Because("the submission was accepted - what is unknown is whether it landed, and "
                   + "that distinction is the whole of this step.");
    }

    [Test]
    public async Task A_refused_submission_is_refused_rather_than_waited_on()
    {
        await using var stub = new StubControlPlane
        {
            RefuseDecision = "The work changed while this decision was being made.",
        };

        using var http = new HttpClient { BaseAddress = new Uri(stub.BaseAddress) };
        var commands = new FlightCommands(
            new ControlPlaneClient(http), new HeldSessionStore(SignedIn));

        var result = await commands.DecideAsync(
            "GG-42", "reversibility-plan", DecisionOutcomes.Approved, Observed(),
            loop: Instant());

        var report = ((VerbResult.Decided)result).Value;

        await Assert.That(report.Observation.State).IsEqualTo(ObservationStates.Refused);
        await Assert.That(report.Observation.Because).Contains("The work changed");
        await Assert.That(report.Decision).IsNull()
            .Because("nothing was recorded, so there is no record to carry.");
    }

    // ---- the bound is stated rather than hidden ----

    [Test]
    public async Task The_output_says_how_long_it_looked_and_how_long_it_was_willing_to()
    {
        await using var stub = new StubControlPlane { VisibleAfterPolls = 10_000 };

        var text = VerbOutput.ToText(new VerbResult.Decided(await DecideAsync(stub, boundSeconds: 4)));

        await Assert.That(text).Contains("not yet visible");
        await Assert.That(text).Contains("4s bound")
            .Because("'we do not know yet' is only actionable next to how long we looked - "
                   + "otherwise nobody can tell a bound that is too short from a control plane "
                   + "that is stuck.");
    }

    [Test]
    public async Task The_json_a_script_reads_carries_the_state()
    {
        await using var stub = new StubControlPlane();

        var json = VerbOutput.ToJson(new VerbResult.Decided(await DecideAsync(stub)));

        await Assert.That(json).Contains("\"state\"");
        await Assert.That(json).Contains(ObservationStates.Decided);
        await Assert.That(json).Contains("\"decision\"")
            .Because("the synchronous answer is still carried while it exists, so a script "
                   + "written today keeps working when it stops being.");
    }

}
