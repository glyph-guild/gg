using System.Reflection;
using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// How a machine tells a control plane what its allowance has left.
/// </summary>
/// <remarks>
/// <para>
/// <b>Its own record and its own prefix, which is <c>PoolAttestation</c>'s
/// argument unchanged.</b> The fact plumbing is lease-welded at four points —
/// a batch carries a generation, the endpoint is lease-scoped, the ship call
/// takes a lease id, and the idempotency key needs a flight id — and a reading
/// of what a machine has spent has no flight. So it is not a fact, and the
/// vocabulary tests must go on saying so.
/// </para>
/// <para>
/// <b>And not on the heartbeat, which is the tempting shortcut.</b>
/// <c>RunnerHeartbeat</c> is declared <i>"Liveness ONLY … deliberately no field
/// here by which a runner reports its status"</i>, on the argument that a
/// runner able to report something about itself can report it while dead. A
/// reading carries its own <c>MeasuredAt</c> for exactly that reason: the
/// control plane can see how old it is instead of believing it.
/// </para>
/// <para>
/// <b>A name, never an account.</b> What crosses is the string a person put in
/// their own configuration file. Nothing here can carry an account id, an
/// email, an organisation uuid or a token, and the control plane is not able to
/// tell two subscriptions apart — which is the same boundary the whole system
/// is shaped around, arriving at a new surface.
/// </para>
/// </remarks>
public class AnAllowanceCrossesAsItsOwnRecordTests
{
    [Test]
    public async Task A_runner_posts_a_reading_to_its_own_endpoint()
    {
        var declared = ProtocolSurface.Endpoints.SingleOrDefault(
            e => e.Path == "/v1/allowances/readings" && e.Method == "POST");

        await Assert.That(declared).IsNotNull()
            .Because("the two repositories cannot reference each other, so this "
                   + "declaration is the only thing holding them together.");

        await Assert.That(declared!.Audience).IsEqualTo(Audience.Runner);
        await Assert.That(declared.Request).IsEqualTo(typeof(AllowanceReading));
        await Assert.That(declared.Response).IsNull()
            .Because("a write endpoint answers with refusals and never with read data. "
                   + "What the control plane made of it is a query resource.");
        await Assert.That(declared.Statuses).Contains(202);
        await Assert.That(declared.RequiredHeaders).Contains(ProtocolSurface.RunnerHeader);
    }

    [Test]
    public async Task A_person_reads_what_their_tenant_has_spent()
    {
        var declared = ProtocolSurface.Endpoints.SingleOrDefault(
            e => e.Path == "/v1/allowances" && e.Method == "GET");

        await Assert.That(declared).IsNotNull();
        await Assert.That(declared!.Audience).IsEqualTo(Audience.Developer);
        await Assert.That(declared.Response).IsEqualTo(typeof(AllowanceList));
        await Assert.That(declared.RequiredHeaders).Contains(ProtocolSurface.SessionHeader);
    }

    [Test]
    public async Task The_prefix_is_governed_so_nothing_undeclared_serves_under_it()
    {
        await Assert.That(ProtocolSurface.GovernedPrefixes).Contains("/v1/allowances")
            .Because("a runner-audience route nobody declared is an unaudited way for a "
                   + "runner to reach the control plane - the argument /v1/leases and "
                   + "/v1/pools both came in on.");
    }

    [Test]
    public async Task A_reading_is_not_a_fact()
    {
        await Assert.That(typeof(AllowanceReading).GetCustomAttribute<FactKindAttribute>())
            .IsNull()
            .Because("the fact plumbing is lease-welded at four points and a reading has "
                   + "no flight. PoolAttestation settled this and the reasoning is the "
                   + "same one.");

        await Assert.That(FactKinds.All.Any(k => k.Contains("allowance", StringComparison.Ordinal)))
            .IsFalse();
    }

    [Test]
    public async Task It_does_not_ride_the_heartbeat()
    {
        var members = typeof(RunnerHeartbeat)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToList();

        await Assert.That(members).IsNotEmpty()
            .Because("a reflection that found nothing would make the assertion below "
                   + "pass over a heartbeat carrying anything at all.");

        await Assert.That(members.Any(m => m.Contains("Allowance", StringComparison.Ordinal)))
            .IsFalse()
            .Because("RunnerHeartbeat is liveness only, deliberately, because a runner "
                   + "able to report something about itself can report it while dead. "
                   + "A reading carries its own MeasuredAt so the control plane can see "
                   + "how old it is instead of believing it.");
    }

    [Test]
    public async Task The_window_kinds_are_a_closed_set()
    {
        await Assert.That(AllowanceWindows.All).IsEquivalentTo(
            new[] { AllowanceWindows.Session, AllowanceWindows.Week });

        await Assert.That(AllowanceReading.Validate(Reading() with
        {
            Windows = [new AllowanceWindow
            {
                Kind = "fortnight", Tokens = 1, Since = DateTimeOffset.UnixEpoch,
            }],
        })).IsNotNull()
            .Because("the only safe answer to an unknown window is to halt. A control "
                   + "plane that ignored one would under-report what a machine spent, "
                   + "and under-reporting is the direction that spends somebody's "
                   + "allowance for them.");
    }

    [Test]
    public async Task A_reading_that_names_no_allowance_is_refused()
    {
        await Assert.That(AllowanceReading.Validate(Reading() with { Allowance = "  " }))
            .IsNotNull()
            .Because("blank is not unset. An unset allowance is a machine that reports "
                   + "nothing at all, which never reaches this endpoint.");
    }

    private static AllowanceReading Reading() => new()
    {
        Allowance = "kdee-max",
        MeasuredAt = DateTimeOffset.UnixEpoch,
        Windows =
        [
            new() { Kind = AllowanceWindows.Session, Tokens = 10, Since = DateTimeOffset.UnixEpoch },
        ],
    };
}
