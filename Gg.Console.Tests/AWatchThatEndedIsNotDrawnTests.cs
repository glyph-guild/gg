using Gg.Console;

namespace Gg.Console.Tests;

/// <summary>
/// The pane draws the watch that is running, not the one that was.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two sources with two different owners, and only one of them may be
/// remembered.</b> A file tail holds an offset that must survive a session
/// rebuild - that is the whole reason <see cref="LiveTails"/> keeps a
/// dictionary, and asking for a fresh one each tick would replay the flight from
/// the top every second. A watched runner's buffer is the opposite: it belongs
/// to the conversation, and starting a second watch makes a new one.
/// </para>
/// <para>
/// <b>So remembering it is how a watch that ended stays on the screen.</b>
/// <c>WatchedRunner.Start</c> stops the old conversation and builds a new
/// buffer; a pane holding the old one draws a source nothing is filling any
/// more, and nothing anywhere says so. Keyed by flight it only showed on
/// re-watching the SAME flight. Keyed by runner - which is what watching an idle
/// machine requires - it is every second press of the key.
/// </para>
/// </remarks>
public class AWatchThatEndedIsNotDrawnTests
{
    private sealed class Said(params string[] lines) : ILiveSource
    {
        private bool _read;

        public bool Exists => true;

        public int Reads { get; private set; }

        public IReadOnlyList<StreamLine> Read()
        {
            Reads++;

            if (_read)
            {
                return [];
            }

            _read = true;

            return [.. lines.Select(l => new StreamLine
            {
                Kind = StreamLineKind.Text,
                Text = l,
                At = DateTimeOffset.UnixEpoch,
            })];
        }
    }

    private static AppState Watching() => new()
    {
        LiveVisible = true,
        WatchedRunnerId = "vmlinux001",
    };

    [Test]
    public async Task A_second_watch_replaces_the_first_on_the_screen()
    {
        ILiveSource watched = new Said("the first conversation");

        var tails = new LiveTails(_ => throw new InvalidOperationException(
            "a watched runner has no flight file to fall back to"),
            () => watched);

        var first = tails.Advance(Watching());

        await Assert.That(first.Live.Select(l => l.Text)).Contains("the first conversation");

        // WHAT `w' DOES A SECOND TIME: the old conversation is stopped and a new
        // buffer takes its place.
        watched = new Said("the second conversation");

        var second = tails.Advance(first);

        await Assert.That(second.Live.Select(l => l.Text)).Contains("the second conversation")
            .Because("a pane holding the buffer of a watch that ended draws a source nothing "
                   + "is filling any more, and says nothing about it.");
    }

    [Test]
    public async Task And_a_flight_file_is_still_opened_once()
    {
        // THE PROPERTY THAT MUST SURVIVE THE FIX. A file tail holds an offset;
        // a fresh one each tick replays the flight from the top once a second.
        var opened = 0;
        var file = new Said("a line from the file");

        var tails = new LiveTails(_ => { opened++; return file; });

        var state = new AppState { LiveVisible = true, WatchedFlightId = "flight-84" };

        state = tails.Advance(state);
        state = tails.Advance(state);

        await Assert.That(opened).IsEqualTo(1)
            .Because("the offset is the reason this remembers anything at all.");
    }
}
