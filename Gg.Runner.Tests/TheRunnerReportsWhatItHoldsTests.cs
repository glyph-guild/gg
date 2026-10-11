using System.Net;
using System.Text;
using Gg.Contracts;
using Gg.Runner;

namespace Gg.Runner.Tests;

/// <summary>
/// The runner says on every beat which credentials its store holds, so a credential it
/// receives or forgets is in the next one (slice seventy-three, S73.3-01).
/// </summary>
/// <remarks>
/// <para>
/// <b>Asked every beat, never remembered.</b> A list kept from the last push would be the
/// push log this slice decided against - right until the machine lost the file.
/// </para>
/// <para>
/// <b>Through the contract's <c>Reportable</c></b>, so what leaves the machine is
/// locators only, never a vault reference - whatever the store handed over.
/// </para>
/// </remarks>
public class TheRunnerReportsWhatItHoldsTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);

    private sealed class NoLog : IReadOnlyLog
    {
        public TailRead Tail(int lines) => new([], false);
    }

    private static RunnerLoop Loop(
        FakeProtocol protocol, MovableClock clock, Func<IReadOnlyList<string>>? holdings,
        Func<CancellationTokenSource> stopping)
    {
        var says = new WhatThisRunnerSays(new RecordingObserver(), _ => new NoLog(), () => T0);

        return new RunnerLoop(protocol, clock,
            (_, _) =>
            {
                // ONE BEAT A RUN: the first wait after it ends the run.
                stopping().Cancel();
                return Task.CompletedTask;
            },
            says, new NoCredentialResolver(), new NoWorkspace(),
            holdings: holdings);
    }

    private static async Task BeatAsync(
        FakeProtocol protocol, int beats, Func<IReadOnlyList<string>>? holdings)
    {
        CancellationTokenSource? stopping = null;
        var clock = new MovableClock(T0);
        var loop = Loop(protocol, clock, holdings, () => stopping!);

        for (var i = 0; i < beats; i++)
        {
            // PAST THE NEXT BEAT, so the second run beats again rather than
            // finding the last one still fresh.
            clock.Advance(TimeSpan.FromMinutes(10));
            using var run = new CancellationTokenSource();
            stopping = run;
            await loop.RunAsync("runner-1", ["linux"], run.Token);
        }
    }

    [Test]
    public async Task A_beat_carries_what_the_store_holds()
    {
        var protocol = new FakeProtocol();

        await BeatAsync(protocol, beats: 1,
            () => ["local:jdx/jdnext", "local:analysis/sonarcloud", "keyvault://acme-fleet.vault.example.net/x"]);

        await Assert.That(protocol.LastHolds).IsEquivalentTo(
            new[] { "local:analysis/sonarcloud", "local:jdx/jdnext" });
    }

    [Test]
    public async Task What_it_receives_or_forgets_is_in_the_next_beat()
    {
        var protocol = new FakeProtocol();
        var asked = 0;

        await BeatAsync(protocol, beats: 2, () => ++asked == 1
            ? ["local:jdx/jdnext"]
            : ["local:analysis/sonarcloud"]);

        await Assert.That(asked).IsGreaterThanOrEqualTo(2)
            .Because("the store is asked on every beat, not remembered from the first.");
        await Assert.That(protocol.LastHolds).IsEquivalentTo(new[] { "local:analysis/sonarcloud" });
    }

    [Test]
    public async Task A_runner_given_no_store_says_nothing()
    {
        var protocol = new FakeProtocol();

        await BeatAsync(protocol, beats: 1, holdings: null);

        await Assert.That(protocol.LastHolds).IsNull()
            .Because("nothing to ask is not 'holds none'.");
    }

    private sealed class Capturing : HttpMessageHandler
    {
        internal string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"nextHeartbeatSeconds":30}""", Encoding.UTF8, "application/json"),
            };
        }
    }

    [Test]
    public async Task The_wire_carries_it_as_holds()
    {
        var handler = new Capturing();
        var client = new RunnerProtocolClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://cp.invalid/") }, "runner-token");

        _ = await client.HeartbeatAsync(
            "runner-1", [], acceptsConfiguration: null, holds: ["local:analysis/sonarcloud"]);

        await Assert.That(handler.Body).Contains("\"holds\":[\"local:analysis/sonarcloud\"]");
    }
}
