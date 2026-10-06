using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// <b>S62.4-02</b> - a plan file's <c>intent:</c> may be a file reference, and
/// <c>gg itinerary check</c> sends it unread.
/// </summary>
/// <remarks>
/// <b>Unread</b>, because the words a plan's legs will work from are the ones a runner reads at
/// a commit it records. Reading them here would check one version and fly another.
/// </remarks>
public class APlanMayBeAboutAFileTests
{
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

    private static ItineraryCheck Answer() => new()
    {
        Planner = "plan",
        DestinationId = "the-plan",
        Legs = [],
    };

    private static async Task<string> FileAsync(string text)
    {
        var path = Path.Combine(Path.GetTempPath(), $"itinerary-{Guid.NewGuid():N}.yaml");
        await File.WriteAllTextAsync(path, text);
        return path;
    }

    [Test]
    public async Task A_file_intent_is_sent_as_a_file_and_never_read()
    {
        await using var stub = new StubControlPlane { ItineraryAnswer = Answer() };

        await Build(stub).CheckItineraryAsync(await FileAsync("""
            intent:
              repository: JDX/JDNext
              path: docs/plans/18291.md
              ref: develop
            legs:
              - subject: the icon
                work-kind: implement
                reason: the plan names it
            """));

        var intent = stub.ObservedDrafts.Single().Intent;
        await Assert.That(intent.Kind).IsEqualTo(FlightIntentKinds.File);
        await Assert.That(intent.Repository).IsEqualTo("JDX/JDNext");
        await Assert.That(intent.Path).IsEqualTo("docs/plans/18291.md");
        await Assert.That(intent.Ref).IsEqualTo("develop");
        await Assert.That(intent.Text).IsNull()
            .Because("nothing on this machine read the file - JDX/JDNext is not even here.");
    }

    [Test]
    public async Task A_file_beside_a_sentence_is_refused_before_anything_is_sent()
    {
        await using var stub = new StubControlPlane { ItineraryAnswer = Answer() };

        var path = await FileAsync("""
            intent:
              text: also these words
              repository: JDX/JDNext
              path: docs/plans/18291.md
            legs:
              - subject: the icon
                work-kind: implement
                reason: the plan names it
            """);

        await Assert.That(async () => await Build(stub).CheckItineraryAsync(path)).ThrowsException();
        await Assert.That(stub.ObservedDrafts).IsEmpty();
    }
}
