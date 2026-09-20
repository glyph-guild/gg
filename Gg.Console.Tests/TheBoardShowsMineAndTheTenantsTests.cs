using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// The board shows the tenant's rows and this person's own, and everybody's
/// only when asked.
/// </summary>
/// <remarks>
/// <para>
/// <b>S42.5-02; slice forty-two rules 13 and 14.</b> ADR-0024 puts every
/// personal watch's rows on the one board. That is what makes them readable -
/// and what makes a busy tenant's board mostly other people's rows, none of
/// which this person may answer. The default view is what they can act on plus
/// what is everybody's; the rest is a keystroke away.
/// </para>
/// <para>
/// <b><c>*</c>, because the letters are gone.</b> Every letter is a tab key or
/// spoken for by the compose modal, and <c>p</c> may never be a tab key. <c>*</c>
/// is unbound in every mode and reads as "all" in a list.
/// </para>
/// </remarks>
public class TheBoardShowsMineAndTheTenantsTests
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    private const string Mine = "fake:01a0bb8c-2794-765e-a25e-eb32cfc4c875";
    private const string Theirs = "fake:01a0bb8c-2766-72ff-b31d-b18f00ac7323";

    private static NominationSummary ARow(string subject, string? forWhom, string? display) => new()
    {
        NominationId = Guid.NewGuid(),
        Nominator = "watch:a-queue",
        Subject = subject,
        Version = "7",
        WorkKind = "implement",
        Mode = "gated",
        State = "standing",
        MadeAt = Noon.AddMinutes(-5),
        For = forWhom,
        ForDisplay = display,
    };

    private static AppState Board(bool everybody = false) => new()
    {
        ActiveTab = TabId.Board,
        Subject = Mine,
        BoardShowsEverybody = everybody,
        Board = new BoardPage
        {
            Nominations =
            [
                ARow("work-item:tenant-one", null, null),
                ARow("work-item:mine-one", Mine, "Kevin"),
                ARow("work-item:theirs-one", Theirs, "Ana"),
            ],
            IncludedEnded = false,
        },
        Watches = new WatchStandingList { Standings = [] },
    };

    [Test]
    public async Task Somebody_elses_row_is_not_shown_by_default()
    {
        var rows = Rows.Board(Board());

        await Assert.That(rows.Select(r => r.Subject))
            .IsEquivalentTo((string[])["work-item:tenant-one", "work-item:mine-one"])
            .Because("a board that is mostly rows this person cannot answer buries the ones "
                   + "they can - and only the row's person may decide it.");
    }

    [Test]
    public async Task Everybodys_rows_are_shown_on_request()
    {
        var rows = Rows.Board(Board(everybody: true));

        await Assert.That(rows.Count).IsEqualTo(3)
            .Because("one board: what is hidden by default is still there to be asked for.");
    }

    [Test]
    public async Task A_row_says_whose_it_is_and_a_tenant_row_says_nothing()
    {
        var rows = Rows.Board(Board(everybody: true));

        await Assert.That(rows.Single(r => r.Subject.EndsWith("mine-one", StringComparison.Ordinal)).For)
            .IsEqualTo("Kevin")
            .Because("the display is what a reader recognises; the subject is in gg board, "
                   + "where there is room for both.");
        await Assert.That(rows.Single(r => r.Subject.EndsWith("theirs-one", StringComparison.Ordinal)).For)
            .IsEqualTo("Ana");
        await Assert.That(rows.Single(r => r.Subject.EndsWith("tenant-one", StringComparison.Ordinal)).For)
            .IsEqualTo("")
            .Because("a tenant row belongs to nobody and says so by saying nothing.");
    }

    [Test]
    public async Task A_person_who_has_left_reads_as_their_subject()
    {
        var state = Board(everybody: true) with
        {
            Board = new BoardPage
            {
                Nominations = [ARow("work-item:gone", Theirs, display: null)],
                IncludedEnded = false,
            },
        };

        await Assert.That(Rows.Board(state).Single().For).IsEqualTo(Theirs)
            .Because("a display is dropped when somebody leaves the tenant, and a blank "
                   + "there would read as a tenant row - which is the one thing it is not.");
    }

    [Test]
    public async Task The_board_has_a_key_for_everybodys_rows_and_it_shadows_nothing()
    {
        await Assert.That(Keymap.Resolve(
            KeyStroke.Char('*'), new KeymapContext(UiMode.Normal, TabId.Board)))
            .IsEqualTo(Command.ShowEverybodysRows);

        foreach (var tab in Enum.GetValues<TabId>())
        {
            if (tab == TabId.Board)
            {
                continue;
            }

            await Assert.That(Keymap.Resolve(
                KeyStroke.Char('*'), new KeymapContext(UiMode.Normal, tab)))
                .IsNull()
                .Because($"'*' must not mean anything on {tab} that it did not mean before.");
        }
    }

    [Test]
    public async Task The_key_turns_the_view_both_ways()
    {
        var widened = Reducer.Reduce(Board(), Command.ShowEverybodysRows);
        await Assert.That(widened.BoardShowsEverybody).IsTrue();

        var narrowed = Reducer.Reduce(widened, Command.ShowEverybodysRows);
        await Assert.That(narrowed.BoardShowsEverybody).IsFalse()
            .Because("a view a person can widen and not narrow is a door with no handle "
                   + "on the inside.");
    }
}
