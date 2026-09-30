using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// A plan opens, and both ways out of it go where they say.
/// </summary>
/// <remarks>
/// <b>The Itineraries tab was the one tab whose <c>enter</c> did nothing.</b>
/// Every other list opens what the cursor is on, and a plan was the row that
/// most needed it: the table cuts each leg's sentence to
/// <see cref="Rows.LegReasonFits"/> columns, and a leg's subject is a hash by
/// construction, so the pane alone cannot say what either leg is.
/// </remarks>
public class APlanOpensAndGoesSomewhereTests
{
    private const string Plan = "itinerary:a2720c15-8575-702e-9300-7fca06fa0cde";

    private static readonly Guid Flew = new("01a0776a-cacb-76dc-b444-2b7031e840d8");

    private const string Whole =
        "This is the core Phase 1 scope the item describes as one user-facing capability: "
      + "Admin (JDX) and Admin (Super) cancel an active workflow.";

    private static FlightSummary AFlight(Guid id, string number) => new()
    {
        FlightId = id.ToString(),
        FlightNumber = number,
        State = "flying",
        Name = "what the plan's leg became",
        Intent = new FlightIntent { Kind = FlightIntentKinds.Text, Text = "phase one" },
        CreatedAt = new DateTimeOffset(2026, 9, 29, 9, 0, 0, TimeSpan.Zero),
        RunnerProtocolVersion = 1,
        FactVocabularyVersion = "0.25.0",
        ConstitutionVersion = "1.0.0",
        EnvelopeVersion = "none",
        Attempts = 1,
        Facts = [],
    };

    private static NominationSummary Leg(
        string reason, Guid? flight = null, string? number = null, int hour = 9) => new()
    {
        NominationId = Guid.NewGuid(),
        Nominator = Plan,
        Subject = "leg:implement@d0a04631313809f9",
        Reason = reason,
        Version = "1",
        WorkKind = "implement",
        Mode = "auto",
        State = NominationStates.Standing,
        ItineraryNumber = "ITN-1",
        IntentKey = "ado#16308",
        FlightId = flight,
        FlightNumber = number,
        MadeAt = new DateTimeOffset(2026, 9, 29, hour, 0, 0, TimeSpan.Zero),
    };

    private static AppState Open(params NominationSummary[] legs) => new()
    {
        Mode = UiMode.ItineraryDetail,
        ActiveTab = TabId.Itineraries,
        Itineraries = new BoardPage { Nominations = legs, IncludedEnded = true },
        ItinerariesSelected = 0,
        ItineraryLegSelected = 0,
        ReaderKeys = ["ado"],
    };

    [Test]
    public async Task Enter_on_a_plan_opens_it_and_on_an_empty_tab_offers_nothing()
    {
        var showing = new AppState
        {
            ActiveTab = TabId.Itineraries,
            Itineraries = new BoardPage { Nominations = [Leg(Whole)], IncludedEnded = true },
        };

        await Assert.That(Keymap.Resolve(KeyStroke.EnterKey, KeymapContext.For(showing)))
            .IsEqualTo(Command.ShowItinerary);

        // AND NOTHING WHERE THERE IS NO PLAN, which is every tenant that has
        // run no `plan` flight. A modal whose only content is that it is empty
        // is a key that punishes pressing it.
        var empty = showing with
        {
            Itineraries = new BoardPage { Nominations = [], IncludedEnded = true },
        };

        await Assert.That(Keymap.Resolve(KeyStroke.EnterKey, KeymapContext.For(empty)))
            .IsNotEqualTo(Command.ShowItinerary);
    }

    [Test]
    public async Task The_modal_carries_the_whole_sentence_the_table_had_to_cut()
    {
        // THE WHOLE REASON THIS MODAL EXISTS. The pane clips at LegReasonFits
        // so the columns after it survive; a modal that repeated the clip
        // would be a wider copy of the thing a person opened it to get past.
        var state = Open(Leg(Whole));

        await Assert.That(Rows.ItineraryLegs(state)[0].Reason).EndsWith("…")
            .Because("the table cuts it, which is the premise of this test.");

        await Assert.That(PaneText.Modal(state)).Contains(Whole)
            .Because("and the modal does not, or it is a bigger box around the same "
                   + "ellipsis.");
    }

