using Gg.Console;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// A board row opens into a modal, and the sentences live there.
/// </summary>
/// <remarks>
/// <para>
/// <b>The table was answering with a clipped cell.</b> A nomination's `why' is
/// the sentence somebody wrote to say what they found, and it was the thing a
/// person answers with - in a column, cut off at whatever width was left. A
/// watch's was its cost, and when the watch was in trouble the cost was
/// replaced by a diagnosis, so the column said neither reliably.
/// </para>
/// <para>
/// <b>One modal for both kinds, because the row is already one record for
/// two.</b> They share a pane and a cursor; two modals would be two cursors on
/// one screen, which this console has met before.
/// </para>
/// <para>
/// <b>And it replaces the decision modal rather than sitting beside it.</b>
/// `open this?' asked over a subject line and showed none of the reasons. The
/// answers now live where the reasons are, and they are offered on exactly the
/// rows that have something to answer.
/// </para>
/// </remarks>
public class ABoardRowOpensIntoAModalTests
{
    private static readonly DateTimeOffset At = new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);

    private const string Sentence =
        "names a customer, nobody owns it, and it has been open for eleven days";

    private static NominationSummary ANomination(string? ending = null) => new()
    {
        NominationId = Guid.Parse("019fe815-6136-7518-bb57-b06d6d3f411a"),
        Nominator = "watch:nightly-triage",
        Subject = "tracker/4242",
        Version = "7",
        WorkKind = "review",
        Mode = "gated",
        State = "standing",
        Because = Sentence,
        MadeAt = At,
        Ending = ending,
        For = "a-directory:dana-4471",
        ForDisplay = "Dana",
    };

    private static WatchStanding AWatch(string? diagnosis = null) => new()
    {
        Name = "nightly-triage",
        Version = "3",
        Window = WatchStanding.DefaultCostWindow,
        Executor = "instructions",
        Outcome = WatchOutcomes.Swept,
        LastHeardAt = At.AddMinutes(-9),
        Opened = 1,
        Budgeted = 5,
        Account = "kdeenanauth",
        Diagnosis = diagnosis,
    };

    private static AppState Board(
        NominationSummary? nomination = null, WatchStanding? watch = null, int selected = 0) =>
        new()
        {
            ActiveTab = TabId.Board,

            // EVERYBODY'S, because this fixture's nomination is for somebody
            // else on purpose - `for' is one of the things the modal must say.
            // Without this the default filter drops the row and every
            // assertion below would be about an empty board.
            BoardShowsEverybody = true,
            Board = new BoardPage
            {
                IncludedEnded = true,
                Nominations = nomination is null ? [] : [nomination],
            },
            Watches = new WatchStandingList { Standings = watch is null ? [] : [watch] },
            BoardSelected = selected,
        };

    private static string Modal(AppState state) =>
        PaneText.Modal(state with { Mode = UiMode.BoardDetail });

    private static Command? Enter(AppState state) =>
        Keymap.Resolve(KeyStroke.EnterKey, KeymapContext.For(state));

    // ---- opening it ----

    [Test]
    public async Task Enter_opens_the_row_under_the_cursor()
    {
        await Assert.That(Enter(Board(nomination: ANomination())))
            .IsEqualTo(Command.ShowBoardRow);
    }

    [Test]
    public async Task Enter_opens_a_sweep_row_too()
    {
        // THE WIDENING THIS EXISTS FOR. A watch's row is machinery with nothing
        // to answer, and a person still needs to read how it is doing - which
        // is the half the old key could not reach at all.
        await Assert.That(Enter(Board(watch: AWatch())))
            .IsEqualTo(Command.ShowBoardRow);
    }

    [Test]
    public async Task Enter_opens_an_ended_nomination_too()
    {
        // A person opening an ended row is asking what became of it and why,
        // which is a read. Answering it again is the thing that is refused.
        await Assert.That(Enter(Board(nomination: ANomination(ending: "declined"))))
            .IsEqualTo(Command.ShowBoardRow);
    }

    [Test]
    public async Task Enter_on_an_empty_board_answers_nothing()
    {
        await Assert.That(Enter(Board())).IsNull()
            .Because("a key advertised where it does nothing is worse than no key.");
    }

    // ---- what it says ----

    [Test]
    public async Task The_modal_says_the_whole_sentence_a_nominator_wrote()
    {
        await Assert.That(Modal(Board(nomination: ANomination()))).Contains(Sentence)
            .Because("this is the reason the modal exists: the cell clipped it, and it is "
                   + "what a person answers with.");
    }

    [Test]
    public async Task The_modal_says_whose_row_it_is_and_who_nominated_it()
    {
        var text = Modal(Board(nomination: ANomination()));

        await Assert.That(text).Contains("Dana");
        await Assert.That(text).Contains("a-directory:dana-4471");
        await Assert.That(text).Contains("watch:nightly-triage");
    }

    [Test]
    public async Task The_modal_says_how_a_watch_is_doing()
    {
        var text = Modal(Board(watch: AWatch()));

        await Assert.That(text).Contains("instructions");
        await Assert.That(text).Contains("1 of 5 in 24h");
        await Assert.That(text).Contains("kdeenanauth")
            .Because("as whom a sweep read is the half a personal watch is about.");
    }

    [Test]
    public async Task A_watch_in_trouble_says_its_diagnosis_and_its_cost()
    {
        // BOTH, which the column could never do: it returned the diagnosis
        // INSTEAD of the cost, so a watch in trouble showed no cost at all -
        // exactly when somebody is asking what it has been doing.
        var text = Modal(Board(watch: AWatch(diagnosis: "has reported nothing since 04:45Z")));

        await Assert.That(text).Contains("has reported nothing since 04:45Z");
        await Assert.That(text).Contains("1 of 5 in 24h");
    }

    // ---- answering from inside it ----

    [Test]
    public async Task A_standing_nomination_can_be_answered_from_inside_it()
    {
        var open = Board(nomination: ANomination()) with { Mode = UiMode.BoardDetail };
        var context = KeymapContext.For(open);

        await Assert.That(Keymap.Resolve(KeyStroke.Char('o'), context))
            .IsEqualTo(Command.OpenNomination);
        await Assert.That(Keymap.Resolve(KeyStroke.Char('d'), context))
            .IsEqualTo(Command.DeclineNomination);
    }

    [Test]
    public async Task A_sweep_row_offers_no_answer_at_all()
    {
        var open = Board(watch: AWatch()) with { Mode = UiMode.BoardDetail };
        var context = KeymapContext.For(open);

        await Assert.That(Keymap.Resolve(KeyStroke.Char('o'), context)).IsNull();
        await Assert.That(Keymap.Resolve(KeyStroke.Char('d'), context)).IsNull()
            .Because("a watch is machinery: there is nothing on it to open or decline.");
    }

    [Test]
    public async Task An_ended_nomination_cannot_be_answered_again()
    {
        var open = Board(nomination: ANomination(ending: "declined"))
            with { Mode = UiMode.BoardDetail };

        await Assert.That(Keymap.Resolve(KeyStroke.Char('o'), KeymapContext.For(open))).IsNull()
            .Because("an ended row is a 409 at the door.");
    }

    // ---- the column it left behind ----

    [Test]
    public async Task The_column_is_the_cost_and_the_sentence_is_not_in_it()
    {
        await Assert.That(Rows.BoardColumns).Contains("cost");
        await Assert.That(Rows.BoardColumns).DoesNotContain("why")
            .Because("what is left in the table is a number; the prose moved.");
    }

    [Test]
    public async Task A_nomination_spends_nothing_so_its_cost_is_blank()
    {
        var row = Rows.Board(Board(nomination: ANomination())).Single();

        await Assert.That(row.Cost).IsEqualTo("");
        await Assert.That(row.Cost).DoesNotContain(Sentence);
    }

    [Test]
    public async Task A_watch_in_trouble_still_shows_its_cost_in_the_table()
    {
        var row = Rows.Board(Board(watch: AWatch(diagnosis: "has reported nothing"))).Single();

        await Assert.That(row.Cost).IsEqualTo("1 of 5 in 24h, as kdeenanauth")
            .Because("the diagnosis used to replace it, which hid the cost exactly when it "
                   + "was being asked for.");
    }
}
