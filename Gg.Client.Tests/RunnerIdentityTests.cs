namespace Gg.Client.Tests;

/// <summary>
/// A machine keeps the runner it registered, and two runners on one machine do
/// not share a slot.
/// </summary>
/// <remarks>
/// <para>
/// <b>One host showed as eleven runners, ten of them permanently offline.</b>
/// <c>gg runner up</c> called <c>RegisterRunnerAsync</c> on every start and
/// stored nothing, so each restart minted a new identity and abandoned the last.
/// "One machine restarted ten times" and "ten machines are down" then render
/// identically, and the second is an incident.
/// </para>
/// <para>
/// <b><c>gg runner maintain</c> already did the right thing</b>, which is what
/// makes this a divergence rather than a missing feature: it reads a stored
/// runner and registers only when there is not one. <c>FileRunnerStore</c>'s own
/// remarks were written for that fix — <i>"a host holding a session cannot
/// restart after half a day"</i> — and <c>up</c> never got it.
/// </para>
/// <para>
/// <b>The trap, and why the store had to change first.</b> A pool host runs BOTH
/// verbs, and <c>StoredRunner</c> had no name while <c>FileRunnerStore</c> was a
/// single file. <c>up</c> persisting into it would have overwritten the maintain
/// service's thirty-day token, and the next maintain start would have found
/// somebody else's credential — on a box with nobody at it, that refuses to
/// start. Two identities needed two slots before either could keep one.
/// </para>
/// </remarks>
public class RunnerIdentityTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);

    /// <summary>A store root of this test's own, never the developer's.</summary>
    private sealed class Scratch : IDisposable
    {
        internal string Root { get; } = Path.Combine(
            Path.GetTempPath(), "gg-runner-identity", Guid.NewGuid().ToString("n"));

        internal Scratch() => Directory.CreateDirectory(Root);

        internal FileRunnerStore For(string name) =>
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

    private static StoredRunner A(string id, DateTimeOffset expires) => new()
    {
        RunnerId = id,
        RunnerToken = "a-token",
        ExpiresAt = expires,
    };

    [Test]
    public async Task Two_runners_on_one_machine_do_not_share_a_slot()
    {
        // THE TRAP. A pool host runs `up` and `maintain`, and before this they
        // were one file: whichever registered last owned the only credential.
        using var scratch = new Scratch();

        scratch.For("vmlinux001").Write(A("the-worker", Now.AddDays(30)));
        scratch.For("vmlinux001:maintain").Write(A("the-maintainer", Now.AddDays(30)));

        await Assert.That(scratch.For("vmlinux001").Read()!.RunnerId).IsEqualTo("the-worker")
            .Because("registering the second runner must not hand the first somebody else's "
                   + "credential - on a pool host that is a service that refuses to start.");
        await Assert.That(scratch.For("vmlinux001:maintain").Read()!.RunnerId)
            .IsEqualTo("the-maintainer");
    }

    [Test]
    public async Task A_name_that_is_not_a_filename_still_gets_its_own_slot()
    {
        // ':' is in the maintain name and is not a path character everywhere.
        // A scheme that mangled two names to one string would recreate the
        // shared slot with extra steps.
        await Assert.That(FileRunnerStore.FileNameFor("vmlinux001:maintain"))
            .IsNotEqualTo(FileRunnerStore.FileNameFor("vmlinux001"));

        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            await Assert.That(FileRunnerStore.FileNameFor($"host{invalid}name"))
                .DoesNotContain(invalid.ToString())
                .Because("the file name has to be one, on every platform this runs on.");
        }
    }

    [Test]
    public async Task A_usable_runner_is_reused_rather_than_registered_again()
    {
        // THE DEFECT ITSELF. Every restart was a new row in `gg runners`.
        using var scratch = new Scratch();
        var store = scratch.For("vmlinux001");
        store.Write(A("the-one-already-registered", Now.AddDays(30)));

        var registrations = 0;

        var identity = await RunnerIdentity.EnsureAsync(
            store,
            () =>
            {
                registrations++;
                return Task.FromResult(A("a-brand-new-one", Now.AddDays(30)));
            },
            Now);

        await Assert.That(registrations).IsEqualTo(0)
            .Because("a machine that already has a runner is that runner. Registering again "
                   + "leaves the last one offline forever and needs a person to be signed in.");
        await Assert.That(identity.RunnerId).IsEqualTo("the-one-already-registered");
    }

    [Test]
    public async Task A_machine_with_no_runner_registers_once_and_keeps_it()
    {
        using var scratch = new Scratch();
        var store = scratch.For("vmlinux001");

        var identity = await RunnerIdentity.EnsureAsync(
            store, () => Task.FromResult(A("freshly-registered", Now.AddDays(30))), Now);

        await Assert.That(identity.RunnerId).IsEqualTo("freshly-registered");
        await Assert.That(store.Read()!.RunnerId).IsEqualTo("freshly-registered")
            .Because("kept, or the next start does this again and the row count grows.");
    }

    [Test]
    public async Task A_lapsed_runner_is_replaced_rather_than_presented()
    {
        // Thirty days is a cadence, not a bug - and a lapsed credential must not
        // be handed to the protocol to fail as a 401 on the first call.
        using var scratch = new Scratch();
        var store = scratch.For("vmlinux001");
        store.Write(A("expired-last-week", Now.AddDays(-7)));

        var identity = await RunnerIdentity.EnsureAsync(
            store, () => Task.FromResult(A("its-replacement", Now.AddDays(30))), Now);

        await Assert.That(identity.RunnerId).IsEqualTo("its-replacement");
        await Assert.That(store.Read()!.RunnerId).IsEqualTo("its-replacement");
    }
}
