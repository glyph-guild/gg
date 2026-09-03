namespace Gg.Client.Tests;

/// <summary>
/// A member becomes a runner by redeeming its nonce once, and never again.
/// </summary>
/// <remarks>
/// <para>
/// <b>The last hop.</b> Everything before this gives a member an identity it
/// COULD obtain: the control plane mints, the create spec carries a nonce, the
/// pool counts members. This is where a container actually becomes somebody.
/// </para>
/// <para>
/// <b>Once, because a nonce is spent by the first redemption.</b> A container
/// restarts — Docker restarts it, the host reboots — and a member that redeemed
/// again would find its nonce burnt and be unable to start at all. So the stored
/// credential is the answer whenever there is one, and the nonce is only for the
/// first breath.
/// </para>
/// <para>
/// <b>This is the shape <c>RunnerIdentity.EnsureAsync</c> already had.</b> It
/// decides WHETHER to obtain an identity and lets the caller own HOW — a host
/// reads a session and registers; a member redeems. That seam was built for the
/// duplicate-runner fix and turns out to be exactly what this needs.
/// </para>
/// <para>
/// <b>Labels are stored with the credential</b>, because they arrive with it and
/// have to survive a restart that does not redeem. A member reading them from its
/// environment instead would be advertising what somebody put in a container
/// rather than what the strategy decided.
/// </para>
/// </remarks>
public class MemberRedeemsOnceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);

    private sealed class Scratch : IDisposable
    {
        internal string Root { get; } = Path.Combine(
            Path.GetTempPath(), "gg-member-redeem", Guid.NewGuid().ToString("n"));

        internal Scratch() => Directory.CreateDirectory(Root);

        internal FileRunnerStore Store(string name) =>
            new(Path.Combine(Root, FileRunnerStore.FileNameFor(name)));

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch (IOException)
            {
                // A temp directory that outlives the run is not a failed test.
            }
        }
    }

    [Test]
    public async Task A_member_redeems_once_and_reuses_the_credential_after()
    {
        // THE RESTART CASE, and it is not hypothetical: a pool member is a
        // container, and containers restart. A second redemption finds a burnt
        // nonce and the member never comes up.
        using var scratch = new Scratch();
        var store = scratch.Store("gg-pool-dev-1");
        var redemptions = 0;

        async Task<StoredRunner> RedeemAsync()
        {
            redemptions++;
            return await Task.FromResult(new StoredRunner
            {
                RunnerId = "the-member",
                RunnerToken = "its-token",
                Labels = ["environment=dev"],
                ExpiresAt = Now.AddHours(12),
            });
        }

        var first = await RunnerIdentity.EnsureAsync(store, RedeemAsync, Now);
        var second = await RunnerIdentity.EnsureAsync(store, RedeemAsync, Now.AddMinutes(5));

        await Assert.That(redemptions).IsEqualTo(1)
            .Because("a nonce is spent by the first redemption. A member that redeemed on "
                   + "every start would come up exactly once and never again.");
        await Assert.That(second.RunnerId).IsEqualTo(first.RunnerId);
    }

    [Test]
    public async Task The_labels_the_credential_carried_survive_a_restart()
    {
        // They arrive with the credential and are not in the environment, so if
        // they did not persist a restarted member would advertise nothing and be
        // warm but unmatchable - which is the same as not existing.
        using var scratch = new Scratch();
        var store = scratch.Store("gg-pool-dev-1");

        _ = await RunnerIdentity.EnsureAsync(
            store,
            () => Task.FromResult(new StoredRunner
            {
                RunnerId = "the-member",
                RunnerToken = "its-token",
                Labels = ["environment=dev"],
                ExpiresAt = Now.AddHours(12),
            }),
            Now);

        var reread = await RunnerIdentity.EnsureAsync(
            store,
            () => throw new InvalidOperationException("must not redeem again"),
            Now.AddMinutes(5));

        await Assert.That(reread.Labels).Contains("environment=dev")
            .Because("what a member may advertise was decided by the strategy at mint. "
                   + "Losing it on restart would put that decision back in the container.");
    }

    [Test]
    public async Task A_lapsed_member_credential_is_not_presented()
    {
        // A member's token is short - twelve hours against a resident's thirty
        // days - so a container that outlives its credential is an ordinary
        // state, and presenting a dead one fails at the first protocol call
        // rather than where somebody can read it.
        using var scratch = new Scratch();
        var store = scratch.Store("gg-pool-dev-1");

        store.Write(new StoredRunner
        {
            RunnerId = "expired",
            RunnerToken = "stale",
            Labels = ["environment=dev"],
            ExpiresAt = Now.AddHours(-1),
        });

        var identity = await RunnerIdentity.EnsureAsync(
            store,
            () => Task.FromResult(new StoredRunner
            {
                RunnerId = "replacement",
                RunnerToken = "fresh",
                Labels = ["environment=dev"],
                ExpiresAt = Now.AddHours(12),
            }),
            Now);

        await Assert.That(identity.RunnerId).IsEqualTo("replacement");
    }

    [Test]
    public async Task A_runner_that_carries_no_labels_still_round_trips()
    {
        // THE ANCHOR. A host runner stores no labels - it reads them from its
        // environment - and adding the field must not make its stored identity
        // unreadable.
        using var scratch = new Scratch();
        var store = scratch.Store("vmlinux001");

        store.Write(new StoredRunner
        {
            RunnerId = "a-host-runner",
            RunnerToken = "its-token",
            ExpiresAt = Now.AddDays(30),
        });

        await Assert.That(store.Read()!.Labels).IsEmpty();
    }
}
