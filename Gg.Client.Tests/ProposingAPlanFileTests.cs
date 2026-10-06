using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// <b>S65.5-01</b> - <c>gg itinerary propose &lt;file&gt;</c> sends the plan file as a proposal and
/// prints the <c>ITN-n</c>, the pass, and who answers its gate.
/// </summary>
/// <remarks>
/// <b>By hand, so no agent</b>: a person proposing a file they wrote names no <c>via</c>. And the
/// answer says who is waited on, because a proposed plan opens nothing until somebody answers.
/// </remarks>
public class ProposingAPlanFileTests
{
    private const string Plan = """
        intent: three findings in one bug
        legs:
          - subject: the icon
            work-kind: implement
            reason: the ticket names the icon fix on its own
          - subject: the padding
            work-kind: implement
            reason: a shared padding change
            after: the icon
        """;

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

    private static async Task<string> FileAsync(string text)
    {
        var path = Path.Combine(Path.GetTempPath(), $"proposal-{Guid.NewGuid():N}.yaml");
        await File.WriteAllTextAsync(path, text);
        return path;
    }

    [Test]
    public async Task A_file_is_proposed_and_its_gate_named()
    {
        await using var stub = new StubControlPlane
        {
            ProposalAnswer = new ItineraryProposed
            {
                Itinerary = "ITN-7",
                Pass = "0199a3b1-0000-7000-8000-000000000001",
                Gates = [new LegGate { ObligationId = "plan-reviewed", Approver = "platform-owner" }],
            },
        };

        var result = await Build(stub).ProposeItineraryAsync(await FileAsync(Plan));

        var sent = stub.ObservedProposals.Single();
        await Assert.That(sent.Draft.Legs.Select(l => l.Subject ?? "")).IsEquivalentTo(["the icon", "the padding"]);
        await Assert.That(sent.Draft.Legs[1].After).IsEqualTo("the icon");
        await Assert.That(sent.Via).IsNull()
            .Because("a person proposing a file by hand names no agent.");

        var text = VerbOutput.ToText(result);
        await Assert.That(text).Contains("ITN-7");
        await Assert.That(text).Contains("plan-reviewed");
        await Assert.That(text).Contains("platform-owner")
            .Because("a proposed plan opens nothing until somebody answers, so the answer says who.");
    }

    [Test]
    public async Task A_refusal_is_said_in_the_control_planes_words()
    {
        await using var stub = new StubControlPlane
        {
            ItineraryRefusal = "'plan' requires no gate, so a plan proposed under it would open with nobody having approved its shape.",
        };

        var refused = await Assert.ThrowsAsync<ItineraryRefusedException>(
            async () => await Build(stub).ProposeItineraryAsync(await FileAsync(Plan)));

        await Assert.That(refused!.Message).Contains("requires no gate");
    }

    [Test]
    public async Task A_file_that_is_not_a_plan_is_refused_before_anything_is_sent()
    {
        await using var stub = new StubControlPlane();

        await Assert.ThrowsAsync<ItineraryRefusedException>(
            async () => await Build(stub).ProposeItineraryAsync(await FileAsync("intent: no legs\nlegs: []\n")));

        await Assert.That(stub.ObservedProposals).IsEmpty();
    }
}
