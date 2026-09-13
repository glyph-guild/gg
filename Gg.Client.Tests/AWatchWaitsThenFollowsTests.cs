using Gg.Client;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// A watch attached to a machine that is waiting picks up the work that arrives.
/// </summary>
/// <remarks>
/// <para>
/// <b>The follow loop has never had a test, and the reason is its shape.</b> It
/// hard-coded a one second <c>Task.Delay</c> and took a live
/// <c>Conversation</c>, so the only part reachable from a test was the overlap
/// finder - by reflection. Everything else about it, including when it gives up,
/// was asserted nowhere.
/// </para>
/// <para>
/// <b>tail-log is asked on EVERY tick and is never gated on status.</b> Gating
/// it would make a new console watching an older runner sit saying "idle" while
/// the runner flies, because the flight name it was waiting for is a member that
/// runner does not send - silently absent, which is indistinguishable from
/// satisfied. Status is asked alongside, for two things only: to NAME the flight
/// to the person, and to reset the overlap at a boundary.
/// </para>
/// <para>
/// <b>And one silence no longer ends a watch.</b> A null answer meant "the
/// flight landed and the lease went with it", which was the ordinary end when a
/// watch lasted a flight. It is also what a fifteen second timeout produces, and
/// a watch that is meant to sit on an idle machine for an hour cannot end on the
/// first hiccup.
/// </para>
/// </remarks>
public class AWatchWaitsThenFollowsTests
{
    private static readonly DateTimeOffset T0 = DateTimeOffset.UnixEpoch;

    private static RunnerSaid Tail(params string[] lines) => new()
    {
        Kind = RunnerAskKinds.TailLog,
        Tail = new LogTail { Lines = lines, Truncated = false },
    };

    private static RunnerSaid Status(string doing, string? flying, int beat) => new()
    {
        Kind = RunnerAskKinds.Status,
        Status = new RunnerStatusReport
        {
            Doing = doing,
            At = T0.AddSeconds(beat),
            FlightNumber = flying,
            BeatAt = T0.AddSeconds(beat),
        },
    };

    /// <summary>
    /// Answers each kind from its own script, and stops when the tails run out.
    /// </summary>
    /// <remarks>
    /// <b>By KIND rather than by position</b>, because the loop asks for the log
    /// every tick and about the machine only every few - so one script in
    /// arrival order would be a test that encodes the tick arithmetic and breaks
    /// when the cadence changes, rather than one about what the loop does with
    /// each answer.
    /// </remarks>
    private static async Task<List<string>> FollowedAsync(
        IReadOnlyList<RunnerSaid?> tails,
        IReadOnlyList<RunnerSaid?> statuses,
        IReadOnlyList<string> seen,
        Action<RunnerAsk>? noting = null)
    {
        var written = new List<string>();
        var nextTail = 0;
        var nextStatus = 0;
        using var stopping = new CancellationTokenSource();

        await WatchARunner.FollowAsync(
            (ask, _) =>
            {
                noting?.Invoke(ask);

                // REFUSED THE WAY THE RUNNER REFUSES IT. AskDispatch matches on
                // the kind AND the payload; a double that answered on the kind
                // alone is a double that cannot fail the way the machine does,
                // which is how a loop whose every status ask was thrown away
                // passed all of these.
                if (ask.Status is null && ask.TailLog is null)
                {
                    return Task.FromResult<RunnerSaid?>(null);
                }

                if (string.Equals(ask.Kind, RunnerAskKinds.Status, StringComparison.Ordinal))
                {
                    return Task.FromResult(
                        nextStatus < statuses.Count ? statuses[nextStatus++] : null);
                }

                if (nextTail >= tails.Count)
                {
                    stopping.Cancel();
                    return Task.FromResult<RunnerSaid?>(null);
                }

                return Task.FromResult(tails[nextTail++]);
            },
            (_, _) => Task.CompletedTask,
            seen,
            lines: 40,
            write: written.Add,
            cancellationToken: stopping.Token);

        return written;
    }

