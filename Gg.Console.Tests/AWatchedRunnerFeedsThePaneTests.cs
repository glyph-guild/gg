using Gg.Console;

namespace Gg.Console.Tests;

/// <summary>
/// A runner watched over a channel feeds the live pane the same way a file does.
/// </summary>
/// <remarks>
/// <para>
/// <b>The pane already has the right seam and it was pointed at one thing.</b>
/// <c>LiveTails</c> takes a <c>Func&lt;string, ILiveSource&gt;</c> and the
/// composition root has always answered it with a file. <c>ILiveSource</c> is
/// <c>Read()</c> — lines since the last call — and <c>Exists</c>, which is a
/// BUFFER interface: nothing in it says where the lines came from.
/// </para>
/// <para>
/// <b>Which is why watching a fleet runner inline needs no exception.</b> A UI
/// session may read a local file and nothing else, and the reason is that
/// everything else happens between sessions with the terminal provably free.
/// The session here still only drains a buffer. What fills the buffer is a pump
/// that never touches the terminal and never touches the model — it is a
/// collaborator the console composes, like <c>LiveTails</c> itself.
/// </para>
/// <para>
/// <b>Split in two on purpose, so the split is checkable.</b>
/// <c>RemoteLiveSource</c> is the buffer and holds no conversation; the pump
/// holds the conversation and knows nothing about panes. A single class doing
/// both would be a network call one field away from a session, and
/// <c>LiveStreamingTests</c> scans a NAMED list of files — so a class that did
/// both and was not on that list would pass by not being looked at.
/// </para>
/// </remarks>
public class AWatchedRunnerFeedsThePaneTests
{
    private static readonly DateTimeOffset T0 =
        new(2026, 9, 9, 1, 30, 0, TimeSpan.Zero);

    private static StreamLine Line(string text) =>
        new() { Kind = StreamLineKind.Text, Text = text, At = T0 };

    [Test]
    public async Task It_hands_over_what_arrived_and_then_nothing()
    {
        // THE SAME CONTRACT THE FILE TAIL HAS: lines SINCE THE LAST CALL. A
        // source that re-handed everything would print the flight's whole
        // output on every tick.
        var source = new RemoteLiveSource();

        source.Offer([Line("one"), Line("two")]);

        await Assert.That(source.Read().Select(l => l.Text)).IsEquivalentTo(new[] { "one", "two" });
        await Assert.That(source.Read()).IsEmpty()
            .Because("lines since the last call, or the pane repaints the whole flight every "
                   + "tick.");
    }

    [Test]
    public async Task It_says_whether_there_is_anything_to_read_from_at_all()
    {
        // THE DIFFERENCE BETWEEN TWO SILENCES, which is what Exists is for on
        // the file side: no channel is "not started", and an open channel with
        // nothing new is "the agent is working and has not spoken".
        var source = new RemoteLiveSource();

        await Assert.That(source.Exists).IsFalse();

        source.Opened();

        await Assert.That(source.Exists).IsTrue();

        source.Closed();

        await Assert.That(source.Exists).IsFalse()
            .Because("a channel that closed is not a channel with nothing new on it, and a "
                   + "pane that showed the same thing for both could not tell a person which "
                   + "they are looking at.");
    }

    [Test]
    public async Task It_keeps_a_bound_rather_than_the_whole_flight()
    {
        // A PERSON WATCHING IS NOT A PERSON ARCHIVING. Whatever arrives between
        // two ticks is small; what is unbounded is a console left open on a
        // long flight, and an unread buffer that grows forever is a leak with a
        // pane in front of it.
        var source = new RemoteLiveSource(keep: 3);

        source.Offer([Line("a"), Line("b"), Line("c"), Line("d"), Line("e")]);

        await Assert.That(source.Read().Select(l => l.Text)).IsEquivalentTo(new[] { "c", "d", "e" })
            .Because("the newest are the ones a person is watching for; the oldest are the "
                   + "ones already on the screen.");
    }

    /// <summary>A source that answers with what it was given, once.</summary>
    private sealed class Scripted(bool exists, params StreamLine[] lines) : ILiveSource
    {
        private bool _handed;

        public bool Exists => exists;

        public IReadOnlyList<StreamLine> Read()
        {
            if (_handed)
            {
                return [];
            }

            _handed = true;
            return lines;
        }
    }

    [Test]
    public async Task The_pane_draws_the_flight_it_was_told_to_watch()
    {
        // THE ASSERTION THAT WAS MISSING, and its absence is what let GG-77 be
        // watched with nothing on the screen. Everything about the watch was
        // right - it connected, the buffer filled - and the pane was bound to
        // the queue cursor, which cannot point at a flight that is merely
        // flying. State being correct is not the same as a pane drawing it.
        var asked = new List<string>();

        var tails = new LiveTails(flightId =>
        {
            asked.Add(flightId);
            return new Scripted(exists: true, Line("from the runner"));
        });

        var after = tails.Advance(new AppState
        {
            LiveVisible = true,
            WatchedFlightId = "the-watched-flight",

            // NO QUEUE AT ALL, which is the ordinary state for a flight that is
            // simply flying. The old binding had nothing to read here.
            Queue = [],
        });

        await Assert.That(asked).IsEquivalentTo(new[] { "the-watched-flight" });
        await Assert.That(after.Live.Select(l => l.Text))
            .IsEquivalentTo(new[] { "from the runner" });
    }

    [Test]
    public async Task Following_the_cursor_is_still_what_happens_by_default()
    {
        // THE HALF THAT MUST NOT HAVE CHANGED. Every pane that worked before is
        // a queue row with the cursor on it, and a watch that took the binding
        // away from them would fix one pane by breaking the rest.
        var asked = new List<string>();

        var tails = new LiveTails(flightId =>
        {
            asked.Add(flightId);
            return new Scripted(exists: true, Line("from the file"));
        });

        _ = tails.Advance(new AppState
        {
            LiveVisible = true,
            Queue =
            [
                new QueueRow
                {
                    FlightId = "under-the-cursor",
                    FlightNumber = "GG-1",
                    Name = "a flight needing somebody",
                    Reason = QueueReason.AwaitingDecision,
                    Since = T0,
                },
            ],
        });

        await Assert.That(asked).IsEquivalentTo(new[] { "under-the-cursor" })
            .Because("null means follow the cursor, which is what every pane did before.");
    }

    [Test]
    public async Task The_buffer_reaches_no_conversation_and_no_terminal()
    {
        // THE STRUCTURAL HALF, and the reason the split exists. This type is
        // about to be read from inside a UI session, so it has to be as
        // reachable-from-nothing as LiveTail is - and unlike LiveTail, the
        // thing that fills it is a network call. Asserted here as well as in
        // LiveStreamingTests' list, because a scan over a named list is a scan
        // that misses whatever nobody remembered to name.
        var source = Sources.Read("Gg.Console", "RemoteLiveSource.cs");

        foreach (var forbidden in new[]
                 { "Conversation", "HttpClient", "Socket", "AskAsync", "RunnerAsk" })
        {
            await Assert.That(source.Contains(forbidden, StringComparison.Ordinal)).IsFalse()
                .Because($"the buffer a session drains may not name {forbidden}: the split "
                       + "between it and the pump is what makes watching a fleet runner "
                       + "inline cost no exception at all.");
        }
    }
}
