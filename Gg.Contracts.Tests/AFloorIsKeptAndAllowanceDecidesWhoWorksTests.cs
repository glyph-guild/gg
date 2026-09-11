using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>Source-generated, because Gg.Contracts holds no reflection path.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(WhoAmI))]
internal sealed partial class AdminJson : JsonSerializerContext;

/// <summary>
/// The share a lender keeps, the override that spends it, and targeting by spend.
/// </summary>
/// <remarks>
/// <para>
/// <b>"Floor", because three better words are taken.</b> "Reservation" is
/// <c>FleetReservation</c> — a runner held for one person. "Budget" is three
/// other things. "Allowance" is now the metered thing itself. A floor is the
/// share of a window fleet work may not spend below.
/// </para>
/// <para>
/// <b>A fraction, not a token count.</b> The ceiling lives on the machines and
/// a person setting a floor is saying <i>keep a third of it for me</i> — which
/// stays true when the plan changes, and a token count does not.
/// </para>
/// <para>
/// <b>The hard stop gets its own claim state.</b> A runner narrowed away inside
/// the matcher answers <c>pending</c>, which is what an idle fleet answers, and
/// collapsing those two is the defect <c>waiting</c> and then <c>parked</c> were
/// each added to fix. This is the third instance of the same argument, so the
/// value is added rather than reused.
/// </para>
/// <para>
/// <b>Targeting is work-kind-only, because it is a governance regime.</b> A
/// tenant varies it per kind in the envelope layer document, which is where
/// everything a tenant may vary already lives.
/// </para>
/// </remarks>
public class AFloorIsKeptAndAllowanceDecidesWhoWorksTests
{
    [Test]
    public async Task A_floor_is_a_fraction_of_a_window_and_nothing_else()
    {
        await Assert.That(AllowanceFloor.Validate(new AllowanceFloor
        {
            SessionFraction = 0.3,
            WeekFraction = 0.25,
        })).IsNull();

        await Assert.That(AllowanceFloor.Validate(new AllowanceFloor())).IsNotNull()
            .Because("a floor that keeps nothing is not a floor, and it is the shape a "
                   + "person sends when they meant to clear one. DELETE says that "
                   + "unambiguously; an empty body would leave two ways to mean it.");

        await Assert.That(AllowanceFloor.Validate(
            new AllowanceFloor { WeekFraction = 1.0 })).IsNotNull()
            .Because("keeping the whole window means lending nothing, which is what not "
                   + "naming an allowance already says - and it would read on a screen "
                   + "as a machine that is broken rather than one nobody lent.");

        await Assert.That(AllowanceFloor.Validate(
            new AllowanceFloor { SessionFraction = -0.1 })).IsNotNull();
    }

    [Test]
    public async Task A_person_sets_and_clears_their_own_floor()
    {
        var set = ProtocolSurface.Endpoints.SingleOrDefault(
            e => e.Path == "/v1/allowances/{name}/floor" && e.Method == "PUT");

        await Assert.That(set).IsNotNull();
        await Assert.That(set!.Audience).IsEqualTo(Audience.Developer)
            .Because("a floor is a person's declaration about their own subscription. A "
                   + "runner that could set one could widen its own queue, which is the "
                   + "argument the reservation routes came in on.");
        await Assert.That(set.Request).IsEqualTo(typeof(AllowanceFloor));
        await Assert.That(set.Statuses).Contains(403)
            .Because("somebody else's allowance is not theirs to reserve.");

        var cleared = ProtocolSurface.Endpoints.SingleOrDefault(
            e => e.Path == "/v1/allowances/{name}/floor" && e.Method == "DELETE");

        await Assert.That(cleared).IsNotNull();
        await Assert.That(cleared!.Statuses).DoesNotContain(409)
            .Because("clearing a floor nobody set is the state the caller asked for - the "
                   + "reason DELETE on a reservation carries no conflict either.");
    }

