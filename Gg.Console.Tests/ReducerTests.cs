namespace Gg.Console.Tests;

/// <summary>
/// What the model does when things happen to it. No terminal anywhere.
/// </summary>
public class ReducerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);

    private static QueueRow Row(string id, QueueReason reason = QueueReason.RunnerOffline) => new()
    {
        FlightId = id,
        FlightNumber = "GG-1",
        Name = id,
        Reason = reason,
        Since = T0,
    };

    private static AppState WithQueue(params string[] ids) => new()
    {
        Queue = [.. ids.Select(id => Row(id))],
    };

    // ---- the keymap commands ----

    [Test]
    public async Task ToggleHelpEntersAndLeavesTheModal()
    {
        var opened = Reducer.Reduce(new AppState(), Command.ToggleHelp);
        await Assert.That(opened.Mode).IsEqualTo(UiMode.Help);
        await Assert.That(Reducer.Reduce(opened, Command.ToggleHelp).Mode).IsEqualTo(UiMode.Normal);
    }

    [Test]
    public async Task FocusCyclesThroughEveryVisiblePane()
    {
        // Hidden panes are skipped: tabbing into a pane that is not there
        // strands the focus ring somewhere invisible, which reads as a frozen
        // keyboard.
        var state = new AppState { EvidenceVisible = false, LiveVisible = false };
        var seen = new List<PaneId>();

        for (var i = 0; i < 6; i++)
        {
            state = Reducer.Reduce(state, Command.FocusNextPane);
            seen.Add(state.FocusedPane);
        }

        await Assert.That(seen).DoesNotContain(PaneId.Evidence);
        await Assert.That(seen).DoesNotContain(PaneId.Live);
        await Assert.That(seen.Distinct().Order().ToList())
            .IsEquivalentTo(new[] { PaneId.Queue, PaneId.Flight }.Order().ToList());
    }

    [Test]
    public async Task FocusReachesAPaneOnceItIsShown()
    {
        var state = new AppState { EvidenceVisible = true, LiveVisible = true };
        var seen = new List<PaneId>();

        for (var i = 0; i < 8; i++)
        {
            state = Reducer.Reduce(state, Command.FocusNextPane);
            seen.Add(state.FocusedPane);
        }

        await Assert.That(seen).Contains(PaneId.Evidence);
        await Assert.That(seen).Contains(PaneId.Live);
    }

    [Test]
    public async Task HidingTheFocusedPaneMovesFocusSomewhereReal()
    {
        // Otherwise focus points at nothing and every key appears to do
        // nothing, which is the same symptom as a hang.
        var state = new AppState { LiveVisible = true, FocusedPane = PaneId.Live };

        var hidden = Reducer.Reduce(state, Command.ToggleLive);

        await Assert.That(hidden.LiveVisible).IsFalse();
        await Assert.That(hidden.FocusedPane).IsNotEqualTo(PaneId.Live);
    }

    [Test]
    public async Task SelectionMovesWithinTheQueueAndStopsAtTheEnds()
    {
        var state = WithQueue("a", "b", "c");

        await Assert.That(Reducer.Reduce(state, Command.SelectPrevious).SelectedRow).IsEqualTo(0);

        state = Reducer.Reduce(state, Command.SelectNext);
        await Assert.That(state.SelectedRow).IsEqualTo(1);

        state = Reducer.Reduce(Reducer.Reduce(state, Command.SelectNext), Command.SelectNext);
        await Assert.That(state.SelectedRow).IsEqualTo(2)
            .Because("running off the end of a two-item queue must not select a row that is not there.");
    }

    // ---- arrivals queue, they do not preempt ----

    [Test]
    public async Task AnArrivalDoesNotMoveTheCursor()
    {
        // The discipline the whole console is judged by later. It is barely
        // testable with one flight and it goes in now, because
        // focus-follows-the-work is what a person copies by reflex when one
        // thing is happening and is wrong the moment there are twelve.
        var state = WithQueue("a", "b", "c") with { SelectedRow = 1 };

        var after = Reducer.Arrived(state, Row("d"), startedByMe: false);

        await Assert.That(after.SelectedRow).IsEqualTo(1)
            .Because("a new decision may ask for attention; it may not take it.");
        await Assert.That(after.Queue.Select(r => r.FlightId)).Contains("d");
    }

    [Test]
    public async Task AnArrivalStillPointingAtTheSameFlightAfterTheQueueReorders()
    {
        // The cursor follows the FLIGHT, not the index. Holding an index means
        // an arrival that sorts above the selection silently moves the person
        // to a different flight without the cursor appearing to move at all -
        // which is worse than moving it.
        var state = WithQueue("a", "b", "c") with { SelectedRow = 2 };
        var selected = state.Queue[2].FlightId;

        var after = Reducer.Arrived(state, Row("arrives-first") with { Since = T0.AddHours(-5) }, startedByMe: false);

        await Assert.That(after.Queue[after.SelectedRow].FlightId).IsEqualTo(selected);
    }

    [Test]
    public async Task AnArrivalMarksTheRowRatherThanTakingTheCursor()
    {
        var state = WithQueue("a", "b") with { SelectedRow = 0 };

        var after = Reducer.Arrived(state, Row("b"), startedByMe: false);

        await Assert.That(after.Queue.Single(r => r.FlightId == "b").UnreadArrivals).IsEqualTo(1);
        await Assert.That(after.SelectedRow).IsEqualTo(0);
    }

    [Test]
    public async Task TheOneExceptionIsAFlightYouStartedOrTook()
    {
        var state = WithQueue("a", "b", "c") with { SelectedRow = 0 };

        var after = Reducer.Arrived(state, Row("c"), startedByMe: true);

        await Assert.That(after.Queue[after.SelectedRow].FlightId).IsEqualTo("c")
            .Because("you asked for this one; going to it is the answer to what you just did.");
    }

    [Test]
    public async Task SelectingARowClearsItsUnreadCount()
    {
        var state = Reducer.Arrived(WithQueue("a", "b") with { SelectedRow = 0 }, Row("b"), startedByMe: false);

        var moved = Reducer.Reduce(state, Command.SelectNext);

        await Assert.That(moved.Queue.Single(r => r.FlightId == "b").UnreadArrivals).IsEqualTo(0);
    }

    // ---- freeze for copy ----

    [Test]
    public async Task FreezingStopsTheLiveViewMovingUnderTheSelection()
    {
        // Live rendering and text selection are incompatible, and everybody
        // discovers this while trying to copy a stack trace.
        var state = new AppState { LiveVisible = true, Live = [Line("one")] };

        var frozen = Reducer.Reduce(state, Command.ToggleFreeze);
        var after = Reducer.StreamArrived(frozen, Line("two"));

        await Assert.That(after.Frozen).IsTrue();
        await Assert.That(after.Live.Select(l => l.Text)).IsEquivalentTo(new[] { "one" })
            .Because("what is on screen must not change while somebody is selecting it.");
    }

    [Test]
    public async Task NothingArrivingDuringAFreezeIsLost()
    {
        // Dropping it would be worse than moving the screen: the copy works and
        // the output has a hole in it that nobody sees.
        var state = new AppState { LiveVisible = true, Live = [Line("one")] };

        var frozen = Reducer.Reduce(state, Command.ToggleFreeze);
        frozen = Reducer.StreamArrived(frozen, Line("two"));
        frozen = Reducer.StreamArrived(frozen, Line("three"));

        var thawed = Reducer.Reduce(frozen, Command.ToggleFreeze);

        await Assert.That(thawed.Frozen).IsFalse();
        await Assert.That(thawed.Live.Select(l => l.Text)).IsEquivalentTo(new[] { "one", "two", "three" });
        await Assert.That(thawed.Held).IsEmpty();
    }

    [Test]
    public async Task LinesArriveInOrderAndKeepTheirKind()
    {
        // Verbosity is a data model rather than a regex applied later, so the
        // kind has to survive the trip through the store.
        var state = Reducer.StreamArrived(new AppState(), Line("tool call", StreamLineKind.Tool));

        await Assert.That(state.Live.Single().Kind).IsEqualTo(StreamLineKind.Tool);
    }

    // ---- the live view, recorded as a fact ----

    [Test]
    public async Task AttachingTheLiveViewIsRecordedAsAFactOnTheFlight()
    {
        var state = WithQueue("a") with { Flight = null };

        var attached = Reducer.Reduce(state, Command.ToggleLive);

        var fact = attached.AttachFacts.Single();
        await Assert.That(fact.FlightId).IsEqualTo("a");
        await Assert.That(fact.Attached).IsTrue();
        await Assert.That(fact.AttachCount).IsEqualTo(1);
    }

    [Test]
    public async Task AttachingTwiceCountsTwiceOnTheSameFlight()
    {
        var state = WithQueue("a");

        for (var i = 0; i < 4; i++)
        {
            state = Reducer.Reduce(state, Command.ToggleLive);
        }

        await Assert.That(state.AttachFacts.Single().AttachCount).IsEqualTo(2)
            .Because("two attaches and two detaches is two attaches.");
    }

    [Test]
    public async Task AConsoleNobodyAttachedRecordsARateOfZero()
    {
        // The baseline, and slice one is the only honest moment to take it -
        // measured later, after we have been impressed by the live view, it
        // measures the wrong thing.
        var state = WithQueue("a", "b", "c");

        await Assert.That(AttachRate.Of(state)).IsEqualTo(0d);
    }

    [Test]
    public async Task TheRateIsAttachedFlightsOverFlightsSeen()
    {
        // Attaching is per FLIGHT WATCHED, not per keypress: moving the cursor
        // while the live view is open is watching the flight you moved to.
        var state = WithQueue("a", "b", "c", "d");
        state = Reducer.Reduce(state, Command.ToggleLive);   // watching a
        state = Reducer.Reduce(state, Command.SelectNext);   // now watching b

        await Assert.That(AttachRate.Of(state)).IsEqualTo(0.5d);
    }

    private static StreamLine Line(string text, StreamLineKind kind = StreamLineKind.Text) =>
        new() { Kind = kind, Text = text, At = T0 };
}