    [Test]
    public async Task Every_ask_carries_the_payload_its_kind_declares()
    {
        // MEASURED ON A REAL RUNNER, WHICH IS THE ONLY REASON THIS IS KNOWN.
        // AskDispatch's arm is `case RunnerAskKinds.Status when ask.Status is
        // not null` - a kind with no payload is not a status ask, it is an
        // unrecognised one, and the runner refuses it and counts it. So every
        // status this loop sent was thrown away: no beat, no flight name, no
        // dedupe reset, and nothing on screen to say any of that was missing.
        //
        // THE DOUBLE WAS MORE PERMISSIVE THAN THE DISPATCH. It answered by
        // kind and never looked at the payload, so the tests passed while the
        // feature did nothing at all against a real machine.
        var asks = new List<RunnerAsk>();

        await FollowedAsync(
            tails: [Tail(), Tail()],
            statuses: [Status("idle", null, 15)],
            seen: [],
            noting: asks.Add);

        await Assert.That(asks).IsNotEmpty();

        foreach (var ask in asks)
        {
            var carried = ask.Kind switch
            {
                RunnerAskKinds.Status => ask.Status is not null,
                RunnerAskKinds.TailLog => ask.TailLog is not null,
                _ => false,
            };

            await Assert.That(carried).IsTrue()
                .Because($"a `{ask.Kind}` with no payload is not that ask - it is one the "
                       + "runner does not recognise, and it is refused and counted.");
        }
    }

    [Test]
    public async Task Nothing_is_written_while_the_machine_is_waiting()
    {
        var written = await FollowedAsync(
            tails: [Tail(), Tail()],
            statuses: [Status("idle", null, 15), Status("idle", null, 30)],
            seen: []);

        await Assert.That(written.Any(l => l.Contains("beat", StringComparison.OrdinalIgnoreCase)))
            .IsTrue()
            .Because("a beat is what tells a person the machine is still talking to the "
                   + "control plane, which is the difference between waiting and stopped. "
                   + "Said: " + string.Join(" | ", written));

        await Assert.That(written.Any(l => l.Contains("text:", StringComparison.Ordinal)))
            .IsFalse()
            .Because("there is no flight, so there is no output - and inventing one would be "
                   + "the pane lying about an idle machine.");
    }

    [Test]
    public async Task The_flight_that_arrives_is_named_and_then_followed()
    {
        var written = await FollowedAsync(
            tails: [Tail(), Tail(), Tail(), Tail(), Tail(), Tail("text: starting on it")],
            statuses: [Status("idle", null, 15), Status("working a flight", "GG-84", 30)],
            seen: []);

        await Assert.That(written.Any(l => l.Contains("GG-84", StringComparison.Ordinal))).IsTrue()
            .Because("a pane that silently begins drawing a different flight's output is "
                   + "lying by omission. Said: " + string.Join(" | ", written));

        await Assert.That(written).Contains("text: starting on it")
            .Because("and then it is simply the flight's own words, which is the whole point "
                   + "of having been attached before it started.");
    }

    [Test]
    public async Task A_second_flight_is_not_deduped_against_the_first()
    {
        // THE BOUNDARY CASE. Every live view opens with near-identical setup
        // lines, so the overlap finder matches across a landing and swallows
        // the new flight's opening - or finds nothing and warns about a gap
        // that did not happen. The flight name is what makes the reset
        // deterministic.
        var written = await FollowedAsync(
            tails:
            [
                Tail("setup: cloning", "text: the first flight"),
                Tail("setup: cloning", "text: the first flight"),
                Tail("setup: cloning", "text: the first flight"),
                Tail("setup: cloning", "text: the first flight"),
                Tail("setup: cloning", "text: the first flight"),
                Tail("setup: cloning", "text: the second flight"),
            ],
            statuses:
            [
                Status("working a flight", "GG-84", 15),
                Status("working a flight", "GG-85", 30),
            ],
            seen: []);

        await Assert.That(written).Contains("text: the second flight");

        await Assert.That(written.Count(l => l == "setup: cloning")).IsEqualTo(2)
            .Because("both flights said it, and a watcher shown it once has been told the "
                   + "second flight began somewhere it did not. Said: "
                   + string.Join(" | ", written));
    }

    [Test]
    public async Task One_silence_does_not_end_a_watch_and_three_do()
    {
        var written = await FollowedAsync(
            tails: [null, Tail("text: still here"), null, null, null, Tail("text: never reached")],
            statuses: [Status("idle", null, 15)],
            seen: []);

        await Assert.That(written).Contains("text: still here")
            .Because("a fifteen second timeout produces a null, and a watch meant to sit on "
                   + "an idle machine for an hour cannot end on the first one.");

        await Assert.That(written).DoesNotContain("text: never reached")
            .Because("three in a row is a channel that is gone, and going on asking a peer "
                   + "nobody is on the other end of is a watch that never says it ended.");
    }
}
