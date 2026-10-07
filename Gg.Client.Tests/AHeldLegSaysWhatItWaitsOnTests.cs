using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Client.Tests;

/// <summary>
/// `gg show` on a leg held behind another says what it waits on, in `gg why`'s sentence (slice
/// sixty-seven, S67.3-01); and what a flight is owed says what was said.
/// </summary>
/// <remarks>
/// <b>Found on ITN-60.</b> GG-969 and GG-967 waited for GG-968 to land, and `gg show` said
/// nothing about it - only `gg why` did. A person looking at a leg that never starts was told
/// nothing about why.
/// </remarks>
public class AHeldLegSaysWhatItWaitsOnTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 7, 0, 38, 38, TimeSpan.Zero);

    private static readonly LegHold Held = new()
    {
        Follows = FlightRef.Format(968),
        Subject = "Storybook 10 upgrade",
    };

    private static StoryEntry Opened() => new()
    {
        At = At,
        Kind = StoryKinds.OpenedByAdmission,
        Stage = FlightStages.Of(StoryKinds.OpenedByAdmission),
        Params = ["ui-preview"],
    };

    private static FlightStory NotStarted() => new()
    {
        FlightId = "019fe815-6136-7518-bb57-b06d6d3f411a",
        FlightNumber = FlightRef.Format(969),
        WorkKind = "ui-preview",
        Stage = FlightStoryStages.Reached([Opened()]),
        State = FlightStates.Open,
        Entries = [Opened()],
    };

    private static StoredSession ASession() => new()
    {
        SessionToken = StubControlPlane.IssuedSessionToken,
        ExpiresAt = DateTimeOffset.UtcNow.AddHours(12),
        TenantId = "019fe062-d000-730c-a37d-7247342cd810",
        PrincipalDisplay = "stub-principal",
    };

    private static FlightCommands Build(StubControlPlane stub) =>
        new(new ControlPlaneClient(new HttpClient { BaseAddress = new Uri(stub.BaseAddress) }),
            new HeldSessionStore(ASession()));

    [Test]
    public async Task Show_prints_the_hold_gg_why_prints()
    {
        await using var stub = new StubControlPlane { StoryAnswer = NotStarted(), HeldAnswer = Held };

        var text = VerbOutput.ToText(await Build(stub).ShowAsync("GG-969"));

        await Assert.That(text).Contains("NOT STARTED: this leg follows GG-968 (Storybook 10 upgrade)")
            .Because("a leg that never starts says what it waits on where a person looks.");
        await Assert.That(text).Contains(Held.Sentence())
            .Because("and it is the same sentence gg why prints, from the same hold.");
    }

    [Test]
    public async Task A_leg_that_has_been_leased_is_not_asked()
    {
        var leased = new StoryEntry
        {
            At = At.AddMinutes(1),
            Kind = StoryKinds.LeaseGranted,
            Stage = FlightStages.Of(StoryKinds.LeaseGranted),
            Params = ["a-runner"],
        };
        await using var stub = new StubControlPlane
        {
            StoryAnswer = NotStarted() with
            {
                Stage = FlightStoryStages.Reached([Opened(), leased]),
                Entries = [Opened(), leased],
            },
            HeldAnswer = Held,
        };

        await Build(stub).ShowAsync("GG-969");

        await Assert.That(stub.ObservedPaths.Any(p => p.EndsWith("/why", StringComparison.Ordinal)))
            .IsFalse()
            .Because("a leg that has run is not held behind anything, so the second read is not paid.");
    }

    [Test]
    public async Task What_is_owed_says_what_was_said()
    {
        var asked = new StoryEntry
        {
            At = At.AddMinutes(2),
            Kind = StoryKinds.LoopAsked,
            Stage = FlightStages.Of(StoryKinds.LoopAsked),
            Params = [],
            Said = "Was the repository supposed to be materialized?",
        };
        var story = NotStarted() with
        {
            Stage = FlightStoryStages.Reached([Opened(), asked]),
            Entries = [Opened(), asked],
            Outstanding = [asked],
        };

        var text = VerbOutput.ToText(new VerbResult.Story(story));
        var owed = text[text.IndexOf("waiting on somebody", StringComparison.Ordinal)..];
        owed = owed[..owed.IndexOf(Environment.NewLine + Environment.NewLine, StringComparison.Ordinal)];

        await Assert.That(owed).Contains("Was the repository supposed to be materialized?");
    }
}
