using Gg.Client;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// Reaching the last row asks for the next page, and the rows that arrive are
/// added to the ones already there.
/// </summary>
/// <remarks>
/// <para>
/// <b>Asked for 2026-09-20: infinite scroll.</b> Neither list had a cap, so
/// both grew with a tenant's history for ever and the console re-fetched all of
/// it every thirty seconds. The control plane answers a page and a cursor as of
/// contract 0.212.0, and this is the half that goes on asking.
/// </para>
/// <para>
/// <b>The decision is a reducer step; the read is not.</b> A UI session may not
/// start anything, so what is here is pure - it says which read the cursor's
/// position calls for - and the screen hands that to <c>BackgroundReads</c>,
/// which is the seam a keypress already uses. <c>SelectionMovesWithoutIoTests</c>
/// holds the other half: an arrow key is a reducer step and nothing else.
/// </para>
/// <para>
/// <b>One at a time.</b> A cursor resting on the last row must not ask again
/// while the answer is on its way, or reaching the end becomes a request per
/// frame.
/// </para>
/// <para>
/// <b>A refresh asks for what is on screen.</b> Thirty seconds after somebody
/// scrolled three pages in, a refresh that asked for one page would take away
/// what they were reading.
/// </para>
/// </remarks>
public class TheTableLoadsMoreAtTheEndTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);

    private static FlightSummary AFlight(int n) => new()
    {
        FlightId = AConsolePlane.Id(n),
        FlightNumber = Gg.Contracts.Description.FlightRef.Format(n),
        Name = $"work {n}",
        Intent = new FlightIntent { Kind = FlightIntentKinds.Text, Text = "work" },
        CreatedAt = T0.AddMinutes(n),
        RunnerProtocolVersion = 1,
        FactVocabularyVersion = "0.32.0",
        ConstitutionVersion = "1.0.0",
        EnvelopeVersion = "v12",
        Attempts = 1,
        State = FlightStates.Landed,
        Facts = [],
    };

    private static AppState AtTheEndOfFlights(string? next, bool reading = false) => new()
    {
        ActiveTab = TabId.Flights,
        Flights = new FlightList { Flights = [AFlight(1), AFlight(2)], Next = next },

        // THE LAST ROW AS SHOWN, which is newest first - so row one is flight
        // one, the older of the two.
        FlightSelected = 1,
        ReadInFlight = reading,
    };

    private static AppState AtTheEndOfTheBoard(string? next, int watches = 0) => new()
    {
        ActiveTab = TabId.Board,
        Board = new BoardPage
        {
            Nominations = [AConsolePlane.ANomination(1), AConsolePlane.ANomination(2)],
            IncludedEnded = true,
            Next = next,
        },
        Watches = new WatchStandingList
        {
            Standings = [.. Enumerable.Range(1, watches).Select(AConsolePlane.AStanding)],
        },
        BoardSelected = 1,
        BoardShowsEverybody = true,
    };

    [Test]
    public async Task The_last_row_of_a_paged_list_asks_for_the_next_page()
    {
        var wanted = Reducer.WantsMore(AtTheEndOfFlights("R0ctMQ"));

        await Assert.That(wanted).IsEqualTo(Command.LoadMoreFlights);
    }

    [Test]
    public async Task A_row_short_of_the_end_asks_for_nothing()
    {
        var earlier = AtTheEndOfFlights("R0ctMQ") with { FlightSelected = 0 };

        await Assert.That(Reducer.WantsMore(earlier)).IsNull()
            .Because("more is fetched when somebody reaches the end of what they have, not "
                   + "while they are reading the middle of it.");
    }

    [Test]
    public async Task A_list_that_is_all_of_them_asks_for_nothing()
    {
        await Assert.That(Reducer.WantsMore(AtTheEndOfFlights(next: null))).IsNull()
            .Because("no cursor means that was all of them, and asking anyway would fetch the "
                   + "first page again for ever.");
    }

    [Test]
    public async Task Nothing_is_asked_while_an_answer_is_on_its_way()
    {
        await Assert.That(Reducer.WantsMore(AtTheEndOfFlights("R0ctMQ", reading: true))).IsNull()
            .Because("a cursor resting on the last row would otherwise ask once per frame.");
    }

    [Test]
    public async Task A_cursor_inside_a_modal_asks_for_nothing()
    {
        var reading = AtTheEndOfFlights("R0ctMQ") with { Mode = UiMode.FlightDetail };

        await Assert.That(Reducer.WantsMore(reading)).IsNull()
            .Because("the row a person is moving through inside a modal is the modal's list, "
                   + "which is the same reason Pointed answers the modal before the tab.");
    }

    [Test]
    public async Task A_tab_that_holds_no_page_asks_for_nothing()
    {
        var queue = AtTheEndOfFlights("R0ctMQ") with { ActiveTab = TabId.Queue };

        await Assert.That(Reducer.WantsMore(queue)).IsNull()
            .Because("the queue is derived from the flights read rather than fetched, so there "
                   + "is no next page of it to ask anybody for.");
    }

    [Test]
    public async Task The_board_asks_for_its_own_next_page()
    {
        await Assert.That(Reducer.WantsMore(AtTheEndOfTheBoard("MTIzOmFiYw")))
            .IsEqualTo(Command.LoadMoreBoard);
    }

    [Test]
    public async Task The_board_reaches_its_end_at_the_last_nomination()
    {
        var watched = AtTheEndOfTheBoard("MTIzOmFiYw", watches: 3);

        await Assert.That(Rows.Board(watched).Count).IsEqualTo(5)
            .Because("the watches are rows on the same table, underneath the nominations.");
        await Assert.That(Reducer.WantsMore(watched)).IsEqualTo(Command.LoadMoreBoard)
            .Because("the page the cursor is walking is the nominations, and the watches sit "
                   + "below them - so somebody who reaches the last nomination has seen "
                   + "everything this page brought, and three rows of machinery under it are "
                   + "not more of it.");
    }

    // ---- what arrives is added to what is there ----

    [Test]
    public async Task The_next_page_is_added_to_the_rows_already_held()
    {
        // MORE THAN TWO PAGES' WORTH, so the second page is a page and not
        // whatever is left: what the reader asks for is the size both sides
        // agree on, never the size of the page it is continuing.
        var (data, _) = AConsolePlane.Console(flights: 250);

        var first = (VerbResult.Flights)await data.ListAsync(limit: 2);
        var state = new AppState
        {
            ActiveTab = TabId.Flights,
            Flights = first.Value,
            FlightSelected = 1,
        };

        await Assert.That(state.Flights!.Flights.Count).IsEqualTo(2);
        await Assert.That(state.Flights.Next).IsNotNull();

        var folded = ConsoleMore.FlightsPatch(data, state)(state);
        var expected = 2 + Paging.DefaultLimit;

        await Assert.That(folded.Flights!.Flights.Count).IsEqualTo(expected)
            .Because("a page is ADDED to what is there; replacing would make reaching the end "
                   + "forget everything above it.");
        await Assert.That(folded.Flights.Flights.Select(f => f.FlightId).Distinct().Count())
            .IsEqualTo(expected)
            .Because("and no row twice: the cursor names where the last page stopped, so the "
                   + "next one starts after it.");
        await Assert.That(folded.Flights.Next).IsNotNull();
        await Assert.That(folded.Flights.Next).IsNotEqualTo(state.Flights.Next)
            .Because("the cursor moves with the rows, or the next ask fetches this page again.");
    }

    [Test]
    public async Task The_last_page_leaves_no_cursor_behind()
    {
        var (data, _) = AConsolePlane.Console(flights: 3);

        var first = (VerbResult.Flights)await data.ListAsync(limit: 2);
        var state = new AppState { ActiveTab = TabId.Flights, Flights = first.Value };

        var folded = ConsoleMore.FlightsPatch(data, state)(state);

        await Assert.That(folded.Flights!.Flights.Count).IsEqualTo(3);
        await Assert.That(folded.Flights.Next).IsNull();
        await Assert.That(Reducer.WantsMore(folded with { FlightSelected = 2 })).IsNull()
            .Because("and reaching the end of the last page stops asking.");
    }

    [Test]
    public async Task A_page_the_cursor_has_no_room_for_keeps_the_cursor_where_it_is()
    {
        var (data, _) = AConsolePlane.Console(flights: 5);

        var first = (VerbResult.Flights)await data.ListAsync(limit: 2);
        var state = new AppState
        {
            ActiveTab = TabId.Flights,
            Flights = first.Value,
            FlightSelected = 1,
        };

        var folded = ConsoleMore.FlightsPatch(data, state)(state);

        await Assert.That(PaneText.Detailed(folded)!.FlightId)
            .IsEqualTo(PaneText.Detailed(state)!.FlightId)
            .Because("the rows arrive BELOW the cursor, and a person who scrolled to the "
                   + "bottom to fetch them must still be looking at the row they scrolled to.");
    }

    [Test]
    public async Task The_board_adds_its_next_page_the_same_way()
    {
        var (data, _) = AConsolePlane.Console(nominations: 150);

        var first = (VerbResult.Board)await data.BoardAsync(ended: true, limit: 2);
        var state = new AppState
        {
            ActiveTab = TabId.Board,
            Board = first.Value,
            BoardSelected = 1,
        };

        await Assert.That(state.Board!.Nominations.Count).IsEqualTo(2);

        var folded = ConsoleMore.BoardPatch(data, state)(state);

        await Assert.That(folded.Board!.Nominations.Count).IsEqualTo(2 + Paging.DefaultLimit);
        await Assert.That(folded.Board.Nominations.Select(n => n.NominationId).Distinct().Count())
            .IsEqualTo(2 + Paging.DefaultLimit);
        await Assert.That(folded.BoardSelected).IsEqualTo(1)
            .Because("the rows arrive below the cursor here too.");
        await Assert.That(folded.Board.IncludedEnded).IsTrue()
            .Because("the next page answers the same question as the first, and a page that "
                   + "quietly dropped the ended rows would be a shorter board halfway down.");
    }

    // ---- a refresh keeps what is on screen ----

    [Test]
    public async Task A_refresh_asks_for_as_many_rows_as_are_on_screen()
    {
        var (data, plane) = AConsolePlane.Console(flights: 400);

        var state = new AppState
        {
            ActiveTab = TabId.Flights,
            Flights = new FlightList
            {
                Flights = [.. Enumerable.Range(1, 250).Select(AFlight)],
                Next = "R0ctMQ",
            },
        };

        _ = await ConsoleRefresh.ForTabAsync(data, TabId.Flights, state);

        await Assert.That(plane.Limits).Contains(250)
            .Because("two hundred and fifty rows were scrolled to, and a refresh that asked "
                   + "for a hundred would take away the hundred and fifty below them.");
    }

    [Test]
    public async Task A_refresh_never_asks_for_less_than_a_page()
    {
        var (data, plane) = AConsolePlane.Console(flights: 400);

        var state = new AppState
        {
            ActiveTab = TabId.Flights,
            Flights = new FlightList { Flights = [AFlight(1)], Next = "R0ctMQ" },
        };

        _ = await ConsoleRefresh.ForTabAsync(data, TabId.Flights, state);

        await Assert.That(plane.Limits).Contains(Paging.DefaultLimit)
            .Because("asking for exactly what is held would pin a short list at its length, "
                   + "and the rows created since would need a scroll to reach.");
    }

    [Test]
    public async Task A_first_read_asks_for_the_page_the_contract_declares()
    {
        var (data, plane) = AConsolePlane.Console(flights: 400);

        _ = await ConsoleRefresh.ForTabAsync(data, TabId.Flights, new AppState());

        await Assert.That(plane.Limits).Contains(Paging.DefaultLimit)
            .Because("nothing is on screen yet, so the page is the one both sides agree on.");
    }
}
