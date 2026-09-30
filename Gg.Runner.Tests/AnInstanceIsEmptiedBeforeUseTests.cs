using Gg.Runner.Environments;

namespace Gg.Runner.Tests;

/// <summary>
/// A flight granted an environment instance finds its daemon empty of the last
/// flight's containers and warm with its images.
/// </summary>
/// <remarks>
/// <para>
/// <b>Slice fifty-six rule 2: on the way in, never on the way out.</b> A flight
/// that is killed keeps no promises — no <c>finally</c> runs, no handler fires —
/// so a tear-down at landing cannot be what makes the next flight's environment
/// trustworthy. The runner already reached this conclusion for working trees:
/// <i>"cleanup cannot be only a disposal… a startup sweep removes what a previous
/// life left."</i>
/// </para>
/// <para>
/// <b>It needs no knowledge of what is in there</b> — rule 1, and it is
/// ADR-0034's central property doing work. An instance's daemon holds only that
/// instance's things, so "remove everything" is exactly right and cannot reach
/// another instance, the pool's members, or the host.
/// </para>
/// <para>
/// <b>Images survive, and the PORT is what guarantees it.</b> Rule 4 says warmth
/// is the image store; this does not test that images are spared so much as make
/// it unrepresentable — <see cref="IInstanceDaemon"/> has no method that removes
/// an image, so no reclaim can. A test can be deleted; a method that does not
/// exist cannot be called.
/// </para>
/// <para>
/// <b>And it never falls back to the host's own daemon</b> (S56.2-03). The client
/// is bound to one socket path by construction rather than reading an ambient
/// <c>DOCKER_HOST</c>, so an unreachable instance is a connect error and not a
/// reclaim that quietly emptied something else.
/// </para>
/// </remarks>
public class AnInstanceIsEmptiedBeforeUseTests
{
    /// <summary>A daemon that records what it was asked to do.</summary>
    private sealed class Watched : IInstanceDaemon
    {
        internal List<string> Present { get; } = [];

        internal List<string> Removed { get; } = [];

        internal int NetworkPrunes { get; private set; }

        internal int VolumePrunes { get; private set; }

        internal Exception? Throws { get; set; }

        /// <summary>
        /// What a prune finds. Zero by default, because an instance with no
        /// containers has no networks or volumes left attached to anything —
        /// a fake that returned otherwise would describe a daemon that cannot
        /// exist, and one test read it as "something was removed".
        /// </summary>
        internal int Networks { get; set; }

        internal int Volumes { get; set; }

        public Task<IReadOnlyList<string>> ContainersAsync(
            CancellationToken cancellationToken = default) =>
            Throws is not null
                ? Task.FromException<IReadOnlyList<string>>(Throws)
                : Task.FromResult<IReadOnlyList<string>>([.. Present]);

        public Task RemoveContainerAsync(
            string id, CancellationToken cancellationToken = default)
        {
            Removed.Add(id);
            return Task.CompletedTask;
        }

        public Task<int> PruneNetworksAsync(CancellationToken cancellationToken = default)
        {
            NetworkPrunes++;
            return Task.FromResult(Networks);
        }

        public Task<int> PruneVolumesAsync(CancellationToken cancellationToken = default)
        {
            VolumePrunes++;
            return Task.FromResult(Volumes);
        }
    }

    [Test]
    public async Task Every_container_in_the_instance_is_removed()
    {
        var daemon = new Watched();
        daemon.Present.AddRange(["abc123", "def456"]);

        var emptied = await InstanceReclaim.EmptyAsync(daemon);

        await Assert.That(daemon.Removed).IsEquivalentTo((string[])["abc123", "def456"])
            .Because("the last flight's stack is still running and holding its ports, and this "
                   + "is the only thing in the system that takes it down after a flight died.");
        await Assert.That(emptied.Containers).IsEqualTo(2);
    }

