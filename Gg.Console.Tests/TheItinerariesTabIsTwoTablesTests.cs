using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// The Itineraries tab is a plan on the left and that plan's legs on the right.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two tables, because one grouped list was not what was asked for and did
/// not read as one.</b> The first version put a header row per nominator with
/// its work indented under it. On the live tenant that drew ONE itinerary of
/// three lines above thirty rows of flights a person had opened by hand, in
/// the same weight, under a first column headed <c>plan</c> that was blank on
/// every child row. The ask was "itineraries on the left and their attached
/// nominations indented on the right" - a master and a detail, where choosing
/// on the left changes the right.
/// </para>
/// <para>
/// <b>And only itineraries on the left.</b> The read behind this tab was
/// widened to every nominator while no itinerary had ever been minted, so the
/// tab would show something rather than "no plans yet" at a tenant with thirty
/// nominations. Itineraries mint now, so that workaround is what the clutter
/// is, and a flight somebody opened by hand is already on the flights tab.
/// </para>
/// </remarks>
public class TheItinerariesTabIsTwoTablesTests
{
    private const string One = "itinerary:a2720c15-8575-702e-9300-7fca06fa0cde";
    private const string Two = "itinerary:b1830d26-9686-813f-a411-8fdb17ab1def";

    private static NominationSummary Leg(
        string nominator, string kind, string subject, string? plan = null,
        string? flight = null, string? ending = null, int hour = 9) => new()
    {
        NominationId = Guid.NewGuid(),
        Nominator = nominator,
        // AS THE CONTROL PLANE REALLY SPELLS IT. A leg's subject is a hash,
        // and a fixture that put readable words here would have let the
        // console draw the subject and call the pane finished - which is
        // exactly what the first version of this did.
        Subject = "leg:" + kind + "@d0a04631313809f9",
        Reason = subject,
        Version = "abcdef",
        WorkKind = kind,
        Mode = "auto",
        State = ending ?? NominationStates.Standing,
        Ending = ending,
        FlightNumber = flight,
        ItineraryNumber = plan,
        MadeAt = new DateTimeOffset(2026, 9, 29, hour, 0, 0, TimeSpan.Zero),
    };

    private static AppState Showing(int selected, params NominationSummary[] rows) => new()
    {
        Itineraries = new BoardPage { Nominations = rows, IncludedEnded = true },
        ItinerariesSelected = selected,
    };

    [Test]
    public async Task The_left_table_is_one_row_for_each_plan()
    {
        var state = Showing(0,
            Leg(One, "implement", "16308: phase 1", plan: "ITIN-1"),
            Leg(One, "implement", "16308: history log", plan: "ITIN-1"),
            Leg(Two, "research", "19069: the spike", plan: "ITIN-2", hour: 10));

        var plans = Rows.Itineraries(state);

        await Assert.That(plans.Count).IsEqualTo(2)
            .Because("three legs across two plans is two rows on the left - one per plan, "
                   + "not one per leg, which is what the right-hand table is for.");

        await Assert.That(plans.Select(p => p.Plan)).IsEquivalentTo((string[])["ITIN-2", "ITIN-1"])
            .Because("newest first, and by its number: a person came to this tab for a plan "
                   + "and the number is what names one.");
    }

    [Test]
    public async Task A_plan_says_what_it_is_about_and_how_many_legs_it_has()
    {
        var state = Showing(0,
            Leg(One, "implement", "16308: phase 1", plan: "ITIN-1"),
            Leg(One, "implement", "16308: history log", plan: "ITIN-1"));

        var plan = Rows.Itineraries(state).Single();

        await Assert.That(plan.Legs).IsEqualTo("2")
            .Because("how big the plan is, which is most of what a person scanning the left "
                   + "wants before choosing one.");
    }

