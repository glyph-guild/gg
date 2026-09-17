using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// The board tab: every nomination, and the watches whose sweeps make them.
/// </summary>
/// <remarks>
/// <para>
/// <b>S39.6-03, and the shape was the owner's call.</b> A watch is not work
/// and has no flight, so <c>QueueRow</c> fits it no better than it fits a
/// standing nomination - which is the excavation S37.4-01 named. A tab of its
/// own holds both without asking the queue's row to mean a third thing.
/// </para>
/// <para>
/// <b>One table, because they are one story.</b> A watch finds an item, the
/// item stands as a nomination, a person opens it. Two stacked tables would be
/// two cursors on one screen, which this console has already met once and
/// wrote down - so the first column says which kind of row this is and the
/// cursor walks both.
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
    public async Task Both_kinds_of_row_are_in_one_table_and_say_which_they_are()
    {
        var rows = Rows.Board(Board());

        await Assert.That(rows.Select(r => r.What))
            .IsEquivalentTo((string[])["nomination", "sweep"])
            .Because("a reader who cannot tell them apart at a glance has a list of two "
                   + "things pretending to be one.");

        var nomination = rows[0];

        await Assert.That(nomination.Subject).Contains("4242");
        await Assert.That(nomination.State).IsEqualTo("auto")
            .Because("a standing row shows its mode, because whether it opens without a "
                   + "person is the thing somebody is deciding whether to answer.");
        await Assert.That(nomination.Kind).IsEqualTo("review");

        var sweep = rows[1];

        await Assert.That(sweep.Subject).IsEqualTo("nightly-triage");
        await Assert.That(sweep.State).IsEqualTo(WatchOutcomes.Swept);
        await Assert.That(sweep.Kind).IsEqualTo(WatchExecutors.Instructions)
            .Because("the executor in force is what would run this watch's next sweep, and "
                   + "it is one of the three facts the criterion names.");
        await Assert.That(sweep.Why).IsEqualTo("3 of 5 in 24h")
            .Because("`3` says nothing and `3 of 5 in 24h` says whether the next nomination "
                   + "will stand.");
    }

    [Test]
    public async Task An_ended_nomination_shows_what_happened_rather_than_its_mode()
    {
        var rows = Rows.Board(Board(
            [ANomination(ending: "refused", because: "'review' is not on this watch's menu")]));

        await Assert.That(rows[0].State).IsEqualTo("refused")
            .Because("a row reading `auto` that has in fact been refused is the one thing "
                   + "this column must never say.");
        await Assert.That(rows[0].Why).Contains("menu");
    }

    [Test]
    public async Task A_watch_that_has_gone_quiet_reads_as_a_state_not_a_timestamp()
    {
        var rows = Rows.Board(Board(watches: [AWatch(quietSince: Noon.AddHours(-5))]));

        await Assert.That(rows.Single(r => r.What == "sweep").State).IsEqualTo("quiet")
            .Because("making a person subtract two clocks to find out nothing is sweeping is "
                   + "how a board comes to look healthy while it is blind - rule 11.");
    }

    [Test]
    public async Task A_watch_that_could_not_sweep_carries_the_runners_own_sentence()
    {
        var rows = Rows.Board(Board(watches:
            [AWatch(outcome: WatchOutcomes.Unreachable,
                    diagnosis: "the tracker refused all 3 of this sweep's reads")]));

        var sweep = rows.Single(r => r.What == "sweep");

        await Assert.That(sweep.State).IsEqualTo(WatchOutcomes.Unreachable);
        await Assert.That(sweep.Why).Contains("refused all 3")
            .Because("the diagnosis was written on the machine that tried, and it is the "
                   + "only thing anybody can act on.");
    }

    [Test]
    public async Task A_watch_with_no_budget_shows_its_cost_and_no_bound()
    {
        var rows = Rows.Board(Board(watches: [AWatch(opened: 7, budgeted: null)]));

        await Assert.That(rows.Single(r => r.What == "sweep").Why).IsEqualTo("7 in 24h")
            .Because("unbounded is a state rather than a bound of zero.");
    }

    [Test]
    public async Task A_watch_that_has_never_swept_says_so_rather_than_nothing()
    {
        var rows = Rows.Board(Board(watches:
            [AWatch(executor: null, outcome: null)]));

        await Assert.That(rows.Single(r => r.What == "sweep").State).IsEqualTo("never swept")
            .Because("a watch applied a minute ago and one whose runner never came both "
                   + "render as an empty column unless somebody decides otherwise.");
    }

    [Test]
    public async Task An_unread_board_says_that_rather_than_showing_an_empty_one()
    {
        // THREE THINGS AN EMPTY PANE MEANS, and two of them are not "nothing
        // was nominated". A person shown the wrong one stops looking.
        await Assert.That(PaneText.Board(new AppState { ActiveTab = TabId.Board }))
            .Contains("could not load");

        await Assert.That(Tabs.HasRead(new AppState
        {
            Board = new BoardPage { Nominations = [], IncludedEnded = true },
        }, TabId.Board)).IsFalse()
            .Because("one read arriving alone is a board that looks complete and is half a "
                   + "story.");
    }

    [Test]
    public async Task A_board_with_watches_and_no_nominations_still_draws_a_table()
    {
        // ONLY ONE EMPTY CASE IS REACHABLE, and the first version of this pane
        // had two sentences for it. A watch in force is always a row - it is on
        // the board whether or not it has found anything - so a tenant whose
        // watches have nominated nothing is looking at a working board rather
        // than an empty one.
        await Assert.That(Rows.Board(Board(nominations: [])).Count).IsEqualTo(1);
        await Assert.That(PaneText.Board(Board(nominations: []))).IsEmpty()
            .Because("the table speaks when there are rows, and there is one.");

        await Assert.That(PaneText.Board(Board(nominations: [], watches: [])))
            .Contains("no watch is in force")
            .Because("a tenant with neither is waiting for somebody to declare a watch, which "
                   + "is a different next step from waiting for one to find something.");
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
    public async Task The_cursor_walks_both_kinds_and_stops_at_the_ends()
    {
        var state = Board(
            [ANomination(subject: "one"), ANomination(subject: "two")],
            [AWatch(name: "a-watch")]);

        var moved = Reducer.Reduce(state, Command.SelectNext);
        moved = Reducer.Reduce(moved, Command.SelectNext);
        moved = Reducer.Reduce(moved, Command.SelectNext);

        await Assert.That(moved.BoardSelected).IsEqualTo(2)
            .Because("three rows, two of them nominations and one a watch: the cursor walks "
                   + "the table a person is looking at rather than either list.");

        var back = Reducer.Reduce(
            Reducer.Reduce(moved, Command.SelectPrevious), Command.SelectPrevious);
        back = Reducer.Reduce(back, Command.SelectPrevious);

        await Assert.That(back.BoardSelected).IsEqualTo(0);
    }
}
