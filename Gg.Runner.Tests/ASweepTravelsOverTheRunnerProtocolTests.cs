using System.Net;
using System.Text;
using Gg.Contracts;
using Gg.Runner.Sweeps;

namespace Gg.Runner.Tests;

/// <summary>
/// A runner pulls a watch's sweeps and reports each one over its own protocol,
/// with its own credential.
/// </summary>
/// <remarks>
/// <para>
/// <b>The two routes gg 0.182.0 declared</b>, reached the way the pools pair is:
/// the runner header and nothing else authenticates, the watch's name is in the
/// path, and the report is the contract's own record.
/// </para>
/// <para>
/// <b>A refused report is said, not swallowed.</b> A 400 is the contract's
/// Validate on the other side, and a runner that treated it as delivered would
/// believe a sweep was reported that the control plane never recorded.
/// </para>
/// </remarks>
public class ASweepTravelsOverTheRunnerProtocolTests
{
    private sealed class Answering(HttpStatusCode status, string body = "") : HttpMessageHandler
    {
        internal HttpRequestMessage? Seen { get; private set; }

        internal string? SentBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Seen = request;
            if (request.Content is not null)
            {
                SentBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            return new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
        }
    }

    private static ISweepProtocol Client(Answering handler) =>
        new RunnerProtocolClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://cp.invalid/") }, "a-runner-token");

    [Test]
    public async Task A_pull_asks_for_the_watchs_sweeps_with_the_runners_credential()
    {
        var handler = new Answering(HttpStatusCode.OK, """{"actions":[]}""");

        var pulled = await Client(handler).PullSweepsAsync("nightly triage");

        await Assert.That(pulled.Actions).IsEmpty();
        await Assert.That(handler.Seen!.Method).IsEqualTo(HttpMethod.Get);
        await Assert.That(handler.Seen.RequestUri!.AbsolutePath)
            .IsEqualTo("/v1/watches/nightly%20triage/actions")
            .Because("the watch's name is escaped into the path, where the pools pull puts its pool.");
        await Assert.That(handler.Seen.Headers.GetValues("X-Gg-Runner").Single())
            .IsEqualTo("a-runner-token");
    }

    [Test]
    public async Task A_report_is_posted_as_the_contracts_own_record()
    {
        var handler = new Answering(HttpStatusCode.Accepted);
        var report = new WatchAttestation
        {
            AttestationId = Guid.CreateVersion7(),
            Watch = "nightly-triage",
            ActionId = Guid.CreateVersion7(),
            Outcome = WatchOutcomes.Swept,
            Nominated = [new SweepNomination { Subject = "4242", Version = "7", Reason = "why" }],
            MeasuredAt = DateTimeOffset.UnixEpoch.AddYears(56),
            SkillSha = new string('d', 40),
        };

        await Client(handler).AttestSweepAsync("nightly-triage", report);

        await Assert.That(handler.Seen!.Method).IsEqualTo(HttpMethod.Post);
        await Assert.That(handler.Seen.RequestUri!.AbsolutePath)
            .IsEqualTo("/v1/watches/nightly-triage/attestations");
        await Assert.That(handler.SentBody!).Contains("\"nominated\":[");
        await Assert.That(handler.SentBody!).Contains("\"subject\":\"4242\"");
        await Assert.That(handler.SentBody!).Contains("\"skillSha\":");
    }

    [Test]
    public async Task A_refused_report_is_said_rather_than_swallowed()
    {
        var handler = new Answering(HttpStatusCode.BadRequest, "sweep 1 was handed to a different runner");

        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Client(handler).AttestSweepAsync("nightly-triage", new WatchAttestation
            {
                AttestationId = Guid.CreateVersion7(),
                Watch = "nightly-triage",
                ActionId = Guid.CreateVersion7(),
                Outcome = WatchOutcomes.Unreachable,
                MeasuredAt = DateTimeOffset.UnixEpoch.AddYears(56),
                Diagnosis = "no pin",
            }));

        await Assert.That(refused!.Message).Contains("different runner")
            .Because("the control plane's sentence is the only thing anybody can act on.");
    }
}
