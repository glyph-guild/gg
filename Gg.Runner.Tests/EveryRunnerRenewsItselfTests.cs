using System.Text.RegularExpressions;
using Gg.Contracts;
using Gg.Runner.Pools;

namespace Gg.Runner.Tests;

/// <summary>
/// A resident runner keeps its own credential alive, as the maintainer beside
/// it already does.
/// </summary>
/// <remarks>
/// <para>
/// <b>Half the fleet renewed and half did not.</b> <c>gg runner maintain</c>
/// has renewed through <c>/v1/runner/renewal</c> since 0.30.0. <c>gg runner
/// up</c> - the runner that actually flies - never asked, so every thirty days
/// it stopped with a 401 and waited for somebody to sign in at a machine
/// nobody visits. The route serves any runner; nothing on this side called it.
/// </para>
/// <para>
/// <b>What stays a person's act is unchanged.</b> A renewal is authorized by
/// the credential it renews, so a revoked runner cannot ask, and one that was
/// down when its credential ran out cannot either: that machine still needs a
/// person. A member is refused by the control plane and is never wired to ask.
/// </para>
/// </remarks>
public class EveryRunnerRenewsItselfTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 19, 6, 0, 0, TimeSpan.Zero);

    /// <summary>A control plane's renewal door, answered from a script.</summary>
    private sealed class ARenewalDoor : IRunnerCredential
    {
        public Queue<RunnerCredentialRenewed?> Answers { get; } = [];

        public int Asked { get; private set; }

        public Task<RunnerCredentialRenewed?> RenewCredentialAsync(
            CancellationToken cancellationToken = default)
        {
            Asked++;
            return Task.FromResult(Answers.Count > 0 ? Answers.Dequeue() : null);
        }
    }

    private sealed record Ran(ARenewalDoor Door, List<DateTimeOffset> Remembered);

    /// <summary>Runs an idle runner for a few turns, asking for work that never comes.</summary>
    private static async Task<Ran> IdleAsync(
        DateTimeOffset? expiresAt, int turns = 4, params RunnerCredentialRenewed?[] answers)
    {
        using var fixture = new GitFixture();
        using var trees = new ScratchTreeRoot();
        var clock = new MovableClock(T0);

        var door = new ARenewalDoor();
        foreach (var answer in answers)
        {
            door.Answers.Enqueue(answer);
        }

        var remembered = new List<DateTimeOffset>();
        using var stopping = new CancellationTokenSource();
        var waited = 0;

        _ = await new RunnerLoop(new FakeProtocol(), clock,
                (span, token) =>
                {
                    token.ThrowIfCancellationRequested();
                    clock.Advance(span);
                    if (++waited >= turns)
                    {
                        stopping.Cancel();
                    }

                    return Task.CompletedTask;
                },
                new RecordingObserver(), new NoCredentialResolver(),
                trees.Workspace(new Vcs.LocalVcsAdapter(fixture.Directory)),
                credentialExpiresAt: expiresAt,
                credential: door,
                credentialRenewed: renewed =>
                {
                    remembered.Add(renewed);
                    return Task.CompletedTask;
                })
        {
            HoldFor = TimeSpan.FromSeconds(1),
        }
            .RunAsync("runner-1", ["linux"], stopping.Token)
            .WaitAsync(TimeSpan.FromSeconds(10));

        return new Ran(door, remembered);
    }

    [Test]
    public async Task A_credential_inside_three_days_of_its_end_is_renewed_with_nobody_there()
    {
        var renewedTo = T0 + TimeSpan.FromDays(30);
        var ran = await IdleAsync(
            T0 + TimeSpan.FromDays(2),
            answers: new RunnerCredentialRenewed { ExpiresAt = renewedTo });

        await Assert.That(ran.Door.Asked).IsEqualTo(1)
            .Because("an idle runner inside the maintainer's window asks once, and a renewed "
                   + "credential is thirty days from its window again.");
        await Assert.That(ran.Remembered).IsEquivalentTo([renewedTo])
            .Because("the next start reads the stored identity; a renewal only this process "
                   + "knew about would read as expired and ask for a person.");
    }

    [Test]
    public async Task A_credential_with_time_left_is_not_asked_about()
    {
        var ran = await IdleAsync(T0 + TimeSpan.FromDays(20));

        await Assert.That(ran.Door.Asked).IsEqualTo(0);
    }

    [Test]
    public async Task A_refused_renewal_is_said_once_and_not_asked_again()
    {
        // REVOKED IS A 401 AND ENDS THE LOOP (ACredentialEndingIsNotACrashTests);
        // THIS IS THE OTHER SETTLED NO - a credential the control plane will not
        // extend. Asking on every turn for the rest of its life is its own outage.
        var ran = await IdleAsync(T0 + TimeSpan.FromDays(1), turns: 6, answers: [null]);

        await Assert.That(ran.Door.Asked).IsEqualTo(1);
        await Assert.That(ran.Remembered).IsEmpty();
    }

    [Test]
    public async Task A_runner_with_no_recorded_expiry_never_asks()
    {
        var ran = await IdleAsync(expiresAt: null);

        await Assert.That(ran.Door.Asked).IsEqualTo(0)
            .Because("unknown is not due: renewing on a guess would have every runner "
                   + "registered before expiries were recorded asking on every turn.");
    }

    [Test]
    public async Task Runner_up_hands_its_host_the_way_to_write_a_renewal_down()
    {
        // THE WIRING, WHICH IS WHERE THIS WAS MISSING. RunnerHost builds the
        // client the renewal goes through, so the root hands in only where the
        // new date is kept - and a root that hands in nothing is a runner that
        // never asks, however right the loop is.
        var here = new DirectoryInfo(AppContext.BaseDirectory);
        while (here is not null && !Directory.Exists(Path.Combine(here.FullName, "Gg.Cli")))
        {
            here = here.Parent;
        }

        var program = File.ReadAllText(Path.Combine(here!.FullName, "Gg.Cli", "Program.cs"));

        var hosts = Regex.Matches(
                program,
                @"RunnerHost\.RunAsync\((?<args>(?>[^()]+|\((?<depth>)|\)(?<-depth>))*(?(depth)(?!)))\)",
                RegexOptions.Singleline)
            .Select(m => m.Groups["args"].Value)
            .ToList();

        await Assert.That(hosts.Count).IsGreaterThanOrEqualTo(3)
            .Because("the scan must find the hand-flight, runner up and a member, or it proves nothing.");
        await Assert.That(hosts.Count(args => args.Contains("credentialRenewed:", StringComparison.Ordinal)))
            .IsEqualTo(1)
            .Because("runner up renews; a member's twelve hours are the boundary and a hand-flight "
                   + "lasts one flight, so exactly one host is handed a renewal.");
    }
}
