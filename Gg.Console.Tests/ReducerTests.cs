using Gg.Contracts;

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
        Key = id,
        Reference = "GG-1",
        FlightNumber = "GG-1",
        Name = id,
        Reason = reason,
        Since = T0,
    };

    private static AppState WithQueue(params string[] ids) => new()
    {
        Queue = [.. ids.Select(id => Row(id))],
    };

    /// <summary>Opens the watch on one flight, the way a person does.</summary>
    private static AppState Watching(AppState state, string flightId) =>
        Reducer.Reduce(
            state with
            {
                Mode = UiMode.FlightDetail,
                Flights = new FlightList { Flights = [AFlight(flightId)] },
                FlightSelected = 0,
            },
            Command.WatchThisFlight);

    private static FlightSummary AFlight(string id) => new()
    {
        FlightId = id,
        FlightNumber = $"GG-{id}",
        Name = id,
        Intent = new FlightIntent { Kind = FlightIntentKinds.Text, Text = "do it" },
        CreatedAt = T0,
        RunnerProtocolVersion = 1,
        FactVocabularyVersion = "0.25.0",
        ConstitutionVersion = "1.0.0",
        EnvelopeVersion = "none",
        Attempts = 1,
        Facts = [],
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
    public async Task TabWalksEveryTabAndComesBackRound()
    {
        // WAS FocusCyclesThroughEveryVisiblePane, and the subject has moved
        // FOUR times. Tab cycled FOCUS between the panes that happened to be
        // visible; then a view took the whole screen and it walked the open
        // tabs; then every tab was on the bar, so it walked all of them; and
        // now one tab is conditional, so it walks what the console OFFERS. A
        // pane that says what it is waiting for is a better answer than a tab
        // a person cannot reach - and a tab nobody may see is worse than
        // either, because `tab' would land on a view with no name on the bar.
        //
        // BOTH GATES OPEN, so the walk is over every tab there is. The
        // narrower walk is held by the pane's own tests; what this is about is
        // that one press moves one tab and the cycle closes.
        var state = new AppState { IsAdmin = true, FleetAllowancesShown = true };
        var seen = new List<TabId>();

        var offered = Tabs.Offered(state);

        for (var i = 0; i < offered.Count; i++)
        {
            state = Reducer.Reduce(state, Command.FocusNextPane);
            seen.Add(state.ActiveTab);
        }

        await Assert.That(seen.Distinct().Count()).IsEqualTo(offered.Count)
            .Because("one press per tab reaches each one exactly once. Found: "
                   + string.Join(", ", seen));
        await Assert.That(seen[^1]).IsEqualTo(TabId.Queue)
            .Because("and comes back to where the console opened.");
    }

    [Test]
    public async Task TabReachesAViewOnceItIsOpen()
    {
        var state = new AppState { CredentialsVisible = true, LiveVisible = true };
        var seen = new List<TabId>();

        // ONE PRESS PER TAB, DERIVED. It was the literal 8, which is the tab
        // count restated in a test - a ninth tab then left the ring incomplete
        // and this failed for a reason that had nothing to do with focus.
        for (var i = 0; i < Tabs.All.Count; i++)
        {
            state = Reducer.Reduce(state, Command.FocusNextPane);
            seen.Add(state.ActiveTab);
        }

        await Assert.That(seen).Contains(TabId.Credentials);
        await Assert.That(seen).Contains(TabId.Queue)
            .Because("and the queue is in the ring, because it is a tab like the others.");
    }

    [Test]
    public async Task ClosingTheTabShowingLandsSomewhereReal()
    {
        // Otherwise the screen shows a view nobody opened and every key appears
        // to do nothing, which is the same symptom as a hang.
        var state = new AppState { BrowseVisible = true, ActiveTab = TabId.Intents };

        var closed = Reducer.Reduce(state, Command.ToggleIntents);

        await Assert.That(closed.BrowseVisible).IsFalse();
        await Assert.That(closed.ActiveTab).IsEqualTo(TabId.Queue);
        await Assert.That(Tabs.All).Contains(closed.ActiveTab);
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
    public async Task AnArrivalBehavesTheSameWhateverPutTheRowThere()
    {
        // WHOSE SUBJECT IS THIS? These arrival assertions were written when the queue held
        // flights and it now holds DECISIONS, so a test asserting the old subject would be
        // satisfied while proving nothing about the new one.
        //
        // It is one path over every reason: Arrived copies the reason through and branches
        // on nothing. Asserted rather than inspected, so the day somebody adds a reason
        // that IS special the difference shows up here instead of in the criteria file's
        // assumptions.
        foreach (var reason in Enum.GetValues<QueueReason>())
        {
            var state = new AppState { Queue = [Row("a"), Row("b")], SelectedRow = 1 };

            var after = Reducer.Arrived(state, Row("c", reason), startedByMe: false);

            await Assert.That(after.Queue[after.SelectedRow].FlightId).IsEqualTo("b")
                .Because($"a '{reason}' arrival must not take the cursor either.");
            await Assert.That(after.Queue.Single(r => r.FlightId == "c").UnreadArrivals)
                .IsEqualTo(1)
                .Because($"and a '{reason}' arrival still marks its row.");
        }
    }

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
        // THROUGH THE WATCH, because that is where watching happens now. It
        // used to be the cursor moving with the live pane open, which was true
        // of a pane beside the queue and cannot be true of a modal over it.
        var attached = Watching(WithQueue("a"), "a");

        var fact = attached.AttachFacts.Single();
        await Assert.That(fact.FlightId).IsEqualTo("a");
        await Assert.That(fact.Attached).IsTrue();
        await Assert.That(fact.AttachCount).IsEqualTo(1);
    }

    [Test]
    public async Task AttachingTwiceCountsTwiceOnTheSameFlight()
    {
        var state = WithQueue("a");

        for (var i = 0; i < 2; i++)
        {
            state = Reducer.Reduce(Watching(state, "a"), Command.CloseModal);
        }

        await Assert.That(state.AttachFacts.Single().AttachCount).IsEqualTo(2)
            .Because("opening the watch twice is two attaches; closing it is not a third.");
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
        // PER FLIGHT WATCHED, not per keypress, and the denominator is still
        // the queue: how much of what needed somebody did somebody feel they
        // had to watch.
        var state = WithQueue("a", "b", "c", "d");
        state = Reducer.Reduce(Watching(state, "a"), Command.CloseModal);
        state = Reducer.Reduce(Watching(state, "b"), Command.CloseModal);

        await Assert.That(AttachRate.Of(state)).IsEqualTo(0.5d);
    }

    private static StreamLine Line(string text, StreamLineKind kind = StreamLineKind.Text) =>
        new() { Kind = kind, Text = text, At = T0 };
}
