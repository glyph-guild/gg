namespace Gg.Console.Tests;

/// <summary>
/// The console's two buffers are bounded, and a tail arrives at the end.
/// </summary>
/// <remarks>
/// <b>An unbounded list is also an unbounded <c>GG_STATE_DUMP</c>.</b> AppState
/// is serialized when the terminal is released, so a pane left attached to a
/// long flight would write every line it ever saw to disk on the way out.
/// </remarks>
public class LiveBoundsTests
{
    private static StreamLine Line(int n) =>
        new() { Kind = StreamLineKind.Text, Text = $"line {n}", At = DateTimeOffset.UnixEpoch };

    private static AppState Fill(AppState state, int count)
    {
        for (var i = 0; i < count; i++)
        {
            state = Reducer.StreamArrived(state, Line(i));
        }

        return state;
    }

    // ---- S31.5-01 ----

    [Test]
    public async Task The_live_list_is_capped_and_drops_the_oldest_first()
    {
        var state = Fill(new AppState(), 1200);

        await Assert.That(state.Live.Count).IsEqualTo(500)
            .Because("an unbounded list is also an unbounded state dump on the way out.");
        await Assert.That(state.Live[^1].Text).IsEqualTo("line 1199")
            .Because("the newest is what a person is reading.");
        await Assert.That(state.Live[0].Text).IsEqualTo("line 700")
            .Because("oldest first: dropping the newest would be a pane that stops updating "
                   + "at exactly the moment it matters most.");
    }

    // ---- S31.5-02 ----

    [Test]
    public async Task Held_is_capped_too_because_a_forgotten_freeze_has_no_ceiling()
    {
        var state = Fill(new AppState { Frozen = true }, 1200);

        await Assert.That(state.Held.Count).IsEqualTo(500);
        await Assert.That(state.Held[^1].Text).IsEqualTo("line 1199");
        await Assert.That(state.Live).IsEmpty()
            .Because("a freeze holds arrivals; nothing reaches the visible list while it is on.");
    }

    [Test]
    public async Task A_capped_state_still_round_trips_as_plain_json()
    {
        // The cap exists because of this: AppState is serialized under
        // GG_STATE_DUMP and rebuilt from it.
        var state = Fill(new AppState(), 600);

        var json = System.Text.Json.JsonSerializer.Serialize(state, AppStateJsonContext.Default.AppState);
        var back = System.Text.Json.JsonSerializer.Deserialize(json, AppStateJsonContext.Default.AppState);

        await Assert.That(back!.Live.Count).IsEqualTo(500);
        await Assert.That(back.Live[^1].Text).IsEqualTo("line 599");
    }

    // ---- S31.5-04 ----

    [Test]
    public async Task A_tail_on_an_existing_file_starts_near_the_end()
    {
        // PEEKING IS ABOUT NOW. Replaying an hour of output before showing the
        // current line is the wrong answer and the expensive one.
        var path = Path.Combine(
            Path.GetTempPath(), "gg-tail-" + Guid.NewGuid().ToString("n") + ".ndjson");

        var lines = Enumerable.Range(0, 4000).Select(i =>
            $$"""{"kind":"text","text":"line {{i}} {{new string('y', 200)}}","at":"2026-09-05T00:00:00+00:00"}""");
        await File.WriteAllLinesAsync(path, lines);

        var read = new LiveTail(path).Read();

        await Assert.That(read).IsNotEmpty()
            .Because("arriving mid-flight should show the last screenful, not nothing.");
        await Assert.That(read.Count).IsLessThan(200)
            .Because("and not the whole hour either - the first look reads roughly a "
                   + "screenful from the end.");
        await Assert.That(read[^1].Text).Contains("line 3999")
            .Because("the end is where now is.");
        await Assert.That(read.All(l => l.Text.StartsWith("line", StringComparison.Ordinal)))
            .IsTrue()
            .Because("the start is cut at a line boundary, or the first line read is a "
                   + "fragment the reader would refuse and silently lose.");
    }

    [Test]
    public async Task A_short_file_is_read_whole()
    {
        // The other side of the same rule: a flight that just started has said
        // little, and all of it is worth showing.
        var path = Path.Combine(
            Path.GetTempPath(), "gg-tail-" + Guid.NewGuid().ToString("n") + ".ndjson");
        await File.WriteAllLinesAsync(path,
        [
            """{"kind":"setup","text":"session init","at":"2026-09-05T00:00:00+00:00"}""",
            """{"kind":"text","text":"first thing said","at":"2026-09-05T00:00:00+00:00"}""",
        ]);

        var read = new LiveTail(path).Read();

        await Assert.That(read.Count).IsEqualTo(2)
            .Because("starting near the end must not mean skipping a flight that has barely "
                   + "started, which is the common case for a pane somebody just attached.");
    }
}
