using System.Runtime.CompilerServices;
using Gg.Client;
using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Console.Tests;

/// <summary>
/// The control plane says when something changed, and the console reads it then
/// rather than on its next tick.
/// </summary>
/// <remarks>
/// <para>
/// <b>A doorbell, not a feed.</b> A notice carries a topic and an id and nothing
/// else; what changed is read through the routes the console already reads, so
/// the stream adds no second picture of anything. What it changes is WHEN: the
/// tab in front of somebody is re-read the moment a change is announced, and
/// anything the console is looking for is looked for at once.
/// </para>
/// <para>
/// <b>The poll stays.</b> A console whose stream dropped, or whose control plane
/// serves none, is the console it was before - the thirty-second tick is the
/// backstop, and a 404 is the answer that there is no stream to wait for.
/// </para>
/// <para>
/// <b>Nothing here sleeps.</b> Connections are driven one at a time, the
/// reconnect's wait is handed in, and the one moment a test must observe a
/// connection still open it is told, not guessed.
/// </para>
/// </remarks>
public class TheConsoleHearsWhatChangedTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 18, 14, 0, 0, TimeSpan.Zero);

    private static ChangeNotice Ready() => new() { Topic = ChangeTopics.Ready };

    private static ChangeNotice Changed(string topic, string? id = null) => new() { Topic = topic, Id = id };

    private static async IAsyncEnumerable<ChangeNotice> Says(
        IEnumerable<ChangeNotice> notices,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var notice in notices)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return notice;
        }
    }

    [Test]
    public async Task A_stream_that_said_ready_is_live_until_it_ends()
    {
        var heardReady = new TaskCompletionSource();
        var hold = new TaskCompletionSource();

        async IAsyncEnumerable<ChangeNotice> Open([EnumeratorCancellation] CancellationToken ct = default)
        {
            yield return Ready();
            heardReady.SetResult();
            await hold.Task;
        }

        var stream = new ChangeStream(ct => Open(ct));
        var connection = stream.ListenOnceAsync(CancellationToken.None);

        // BOUNDED, so a stream that never opens fails here rather than hanging.
        await heardReady.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(stream.Live).IsTrue()
            .Because("ready is the stream's word that it is listening.");

        hold.SetResult();
        await connection;
        await Assert.That(stream.Live).IsFalse()
            .Because("a connection that ended tells this console nothing more.");
    }

    [Test]
    public async Task Being_told_reads_the_tab_now_and_says_the_console_is_live()
    {
        var stream = new ChangeStream(ct => Says([Ready(), Changed(ChangeTopics.Gates, "GG-1")], ct));
        await stream.ListenOnceAsync(CancellationToken.None);

        var after = stream.Advance(new AppState());

        await Assert.That(after.Refresh.Wanted).IsTrue()
            .Because("a change announced is a change the tab in front of somebody may show, so "
                   + "it is read now rather than on the tick.");
    }

    [Test]
    public async Task Nothing_heard_asks_for_nothing()
    {
        var stream = new ChangeStream(ct => Says([], ct));

        var after = stream.Advance(new AppState());

        await Assert.That(after.Refresh.Wanted).IsFalse();
    }

    [Test]
    public async Task While_it_is_live_the_key_says_so()
    {
        var heardReady = new TaskCompletionSource();
        var hold = new TaskCompletionSource();

        async IAsyncEnumerable<ChangeNotice> Open([EnumeratorCancellation] CancellationToken ct = default)
        {
            yield return Ready();
            heardReady.SetResult();
            await hold.Task;
        }

        var stream = new ChangeStream(ct => Open(ct));
        var connection = stream.ListenOnceAsync(CancellationToken.None);
        // BOUNDED, so a stream that never opens fails here rather than hanging.
        await heardReady.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var live = stream.Advance(new AppState());

        await Assert.That(live.Refresh.Live).IsTrue();
        await Assert.That(AutoRefresh.Says(live.Refresh with { NextIn = 20 })).IsEqualTo("live")
            .Because("the countdown is the backstop now, and counting down to it reads as the "
                   + "way the screen stays true.");

        hold.SetResult();
        await connection;

        var dropped = stream.Advance(live);
        await Assert.That(dropped.Refresh.Live).IsFalse();
        await Assert.That(AutoRefresh.Says(dropped.Refresh with { NextIn = 20 })).IsEqualTo("20s");
    }

    [Test]
    public async Task Being_told_looks_for_what_is_expected_at_once()
    {
        // A FLIGHT THE CONSOLE IS WAITING FOR, whose last look found nothing and
        // whose next is a quarter of a second away. A notice about flights is
        // the reason to look now rather than then.
        var clock = new Clock();
        var looks = 0;
        var expectations = new Expectations(
            Expectations.Looks(_ =>
            {
                looks++;
                return Task.FromResult<FlightSummary?>(null);
            }),
            clock);

        var state = expectations.Advance(new AppState
        {
            Expecting = [new Expectation { Kind = ExpectationKind.FlightAppears, Id = "f-1" }],
        });

        var stream = new ChangeStream(ct => Says([Changed(ChangeTopics.Flights, "f-1")], ct), expectations);
        await stream.ListenOnceAsync(CancellationToken.None);

        state = stream.Advance(state);
        state = expectations.Advance(state);

        await Assert.That(looks).IsEqualTo(2)
            .Because("the second look happened at the notice, not a quarter of a second later.");
    }

    [Test]
    public async Task A_control_plane_with_no_stream_is_polled_and_not_asked_again()
    {
        var calls = 0;

        IAsyncEnumerable<ChangeNotice> Refuse(CancellationToken ct)
        {
            calls++;
            throw new ChangeStreamUnavailableException("no stream here");
        }

        var stream = new ChangeStream(Refuse, delay: (_, _) => Task.CompletedTask);

        await stream.RunAsync(CancellationToken.None);

        await Assert.That(stream.Absent).IsTrue();
        await Assert.That(calls).IsEqualTo(1)
            .Because("a 404 is the answer that there is no stream, and asking again changes it "
                   + "not at all.");
        await Assert.That(stream.Advance(new AppState()).Refresh.Live).IsFalse();
    }

    [Test]
    public async Task A_dropped_stream_is_opened_again_after_a_wait_that_grows()
    {
        var calls = 0;
        var waits = new List<TimeSpan>();

        IAsyncEnumerable<ChangeNotice> Drops(CancellationToken ct) =>
            ++calls < 4
                ? Says([], ct)
                : throw new ChangeStreamUnavailableException("gone");

        var stream = new ChangeStream(Drops, delay: (span, _) =>
        {
            waits.Add(span);
            return Task.CompletedTask;
        });

        await stream.RunAsync(CancellationToken.None);

        await Assert.That(waits).IsEquivalentTo(new[]
        {
            TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4),
        })
            .Because("a stream that keeps dropping is asked less often, not hammered.");
    }

    [Test]
    public async Task A_refresh_that_brings_a_new_gate_says_somebody_is_waited_on()
    {
        // THE ONE NOTIFICATION ABOUT WHAT SOMEBODY ELSE DID. The queue grew
        // while a person was looking at something else; a refresh - whether a
        // notice or the tick asked for it - is how the console finds out, so
        // that is where it says so. A gate it already had is not news.
        var clock = new Clock();
        var known = AGate("GG-1", "somebody-looks");
        var arrived = AGate("GG-2", "another-looks");

        var refresh = new AutoRefresh(
            _ => Task.FromResult<Func<AppState, AppState>>(s => s with
            {
                Gates = new GateList { Gates = [known, arrived] },
            }),
            clock,
            TimeSpan.FromSeconds(30));

        var state = refresh.Advance(new AppState
        {
            Gates = new GateList { Gates = [known] },
            Refresh = new RefreshState { Wanted = true },
        });
        state = refresh.Advance(state);

        await Assert.That(state.Notifications.Select(n => (n.Kind, n.FlightNumber, n.Name)))
            .IsEquivalentTo(new[] { (NotificationKind.GateOpened, (string?)"GG-2", (string?)"another-looks") });
    }

    [Test]
    public async Task What_a_new_gate_says()
    {
        var state = new AppState
        {
            Notifications =
            [
                new Notification
                {
                    Kind = NotificationKind.GateOpened,
                    FlightId = "GG-2",
                    FlightNumber = "GG-2",
                    Name = "another-looks",
                },
            ],
        };

        await Assert.That(PaneText.NotificationTitle(state)).IsEqualTo("waiting on you");
        await Assert.That(PaneText.NotificationLines(state)).IsEquivalentTo(
            new[] { "GG-2 has a gate to answer", "another-looks" });
    }

    private static PendingGate AGate(string number, string obligation) => new()
    {
        FlightNumber = number,
        ObligationId = obligation,
        Approver = "a-lead",
        Because = "somebody has to look",
        AwaitingSince = T0,
        ManifestHash = new string('a', 64),
        Attempt = 1,
    };

    private sealed class Clock : IClock
    {
        public DateTimeOffset Now { get; set; } = T0;

        public DateTimeOffset UtcNow => Now;
    }
}
