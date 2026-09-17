using System.Net;
using Gg.Contracts;
using Gg.Runner.Sweeps;

namespace Gg.Runner.Tests;

/// <summary>
/// The sweeping runner's own liveness: it asks again when the control plane is
/// unwell, and it stops only for the one refusal waiting cannot fix.
/// </summary>
/// <remarks>
/// <para>
/// <b><c>MaintainLoop</c>'s three arms, and its three scars.</b> That loop died
/// on a 500 and was restarted into the same wall six times while the pool it
/// managed grew unattended; it aborted four times over 180 microseconds of
/// clock skew; and it left a 401 unhandled, which on a host nobody visits is a
/// stack trace in a journal with the process gone. A second copy of the loop
/// shape is a second chance to relearn all three, so this one is asserted
/// rather than assumed.
/// </para>
/// <para>
/// <b>A 401 is the one that stops.</b> No amount of waiting fixes this
/// machine's credential, and the exit code says so without anybody parsing a
/// sentence.
/// </para>
/// </remarks>
public class ASweepingRunnerKeepsGoingTests
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 17, 12, 0, 0, TimeSpan.Zero);

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => Noon;
    }

    private sealed class Answering(params Func<int, WatchActionList>[] answers) : ISweepProtocol
    {
        internal int Pulls { get; private set; }

        internal List<WatchAttestation> Attested { get; } = [];

        internal Func<WatchAttestation, Task>? OnAttest { get; init; }

        public Task<WatchActionList> PullSweepsAsync(
            string watch, CancellationToken cancellationToken = default)
        {
            var at = Pulls++;
            return Task.FromResult(
                answers[Math.Min(at, answers.Length - 1)](at));
        }

        public async Task AttestSweepAsync(
            string watch, WatchAttestation attestation, CancellationToken cancellationToken = default)
        {
            Attested.Add(attestation);

            if (OnAttest is { } refuse)
            {
                await refuse(attestation);
            }
        }
    }

    private sealed class NeverRuns : ISweepExecutor
    {
        public Task<SweepExecution> RunAsync(
            SweepRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult<SweepExecution>(new SweepExecution.Ran([]));
    }

    private static WatchActionList Nothing() => new() { Actions = [] };

    /// <summary>A loop whose waiting is a test's, stopped after so many waits.</summary>
    private static (SweepLoop Loop, List<TimeSpan> Waits, List<string> Said) Loop(
        ISweepProtocol protocol, int stopAfter, CancellationTokenSource stopping)
    {
        var waits = new List<TimeSpan>();
        var said = new List<string>();

        var loop = new SweepLoop(
            protocol,
            new SkillReader(
                [],
                Path.Combine(Path.GetTempPath(), "gg-sweep-cache", Guid.NewGuid().ToString("n")),
                secretFor: _ => Task.FromResult<string?>(null)),
            new NeverRuns(),
            new FixedClock(),
            transcripts: Path.Combine(Path.GetTempPath(), "gg-sweep-transcripts"),
            delay: (wait, _) =>
            {
                waits.Add(wait);
                if (waits.Count >= stopAfter)
                {
                    stopping.Cancel();
                }

                return Task.CompletedTask;
            },
            narrate: said.Add);

        return (loop, waits, said);
    }

    [Test]
    public async Task A_refused_credential_stops_the_loop_and_says_which()
    {
        using var stopping = new CancellationTokenSource();
        var protocol = new Answering(_ => throw new HttpRequestException(
            "runner credential", null, HttpStatusCode.Unauthorized));
        var (loop, waits, said) = Loop(protocol, stopAfter: 5, stopping);

        var exit = await loop.RunAsync("nightly-triage", stopping.Token);

        await Assert.That(exit).IsEqualTo(CredentialEnding.Refused)
            .Because("a distinct code lets a restart policy tell `this machine's credential is "
                   + "gone` from any other failure, without parsing a sentence.");
        await Assert.That(waits).IsEmpty()
            .Because("no amount of waiting fixes this machine's credential.");
        await Assert.That(said).IsNotEmpty()
            .Because("nobody is at a sweeping runner, so the reason reaches a journal or "
                   + "nowhere.");
    }

    [Test]
    public async Task A_control_plane_having_a_moment_is_asked_again()
    {
        using var stopping = new CancellationTokenSource();
        var protocol = new Answering(
            at => at == 0
                ? throw new HttpRequestException(
                    "bad gateway", null, HttpStatusCode.BadGateway)
                : Nothing());
        var (loop, waits, said) = Loop(protocol, stopAfter: 2, stopping);

        var exit = await loop.RunAsync("nightly-triage", stopping.Token);

        await Assert.That(exit).IsEqualTo(0);
        await Assert.That(protocol.Pulls).IsGreaterThanOrEqualTo(2)
            .Because("a deploy, a restart and a cold start all pass, and none of them is a "
                   + "reason to stop sweeping.");
        await Assert.That(waits[0]).IsGreaterThan(TimeSpan.Zero);
        await Assert.That(waits[1]).IsEqualTo(SweepLoop.PollEvery)
            .Because("a served cycle clears the backoff, so an hour of health does not inherit "
                   + "a bad minute's wait.");
        await Assert.That(said.Any(s => s.Contains("502", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    public async Task A_report_the_control_plane_refused_is_said_and_not_fatal()
    {
        using var stopping = new CancellationTokenSource();
        var protocol = new Answering(_ => new WatchActionList
        {
            Actions =
            [
                TheSweepLauncherAttachesTheWatchsServersTests.ASweep().Action with
                {
                    Skill = null,
                    Diagnosis = "the ref no longer points at a commit",
                },
            ],
        })
        {
            OnAttest = _ => throw new InvalidOperationException(
                "sweep 1 was handed to a different runner"),
        };
        var (loop, waits, said) = Loop(protocol, stopAfter: 2, stopping);

        var exit = await loop.RunAsync("nightly-triage", stopping.Token);

        await Assert.That(exit).IsEqualTo(0);
        await Assert.That(protocol.Attested.Count).IsGreaterThanOrEqualTo(2)
            .Because("a refusal may be transient, and a live loop saying so every cycle is "
                   + "more findable than a crash loop something keeps restarting.");
        await Assert.That(said.Any(s => s.Contains("different runner", StringComparison.Ordinal)))
            .IsTrue();
        await Assert.That(waits[0]).IsGreaterThan(TimeSpan.Zero);
    }

    [Test]
    public async Task A_quiet_watch_waits_its_period_rather_than_spinning()
    {
        using var stopping = new CancellationTokenSource();
        var protocol = new Answering(_ => Nothing());
        var (loop, waits, _) = Loop(protocol, stopAfter: 3, stopping);

        await loop.RunAsync("nightly-triage", stopping.Token);

        await Assert.That(waits.Distinct()).IsEquivalentTo((TimeSpan[])[SweepLoop.PollEvery]);
    }
}
