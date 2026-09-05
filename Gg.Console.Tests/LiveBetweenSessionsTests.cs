namespace Gg.Console.Tests;

/// <summary>
/// The console reads the live view between sessions, and says which silence it is showing.
/// </summary>
/// <remarks>
/// <para>
/// <b>Step three of five, and deliberately before streaming.</b> A tail that
/// advances when a person presses a key is already useful, and it proves the
/// producer, the shared path and the consumer end to end without the
/// architectural change a mid-session read needs. If step four is never wanted,
/// this is still worth having.
/// </para>
/// <para>
/// <b>The offset is held by the LOOP, never by a session.</b> <c>IUiSession</c>
/// builds views FROM the given state and must not retain anything across calls;
/// a file offset rotting in one is exactly the failure that rule exists for. The
/// loop is the lifetime that spans sessions, so it is the thing that may
/// remember where it read to.
/// </para>
/// </remarks>
public class LiveBetweenSessionsTests
{
    private sealed class ScriptedUi(params Func<AppState, UiOutcome>[] script) : IUiSession
    {
        private readonly Queue<Func<AppState, UiOutcome>> _script = new(script);

        public List<AppState> StatesSeen { get; } = [];

        public UiOutcome Run(AppState state)
        {
            StatesSeen.Add(state);
            return _script.Dequeue()(state);
        }
    }

    private sealed class NoEditor : IEditorSession
    {
        public string Edit(string initialText) => initialText;
    }

    /// <summary>A source that hands out a scripted batch per look.</summary>
    private sealed class ScriptedSource(bool exists, params StreamLine[][] batches) : ILiveSource
    {
        private readonly Queue<StreamLine[]> _batches = new(batches);

        public int Reads { get; private set; }

        public bool Exists => exists;

        public IReadOnlyList<StreamLine> Read()
        {
            Reads++;
            return _batches.Count > 0 ? _batches.Dequeue() : [];
        }
    }

    private static StreamLine Line(string text) =>
        new() { Kind = StreamLineKind.Text, Text = text, At = DateTimeOffset.UnixEpoch };

    private static AppState Watching(params string[] flightIds) => new()
    {
        LiveVisible = true,
        Queue = [.. flightIds.Select((id, i) => new QueueRow
        {
            FlightId = id,
            FlightNumber = $"GG-{i + 1}",
            Name = "a flight",
            Reason = QueueReason.AwaitingDecision,
            Since = DateTimeOffset.UnixEpoch,
        })],
    };

    // ---- S31.3-01 ----

    [Test]
    public async Task Lines_reach_the_pane_between_one_session_and_the_next()
    {
        var source = new ScriptedSource(exists: true, [Line("first")], [Line("second")]);
        var ui = new ScriptedUi(
            s => new UiOutcome(Command.OpenFlight, s),
            s => new UiOutcome(Command.OpenFlight, s),
            s => new UiOutcome(Command.Quit, s));

        new ConsoleLoop(ui, new NoEditor(), tails: new LiveTails(_ => source)).Run(Watching("f1"));

        await Assert.That(ui.StatesSeen[0].Live.Select(l => l.Text)).IsEquivalentTo((string[])["first"])
            .Because("the loop reads before it hands the state to a session, so the first "
                   + "screen already shows what had been written.");
        await Assert.That(ui.StatesSeen[1].Live.Select(l => l.Text))
            .IsEquivalentTo((string[])["first", "second"])
            .Because("the second look resumes rather than replaying, which is what the offset "
                   + "held by the loop is for.");
    }

    // ---- S31.3-02 ----

    [Test]
    public async Task Moving_the_selection_tails_a_different_flight()
    {
        var asked = new List<string>();
        var ui = new ScriptedUi(
            s => new UiOutcome(Command.OpenFlight, s with { SelectedRow = 1 }),
            s => new UiOutcome(Command.Quit, s));

        new ConsoleLoop(ui, new NoEditor(), tails: new LiveTails(id =>
        {
            asked.Add(id);
            return new ScriptedSource(exists: true, [Line("from " + id)]);
        })).Run(Watching("f1", "f2"));

        await Assert.That(asked).IsEquivalentTo((string[])["f1", "f2"])
            .Because("the pane follows the selection, so moving the cursor tails the file "
                   + "belonging to the flight now under it.");
    }

    [Test]
    public async Task A_flight_returned_to_does_not_replay_what_was_already_seen()
    {
        // ONE TAIL PER FLIGHT, which is why the loop keeps a map rather than a
        // single reader: away and back must not re-read from zero.
        var f1 = new ScriptedSource(exists: true, [Line("one")], []);
        var ui = new ScriptedUi(
            s => new UiOutcome(Command.OpenFlight, s with { SelectedRow = 1 }),
            s => new UiOutcome(Command.OpenFlight, s with { SelectedRow = 0 }),
            s => new UiOutcome(Command.Quit, s));

        new ConsoleLoop(ui, new NoEditor(), tails: new LiveTails(id =>
            id == "f1" ? f1 : new ScriptedSource(exists: true, [Line("elsewhere")]))).Run(
                Watching("f1", "f2"));

        await Assert.That(f1.Reads).IsEqualTo(2)
            .Because("the same tail was asked twice and kept its offset between them.");
        await Assert.That(ui.StatesSeen[^1].Live.Count(l => l.Text == "one")).IsEqualTo(1)
            .Because("coming back to a flight must not show its first line twice.");
    }

