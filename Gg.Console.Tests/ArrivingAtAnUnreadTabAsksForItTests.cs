using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// A tab nobody has read asks for itself the moment somebody arrives, rather
/// than waiting out the countdown.
/// </summary>
/// <remarks>
/// <para>
/// <b>Found auditing the board tab, and it is thirty seconds of a sentence
/// that reads like a failure.</b> The boot fetches what the queue needs; the
/// board needs two reads of its own, and neither is one of them. Nothing asked
/// for them on arrival, so pressing `;` on a fresh console drew
/// <i>"could not load the board"</i> until <c>AutoRefresh</c>'s next tick - up
/// to thirty seconds, during which the honest answer was "nobody has asked
/// yet".
/// </para>
/// <para>
/// <b>The mechanism already existed and nothing used it for this.</b>
/// <c>RefreshState.Wanted</c> is how `g` gets past the countdown, and a tab
/// arriving unread wants exactly that. It is set in the REDUCER, where the tab
/// changes, because that is the edge: a level-triggered version - "read while
/// this tab is unread" - would hammer a control plane that is refusing, once
/// per frame, which is the opposite of what a person with a broken connection
/// needs.
/// </para>
/// <para>
/// <b>And the sentence under an unread pane is not the sentence under a failed
/// read.</b> Three things an empty pane can mean, and this console has written
/// that down twice; "could not load" is the one a person acts on by going to
/// look for an outage.
/// </para>
/// </remarks>
public class ArrivingAtAnUnreadTabAsksForItTests
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 17, 12, 0, 0, TimeSpan.Zero);

    private static AppState Fresh() => new()
    {
        ActiveTab = TabId.Queue,
        Flights = new FlightList { Flights = [] },
    };

    private static AppState Read(AppState state) => state with
    {
        Board = new BoardPage { Nominations = [], IncludedEnded = true },
        Watches = new WatchStandingList
        {
            Standings =
            [
                new()
                {
                    Name = "nightly-triage",
                    Version = "nightly-triage@v1",
                    LastHeardAt = Noon,
                    Nominated = 0,
                    Opened = 0,
                    Window = "24h",
                },
            ],
        },
    };

    [Test]
    public async Task Arriving_at_the_board_asks_for_it_rather_than_waiting_out_the_countdown()
    {
        var arrived = Reducer.Reduce(Fresh(), Command.ShowBoardTab);

        await Assert.That(arrived.ActiveTab).IsEqualTo(TabId.Board);
        await Assert.That(arrived.Refresh.Wanted).IsTrue()
            .Because("the boot does not fetch this tab's two reads, so without asking here a "
                   + "person who pressed `;` looks at a sentence for up to thirty seconds - "
                   + "and the sentence says the read failed.");
    }

    [Test]
    public async Task Arriving_at_one_already_read_asks_for_nothing()
    {
        // THE CONTAINMENT HALF. A version that always asked would turn the tab
        // keys into a request each, and cycling the bar with tab would be six -
        // for panes whose answers are already on the screen.
        var arrived = Reducer.Reduce(Read(Fresh()), Command.ShowBoardTab);

        await Assert.That(arrived.Refresh.Wanted).IsFalse()
            .Because("what is already held is what a person is looking at, and re-reading it "
                   + "because they pressed a key to see it is a request nobody asked for.");
    }

    [Test]
    public async Task The_tab_key_asks_too_because_it_arrives_the_same_way()
    {
        // ONE RULE, NOT ONE PER KEY. `;` and tab both land somebody on a pane,
        // and a fix bound to the letter would leave the convention key broken -
        // which is the shape the enter binding already recorded.
        var cycling = Fresh();

        for (var press = 0; press < Enum.GetValues<TabId>().Length; press++)
        {
            cycling = Reducer.Reduce(cycling, Command.FocusNextPane);

            if (cycling.ActiveTab == TabId.Board)
            {
                break;
            }
        }

        await Assert.That(cycling.ActiveTab).IsEqualTo(TabId.Board)
            .Because("ASK WHY IT PASSES: a cycle that never reached the board would make the "
                   + "assertion below hold for the wrong reason.");
        await Assert.That(cycling.Refresh.Wanted).IsTrue();
    }

    [Test]
    public async Task Going_back_to_a_tab_the_boot_filled_asks_for_nothing()
    {
        var back = Reducer.Reduce(
            Read(Fresh()) with { ActiveTab = TabId.Board }, Command.ShowQueueTab);

        await Assert.That(back.Refresh.Wanted).IsFalse()
            .Because("the queue is never unread - the boot builds it - so arriving there is "
                   + "arriving at something already on the screen.");
    }

    [Test]
    public async Task An_unread_board_says_nobody_has_asked_yet_and_not_that_it_failed()
    {
        var text = PaneText.Board(Fresh() with { ActiveTab = TabId.Board });

        await Assert.That(text.Contains("could not", StringComparison.OrdinalIgnoreCase)).IsFalse()
            .Because("a read nobody has made yet has not failed, and the difference is what a "
                   + "person does next: one is waited out, the other sends them looking for an "
                   + "outage that is not there.");
        await Assert.That(text.Contains("reading", StringComparison.OrdinalIgnoreCase)).IsTrue()
            .Because("the pane is about to be filled - the arrival asked - so what it says is "
                   + "what is happening.");
    }

    [Test]
    public async Task A_board_whose_read_did_not_finish_says_that_instead()
    {
        // THE THIRD MEANING, and the one that keeps the second honest. A
        // refresh that failed writes its own sentence into the diagnosis, and a
        // pane still saying "reading" while nothing is coming is the staleness
        // this whole pane exists to avoid.
        var failed = Fresh() with
        {
            ActiveTab = TabId.Board,
            Diagnosis = "The last refresh did not finish: no route to the control plane.",
        };

        await Assert.That(PaneText.Board(failed).Contains(
                "did not finish", StringComparison.OrdinalIgnoreCase)).IsTrue()
            .Because("the reason is the only thing anybody can act on, and it was written by "
                   + "whatever tried.");
    }
}
