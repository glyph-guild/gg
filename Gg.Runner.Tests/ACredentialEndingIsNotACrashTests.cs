using System.Net;
using Gg.Contracts;
using Gg.Runner.Vcs;

namespace Gg.Runner.Tests;

/// <summary>
/// A runner whose credential stops working says so and stops. It does not
/// crash.
/// </summary>
/// <remarks>
/// <para>
/// <b>Measured on a live pool member.</b> <c>gg-pool-dev-1</c> ran for twelve
/// hours and six hundred milliseconds - exactly
/// <c>RunnerRegistry.MemberTokenLifetime</c> - and then:
/// </para>
/// <code>
/// Unhandled exception. System.Net.Http.HttpRequestException:
///   Response status code does not indicate success: 401 (Unauthorized).
///    at Gg.Runner.RunnerLoop.&lt;RunAsync&gt;…
/// </code>
/// <para>
/// exit 139. The container stopped, its corpse kept its slot in the pool, and
/// the pool never refilled.
/// </para>
/// <para>
/// <b>Both halves of that were deliberate and neither is wrong.</b> A member's
/// credential is short *because* <i>"reset is only a boundary if the credential
/// dies with the container"</i>, and a 401 leaves loudly because <i>"the token
/// is this machine's credential and no waiting fixes it"</i>. What nothing
/// decided is the SHAPE of leaving: an unhandled exception is not a diagnosis,
/// it is a stack trace in a container log nobody can open a shell on.
/// </para>
/// <para>
/// <b>Loudly is kept. The crash is not.</b> The runner says which of the two
/// things happened - a credential that reached the end of its life, or one
/// taken away - and exits with a code that says the same, so a restart policy
/// and a pool can both tell an ordinary ending from a broken machine.
/// </para>
/// </remarks>
public class ACredentialEndingIsNotACrashTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 15, 6, 0, 0, TimeSpan.Zero);

    private static HttpRequestException Refused() =>
        new("Response status code does not indicate success: 401 (Unauthorized).",
            inner: null, HttpStatusCode.Unauthorized);

    /// <summary>Runs one runner whose very first claim is refused.</summary>
    private static async Task<(int Exit, RecordingObserver Observer)> RefusedAsync(
        DateTimeOffset? credentialExpires)
    {
        using var fixture = new GitFixture();
        using var trees = new ScratchTreeRoot();
        var clock = new MovableClock(T0);

        var protocol = new FakeProtocol();
        protocol.ClaimThrows.Enqueue(Refused());

        var observer = new RecordingObserver();
        using var stopping = new CancellationTokenSource();

        var exit = await new RunnerLoop(protocol, clock,
                (span, token) =>
                {
                    token.ThrowIfCancellationRequested();
                    clock.Advance(span);
                    return Task.CompletedTask;
                },
                observer, new NoCredentialResolver(),
                trees.Workspace(new LocalVcsAdapter(fixture.Directory)),
                credentialExpiresAt: credentialExpires)
        {
            HoldFor = TimeSpan.FromSeconds(1),
        }
            .RunAsync("runner-1", ["linux"], stopping.Token);

        return (exit, observer);
    }

    [Test]
    public async Task A_credential_that_reached_its_end_is_an_ordinary_ending()
    {
        // THE MEMBER'S CASE, and the pool's whole model: a member is created
        // and destroyed by machinery, and its credential is sized to that. The
        // container stopping is the design working, so the exit says so.
        var (exit, observer) = await RefusedAsync(credentialExpires: T0.AddMinutes(-1));

        await Assert.That(exit).IsEqualTo(0)
            .Because("a member that reached the end of its credential did what it was built "
                   + "to do. A non-zero exit would make a restart policy fight the design, "
                   + "and an unhandled exception makes a pool read a normal ending as a "
                   + "broken machine.");

        await Assert.That(string.Join(" ", observer.Events)).Contains("expired");
    }

    [Test]
    public async Task A_credential_taken_away_early_still_leaves_loudly()
    {
        // NOT THE SAME THING AT ALL. A 401 before the credential's own expiry
        // is a revocation or a retirement - somebody acted - and that is the
        // case the original rule was written for: no waiting fixes it, and a
        // silent exit would retire a machine nobody meant to retire.
        var (exit, observer) = await RefusedAsync(credentialExpires: T0.AddDays(20));

        await Assert.That(exit).IsNotEqualTo(0)
            .Because("somebody took this runner's credential away while it was still live, "
                   + "and that is a thing to find out about rather than a tidy shutdown.");

        await Assert.That(string.Join(" ", observer.Events)).Contains("revoked");
    }

    [Test]
    public async Task A_runner_that_does_not_know_its_expiry_is_told_apart_from_neither()
    {
        // UNKNOWN IS NOT EXPIRED, which is ScopeProbe's rule one concern over:
        // "unknown is not false". A runner with no recorded expiry - every
        // runner registered before this was carried - must not have its
        // credential's end inferred, because inferring "expired" would make a
        // revocation exit 0 and vanish.
        var (exit, observer) = await RefusedAsync(credentialExpires: null);

        await Assert.That(exit).IsNotEqualTo(0);
        await Assert.That(string.Join(" ", observer.Events)).Contains("401");
    }

    [Test]
    public async Task Whatever_happened_it_is_not_a_stack_trace()
    {
        // THE POINT. All three paths above end by RETURNING. The test that this
        // is true is that RefusedAsync returned at all - an unhandled
        // HttpRequestException would have come out of RunAsync and failed every
        // assertion in this file by never reaching one.
        var (_, observer) = await RefusedAsync(credentialExpires: T0.AddMinutes(-1));

        await Assert.That(observer.Events).IsNotEmpty()
            .Because("the runner said something about its own ending, which is the whole "
                   + "difference between a diagnosis and exit 139 in a container log.");
    }
}
