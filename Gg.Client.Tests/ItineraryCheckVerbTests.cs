using System.Text.Json;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// <b>S61.6-01</b> - <c>gg itinerary check &lt;file&gt;</c> reads the file, refuses a leg with no
/// subject before sending anything, and prints one block per leg with its verdict and reason
/// first; <b>S61.6-02</b> - <c>--json</c> prints the control plane's answer unchanged.
/// </summary>
/// <remarks>
/// <para>
/// <b>The file is a person's document</b>, read by the product's one YAML parser, so it is
/// strict for the reason every airspace document is: a mistyped key ignored would check a plan
/// nobody wrote.
/// </para>
/// <para>
/// <b>Refused here before it is refused there.</b> The contract's own validator runs on this
/// side first, so a draft the control plane would 400 is never sent - and its sentence is the
/// same one, because it is the same function.
/// </para>
/// </remarks>
public class ItineraryCheckVerbTests
{
    private const string Plan = """
        intent: three findings in one bug
        legs:
          - subject: the icon
            work-kind: implement
            reason: the ticket names the icon fix on its own
          - subject: the padding
            work-kind: implement
            reason: a shared padding change, separate from the icon
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

    private static ItineraryCheck Answer() => new()
    {
        Planner = "plan",
        DestinationId = "the-plan",
        Legs =
        [
            new LegCheck
            {
                Subject = "the icon",
                WorkKind = "implement",
                Verdict = LegVerdicts.Opens,
                Reason = "A 'implement' flight would open, nominated because: the ticket names it",
                Obligations = [],
                Gates = [new LegGate { ObligationId = "code-reviewed", Approver = "a-lead" }],
                PassedOver = [],
            },
            new LegCheck
            {
                Subject = "the padding",
                WorkKind = "bugfix",
                Verdict = LegVerdicts.Refused,
                Reason = "Destination 'the-plan' does not open 'bugfix'.",
                Obligations = [],
                Gates = [],
                PassedOver = [],
            },
        ],
    };

    private static async Task<string> FileAsync(string text)
    {
        var path = Path.Combine(Path.GetTempPath(), $"itinerary-{Guid.NewGuid():N}.yaml");
        await File.WriteAllTextAsync(path, text);
        return path;
    }

    [Test]
    public async Task A_file_is_read_sent_and_answered_leg_by_leg()
    {
        await using var stub = new StubControlPlane { ItineraryAnswer = Answer() };

        var result = await Build(stub).CheckItineraryAsync(await FileAsync(Plan));

        var sent = stub.ObservedDrafts.Single();
        await Assert.That(sent.Planner).IsEqualTo("plan")
            .Because("a file that names no planner is checked against `plan`, the kind that "
                   + "proposes legs today.");
        await Assert.That(sent.Legs.Select(l => l.Subject)).IsEquivalentTo(["the icon", "the padding"]);
        await Assert.That(sent.Legs[1].After).IsEqualTo("the icon");
        await Assert.That(sent.Intent.Text).IsEqualTo("three findings in one bug");

        var text = VerbOutput.ToText(result);

        await Assert.That(text).Contains("the icon");
        await Assert.That(text).Contains("the padding");
        await Assert.That(text).Contains("does not open 'bugfix'")
            .Because("the reason is admission's sentence, and it is what a person acts on.");
        await Assert.That(text).Contains("a-lead")
            .Because("a gate is only useful to know about if it says who answers it.");

        // VERDICT FIRST, on each leg's own block: the word a person scans for comes before the
        // sentence that explains it.
        var refused = text.IndexOf("refused", StringComparison.Ordinal);
        var reason = text.IndexOf("does not open 'bugfix'", StringComparison.Ordinal);
        await Assert.That(refused).IsGreaterThanOrEqualTo(0);
        await Assert.That(refused).IsLessThan(reason);
    }

    [Test]
    public async Task A_leg_with_no_subject_is_refused_before_anything_is_sent()
    {
        await using var stub = new StubControlPlane { ItineraryAnswer = Answer() };

        var path = await FileAsync("""
            intent: one bug
            legs:
              - work-kind: implement
                reason: no subject on this one
            """);

        var refused = await Assert.ThrowsAsync<ItineraryRefusedException>(
            () => Build(stub).CheckItineraryAsync(path));

        await Assert.That(refused!.Message).Contains("Leg 1");
        await Assert.That(stub.ObservedDrafts).IsEmpty()
            .Because("a draft the control plane would refuse is not sent at all.");
    }

    [Test]
    public async Task A_mistyped_key_is_refused_naming_it()
    {
        await using var stub = new StubControlPlane { ItineraryAnswer = Answer() };

        var path = await FileAsync("""
            intent: one bug
            legs:
              - subject: the icon
                work-kind: implement
                reason: the ticket names it
                afer: the padding
            """);

        var refused = await Assert.ThrowsAsync<ItineraryRefusedException>(
            () => Build(stub).CheckItineraryAsync(path));

        await Assert.That(refused!.Message).Contains("afer")
            .Because("an order silently dropped would check a different plan from the one written.");
        await Assert.That(stub.ObservedDrafts).IsEmpty();
    }

    [Test]
    public async Task A_ticket_intent_is_a_ticket()
    {
        await using var stub = new StubControlPlane { ItineraryAnswer = Answer() };

        await Build(stub).CheckItineraryAsync(await FileAsync("""
            intent:
              provider: ado
              id: "18291"
            legs:
              - subject: the icon
                work-kind: implement
                reason: the ticket names it
            """));

        var intent = stub.ObservedDrafts.Single().Intent;

        await Assert.That(intent.Kind).IsEqualTo(FlightIntentKinds.Ticket)
            .Because("the kind is derived from the payload the way `gg fly` derives it, so a "
                   + "plan about a work item keeps every leg able to find that item.");
        await Assert.That(intent.Provider).IsEqualTo("ado");
        await Assert.That(intent.Id).IsEqualTo("18291");
    }

    [Test]
    public async Task The_control_planes_refusal_is_carried_through()
    {
        await using var stub = new StubControlPlane { ItineraryRefusal = "the planner said no, here is why" };
        var path = await FileAsync(Plan);

        var refused = await Assert.ThrowsAsync<ItineraryRefusedException>(
            () => Build(stub).CheckItineraryAsync(path));

        await Assert.That(refused!.Message).Contains("the planner said no, here is why");
    }

    [Test]
    public async Task Json_is_the_control_planes_answer_unchanged()
    {
        await using var stub = new StubControlPlane { ItineraryAnswer = Answer() };

        var result = await Build(stub).CheckItineraryAsync(await FileAsync(Plan));

        var printed = JsonSerializer.Deserialize<ItineraryCheck>(
            VerbOutput.ToJson(result), JsonSerializerOptions.Web);

        await Assert.That(JsonSerializer.Serialize(printed, JsonSerializerOptions.Web))
            .IsEqualTo(JsonSerializer.Serialize(Answer(), JsonSerializerOptions.Web));
    }
}