    [Test]
    public async Task The_right_table_is_the_legs_of_the_plan_chosen_on_the_left()
    {
        // THE WHOLE POINT OF THE CHANGE. The left drives the right; nothing
        // about the second plan's legs may appear while the first is chosen.
        var state = Showing(0,
            Leg(Two, "research", "19069: the spike", plan: "ITIN-2", hour: 10),
            Leg(One, "implement", "16308: phase 1", plan: "ITIN-1"),
            Leg(One, "implement", "16308: history log", plan: "ITIN-1"));

        var chosen = Rows.Itineraries(state)[0];
        var legs = Rows.ItineraryLegs(state);

        await Assert.That(chosen.Plan).IsEqualTo("ITIN-2");
        await Assert.That(legs.Select(l => l.Reason)).IsEquivalentTo((string[])["19069: the spike"])
            .Because("the right table is driven by the left, so it holds that plan's legs "
                   + "and no other plan's.");
    }

    [Test]
    public async Task Choosing_the_other_plan_changes_the_right_table()
    {
        // The same assertion from the other side, because a detail pane that
        // ignores the cursor passes the test above by accident whenever the
        // chosen plan happens to be first.
        var rows = new[]
        {
            Leg(Two, "research", "19069: the spike", plan: "ITIN-2", hour: 10),
            Leg(One, "implement", "16308: phase 1", plan: "ITIN-1"),
            Leg(One, "implement", "16308: history log", plan: "ITIN-1"),
        };

        var legs = Rows.ItineraryLegs(Showing(1, rows));

        await Assert.That(legs.Select(l => l.Reason))
            .IsEquivalentTo((string[])["16308: phase 1", "16308: history log"])
            .Because("moving the cursor down one plan must redraw the right-hand table, or "
                   + "the two tables are a master and a detail that never met.");
    }

    [Test]
    public async Task A_leg_says_what_it_is_rather_than_only_its_kind()
    {
        // WHAT THE OLD TABLE COULD NOT SAY. Two legs of one plan are both
        // `implement`; a table showing the kind alone showed the same word
        // twice and nothing about which piece of work either was.
        var state = Showing(0,
            Leg(One, "implement", "16308: phase 1", plan: "ITIN-1", flight: "GG-431"),
            Leg(One, "implement", "16308: history log", plan: "ITIN-1"));

        var legs = Rows.ItineraryLegs(state);

        await Assert.That(legs.Select(l => l.Kind).Distinct().Count()).IsEqualTo(1);
        await Assert.That(legs.Select(l => l.Reason).Distinct().Count()).IsEqualTo(2)
            .Because("the nominator's own sentence is the only thing telling two legs of one "
                   + "plan apart - the subject is a hash, and kind, state, flight and age "
                   + "are identical on both.");

        await Assert.That(legs[0].Flight).IsEqualTo("GG-431");
    }

    [Test]
    public async Task Nothing_but_an_itinerary_reaches_the_left_table()
    {
        // THE WORKAROUND COMING BACK OUT. `person:` and `flight:` rows are how
        // the tab came to hold thirty hand-opened flights; they belong to the
        // flights tab and to the board respectively.
        var state = Showing(0,
            Leg(One, "implement", "16308: phase 1", plan: "ITIN-1"),
            Leg("person:Kevin Deenanauth", "plan", "ado#18291", hour: 10),
            Leg("flight:01a0edc2-ecc0-767c-9fc7-9bdae5c48109", "implement", "ado#18291", hour: 11));

        var plans = Rows.Itineraries(state);

        await Assert.That(plans.Count).IsEqualTo(1)
            .Because("the two newer rows are a flight opened by hand and a pass that minted "
                   + "no itinerary; neither is a plan, and both have a tab already.");
        await Assert.That(plans[0].Plan).IsEqualTo("ITIN-1");
    }

    [Test]
    public async Task With_no_plans_both_tables_are_empty_and_neither_throws()
    {
        var state = Showing(0);

        await Assert.That(Rows.Itineraries(state)).IsEmpty();
        await Assert.That(Rows.ItineraryLegs(state)).IsEmpty()
            .Because("the detail of a selection that does not exist is nothing, and a tab "
                   + "with no plans is the ordinary state of a tenant that has run none.");
    }

    [Test]
    public async Task A_cursor_past_the_end_shows_nothing_rather_than_throwing()
    {
        // The console clamps, but a row builder that indexes a stale cursor
        // crashes the whole console rather than one pane - which is how two
        // of the last tab's followers were found.
        var state = Showing(7, Leg(One, "implement", "16308: phase 1", plan: "ITIN-1"));

        await Assert.That(Rows.ItineraryLegs(state)).IsEmpty();
    }
}
