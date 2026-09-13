using Gg.Console;
using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Console.Tests;

/// <summary>
/// The log is a tab of its own: the table above, and what the entry under the
/// cursor actually says below it.
/// </summary>
/// <remarks>
/// <para>
/// <b>It was a table inside another tab, and it had to invent row heights.</b>
/// Terminal.Gui has none, so an entry whose detail did not fit became SEVERAL
/// rows — the first carrying the entry, the rest carrying the remainder with
/// every other column blank. That is what the mark column, <c>Unwrapped</c>
/// and <c>DetailWidth</c> existed for, and it is why the log's cursor had to
/// mean an ENTRY while the widget's meant a row.
/// </para>
/// <para>
/// <b>A pane below the table needs none of it.</b> The detail is prose and
/// gets somewhere prose can be read; every entry is one row again, so the two
/// cursors become one. The whole apparatus comes out rather than sitting
/// beside the thing that replaced it — an entry that both expanded in place
/// and rendered below would be one fact drawn twice, and the two would
/// disagree the first time either changed.
/// </para>
/// <para>
/// <b>And the details tab stops carrying it.</b> Sharing a tab was what made
/// the log fight the fields for height; a tab per question is what the modal
/// already does for the gate.
/// </para>
/// </remarks>
public class TheLogIsItsOwnTabTests
{
    private static readonly DateTimeOffset At = new(2026, 9, 13, 12, 0, 0, TimeSpan.Zero);

    private static StoryEntry[] Happenings() =>
    [
        new StoryEntry
        {
            At = At,
            Kind = StoryKinds.LeaseGranted,
            Stage = FlightStages.Of(StoryKinds.LeaseGranted),
            Params = ["a-runner"],
            Attempt = 2,
            Actor = new Actor { Kind = ActorKinds.Runner, Name = "a-runner" },
        },
        new StoryEntry
        {
            At = At.AddMinutes(20),
            Kind = StoryKinds.ObligationHalted,
            Stage = FlightStages.Of(StoryKinds.ObligationHalted),
            Params = [],
            Said = "the move bound could not be measured\nand nothing said what it should be",
        },
    ];

    private static AppState Opened(int selected = 0)
    {
        var entries = Happenings();

        return new AppState
        {
            Mode = UiMode.FlightDetail,
            FlightTab = FlightTab.Log,
            LogSelected = selected,
            Story = new FlightStory
            {
                FlightId = "019fe815-6136-7518-bb57-b06d6d3f411a",
                FlightNumber = FlightRef.Format(42),
                Stage = FlightStoryStages.Reached(entries),
                State = FlightStates.Open,
                Entries = entries,
            },
        };
    }

    [Test]
    public async Task The_modal_has_a_tab_for_it()
    {
        // A THIRD TAB, which the enum's own remark said is what it exists for:
        // "a bool would say evidence is showing ... this says which of a set
        // has the body, which is what a third tab would need".
        await Assert.That(Enum.GetValues<FlightTab>()).Contains(FlightTab.Log);
    }

    [Test]
    public async Task The_key_that_cycles_the_modal_reaches_it()
    {
        // `v` is the console's word for "show me the other half" and it now
        // has three halves. Reached by cycling rather than by a key of its
        // own: the modal's letters are nearly spent, and a tab a person finds
        // by pressing the key they already know is one they find.
        var state = Opened() with { FlightTab = FlightTab.Gate };

        await Assert.That(Reducer.Reduce(state, Command.NextFlightTab).FlightTab)
            .IsEqualTo(FlightTab.Log);
        await Assert.That(
            Reducer.Reduce(Opened() with { FlightTab = FlightTab.Log }, Command.NextFlightTab)
                .FlightTab)
            .IsEqualTo(FlightTab.Details)
            .Because("cycling has to come back round, or the third tab is a place a person gets "
                   + "stuck rather than a place they visit.");
    }

    [Test]
    public async Task Every_entry_is_one_row_again()
    {
        // THE CONTINUATIONS ARE GONE. An entry became several rows only so its
        // detail could be read inside the table; with a pane below, one entry
        // is one row and the cursor means the same thing to the model and to
        // the widget.
        var rows = Rows.Log(Opened());

        await Assert.That(rows.Count).IsEqualTo(2)
            .Because("two things happened to this flight, and the second one's detail is two "
                   + "lines - which used to make three rows or more.");
        await Assert.That(rows.Select(r => r.Entry).Distinct().Count()).IsEqualTo(rows.Count)
            .Because("one row per entry means no two rows share an entry.");
    }

    [Test]
    public async Task The_table_no_longer_carries_a_column_for_a_mark()
    {
        // The mark said open or closed, and nothing opens or closes now.
        await Assert.That(Rows.LogColumns).IsEquivalentTo((string[])["time", "attempt", "event"]);
    }

    [Test]
    public async Task What_is_under_the_cursor_is_what_the_pane_below_says()
    {
        // THE POINT OF THE LAYOUT. The table is scannable because it is one
        // line per entry; the prose is readable because it is somewhere prose
        // fits. Moving the cursor changes the second.
        await Assert.That(FlightDetails.LogDetail(Opened(selected: 1)))
            .Contains("the move bound could not be measured");
        await Assert.That(FlightDetails.LogDetail(Opened(selected: 1)))
            .Contains("nothing said what it should be")
            .Because("the second line is exactly what the table could not show, and it is why "
                   + "there is a pane.");
    }

    [Test]
    public async Task An_entry_with_nothing_more_to_say_says_that()
    {
        // Most entries are a sentence and a time. A pane that went blank on
        // them would read as one that failed to render rather than as an entry
        // with nothing further.
        await Assert.That(FlightDetails.LogDetail(Opened(selected: 0))).IsNotEmpty();
    }

    [Test]
    public async Task The_details_tab_no_longer_draws_the_log()
    {
        // WHAT MOVING IT MEANS. Left in both places the fields and the log go
        // on fighting for the same height, which is the problem a tab solves.
        var details = PaneText.Modal(Opened() with { FlightTab = FlightTab.Details });

        await Assert.That(details).DoesNotContain("lease-granted")
            .Because("the log has a tab now, and a reader who presses v expects to find it there "
                   + "rather than in both places.");
    }

    [Test]
    public async Task Which_absence_it_is_still_gets_said()
    {
        // Unchanged and asserted anyway: a story nobody fetched and a flight
        // nothing happened to are different facts, and the sentence that tells
        // them apart is the one thing the empty log tab has to keep.
        var never = new AppState { Mode = UiMode.FlightDetail, FlightTab = FlightTab.Log };

        await Assert.That(FlightDetails.LogAbsence(never)).IsNotEmpty();
    }
}
