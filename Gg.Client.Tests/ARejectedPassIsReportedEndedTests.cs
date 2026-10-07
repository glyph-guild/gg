using Gg.Client;
using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Client.Tests;

/// <summary>
/// `gg decide` rejecting a held pass's gate reports the pass ended, not "not yet visible"
/// (slice sixty-eight, S68.5-01).
/// </summary>
/// <remarks>
/// <b>Found rejecting GG-972</b> (ITN-61's pass): the decision landed in a second and the pass
/// ended withdrawn, and gg waited out its whole 30-second bound and said "not yet visible". It
/// waits for the obligation's outcome, and a withdrawn pass never records one - the flight ended
/// before anything was evaluated.
/// </remarks>
public class ARejectedPassIsReportedEndedTests
{
    private static StoredSession SignedIn { get; } = new()
    {
        SessionToken = "stub-session",
        ExpiresAt = new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero),
        TenantId = "stub-tenant",
        PrincipalDisplay = "someone@example.test",
    };

    [Test]
    public async Task The_report_says_it_ended_withdrawn()
    {
        await using var stub = new StubControlPlane
        {
            RejectionWithdraws = true,
            StoryAnswer = new FlightStory
            {
                FlightId = "019fe815-6136-7518-bb57-b06d6d3f411a",
                FlightNumber = FlightRef.Format(42),
                WorkKind = "plan",
                Stage = FlightStoryStages.Reached([]),
                State = FlightStates.Withdrawn,
                Entries = [],
            },
        };
        using var http = new HttpClient { BaseAddress = new Uri(stub.BaseAddress) };
        var commands = new FlightCommands(new ControlPlaneClient(http), new HeldSessionStore(SignedIn));
        var now = new DateTimeOffset(2026, 10, 7, 4, 3, 26, TimeSpan.Zero);

        var result = await commands.DecideAsync(
            "GG-42", "reversibility-plan", DecisionOutcomes.Rejected,
            new DecisionObservations { Interactive = false, EvidenceRendered = false, SecondsToDecide = null },
            "superseded by ITN-62",
            new ObservationBound
            {
                Wait = TimeSpan.FromSeconds(30),
                FirstDelay = TimeSpan.FromMilliseconds(200),
                MaxDelay = TimeSpan.FromSeconds(2),
            },
            new SubmitAndObserve((span, _) => { now = now.Add(span); return Task.CompletedTask; }, () => now));

        var text = VerbOutput.ToText(result);
        await Assert.That(text).DoesNotContain("not yet visible")
            .Because("the decision was visible at once: the gate closed and the flight ended.");
        await Assert.That(text).Contains("withdrawn");
    }
}
