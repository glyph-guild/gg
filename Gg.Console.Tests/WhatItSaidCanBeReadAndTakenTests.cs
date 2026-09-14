using Gg.Console;
using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Console.Tests;

/// <summary>
/// The panes that hold prose can be scrolled through and copied out of.
/// </summary>
/// <remarks>
/// <para>
/// <b>A pane that clips is the problem it was built to solve.</b> Both detail
/// panes exist because a table cell shows as much of a sentence as the column
/// happens to be wide — and both were <c>Label</c>s, which draw what fits and
/// drop the rest. A runner's explanation or a tracker's change note longer than
/// the pane was no more readable than it had been in the cell.
/// </para>
/// <para>
/// <b>Broken into lines here, scrolled by the widget.</b> The reading views
/// already do exactly this: wrap to the viewport's width and hand a list to a
/// <c>ListView</c>, which scrolls with the arrows a person already uses in
/// every other list in this console. The wrap belongs in the model because a
/// <c>TableView</c> or a <c>ListView</c> cannot be built without a terminal, so
/// arithmetic left in the view is arithmetic no test can reach.
/// </para>
/// <para>
/// <b>And copying takes what is on screen.</b> <c>CopyModal</c> already exists
/// and already copies <c>PaneText.Modal</c> — the producer that drew the modal,
/// deliberately, so a copy cannot be a second rendering that differs. Two
/// modals never bound it, and the work item's text was not tab-aware: copying
/// from its history tab would have handed over the description instead, which
/// is the same fact drawn twice disagreeing with itself.
/// </para>
/// </remarks>
public class WhatItSaidCanBeReadAndTakenTests
{
    private const string Long =
        "The runner could not measure the move bound because the manifest named a path "
      + "outside the working copy, and the envelope's scope had already been narrowed to "
      + "the repository root by a narrowing applied three versions earlier.";

    private static readonly DateTimeOffset At = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);

    private const string Id = "019fe815-6136-7518-bb57-b06d6d3f411a";

    private static AppState Flight() => new()
    {
        Mode = UiMode.FlightDetail,
        FlightTab = FlightTab.Log,
        Flights = new FlightList
        {
            Flights =
            [
                new FlightSummary
                {
                    FlightId = Id,
                    FlightNumber = FlightRef.Format(42),
                    Name = "a flight",
                    Intent = new FlightIntent { Kind = FlightIntentKinds.Text, Text = "work" },
                    CreatedAt = At,
                    RunnerProtocolVersion = 1,
                    FactVocabularyVersion = "0.30.0",
                    ConstitutionVersion = "1.0.0",
                    EnvelopeVersion = "v6",
                    Attempts = 1,
                    State = FlightStates.Open,
                    Facts = [],
                },
            ],
        },
        Story = new FlightStory
        {
            FlightId = Id,
            FlightNumber = FlightRef.Format(42),
            Stage = FlightStoryStages.Reached([]),
            State = FlightStates.Open,
            Entries =
            [
                new StoryEntry
                {
                    At = At,
                    Kind = StoryKinds.ObligationHalted,
                    Stage = FlightStages.Of(StoryKinds.ObligationHalted),
                    Params = [],
                    Said = Long,
                },
            ],
        },
    };

    private static AppState Item(WorkItemTab tab = WorkItemTab.History) => new()
    {
        ActiveTab = TabId.Browse,
        Mode = UiMode.WorkItemDetail,
        WorkItemTab = tab,
        BrowseSelected = 0,
        Browse = new BrowseListing
        {
            ProviderKey = "a-tracker",
            Items =
            [
                new BrowseRow { Id = "18515", Title = "an item", State = "Active" },
            ],
        },
        WorkItemSaid = "the description nobody asked for",
        WorkItemChanges = [new WorkItemChangeRow { When = "now", Who = "Kevin", What = Long }],
    };

    [Test]
    public async Task A_long_explanation_is_broken_to_the_width_it_is_shown_at()
    {
        // THE WRAP IS THE MODEL'S because a ListView cannot be built without a
        // terminal - the same reason KeepingTheCursorsLine lives beside the
        // rows rather than in the view.
        var lines = FlightDetails.LogDetailLines(Flight(), width: 40);

        await Assert.That(lines.Count).IsGreaterThan(1)
            .Because("a sentence of this length at forty columns is several lines, and a pane "
                   + "that drew it as one drew the first forty characters of it.");
        await Assert.That(lines.All(l => l.Length <= 40)).IsTrue()
            .Because("a line wider than the pane is a line whose end nobody can read, which is "
                   + "the defect this pane exists to fix.");
    }

    [Test]
    public async Task Nothing_of_what_was_said_is_dropped_in_the_breaking()
    {
        var joined = string.Join(' ', FlightDetails.LogDetailLines(Flight(), width: 40));

        await Assert.That(joined).Contains("narrowing applied three versions earlier")
            .Because("the end of the sentence is the part a cell already lost; losing it again "
                   + "here would make the pane decorative.");
    }

    [Test]
    public async Task The_same_holds_for_what_a_change_says()
    {
        var lines = WorkItemDetails.ChangeDetailLines(Item(), width: 40);

        await Assert.That(lines.Count).IsGreaterThan(1);
        await Assert.That(lines.All(l => l.Length <= 40)).IsTrue();
    }

    [Test]
    public async Task A_width_nothing_has_measured_yet_wraps_nothing()
    {
        // ZERO BEFORE THE FIRST LAYOUT, and a wrap to no width is one row per
        // character. The reading views already take this care; so does this.
        await Assert.That(FlightDetails.LogDetailLines(Flight(), width: 0).Count).IsEqualTo(1);
        await Assert.That(WorkItemDetails.ChangeDetailLines(Item(), width: 0).Count).IsEqualTo(1);
    }

    [Test]
    public async Task Both_modals_bind_the_key_that_copies()
    {
        // THE COMMAND, THE CLIPBOARD AND THE WIRING ALL EXISTED. Three reading
        // modes bind `c` to it and these two never did, so the one thing a
        // person wanted out of a modal full of somebody else's prose was the
        // one thing they could not take.
        var flight = Keymap.Resolve(
            KeyStroke.Char('c'), new KeymapContext(UiMode.FlightDetail, TabId.Flights));
        var item = Keymap.Resolve(
            KeyStroke.Char('c'), new KeymapContext(UiMode.WorkItemDetail, TabId.Browse));

        await Assert.That(flight).IsEqualTo(Command.CopyModal);
        await Assert.That(item).IsEqualTo(Command.CopyModal);
    }

    [Test]
    public async Task Copying_the_work_item_takes_the_tab_a_person_is_looking_at()
    {
        // WHAT IS ON SCREEN, from the producer that drew it - the rule the copy
        // already keeps and the work item's text did not. On the history tab
        // this used to answer with the description, so `c` would have handed
        // over something the person was not looking at.
        var history = PaneText.Modal(Item(WorkItemTab.History));

        await Assert.That(history).Contains("narrowing applied three versions earlier")
            .Because("the change under the cursor is what the history tab shows, so it is what "
                   + "copying the history tab has to take.");

        await Assert.That(PaneText.Modal(Item(WorkItemTab.Details)))
            .Contains("the description nobody asked for")
            .Because("and the details tab still copies the description, because that is what IT "
                   + "shows.");
    }
}