    [Test]
    public async Task The_cursor_is_drawn_and_moves_over_the_legs()
    {
        // `f` acts on ONE leg and this modal is prose rather than a table, so
        // a cursor nothing draws asks a person to act on a row the screen
        // never said they were on.
        var state = Open(Leg("the first", hour: 10), Leg("the second", hour: 9));

        // THE KEY, not just the command. The first version of this test called
        // the reducer directly and passed while `j` did nothing in the live
        // console - a command nothing resolves to is a command nobody can
        // reach.
        await Assert.That(Keymap.Resolve(KeyStroke.Char('j'), KeymapContext.For(state)))
            .IsEqualTo(Command.SelectNext);

        var moved = Reducer.Reduce(state, Command.SelectNext);

        await Assert.That(moved.ItineraryLegSelected).IsEqualTo(1);
        await Assert.That(moved.ItinerariesSelected).IsEqualTo(0)
            .Because("moving inside the modal must not change which plan the modal is "
                   + "about - that is the tab's cursor and this is the modal's.");
    }

    [Test]
    public async Task F_goes_to_the_flight_a_leg_became_and_closes_the_modal()
    {
        var state = Open(Leg(Whole, flight: Flew, number: "GG-52")) with
        {
            Flights = new FlightList
            {
                Flights = [AFlight(Flew, "GG-52")],
            },
        };

        await Assert.That(Keymap.Resolve(KeyStroke.Char('f'), KeymapContext.For(state)))
            .IsEqualTo(Command.GoToTheFlight);

        var went = Reducer.Reduce(state, Command.GoToTheFlight);

        await Assert.That(went.ActiveTab).IsEqualTo(TabId.Flights);
        await Assert.That(went.Mode).IsEqualTo(UiMode.Normal)
            .Because("a modal left open over the tab it just sent somebody to would own the "
                   + "keyboard on the screen they were sent to use.");
    }

    [Test]
    public async Task F_is_not_offered_where_the_flight_is_not_loaded()
    {
        // TWO REASONS, AND THE KEY WANTS NEITHER: a standing leg opened into
        // nothing, and the flights tab is PAGED so a leg answered a fortnight
        // ago names a flight this console does not have. Landing the cursor
        // somewhere arbitrary reads as a jump gone wrong.
        var standing = Open(Leg(Whole));

        await Assert.That(Keymap.Resolve(KeyStroke.Char('f'), KeymapContext.For(standing)))
            .IsNotEqualTo(Command.GoToTheFlight);

        var offPage = Open(Leg(Whole, flight: Flew, number: "GG-52"));

        await Assert.That(Keymap.Resolve(KeyStroke.Char('f'), KeymapContext.For(offPage)))
            .IsNotEqualTo(Command.GoToTheFlight);
    }

    [Test]
    public async Task T_opens_the_ticket_the_whole_plan_is_about()
    {
        // FROM THE PLAN, NOT THE LEG. A leg's subject is `leg:{kind}@{digest}`
        // and holds no ticket at all; every leg of a plan is about the one
        // piece of work the planning flight read.
        var state = Open(Leg(Whole));

        await Assert.That(Keymap.Resolve(KeyStroke.Char('t'), KeymapContext.For(state)))
            .IsEqualTo(Command.OpenTheTicket);

        var opened = Reducer.Reduce(state, Command.OpenTheTicket);

        await Assert.That(opened.Mode).IsEqualTo(UiMode.WorkItemDetail);
        await Assert.That(opened.WorkItemId).IsEqualTo("16308")
            .Because("`ado#16308` splits through the rule `gg fly --ticket` already uses, "
                   + "so the console and the command line cannot disagree about it.");
    }

    [Test]
    public async Task T_is_not_offered_where_no_reader_here_can_read_it()
    {
        // An id this console cannot fetch opens a modal that can only say so.
        var state = Open(Leg(Whole)) with { ReaderKeys = [] };

        await Assert.That(Keymap.Resolve(KeyStroke.Char('t'), KeymapContext.For(state)))
            .IsNotEqualTo(Command.OpenTheTicket);
    }

    [Test]
    public async Task The_flight_modals_own_ticket_is_untouched_by_the_shared_flag()
    {
        // THE RISK OF SHARING ONE FLAG ACROSS TWO MODALS, said out loud: the
        // flag is derived per mode, so a plan with a readable ticket must not
        // offer `t` in the flight modal, and vice versa. A flag that answered
        // about whichever happened to be non-null would do exactly that.
        var overAPlan = Open(Leg(Whole)) with { Mode = UiMode.FlightDetail };

        await Assert.That(Keymap.Resolve(KeyStroke.Char('t'), KeymapContext.For(overAPlan)))
            .IsNotEqualTo(Command.OpenTheTicket)
            .Because("the flight modal is open and no flight is selected, so the plan's "
                   + "ticket is not this modal's to offer.");
    }
}
