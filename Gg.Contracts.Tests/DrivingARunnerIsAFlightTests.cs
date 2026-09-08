using System.Text.Json;
using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// The one new member a remote hand-flight needed, and the refusal beside it.
/// </summary>
/// <remarks>
/// <para>
/// <b>ADR-0013: a remote hand-flight is three things and only one is new.</b> A
/// flight directed at a runner already exists — <c>FlightLaunchRequest.Runner</c>
/// names a machine and the runner still PULLS — the channel is decision 3, and
/// <c>Attended</c> is the marker that tells the runner to open one instead of
/// running headless. What that buys is the security argument for free: the lease
/// authorises, the envelope scopes, the story records, and none of it was
/// invented for this.
/// </para>
/// <para>
/// <b>The risk was never that a runner can do dangerous things.</b> It already
/// runs an agent over customer code with credentials. The risk is capability
/// without governance — a path no lease authorises and no story records — which
/// is exactly what making it a flight removes.
/// </para>
/// </remarks>
public class DrivingARunnerIsAFlightTests
{
    /// <summary>
    /// What the wire actually is: camel case, and nulls omitted.
    /// </summary>
    /// <remarks>
    /// <b>WhenWritingNull is what makes "absent rather than null" true, and the
    /// type being nullable is what lets it.</b> Plain web defaults write
    /// <c>"attended": null</c> — the first version of this test used them and
    /// failed, which is the useful way round: the rule is a property of the
    /// producer's configuration AND the member's nullability together, and a
    /// non-nullable <c>bool</c> would serialise <c>false</c> under any
    /// configuration at all.
    /// </remarks>
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    [Test]
    public async Task A_headless_flight_says_nothing_new_on_the_wire()
    {
        // ABSENT RATHER THAN FALSE, which is this slice's rule 8. A member that
        // serialised as `"attended": false` would change every body in the
        // system on the day it shipped, and the two repositories are not
        // upgraded in step.
        var headless = JsonSerializer.Serialize(
            new FlightLaunchRequest { Name = "a-flight", Intent = new FlightIntent { Kind = FlightIntentKinds.Text, Text = "do" } },
            Wire);

        await Assert.That(headless).DoesNotContain("attended")
            .Because("nearly every flight is headless, so nearly every body must be unchanged.");

        // AND THE OTHER HALF OF WHY IT IS NULLABLE. A non-nullable bool is never
        // null, so no ignore condition can omit it - every body in the system
        // would have grown "attended": false on the day this shipped.
        await Assert.That(typeof(FlightLaunchRequest).GetProperty("Attended")!.PropertyType)
            .IsEqualTo(typeof(bool?))
            .Because("absent-on-the-wire needs a value that CAN be absent.");
    }

    [Test]
    public async Task A_headless_lease_says_nothing_new_either()
    {
        var granted = JsonSerializer.Serialize(ALease(), Wire);

        await Assert.That(granted).DoesNotContain("attended");

        await Assert.That(typeof(LeaseGranted).GetProperty("Attended")!.PropertyType)
            .IsEqualTo(typeof(bool?));
    }

    [Test]
    public async Task An_attended_flight_says_so_and_names_the_machine()
    {
        // BOTH, AND THEY ARE NOT ONE MEMBER. Runner says which machine; attended
        // says a person is at the other end. Attended with no runner is a flight
        // nobody can reach; a runner with no attended is the ordinary directed
        // flight that has worked since it shipped.
        var body = JsonSerializer.Serialize(
            new FlightLaunchRequest
            {
                Name = "a-flight",
                Intent = new FlightIntent { Kind = FlightIntentKinds.Text, Text = "drive it" },
                Runner = "01a06385-322f-7371-93a2-ce35db5c4fbe",
                Attended = true,
            },
            Wire);

        await Assert.That(body).Contains("\"attended\":true");
        await Assert.That(body).Contains("01a06385-322f-7371-93a2-ce35db5c4fbe");
    }

    [Test]
    public async Task The_lease_can_carry_it_to_the_runner()
    {
        // THE MACHINE THAT HAS TO ACT ON IT. The runner opens a channel because
        // the LEASE said so, which is what makes the channel's lifetime the
        // flight's - the machine that can end a channel is the one serving it.
        var granted = JsonSerializer.Serialize(ALease() with { Attended = true }, Wire);

        await Assert.That(granted).Contains("\"attended\":true");
    }

    [Test]
    public async Task Busy_is_a_refusal_where_absent_is_a_wait()
    {
        // THE WHOLE REASON THEY ARE TWO KINDS. An absent runner will come back,
        // so the flight waits. A busy one is somebody else's machine, and
        // nothing here queues behind it: a person at a terminal is the one
        // caller who cannot wait an unknown time for an answer.
        await Assert.That(ReasonKinds.FamilyOf(ReasonKinds.DirectedRunnerBusy))
            .IsEqualTo(ReasonFamilies.Refused);

        await Assert.That(ReasonKinds.FamilyOf(ReasonKinds.DirectedRunnerAbsent))
            .IsEqualTo(ReasonFamilies.Failed)
            .Because("its sibling is a wait, and filing them together would put a flight that "
                   + "is going to run in the bucket somebody was told no in.");
    }

    [Test]
    public async Task The_refusal_says_what_to_do_instead()
    {
        var said = Reason.Sentence(
            ReasonKinds.DirectedRunnerBusy, ["laptop-7"]);

        await Assert.That(said).Contains("laptop-7");
        await Assert.That(said).Contains("nothing is queued")
            .Because("a person who thinks they are in a queue waits, and there is no queue.");
        await Assert.That(said.Length).IsGreaterThan(60)
            .Because("a refusal whose whole account is a word is one nobody can act on.");
    }

    [Test]
    public async Task The_new_kind_is_in_the_closed_vocabulary()
    {
        // The liveness half: a kind with a sentence and a family that nothing
        // lists is a kind no consumer will accept.
        await Assert.That(ReasonKinds.All).Contains(ReasonKinds.DirectedRunnerBusy);

        foreach (var kind in ReasonKinds.All)
        {
            await Assert.That(ReasonKinds.FamilyOf(kind)).IsNotNull()
                .Because($"'{kind}' is listed, so it has to have a family.");
        }
    }

    private static LeaseGranted ALease() => new()
    {
        LeaseId = "a-lease",
        Generation = 1,
        FlightId = "a-flight",
        FlightNumber = "GG-1",
        Repos = [],
        Credentials = [],
        ClassificationCeiling = "internal",
        ClassificationRules = [],
        ExpiresAt = DateTimeOffset.UnixEpoch,
        RenewWithinSeconds = 30,
    };
}
