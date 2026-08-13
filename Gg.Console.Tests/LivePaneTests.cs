using Gg.Console;

namespace Gg.Console.Tests;

/// <summary>
/// The Live pane, against output a real agent produced.
/// </summary>
/// <remarks>
/// <para>
/// <b>Built in 4b against nothing.</b> The console has carried a line KIND since
/// then with nothing producing one, so verbosity could be a data model rather
/// than a regular expression applied to a screen later. This is the first time
/// there is something to classify, and it is the point at which that bet either
/// paid or did not.
/// </para>
/// <para>
/// <b>The transport is a file.</b> ADR-0007 case 1 only: same machine, no relay.
/// The console can be closed and reopened and the flight neither knows nor
/// cares, which is the realistic case because leases outlive clients.
/// </para>
/// <para>
/// <b>It is a view, not evidence.</b> Nothing here is stored or shipped; the
/// transcript is the reference and the digest is what crosses.
/// </para>
/// </remarks>
public class LivePaneTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 13, 12, 0, 0, TimeSpan.Zero);

    private static string Scratch() =>
        Path.Combine(Path.GetTempPath(), "gg-live-" + Guid.NewGuid().ToString("N")[..8], "flight.ndjson");

    /// <summary>
    /// A live file as the runner writes one.
    /// </summary>
    /// <remarks>
    /// The kinds and text are what the executor emits for a real stream: a
    /// session announcing itself, tool calls and their results, what the agent
    /// said, a line nothing could classify, and the run ending.
    /// </remarks>
    private static void Write(string path, params (string Kind, string Text)[] lines)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        using var writer = new StreamWriter(path, append: true);
        foreach (var (kind, text) in lines)
        {
            writer.WriteLine(
                $$"""{"kind":"{{kind}}","text":"{{text}}","at":"2026-08-13T12:00:00+00:00"}""");
        }
    }

    [Test]
    public async Task All_five_kinds_arrive_from_what_the_runner_writes()
    {
        // The 4b bet, collected. Five kinds the stream really distinguishes -
        // not five buckets a regex sorts a screen into afterwards.
        var path = Scratch();
        Write(path,
            ("setup", "session init"),
            ("tool", "Read"),
            ("tool", "-> ok"),
            ("text", "I'll look at the project's style first."),
            ("raw", "{unparseable"),
            ("meta", "loop success"));

        var lines = new LiveTail(path).Read();

        await Assert.That(lines.Select(l => l.Kind).Distinct().Order().ToList())
            .IsEquivalentTo(new[]
            {
                StreamLineKind.Text, StreamLineKind.Tool, StreamLineKind.Raw,
                StreamLineKind.Meta, StreamLineKind.Setup,
            }.Order().ToList());

        await Assert.That(lines.Single(l => l.Kind == StreamLineKind.Text).Text)
            .Contains("style");
    }

    [Test]
    public async Task A_kind_this_console_does_not_know_is_shown_rather_than_hidden()
    {
        // The console is the older half here. Refusing a line because a newer
        // runner named its kind something new would hide output for a reason
        // nobody watching could see.
        var path = Scratch();
        Write(path, ("reasoning", "a kind from a later runner"));

        var line = new LiveTail(path).Read().Single();

        await Assert.That(line.Kind).IsEqualTo(StreamLineKind.Raw);
        await Assert.That(line.Text).Contains("later runner");
    }

    // ---- closed, reopened, and the flight does not care ----

    [Test]
    public async Task Closing_the_console_and_reopening_it_resumes_rather_than_replays()
    {
        // A file survives the reader. This is the whole reason the transport is
        // one: leases outlive clients, so the console being gone must not be an
        // event the run has to handle.
        var path = Scratch();
        Write(path, ("setup", "session init"), ("tool", "Read"));

        var first = new LiveTail(path);
        var before = first.Read();
        await Assert.That(before.Count).IsEqualTo(2);

        // The console is closed. The runner keeps writing, because it is not
        // watching whether anybody is watching.
        Write(path, ("text", "carried on"), ("meta", "loop success"));

        // And reopened, from where it stopped.
        var reopened = new LiveTail(path) { };
        var everything = reopened.Read();

        await Assert.That(everything.Count).IsEqualTo(4)
            .Because("a console with no memory of the offset reads the file from the start, which "
                   + "is right: it has no way to know what a previous process had already shown.");

        var resumed = first.Read();

        await Assert.That(resumed.Count).IsEqualTo(2)
            .Because("the one that DID hold an offset takes only what arrived while it was away.");
        await Assert.That(resumed[0].Text).IsEqualTo("carried on");
    }

    [Test]
    public async Task Reading_an_absent_file_is_empty_rather_than_an_error()
    {
        // The pane is off by default and most flights never write one, so
        // nothing-there is the ordinary case and not a degradation.
        await Assert.That(new LiveTail(Scratch()).Read()).IsEmpty();
    }

    [Test]
    public async Task A_half_written_line_is_left_for_next_time()
    {
        // The runner appends while this reads. A fragment parsed now is a line
        // with a hole in it; left alone it is a whole line a moment later.
        var path = Scratch();
        Write(path, ("text", "complete"));
        File.AppendAllText(path, "{\"kind\":\"text\",\"text\":\"half");

        var tail = new LiveTail(path);

        await Assert.That(tail.Read().Count).IsEqualTo(1);

        File.AppendAllText(path, " written\",\"at\":\"2026-08-13T12:00:00+00:00\"}\n");

        var rest = tail.Read();

        await Assert.That(rest.Count).IsEqualTo(1);
        await Assert.That(rest[0].Text).IsEqualTo("half written")
            .Because("the fragment was not consumed, so the whole line survived.");
    }

    // ---- freeze, against a stream that is actually moving ----

    [Test]
    public async Task Freezing_holds_the_screen_and_loses_nothing_that_arrives()
    {
        // Built in 4b against nothing. This is it against a stream that moves:
        // a copy that works, over output with a hole in it nobody can see.
        var state = Reducer.Reduce(AnState(), Command.ToggleLive);
        state = Reducer.StreamArrived(state, Line("before the freeze"));

        var frozen = Reducer.Reduce(state, Command.ToggleFreeze);

        foreach (var text in (string[])["arrived while frozen", "and another"])
        {
            frozen = Reducer.StreamArrived(frozen, Line(text));
        }

        await Assert.That(frozen.Live.Count).IsEqualTo(1)
            .Because("the screen is still, which is what makes a selection copyable.");
        await Assert.That(frozen.Held.Count).IsEqualTo(2)
            .Because("held, not dropped.");
        await Assert.That(PaneText.Live(frozen)).Contains("2 line(s) waiting")
            .Because("a frozen screen that is silently behind looks like a run that stopped.");

        var thawed = Reducer.Reduce(frozen, Command.ToggleFreeze);

        await Assert.That(thawed.Live.Count).IsEqualTo(3);
        await Assert.That(thawed.Held).IsEmpty();
        await Assert.That(thawed.Live.Select(l => l.Text).ToList())
            .IsEquivalentTo((string[])["before the freeze", "arrived while frozen", "and another"])
            .Because("in the order they arrived. A thaw that reordered would be worse than a hole, "
                   + "because it reads as a run that did things in an order it did not.");
    }

    // ---- off by default, and the number ----

    [Test]
    public async Task Live_is_off_until_somebody_asks_for_it()
    {
        // A trust artifact meant to decay. On by default is a viewer.
        await Assert.That(new AppState().LiveVisible).IsFalse();
        await Assert.That(PaneText.Live(new AppState()))
            .Contains("off by default");
    }

    [Test]
    public async Task The_attach_rate_follows_the_selection_rather_than_the_keypress()
    {
        // Per 4b: counting keypresses measures how often somebody presses `l`.
        // Moving the cursor while the pane is open IS watching the flight you
        // moved to, and that is the number we want to fall.
        var state = Reducer.Reduce(AnState(), Command.ToggleLive);

        await Assert.That(state.AttachFacts.Single().AttachCount).IsEqualTo(1);

        state = Reducer.Reduce(state, Command.SelectNext);

        await Assert.That(state.AttachFacts.Count).IsEqualTo(2)
            .Because("the second flight was watched, without a key that says 'watch' being pressed.");

        var closed = Reducer.Reduce(state, Command.ToggleLive);

        await Assert.That(closed.AttachFacts.Sum(f => f.AttachCount)).IsEqualTo(2)
            .Because("detaching does not count, or a rate that should fall looks like it doubled.");
    }

    private static StreamLine Line(string text) =>
        new() { Kind = StreamLineKind.Text, Text = text, At = T0 };

    private static AppState AnState() => new()
    {
        Queue =
        [
            new QueueRow
            {
                FlightId = "flight-1", FlightNumber = "GG-1", Name = "first",
                Reason = QueueReason.RunnerOffline, Since = T0,
            },
            new QueueRow
            {
                FlightId = "flight-2", FlightNumber = "GG-2", Name = "second",
                Reason = QueueReason.LeaseExpiredTwice, Since = T0,
            },
        ],
    };
}
