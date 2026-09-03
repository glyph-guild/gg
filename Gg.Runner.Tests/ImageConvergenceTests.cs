using System.Net;
using System.Text;
using Gg.Contracts;
using Gg.Runner.Pools;

namespace Gg.Runner.Tests;

/// <summary>
/// A member whose image drifted from the strategy's pin is converged to it —
/// the arm the contract, the port and the adapter's own comment all promise
/// and none of them performed.
/// </summary>
/// <remarks>
/// <para>
/// <b>The comment described behaviour the code did not have.</b>
/// <c>PoolActions.Refresh</c> says <i>"converge it to the strategy's image if
/// it drifted"</i>, <c>IPoolAdapter.RefreshAsync</c> says <i>"create, start, or
/// converge"</i>, and the adapter's running branch said <i>"Converge: a member
/// whose image drifted from the strategy's is reset to it"</i> — and then
/// returned <c>Verified</c> without comparing anything. The <c>image</c>
/// parameter was unused on that path. A drifted member was attested healthy and
/// kept taking flights.
/// </para>
/// <para>
/// <b>The obvious fix cannot work, and finding out why produced this one.</b>
/// Resolving the strategy's image through the daemon means <c>GET
/// /images/…/json</c>, and the pull point refuses everything outside the
/// container paths: <i>"exec, images, volumes, build, networks, other
/// containers — answers 403 from the proxy itself"</i>. That design would attest
/// <c>failed</c> on every refresh of a running member, which is worse than the
/// no-op it replaces.
/// </para>
/// <para>
/// So the comparison is between what the container says it was made FROM and
/// what the strategy pins — on a path the proxy already allows. Both sides are
/// digest-pinned by a shipped refusal (<c>EnvironmentStrategy.Validate</c>
/// requires <c>name@sha256:…</c>), so this is exact rather than approximate.
/// <b>An approximate drift check is a billing incident, not a rough edge</b>:
/// a comparison that can be wrong resets every sweep, forever.
/// </para>
/// </remarks>
public class ImageConvergenceTests
{
    private const string Pinned =
        "ghcr.io/acme/env@sha256:" + "1111111111111111111111111111111111111111111111111111111111111111";

    private const string Drifted =
        "ghcr.io/acme/env@sha256:" + "2222222222222222222222222222222222222222222222222222222222222222";