    // ---- S31.3-03 ----

    [Test]
    public async Task A_flight_with_no_live_view_says_so_rather_than_showing_an_empty_box()
    {
        var ui = new ScriptedUi(s => new UiOutcome(Command.Quit, s));

        new ConsoleLoop(ui, new NoEditor(),
            tails: new LiveTails(_ => new ScriptedSource(exists: false))).Run(Watching("f1"));

        var shown = ui.StatesSeen[0];

        await Assert.That(shown.Silence).IsEqualTo(LiveSilence.NotStarted);
        await Assert.That(PaneText.Live(shown)).Contains("Nothing is writing one")
            .Because("no file means the flight has not been claimed or predates always-write, "
                   + "which is a different thing from an agent that has not spoken.");
    }

    [Test]
    public async Task A_flight_writing_nothing_yet_says_that_instead()
    {
        var ui = new ScriptedUi(s => new UiOutcome(Command.Quit, s));

        new ConsoleLoop(ui, new NoEditor(),
            tails: new LiveTails(_ => new ScriptedSource(exists: true))).Run(Watching("f1"));

        var shown = ui.StatesSeen[0];

        await Assert.That(shown.Silence).IsEqualTo(LiveSilence.NothingYet);
        await Assert.That(PaneText.Live(shown)).Contains("has not said anything yet")
            .Because("a person reading this as 'nothing is writing' concludes the feature is "
                   + "broken, which is the whole reason the two are different sentences.");
    }

    [Test]
    public async Task A_pane_that_is_off_reads_nothing_at_all()
    {
        var source = new ScriptedSource(exists: true, [Line("never seen")]);
        var ui = new ScriptedUi(s => new UiOutcome(Command.Quit, s));

        new ConsoleLoop(ui, new NoEditor(), tails: new LiveTails(_ => source)).Run(
            Watching("f1") with { LiveVisible = false });

        await Assert.That(source.Reads).IsEqualTo(0)
            .Because("the pane is off by default and stays off; a detached console must not "
                   + "open a file per keypress on every flight somebody scrolls past.");
        await Assert.That(ui.StatesSeen[0].Silence).IsEqualTo(LiveSilence.NotAttached);
    }

    // ---- S31.3-04 ----

    [Test]
    public async Task A_frozen_pane_holds_arrivals_and_thaw_releases_them_in_order()
    {
        // The reducer has always done this and nothing had ever driven it from a
        // real read. This is that, through the loop.
        var ui = new ScriptedUi(
            s => new UiOutcome(Command.OpenFlight, Reducer.Reduce(s, Command.ToggleFreeze)),
            s => new UiOutcome(Command.OpenFlight, s),
            s => new UiOutcome(Command.Quit, Reducer.Reduce(s, Command.ToggleFreeze)));

        new ConsoleLoop(ui, new NoEditor(), tails: new LiveTails(_ => new ScriptedSource(
            exists: true, [Line("before")], [Line("during one")], [Line("during two")])))
            .Run(Watching("f1"));

        var final = ui.StatesSeen[^1];

        await Assert.That(final.Held.Select(l => l.Text))
            .IsEquivalentTo((string[])["during one", "during two"])
            .Because("a freeze holds what arrives so text can be selected, in the order it "
                   + "arrived - a held buffer that reorders is worse than no freeze.");
        await Assert.That(final.Live.Select(l => l.Text)).IsEquivalentTo((string[])["before"]);
    }

    // ---- S31.3-05 ----

    [Test]
    public async Task Attaching_is_recorded_with_its_count()
    {
        // LiveAttachFact has existed with no caller. Watching is recorded; what
        // was watched is not, which is rule 6.
        var ui = new ScriptedUi(
            s => new UiOutcome(Command.OpenFlight, Reducer.Reduce(s, Command.ToggleLive)),
            s => new UiOutcome(Command.OpenFlight, Reducer.Reduce(s, Command.ToggleLive)),
            s => new UiOutcome(Command.Quit, s));

        new ConsoleLoop(ui, new NoEditor(),
            tails: new LiveTails(_ => new ScriptedSource(exists: true))).Run(
                Watching("f1") with { LiveVisible = false });

        var facts = ui.StatesSeen[^1].AttachFacts;

        await Assert.That(facts).IsNotEmpty()
            .Because("that somebody watched an agent work is a fact; what the agent said is "
                   + "not, and stays local.");
        await Assert.That(facts.Any(f => f.AttachCount >= 1)).IsTrue()
            .Because("the count is what makes this readable as a rate rather than a flag.");
    }
}
