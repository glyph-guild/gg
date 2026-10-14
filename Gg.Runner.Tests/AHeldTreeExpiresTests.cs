using Gg.Contracts;
using Gg.Contracts.Description;
using Gg.Runner.Vcs;

namespace Gg.Runner.Tests;

/// <summary>
/// A tree held for handoff is removed once its retention has passed, and the runner says so.
/// </summary>
/// <remarks>
/// <para>
/// <b>Found on the fleet, 2026-10-14.</b> <see cref="HandoffRoot.Expire"/> was written, tested
/// and never called: vmlinux002 held 43 trees, 18 GB, the oldest from 2026-09-20 - nearly four
/// weeks past a seven-day retention nothing applied.
/// </para>
/// <para>
/// <b>When a flight ends</b>, because that is when a tree is added: a runner that has run for
/// a day is a runner whose startup has long passed.
/// </para>
/// </remarks>
public class AHeldTreeExpiresTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static LeaseGranted ALeaseFor(GitFixture fixture) => new()
    {
        LeaseId = "lease-1",
        Generation = 1,
        FlightId = "flight-1",
        FlightNumber = FlightRef.Format(1042),
        Repos =
        [
            new LeaseRepoRef
            {
                Provider = LocalVcsAdapter.ProviderKey,
                Slug = fixture.BarePath,
                PinnedRef = "refs/heads/main",
            },
        ],
        Credentials = [],
        ClassificationCeiling = Classifications.Internal,
        ClassificationRules = ClassificationRules.Default,
        ExpiresAt = T0.AddMinutes(10),
        RenewWithinSeconds = 5,
    };

    private static string ATreeAt(string path)
    {
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, "work.txt"), "an agent's work");
        return path;
    }

    [Test]
    public async Task A_tree_held_longer_than_its_retention_is_removed_when_a_flight_ends()
    {
        using var fixture = new GitFixture();
        using var trees = new ScratchTreeRoot();

        // HELD EIGHT DAYS AGO, by a runner long since restarted; and one held an hour ago,
        // which must survive.
        var now = DateTimeOffset.UtcNow;
        new HandoffRoot(trees.Handoff.Path, new FixedTime(now - TimeSpan.FromDays(8)))
            .Hold("flight-old", ATreeAt(trees.Root.For("flight-old")));
        new HandoffRoot(trees.Handoff.Path, new FixedTime(now - TimeSpan.FromHours(1)))
            .Hold("flight-recent", ATreeAt(trees.Root.For("flight-recent")));

        var clock = new MovableClock(T0);
        var protocol = new FakeProtocol();
        protocol.Claims.Enqueue(new ClaimResult.Granted(ALeaseFor(fixture)));
        var observer = new RecordingObserver();

        using var stopping = new CancellationTokenSource();
        observer.OnEvent = e =>
        {
            if (e.StartsWith("released:", StringComparison.Ordinal))
            {
                stopping.Cancel();
            }
        };

        await new RunnerLoop(protocol, clock,
                (span, token) =>
                {
                    token.ThrowIfCancellationRequested();
                    clock.Advance(span);
                    return Task.CompletedTask;
                },
                observer, new NoCredentialResolver(), trees.Workspace(new LocalVcsAdapter(fixture.Directory)))
            {
                HoldFor = TimeSpan.FromSeconds(3),
            }
            .RunAsync("runner-1", ["linux"], stopping.Token);

        var held = trees.Handoff.Held().Select(t => t.FlightId).ToList();
        await Assert.That(held).DoesNotContain("flight-old")
            .Because("seven days is the retention, and a tree past it is a customer's source code on "
                   + "a disk nobody is going to take it from. Held: " + string.Join(", ", held));
        await Assert.That(held).Contains("flight-recent")
            .Because("an hour-old tree is the one somebody may still take over.");
        await Assert.That(observer.Events).Contains("expired:flight-old")
            .Because("removing somebody's only copy of an agent's work is said where a person will "
                   + "read it. Events: " + string.Join(", ", observer.Events));
    }
}