    /// <summary>
    /// A daemon that answers inspect from a script and records every path asked
    /// for. Not a mock of the adapter — a stand-in for the socket, so the thing
    /// under test is the adapter's own request sequence.
    /// </summary>
    private sealed class RecordingDaemon(string configImage) : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];

        public bool Removed { get; private set; }

        private bool _recreated;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            Paths.Add($"{request.Method} {path}");

            // Anything outside /containers/ is what the proxy refuses, so the
            // stand-in refuses it too - a test whose daemon is more permissive
            // than the deployment proves the wrong thing.
            if (!path.StartsWith("/containers", StringComparison.Ordinal))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden));
            }

            if (path.EndsWith("/json", StringComparison.Ordinal))
            {
                var image = _recreated ? Pinned : configImage;
                var body = "{\"State\":{\"Running\":true,\"Status\":\"running\"},"
                         + "\"Image\":\"sha256:" + new string('f', 64) + "\","
                         + "\"Config\":{\"Image\":\"" + image + "\"}}";

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json"),
                });
            }

            if (request.Method == HttpMethod.Delete)
            {
                Removed = true;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
            }

            if (path.EndsWith("/create", StringComparison.Ordinal))
            {
                _recreated = true;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        }
    }

    /// <summary>A daemon that will not describe the member it admits owning.</summary>
    private sealed class MuteDaemon : HttpMessageHandler
    {
        public bool Created { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/create", StringComparison.Ordinal))
            {
                Created = true;
            }

            // NOT 404. Absent is a different answer and already means "create
            // it"; this is the daemon knowing a name it will not describe.
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        }
    }

    [Test]
    public async Task A_member_that_will_not_inspect_attests_failed_and_converges_nothing()
    {
        // UNKNOWN IS NOT FALSE, and today it is not even unknown. A non-404
        // inspect throws out of RefreshAsync, out of ExecuteAsync and out of
        // the maintain loop's cycle - so NO ATTESTATION IS SHIPPED AT ALL. The
        // pool goes silent, and silence escalates as staleness: "the pull point
        // stopped attesting" rather than "the daemon would not answer", which
        // sends a person to look at the wrong thing.
        var daemon = new MuteDaemon();

        var observed = await new DockerPoolAdapter(
            new HttpClient(daemon) { BaseAddress = new Uri("http://pull-point") })
            .RefreshAsync("gg-pool", "gg-pool-1", Spec(Pinned));

        await Assert.That(observed.Outcome).IsEqualTo(PoolOutcomes.Failed)
            .Because("a refresh that cannot see the member has not converged it, and the "
                   + "ledger has to be able to say so - an action that throws attests "
                   + "nothing, and nothing is indistinguishable from a pull point that "
                   + "went quiet.");
        await Assert.That(observed.Diagnosis!).Contains("gg-pool-1")
            .Because("Article XI: the refusal names the member, because the person reading "
                   + "it has a pool of them.");
        await Assert.That(daemon.Created).IsFalse()
            .Because("converging nothing means creating nothing - a member the daemon will "
                   + "not describe must not be quietly replaced with a second one.");
    }

    private static DockerPoolAdapter Adapter(RecordingDaemon daemon) =>
        new(new HttpClient(daemon) { BaseAddress = new Uri("http://pull-point") });

    [Test]
    public async Task A_member_made_from_another_image_is_converged_to_the_pin()
    {
        var daemon = new RecordingDaemon(Drifted);

        var observed = await Adapter(daemon).RefreshAsync("gg-pool", "gg-pool-1", Spec(Pinned));

        await Assert.That(daemon.Removed).IsTrue()
            .Because("converge means reset - destroy and recreate from the pin. A member "
                   + "running something the strategy does not name is not made current by "
                   + "being described as current.");
        await Assert.That(observed.Outcome).IsEqualTo(PoolOutcomes.Verified);
        await Assert.That(observed.Provenance).IsEqualTo(EnvironmentProvenance.Fresh)
            .Because("what came back is a NEW container whatever its name says, and fresh "
                   + "is the word that makes a reused environment trustworthy again.");
    }

    [Test]
    public async Task A_member_already_on_the_pin_is_left_alone()
    {
        // THE LIVENESS ANCHOR ON ITS OWN AXIS. A convergence that reset every
        // time would satisfy the test above and destroy a warm pool on every
        // sweep - which is the billing incident this arm has to not be.
        var daemon = new RecordingDaemon(Pinned);

        var observed = await Adapter(daemon).RefreshAsync("gg-pool", "gg-pool-1", Spec(Pinned));

        await Assert.That(daemon.Removed).IsFalse()
            .Because("a member already running what the strategy pins IS current, and "
                   + "recreating it would throw away a warm environment to arrive at the "
                   + "same place.");
        await Assert.That(observed.Outcome).IsEqualTo(PoolOutcomes.Verified);
        await Assert.That(observed.Provenance).IsEqualTo(EnvironmentProvenance.Reused);
    }

    [Test]
    public async Task Convergence_asks_the_daemon_nothing_outside_the_container_paths()
    {
        // THE REQUEST SET, ASSERTED - not just the outcome. The pull point 403s
        // /images/, so an adapter that resolved the strategy's image through the
        // daemon would attest failed on every refresh of a running member. The
        // outcome alone would not distinguish "converged correctly" from
        // "converged for the wrong reason", and a later refactor reaching for
        // /images/json would pass an outcome-only test on a permissive fake.
        var daemon = new RecordingDaemon(Drifted);

        _ = await Adapter(daemon).RefreshAsync("gg-pool", "gg-pool-1", Spec(Pinned));

        await Assert.That(daemon.Paths).IsNotEmpty();
        await Assert.That(daemon.Paths.Where(p => !p.Contains("/containers", StringComparison.Ordinal)))
            .IsEmpty()
            .Because("the proxy refuses images, volumes, build and networks - so a "
                   + "convergence that needed any of them would be a convergence that "
                   + "never happens, and the 403 would read as drift.");
    }
    /// <summary>A member spec around an image, for tests whose subject is not the spec.</summary>
    /// <remarks>
    /// The nonce is present because a member without one is refused before
    /// anything is created - see MemberIsARunnerTests, where that refusal is
    /// the subject.
    /// </remarks>
    private static MemberSpec Spec(string image) => new()
    {
        Image = image,
        ControlPlane = "https://control.example.invalid",
        Nonce = "a-nonce",
    };

}
