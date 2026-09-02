using Gg.Client;

namespace Gg.Client.Tests;

/// <summary>
/// A pool host restarts on its own credential, not on a person's.
/// </summary>
/// <remarks>
/// <para>
/// <b>A session was the wrong secret to leave on a machine, and the reason is
/// arithmetic rather than taste.</b> A session lasts twelve hours; a runner
/// token lasts thirty days. <c>gg runner maintain</c> registers on every start,
/// so a host holding a session cannot restart after half a day — a machine
/// rebooted the next morning fails with <i>not signed in</i>, on a box with
/// nobody at it.
/// </para>
/// <para>
/// <b>The separation was designed for exactly this.</b> <c>RunnerRegistry</c>
/// says so: <i>"the runner's lifetime is its own. Nothing here records which
/// session registered it, so revoking that session, or simply letting it expire
/// overnight, cannot take a running runner down mid-flight."</i> Persisting the
/// runner token keeps that property; persisting the session throws it away and
/// keeps the wider authority as well.
/// </para>
/// <para>
/// <b>What this does not fix is the thirty-day edge.</b> Nothing renews a
/// runner token — <c>RenewAsync</c> renews a LEASE — so at thirty days a person
/// signs in again. That is a real cadence rather than a bug, and the refusal
/// says so instead of reading like a broken machine.
/// </para>
/// </remarks>
public class RunnerStoreTests
{
    private static string TempPath() =>
        Path.Combine(Path.GetTempPath(), $"gg-runner-{Guid.NewGuid():N}.json");

    private static StoredRunner ARunner(DateTimeOffset expiresAt) => new()
    {
        RunnerId = "a-runner",
        RunnerToken = "a-token",
        ExpiresAt = expiresAt,
    };

    [Test]
    public async Task A_stored_runner_comes_back_as_it_went_in()
    {
        var path = TempPath();
        try
        {
            var store = new FileRunnerStore(path);
            var runner = ARunner(DateTimeOffset.UtcNow.AddDays(30));

            store.Write(runner);

            await Assert.That(store.Read()!.RunnerToken).IsEqualTo("a-token");
            await Assert.That(store.Read()!.RunnerId).IsEqualTo("a-runner");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task The_file_is_locked_down_before_the_token_goes_into_it()
    {
        // The same property the session store holds, for the same reason: there
        // must be no instant where a readable file carries a live credential.
        // On a pool host this file IS the runner's authority.
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var path = TempPath();
        try
        {
            new FileRunnerStore(path).Write(ARunner(DateTimeOffset.UtcNow.AddDays(30)));

            var mode = File.GetUnixFileMode(path);

            await Assert.That(mode.HasFlag(UnixFileMode.GroupRead)).IsFalse();
            await Assert.That(mode.HasFlag(UnixFileMode.OtherRead)).IsFalse()
                .Because("a pool host is a shared machine, and this file is the runner's whole "
                       + "authority for thirty days.");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task An_expired_runner_is_not_offered_as_usable()
    {
        // Reading it back is fine; treating it as usable is not. A host that
        // started an expired token would fail on its first protocol call
        // instead of at the one place that can say what to do about it.
        var path = TempPath();
        try
        {
            var store = new FileRunnerStore(path);
            store.Write(ARunner(DateTimeOffset.UtcNow.AddMinutes(-1)));

            await Assert.That(store.Usable(DateTimeOffset.UtcNow)).IsNull()
                .Because("thirty days is a cadence, and the moment it lapses is the moment a "
                       + "person is needed - said here rather than discovered as a 401.");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task A_live_runner_is_usable_without_anybody_signing_in()
    {
        var path = TempPath();
        try
        {
            var store = new FileRunnerStore(path);
            store.Write(ARunner(DateTimeOffset.UtcNow.AddDays(29)));

            await Assert.That(store.Usable(DateTimeOffset.UtcNow)!.RunnerToken).IsEqualTo("a-token")
                .Because("this is the whole point: a host reboots and comes back without a person, "
                       + "for as long as its own credential lasts.");
        }
        finally
        {
            File.Delete(path);
        }
    }
}