    [Test]
    public async Task Networks_and_volumes_go_with_them()
    {
        // A STACK IS NOT ONLY CONTAINERS. Aspire creates a network per run and
        // volumes for anything stateful; leaving those behind leaks names a
        // second run collides with, which reads as a stack that will not start.
        var daemon = new Watched { Networks = 2, Volumes = 3 };
        daemon.Present.Add("abc123");

        var emptied = await InstanceReclaim.EmptyAsync(daemon);

        await Assert.That(daemon.NetworkPrunes).IsEqualTo(1);
        await Assert.That(daemon.VolumePrunes).IsEqualTo(1);
        await Assert.That(emptied.Networks).IsEqualTo(2);
        await Assert.That(emptied.Volumes).IsEqualTo(3);
    }

    [Test]
    public async Task Containers_go_before_the_prunes()
    {
        // ORDER MATTERS AND IS NOT COSMETIC. A network a running container is
        // attached to cannot be pruned, and a volume a running container mounts
        // cannot be either - so pruning first would report success having
        // removed nothing.
        var daemon = new OrderWatched();
        daemon.Present.Add("abc123");

        _ = await InstanceReclaim.EmptyAsync(daemon);

        // Compared as a joined string, because the ORDER is the assertion and a
        // set comparison passes on a reclaim that pruned first.
        await Assert.That(string.Join(" -> ", daemon.Order))
            .IsEqualTo("containers -> remove:abc123 -> networks -> volumes");
    }

    private sealed class OrderWatched : IInstanceDaemon
    {
        internal List<string> Present { get; } = [];

        internal List<string> Order { get; } = [];

        public Task<IReadOnlyList<string>> ContainersAsync(
            CancellationToken cancellationToken = default)
        {
            Order.Add("containers");
            return Task.FromResult<IReadOnlyList<string>>([.. Present]);
        }

        public Task RemoveContainerAsync(
            string id, CancellationToken cancellationToken = default)
        {
            Order.Add($"remove:{id}");
            return Task.CompletedTask;
        }

        public Task<int> PruneNetworksAsync(CancellationToken cancellationToken = default)
        {
            Order.Add("networks");
            return Task.FromResult(0);
        }

        public Task<int> PruneVolumesAsync(CancellationToken cancellationToken = default)
        {
            Order.Add("volumes");
            return Task.FromResult(0);
        }
    }

    [Test]
    public async Task An_instance_that_was_already_empty_says_it_removed_nothing()
    {
        // NOT AN ERROR, and worth its own answer: this is the ordinary state of a
        // fresh slot and of one whose last flight brought its own stack down
        // politely. S56.2-04 reports it, and a reclaim that could not tell the
        // two apart would have nothing to report.
        var emptied = await InstanceReclaim.EmptyAsync(new Watched());

        await Assert.That(emptied.Containers).IsEqualTo(0);
        await Assert.That(emptied.RemovedAnything).IsFalse();
    }

    [Test]
    public async Task A_daemon_that_cannot_be_reached_is_not_a_reclaim_of_something_else()
    {
        // S56.2-03, AND THE DANGEROUS ONE. A reclaim that fell back to an ambient
        // DOCKER_HOST would empty the pool host's own daemon - every member on
        // the machine - while reporting that it had tidied an instance. The
        // failure has to escape.
        var daemon = new Watched { Throws = new HttpRequestException("no such socket") };

        await Assert.That(async () => await InstanceReclaim.EmptyAsync(daemon))
            .Throws<HttpRequestException>()
            .Because("a flight whose environment cannot be prepared has to fail loudly; "
                   + "anything softer is a flight standing its stack up beside the last "
                   + "one's, or somewhere else entirely.");
    }

    [Test]
    public async Task The_port_offers_no_way_to_remove_an_image()
    {
        // RULE 4, MADE UNREPRESENTABLE. Warmth is the image store: a reclaim that
        // took images would make every flight cold and defeat the slice that
        // built this. Asserted against the port's own surface rather than against
        // behaviour, because a behaviour test can be satisfied by a reclaim that
        // simply has not got round to it yet.
        var offered = typeof(IInstanceDaemon).GetMethods().Select(m => m.Name).ToList();

        await Assert.That(offered.Any(name =>
            name.Contains("Image", StringComparison.OrdinalIgnoreCase))).IsFalse()
            .Because("no method that removes an image means no reclaim can remove one, which "
                   + "is a guarantee a test cannot be deleted out of. Offered: "
                   + string.Join(", ", offered));
    }
}