    [Test]
    public async Task An_override_is_an_admins_act_and_it_expires()
    {
        var declared = ProtocolSurface.Endpoints.SingleOrDefault(
            e => e.Path == "/v1/allowances/{name}/override" && e.Method == "POST");

        await Assert.That(declared).IsNotNull();
        await Assert.That(declared!.Request).IsEqualTo(typeof(AllowanceOverrideRequest));
        await Assert.That(declared.Statuses).Contains(403);

        await Assert.That(AllowanceOverrideRequest.Validate(
            new AllowanceOverrideRequest { Minutes = 0, Reason = "urgent" })).IsNotNull()
            .Because("an override with no end is a floor somebody deleted without saying "
                   + "so. Every one of these expires.");

        await Assert.That(AllowanceOverrideRequest.Validate(
            new AllowanceOverrideRequest { Minutes = 60, Reason = "  " })).IsNotNull()
            .Because("it spends somebody else's allowance, and the person it was taken "
                   + "from reads this sentence. A blank one is worse than none because "
                   + "it looks like an answer.");
    }

    [Test]
    public async Task The_summary_says_who_owns_it_what_is_kept_and_what_is_spending_it()
    {
        var members = ProtocolSurface.JsonMembers[typeof(AllowanceSummary)];

        await Assert.That(members).Contains("owners")
            .Because("without it nobody can be shown their own, and every person in the "
                   + "tenant sees every allowance - which is what happens today.");
        await Assert.That(members).Contains("floor");
        await Assert.That(members).Contains("override")
            .Because("an override nobody can see is a floor that silently stopped "
                   + "protecting, which is the one outcome the hard stop exists to "
                   + "prevent.");
    }

    [Test]
    public async Task A_machine_told_its_allowance_is_spent_is_not_told_it_is_idle()
    {
        await Assert.That(LeaseClaimStates.All).Contains(LeaseClaimStates.AllowanceSpent);

        await Assert.That(LeaseClaimStates.AllowanceSpent)
            .IsNotEqualTo(LeaseClaimStates.Pending);
        await Assert.That(LeaseClaimStates.AllowanceSpent)
            .IsNotEqualTo(LeaseClaimStates.Parked)
            .Because("parking is a person withholding a MACHINE; this is a subscription "
                   + "with nothing left to lend. The same machine can work again in an "
                   + "hour without anybody touching it, and a person reading `parked` "
                   + "would go looking for who parked it.");
    }

    [Test]
    public async Task Targeting_is_something_a_work_kind_decides()
    {
        await Assert.That(AllowanceTargeting.All).IsEquivalentTo(
            new[] { AllowanceTargeting.Any, AllowanceTargeting.LeastSpent });

        var member = typeof(Envelope).GetProperty(nameof(Envelope.Targeting));

        await Assert.That(member).IsNotNull();

        var composes = member!.GetCustomAttribute<ComposesAttribute>();

        await Assert.That(composes).IsNotNull();
        await Assert.That(composes!.Operator).IsEqualTo(MergeOperators.WorkKindOnly)
            .Because("a work kind selects a governance regime. Root-only would make one "
                   + "answer serve triage and implementation alike, and narrowing has no "
                   + "meaning on a choice of two.");

        await Assert.That(Envelope.Validate(AnEnvelope() with { Targeting = "cheapest" }))
            .IsNotNull()
            .Because("an unknown strategy silently falling back to `any` is a knob "
                   + "somebody believes they turned.");
    }

    [Test]
    public async Task Whoami_says_whether_this_person_administers_the_tenant()
    {
        var members = ProtocolSurface.JsonMembers[typeof(WhoAmI)];

        await Assert.That(members).Contains("isAdmin");

        // ABSENT MEANS NO. A control plane too old to send it omits the member,
        // and the default has to be the narrow answer rather than a throw or a
        // promotion - the argument Notices already carries, with a sharper
        // consequence.
        var older = JsonSerializer.Deserialize(
            """
            {"principalId":"p","principalDisplay":"somebody","tenantId":"t",
             "expiresAt":"2026-01-01T00:00:00Z"}
            """,
            AdminJson.Default.WhoAmI);

        await Assert.That(older!.IsAdmin).IsFalse();
    }

    private static Envelope AnEnvelope() => new()
    {
        Context = new ContextBinding { Scope = "src/", Constitution = "a-constitution" },
        Obligations = [],
        Loops = [],
        Destinations = [],
    };
}
