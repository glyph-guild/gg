using System.Net;
using Gg.Runner.Pools;

namespace Gg.Runner.Tests;

/// <summary>
/// The scope probe: reach for a container outside the declared inventory and
/// be refused by something that is not us.
/// </summary>
/// <remarks>
/// <para>
/// <b>Slice eleven's probe shape, applied to infrastructure.</b> The Docker
/// socket is host root; § 12 says the scope is the provider's, never ours —
/// here the provider is the socket proxy, and the probe's evidence is the
/// proxy's own refusal. Held is true ONLY when the reach was refused; an
/// allowed reach or an error is a broken bound with a diagnosis, because
/// unknown is not false.
/// </para>
/// <para>
/// The poison twin runs against a handler that ALLOWS the reach — no proxy
/// needed — proving the probe cannot report a bound it did not observe.
/// </para>
/// </remarks>
[Category("RealDocker")]
public class DockerScopeProbeTests
{
    private static string Endpoint =>
        Environment.GetEnvironmentVariable("GG_POOL_ENDPOINT")
        ?? throw new InvalidOperationException(
            "GG_POOL_ENDPOINT is not set. These drive a real daemon through a real proxy; "
          + "skipping them would leave the scope bet unverified. See scripts/pool-proxy.");

    [Test]
    public async Task The_out_of_inventory_reach_is_refused_by_the_proxy()
    {
        var adapter = new DockerPoolAdapter(new HttpClient { BaseAddress = new Uri(Endpoint) });

        var probe = await adapter.ProbeScopeAsync();

        await Assert.That(probe.Held).IsTrue()
            .Because("the refusal is the proxy's - a 403 from something that is not us is "
                   + "the whole criterion.");
        await Assert.That(probe.ProbedAt).IsNotEqualTo(default(DateTimeOffset));
    }

    /// <summary>The poison twin: an allowed reach is a broken bound, never a pass.</summary>
    [Test]
    public async Task An_allowed_reach_reports_the_bound_broken()
    {
        var permissive = new HttpClient(new AllowEverything())
        {
            BaseAddress = new Uri("http://permissive.test"),
        };
        var adapter = new DockerPoolAdapter(permissive);

        var probe = await adapter.ProbeScopeAsync();

        await Assert.That(probe.Held).IsFalse();
        await Assert.That(probe.Diagnosis!).Contains("ALLOWED")
            .Because("a probe that reported an allowed reach as held would be a flag "
                   + "wearing a probe's name.");
    }

    private sealed class AllowEverything : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}"),
            });
    }
}
