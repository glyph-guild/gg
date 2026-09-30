using Gg.Contracts;
using Gg.Contracts.Description;
using Gg.Runner.Environments;

namespace Gg.Runner.Tests;

/// <summary>
/// A flight whose work is done brings its stack down, and nothing anywhere
/// depends on it having happened.
/// </summary>
/// <remarks>
/// <para>
/// <b>Slice fifty-six rule 3: a courtesy, never the mechanism.</b> The reclaim on
/// the way in (rule 2) is what makes the next flight's environment trustworthy,
/// because a flight that is killed keeps no promises. This exists for the time
/// BETWEEN flights: an instance holding thirteen idle containers holds their
/// ports and their memory, and a person looking at the host sees a stack that
/// finished hours ago.
/// </para>
/// <para>
/// <b>Politely means stopping, not killing, and that is the whole difference from
/// the reclaim.</b> A stop sends the app its termination signal and waits, so it
/// flushes what it was writing and closes what it held. The reclaim forces,
/// because it is cleaning up after something already dead and nothing is owed a
/// graceful exit twice.
/// </para>
/// <para>
/// <b>Nothing reports it, which is how rule 3 is enforced rather than
/// promised.</b> No fact, no lease member, nothing on the wire says a stack was
/// brought down — so no later decision can read it, and the absence is asserted
/// below.
/// </para>
/// </remarks>
public class AStackIsBroughtDownPolitelyTests
{
    private sealed class Watched : IInstanceDaemon
    {
        internal List<string> Present { get; } = [];

        internal List<string> Order { get; } = [];

        internal Exception? Throws { get; set; }

        public Task<IReadOnlyList<string>> ContainersAsync(
            CancellationToken cancellationToken = default)
        {
            Order.Add("list");
            return Throws is not null
                ? Task.FromException<IReadOnlyList<string>>(Throws)
                : Task.FromResult<IReadOnlyList<string>>([.. Present]);
        }

        public Task StopContainerAsync(string id, CancellationToken cancellationToken = default)
        {
            Order.Add($"stop:{id}");
            return Task.CompletedTask;
        }

        public Task RemoveContainerAsync(string id, CancellationToken cancellationToken = default)
        {
            Order.Add($"remove:{id}");
            return Task.CompletedTask;
        }

        public Task<int> PruneNetworksAsync(CancellationToken cancellationToken = default)
        {
            Order.Add("networks");
            return Task.FromResult(1);
        }

        public Task<int> PruneVolumesAsync(CancellationToken cancellationToken = default)
        {
            Order.Add("volumes");
            return Task.FromResult(1);
        }
    }

    [Test]
    public async Task Each_container_is_stopped_before_it_is_removed()
    {
        // POLITELY IS THE ASSERTION. A stop sends the app its termination signal
        // and waits; removing without one is a SIGKILL, and an app killed
        // mid-write is the thing a courtesy exists to avoid.
        var daemon = new Watched();
        daemon.Present.Add("abc123");

        _ = await InstanceReclaim.BringDownAsync(daemon);

        await Assert.That(string.Join(" -> ", daemon.Order))
            .IsEqualTo("list -> stop:abc123 -> remove:abc123 -> networks -> volumes");
    }

    [Test]
    public async Task The_reclaim_does_not_stop_anything_first()
    {
        // THE DIFFERENCE, STATED AS A TEST. The reclaim is cleaning up after
        // something already dead: waiting politely for a container whose process
        // is gone buys a timeout per container on the way IN, which is the one
        // path a flight is waiting on.
        var daemon = new Watched();
        daemon.Present.Add("abc123");

        _ = await InstanceReclaim.EmptyAsync(daemon);

        await Assert.That(daemon.Order.Any(step => step.StartsWith("stop:", StringComparison.Ordinal)))
            .IsFalse()
            .Because("nothing is owed a graceful exit twice, and a timeout per dead container is "
                   + "paid by the flight that is waiting to start.");
    }

    [Test]
    public async Task An_instance_with_nothing_in_it_is_brought_down_quietly()
    {
        var brought = await InstanceReclaim.BringDownAsync(new Watched());

        await Assert.That(brought.Containers).IsEqualTo(0);
    }

    [Test]
    public async Task Nothing_on_the_wire_says_a_stack_was_brought_down()
    {
        // RULE 3, ENFORCED RATHER THAN PROMISED. If no fact and no lease member
        // carries it, no later decision can read it as a precondition - which is
        // the only way "a courtesy" stays a courtesy once somebody wants to
        // optimise the reclaim away.
        //
        // The same shape as rule 13's absence: a thing that cannot be observed
        // cannot be depended upon.
        var named = FactKinds.All
            .Where(kind => kind.Contains("teardown", StringComparison.OrdinalIgnoreCase)
                        || kind.Contains("brought", StringComparison.OrdinalIgnoreCase)
                        || kind.Contains("down", StringComparison.OrdinalIgnoreCase))
            .ToList();

        await Assert.That(named).IsEmpty()
            .Because("a fact saying the stack came down is a fact the next flight's reclaim "
                   + "could be skipped on, and the flight that reports it is the one that "
                   + "might have died instead. Found: " + string.Join(", ", named));

        await Assert.That(ProtocolSurface.JsonMembers[typeof(LeaseLoop)]
            .Any(member => member.Contains("down", StringComparison.OrdinalIgnoreCase)))
            .IsFalse();
    }
}
