using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// The board tab: every nomination in one table, and the watches whose sweeps
/// make them in another beneath it.
/// </summary>
/// <remarks>
/// <para>
/// <b>S39.6-03, and the shape was the owner's call.</b> A watch is not work
/// and has no flight, so <c>QueueRow</c> fits it no better than it fits a
/// standing nomination - which is the excavation S37.4-01 named. A tab of its
/// own holds both without asking the queue's row to mean a third thing.
/// </para>
/// <para>
/// <b>Two tables, and that was the owner's call too (2026-10-09).</b> This
/// was one table whose first column said which kind of row it was, because
/// two would be two cursors on one screen. What changed is paging: both lists
/// scroll without end now, and in one table a page of nominations landing
/// pushed every watch further down a list the cursor was already in. So each
/// table has its own cursor and pages on it, exactly one is driven, and `v`
/// crosses.
/// </para>
/// <para>
/// <b>What the queue is a subset of.</b> The queue shows what needs somebody
/// and already carries standing nominations; this shows the board they come
/// from - the answered, the refused, and the machinery that goes looking.
/// Flights has exactly that relationship to the queue and for the same reason.
/// </para>
/// </remarks>
public class TheBoardTabHoldsBothKindsOfRowTests
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 17, 12, 0, 0, TimeSpan.Zero);

    private static NominationSummary ANomination(
        string subject = "work-item:https://tracker.example/acme/4242",
        string mode = "auto",
        string? ending = null,
        string? because = null) => new()
    {
        NominationId = Guid.NewGuid(),
        Nominator = "watch:nightly-triage",
        Subject = subject,
        Version = "7",
        WorkKind = "review",
        Mode = mode,
        State = ending is null ? "standing" : "ended",
        Ending = ending,
        Because = because,
        MadeAt = Noon.AddMinutes(-30),
    };

    private static WatchStanding AWatch(
        string name = "nightly-triage",
        string? executor = WatchExecutors.Instructions,
        string? outcome = WatchOutcomes.Swept,
        DateTimeOffset? quietSince = null,
        string? diagnosis = null,
        int opened = 3,
        int? budgeted = 5) => new()
    {
        Name = name,
        Version = $"{name}@v1",
        Executor = executor,
        LastHeardAt = Noon.AddMinutes(-10),
        Outcome = outcome,
        Nominated = 2,
        Diagnosis = diagnosis,
        QuietSince = quietSince,
        Opened = opened,
        Window = "24h",
        Budgeted = budgeted,
    };

    private static AppState Board(
        IReadOnlyList<NominationSummary>? nominations = null,
        IReadOnlyList<WatchStanding>? watches = null) => new()
    {
        ActiveTab = TabId.Board,
        Board = new BoardPage
        {
            Nominations = nominations ?? [ANomination()],
            IncludedEnded = true,
        },
        Watches = new WatchStandingList { Standings = watches ?? [AWatch()] },
    };

    [Test]
    public async Task Nominations_and_sweeps_are_two_tables()
    {
        var state = Board();

        var nomination = Rows.Nominations(state).Single();

        await Assert.That(nomination.Subject).Contains("4242");
        await Assert.That(nomination.State).IsEqualTo("auto")
            .Because("a standing row shows its mode, because whether it opens without a "
                   + "person is the thing somebody is deciding whether to answer.");
        await Assert.That(nomination.Kind).IsEqualTo("review");

        var sweep = Rows.Sweeps(state).Single();

        await Assert.That(sweep.Subject).IsEqualTo("nightly-triage");
        await Assert.That(sweep.State).IsEqualTo(WatchOutcomes.Swept);
        await Assert.That(sweep.Kind).IsEqualTo(WatchExecutors.Instructions)
            .Because("the executor in force is what would run this watch's next sweep, and "
                   + "it is one of the three facts the criterion names.");
        await Assert.That(sweep.Cost).IsEqualTo("3 of 5 in 24h")
            .Because("`3` says nothing and `3 of 5 in 24h` says whether the next nomination "
                   + "will stand.");
    }

    [Test]
    public async Task Neither_table_spends_a_column_saying_what_its_rows_are()
    {
        await Assert.That(Rows.NominationColumns).DoesNotContain("")
            .Because("a column carrying `nomination` down every row of a table titled "
                   + "nominations is width spent on nothing.");
        await Assert.That(Rows.SweepColumns).DoesNotContain("");
        await Assert.That(Rows.SweepColumns[0]).IsEqualTo("watch");
    }

    [Test]
    public async Task An_ended_nomination_shows_what_happened_rather_than_its_mode()
    {
        var rows = Rows.Nominations(Board(
            [ANomination(ending: "refused", because: "'review' is not on this watch's menu")]));

        await Assert.That(rows[0].State).IsEqualTo("refused")
            .Because("a row reading `auto` that has in fact been refused is the one thing "
                   + "this column must never say.");
        // AND THE SENTENCE IS NOT IN THE ROW ANY MORE. It is a nominator's
        // prose and a cell clipped it; `ABoardRowOpensIntoAModalTests` holds
        // it where it went. What stays here is the ending, which is a word.
        await Assert.That(rows[0].Cost).IsEqualTo("")
            .Because("a nomination is a question, not a spender.");
    }

    [Test]
    public async Task A_watch_that_has_gone_quiet_reads_as_a_state_not_a_timestamp()
    {
        var rows = Rows.Sweeps(Board(watches: [AWatch(quietSince: Noon.AddHours(-5))]));

        await Assert.That(rows.Single().State).IsEqualTo("quiet")
            .Because("making a person subtract two clocks to find out nothing is sweeping is "
                   + "how a board comes to look healthy while it is blind - rule 11.");
    }

    [Test]
    public async Task A_watch_that_could_not_sweep_carries_the_runners_own_sentence()
    {
        var rows = Rows.Sweeps(Board(watches:
            [AWatch(outcome: WatchOutcomes.Unreachable,
                    diagnosis: "the tracker refused all 3 of this sweep's reads")]));

        var sweep = rows.Single();

        await Assert.That(sweep.State).IsEqualTo(WatchOutcomes.Unreachable);

        // THE DIAGNOSIS MOVED, AND THE COST STAYED. It was written on the
        // machine that tried and it is still the only thing anybody can act on
        // - it is in the modal now, whole, beside the cost it used to replace.
        // A watch in trouble showing no cost was the worse half of that trade.
        await Assert.That(sweep.Cost).IsEqualTo("3 of 5 in 24h")
            .Because("the column is a cost, and it says one even when the watch is unwell.");
    }

    [Test]
    public async Task A_watch_with_no_budget_shows_its_cost_and_no_bound()
    {
        var rows = Rows.Sweeps(Board(watches: [AWatch(opened: 7, budgeted: null)]));

        await Assert.That(rows.Single().Cost).IsEqualTo("7 in 24h")
            .Because("unbounded is a state rather than a bound of zero.");
    }

    [Test]
    public async Task A_watch_that_has_never_swept_says_so_rather_than_nothing()
    {
        var rows = Rows.Sweeps(Board(watches:
            [AWatch(executor: null, outcome: null)]));

        await Assert.That(rows.Single().State).IsEqualTo("never swept")
            .Because("a watch applied a minute ago and one whose runner never came both "
                   + "render as an empty column unless somebody decides otherwise.");
    }

    [Test]
    public async Task An_unread_board_says_that_rather_than_showing_an_empty_one()
    {
        // THREE THINGS AN EMPTY PANE MEANS, and two of them are not "nothing
        // was nominated". A person shown the wrong one stops looking.
        //
        // THIS ASSERTED "could not load" AND THAT WAS THE DEFECT, not the
        // wording. On a fresh console nobody had asked for the board at all -
        // the boot fetches neither of its two reads - so the pane was reporting
        // a failure that had not happened, for as long as the countdown took.
        // Arriving now asks; the sentence says what is happening; and the
        // failure, when there is one, is the refresh's own words.
        // ArrivingAtAnUnreadTabAsksForItTests holds all three.
        await Assert.That(PaneText.Board(new AppState { ActiveTab = TabId.Board }))
            .Contains("reading the board");

        await Assert.That(Tabs.HasRead(new AppState
        {
            Board = new BoardPage { Nominations = [], IncludedEnded = true },
        }, TabId.Board)).IsFalse()
            .Because("one read arriving alone is a board that looks complete and is half a "
                   + "story.");
    }

    [Test]
    public async Task Each_table_says_its_own_empty()
    {
        // TWO SENTENCES, ONE PER TABLE. One table needed one sentence for a
        // tenant with neither; now each speaks for what it holds, and a board
        // with watches and no nominations says so above a table of watches.
        var noNominations = Board(nominations: []);

        await Assert.That(PaneText.Board(noNominations)).Contains("nothing has been nominated");
        await Assert.That(PaneText.Sweeps(noNominations)).IsEmpty()
            .Because("the sweeps table has a row, and the table speaks when there is one.");

        await Assert.That(PaneText.Sweeps(Board(watches: [])))
            .Contains("no watch is in force")
            .Because("a tenant watching nothing is waiting for somebody to declare a watch, "
                   + "which is a different next step from waiting for one to find something.");
        await Assert.That(PaneText.Board(Board(watches: []))).IsEmpty();
    }

    [Test]
    public async Task A_filtered_board_says_it_is_filtered_rather_than_empty()
    {
        var somebodyElses = Board([ANomination() with { For = "a-directory:ana-1" }])
            with { BoardShowsEverybody = false, Subject = "a-directory:me-1" };

        await Assert.That(Rows.Nominations(somebodyElses)).IsEmpty();
        await Assert.That(PaneText.Board(somebodyElses)).Contains("`*`")
            .Because("a board of somebody else's rows read as one where nothing had happened "
                   + "would send a person away from rows that are one key from view.");
    }

    [Test]
    public async Task The_tab_is_reachable_by_a_key_that_means_nothing_else()
    {
        var key = Tabs.KeyFor(TabId.Board);

        await Assert.That(key).IsNotNull();
        await Assert.That(Keymap.Resolve(
                key!.Value, new KeymapContext(UiMode.Normal, TabId.Queue)))
            .IsEqualTo(Command.ShowBoardTab)
            .Because("a tab that advertises a key which does nothing is worse than a tab with "
                   + "no key on it.");
    }

    [Test]
    public async Task The_keys_drive_the_nominations_until_v_crosses_to_the_sweeps()
    {
        var state = Board(
            [ANomination(subject: "one"), ANomination(subject: "two")],
            [AWatch(name: "a-watch"), AWatch(name: "b-watch")]);

        await Assert.That(state.BoardTable).IsEqualTo(BoardTable.Nominations);

        var moved = Reducer.Reduce(state, Command.SelectNext);
        moved = Reducer.Reduce(moved, Command.SelectNext);

        await Assert.That(moved.BoardSelected).IsEqualTo(1)
            .Because("two nominations: the cursor stops at the end of its own table rather "
                   + "than running on into the watches.");
        await Assert.That(moved.SweepSelected).IsEqualTo(0);

        await Assert.That(Keymap.Resolve(
                KeyStroke.Char('v'), KeymapContext.For(moved)))
            .IsEqualTo(Command.NextBoardTable);

        var crossed = Reducer.Reduce(moved, Command.NextBoardTable);
        crossed = Reducer.Reduce(crossed, Command.SelectNext);

        await Assert.That(crossed.BoardTable).IsEqualTo(BoardTable.Sweeps);
        await Assert.That(crossed.SweepSelected).IsEqualTo(1);
        await Assert.That(crossed.BoardSelected).IsEqualTo(1)
            .Because("each table keeps its own cursor, so crossing back lands where the "
                   + "person left it.");

        await Assert.That(BoardDetails.WatchUnder(crossed)!.Name).IsEqualTo("b-watch")
            .Because("the row a modal opens is the one under the cursor of the table that has "
                   + "the keys.");
        await Assert.That(Rows.StandingUnder(crossed)).IsNull()
            .Because("a watch has nothing to answer, even with a standing nomination under "
                   + "the other table's cursor.");

        var back = Reducer.Reduce(crossed, Command.NextBoardTable);

        await Assert.That(back.BoardTable).IsEqualTo(BoardTable.Nominations);
        await Assert.That(BoardDetails.NominationUnder(back)!.Subject)
            .IsEqualTo(Rows.Nominations(back)[1].Subject)
            .Because("back on the nominations, the modal is about the row that table's own "
                   + "cursor was left on.");
    }
}
