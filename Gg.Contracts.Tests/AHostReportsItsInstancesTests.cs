using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// A host tells the control plane which environment instances it has, on a
/// route of its own.
/// </summary>
/// <remarks>
/// <para>
/// <b>good-grief#617: the table had every reader and no writer.</b> The claim's
/// pick predicate, the anti-join that takes an instance and the lease that names
/// it to a runner all shipped; nothing ever recorded that an instance existed,
/// so a work kind declaring <c>hosts:</c> produced a flight stepped over for
/// ever. The owner's answer (2026-09-29) is that the HOST attests what it has.
/// </para>
/// <para>
/// <b>Its own route, never a field on the beat — and the surface already says
/// why.</b> <c>POST /v1/runner/machine/reading</c> carries that reasoning in as
/// many words: the heartbeat is liveness only, and <i>"a machine that could
/// report its load on the beat could report it while dead"</i>. It applies here
/// with more force than it does to a load figure: a stale list still naming
/// <c>gg-env-1</c> does not merely mislead a reader, it has the claim hand a
/// flight to a daemon that is gone.
/// </para>
/// <para>
/// <b>A report is the WHOLE list, which is what makes a teardown possible.</b>
/// Per-instance news can say a thing appeared and can never say one went: a slot
/// removed from a host emits nothing, by construction. So a report is "these are
/// the instances I have", and the far side reconciles — which is also why an
/// empty list has to be legal and meaningful.
/// </para>
/// </remarks>
public class AHostReportsItsInstancesTests
{
    private static Endpoint TheRoute() =>
        ProtocolSurface.Endpoints.Single(e =>
            string.Equals(e.Path, "/v1/runner/environment/instances", StringComparison.Ordinal));

    [Test]
    public async Task The_route_is_declared_for_a_runner_to_post_to()
    {
        var route = TheRoute();

        await Assert.That(route.Method).IsEqualTo("POST");
        await Assert.That(route.Audience).IsEqualTo(Audience.Runner)
            .Because("only a machine can answer what it has, and a developer session asking "
                   + "would be a person telling the control plane about somebody's disk.");
        await Assert.That(route.Request).IsEqualTo(typeof(EnvironmentInstanceReading));
    }

    [Test]
    public async Task It_is_accepted_and_answers_nothing()
    {
        // THE MACHINE READING'S SHAPE EXACTLY. A measurement is not a question,
        // so there is nothing to give back - and a runner that had to read an
        // answer would have a bookkeeping route it could be blocked on.
        var route = TheRoute();

        await Assert.That(route.Statuses).Contains(202);
        await Assert.That(route.Response).IsNull();
    }

    [Test]
    public async Task The_reading_carries_when_it_was_taken()
    {
        // A LIST WITH NO CLOCK CANNOT BE JUDGED STALE, and this route exists
        // BECAUSE a stale list is dangerous. Same reason MachineReading carries
        // MeasuredAt rather than letting the far side stamp arrival.
        await Assert.That(ProtocolSurface.JsonMembers[typeof(EnvironmentInstanceReading)])
            .Contains("measuredAt");
    }

    [Test]
    public async Task The_reading_carries_the_instances_and_each_says_what_it_serves()
    {
        await Assert.That(ProtocolSurface.JsonMembers[typeof(EnvironmentInstanceReading)])
            .Contains("instances");

        var each = ProtocolSurface.JsonMembers[typeof(EnvironmentInstanceSeen)];

        await Assert.That(each).Contains("instance");
        await Assert.That(each).Contains("environment")
            .Because("the socket is named after the INSTANCE and a grant is made against the "
                   + "ENVIRONMENT, so a report carrying one of the two answers nothing. An "
                   + "environment has many instances; that is the whole reason a grant exists.");
    }

    [Test]
    public async Task An_empty_reading_is_a_statement_rather_than_a_refusal()
    {
        // THE ONE THING THAT CAN EVER RETIRE AN INSTANCE. A host that has the
        // root and no slots in it is saying "I have none" - and validation that
        // rejected that would leave a dead slot grantable for ever, which is
        // the failure this whole route exists to prevent.
        await Assert.That(EnvironmentInstanceReading.Validate(new EnvironmentInstanceReading
        {
            MeasuredAt = DateTimeOffset.UnixEpoch,
            Instances = [],
        })).IsNull();
    }

    [Test]
    public async Task A_nameless_instance_is_refused()
    {
        // An instance with no name resolves to the environment root itself, and
        // an environment with no name matches no chart entry. Both are a host
        // reporting something nobody can act on.
        foreach (var bad in (EnvironmentInstanceSeen[])
                 [new() { Environment = "ui", Instance = "  " },
                  new() { Environment = "", Instance = "gg-env-1" }])
        {
            await Assert.That(EnvironmentInstanceReading.Validate(new EnvironmentInstanceReading
            {
                MeasuredAt = DateTimeOffset.UnixEpoch,
                Instances = [bad],
            })).IsNotNull();
        }
    }

    [Test]
    public async Task One_instance_is_reported_once()
    {
        // A list naming a slot twice is a host that cannot count its own users,
        // and the far side would reconcile against whichever row it read last.
        await Assert.That(EnvironmentInstanceReading.Validate(new EnvironmentInstanceReading
        {
            MeasuredAt = DateTimeOffset.UnixEpoch,
            Instances =
            [
                new() { Environment = "ui", Instance = "gg-env-1" },
                new() { Environment = "api", Instance = "gg-env-1" },
            ],
        })).IsNotNull()
            .Because("an instance is a UNIX user and a host has one of each name, so two "
                   + "entries for it are two answers to a question with one.");
    }
}
