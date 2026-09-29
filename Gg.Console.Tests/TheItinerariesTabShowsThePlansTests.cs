namespace Gg.Console.Tests;

using Gg.Contracts;
using Gg.Contracts.Description;

/// <summary>
/// S53.6-01. The Itineraries tab shows an itinerary by its ITN- number and its
/// legs, and one whose legs have all flown still reads.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE PLAN IS WHAT A PERSON CAME FOR.</b> They approved <c>ITN-7</c> and
/// want to know how it is going - so the number leads, and a leg's own subject
/// is deliberately not shown: it is a digest, an identity rather than a
/// description, which is what keeps somebody's words out of this store.
/// </para>
/// <para>
/// <b>And a finished plan still reads</b>, which is the half that would be
/// skipped. The board hides what is over because its rows are questions; a
/// plan is read to see how it WENT, so every leg stays and the flight it
/// opened is the column a person follows.
/// </para>
/// </remarks>
public class TheItinerariesTabShowsThePlansTests
{
    private static NominationSummary Leg(
        string plan, string kind, string? ending = null, string? flight = null,
        int madeAtHour = 12) => new()
    {
        NominationId = Guid.NewGuid(),
        // ONE NOMINATOR PER PLAN, which is what the stream carries: a leg's
        // nominator IS its itinerary, so legs of one plan share it. This used
        // to mint a fresh guid per leg - harmless while the rows were grouped
        // by number, and wrong the moment anything grouped by nominator.
        Nominator = "itinerary:" + Nominators.GetOrAdd(plan, _ => Guid.NewGuid()),
        Subject = "leg:" + kind + "@abcdef",
        Version = "abcdef",
        WorkKind = kind,
        Mode = "auto",
        State = ending ?? NominationStates.Standing,
        Ending = ending,
        FlightNumber = flight,
        ItineraryNumber = plan,
        MadeAt = new DateTimeOffset(2026, 9, 28, madeAtHour, 0, 0, TimeSpan.Zero),
    };

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Guid>
        Nominators = new(StringComparer.Ordinal);

    private static AppState Showing(params NominationSummary[] legs) => new()
    {
        ActiveTab = TabId.Itineraries,
        Itineraries = new BoardPage { Nominations = [.. legs], IncludedEnded = true },
    };

    [Test]
    public async Task A_plan_is_shown_by_its_number_with_its_legs_under_it()
    {
        var state = Showing(
            Leg(ItineraryRef.Format(7), "research"),
            Leg(ItineraryRef.Format(7), "implement"),
            Leg(ItineraryRef.Format(7), "review"));

        var rows = Rows.Itineraries(state);

        // A HEADER AND ITS THREE LEGS. The number moved onto a header of its
        // own rather than repeating on every line, because repeating it reads
        // as a table of legs rather than as a plan with work under it.
        await Assert.That(rows.Count).IsEqualTo(4);

        await Assert.That(rows[0].Plan).IsEqualTo(ItineraryRef.Format(7))
            .Because("the number is what a person typed and what they are looking for, and "
                   + "the header is where they look for it.");
        await Assert.That(rows[0].Kind).IsEmpty()
            .Because("a plan is not itself a piece of work.");

        await Assert.That(rows.Skip(1).Select(r => r.Kind.Trim())).IsEquivalentTo((string[])
            ["research", "implement", "review"])
            .Because("the kind is what a reader actually reads - a leg's subject is a digest, "
                   + "an identity rather than a description.");
    }

    [Test]
    public async Task Two_plans_are_kept_apart_and_each_ones_legs_stay_together()
    {
        // GROUPED HERE, because nothing on the wire orders them: the rows are
        // nominations read by nominator, and interleaved plans are unreadable.
        var state = Showing(
            Leg(ItineraryRef.Format(7), "research", madeAtHour: 9),
            Leg(ItineraryRef.Format(8), "implement", madeAtHour: 11),
            Leg(ItineraryRef.Format(7), "review", madeAtHour: 10));

        // THE HEADERS, IN ORDER. Each plan's legs sit under its own header
        // rather than carrying the number themselves.
        var plans = Rows.Itineraries(state)
            .Where(r => r.Plan.Length > 0)
            .Select(r => r.Plan)
            .ToList();

        await Assert.That(plans).IsEquivalentTo((string[])
            [ItineraryRef.Format(8), ItineraryRef.Format(7)])
            .Because("the newest plan leads and its legs stay beneath it - a list that "
                   + "interleaved two plans would make a person read the number on every "
                   + "line to tell them apart.");
    }

    [Test]
    public async Task A_plan_whose_legs_have_all_flown_still_reads()
    {
        // THE HALF THAT WOULD BE SKIPPED. The board hides what is over because
        // its rows are questions; a plan is read to see how it WENT.
        var state = Showing(
            Leg(ItineraryRef.Format(7), "research", NominationEndings.Opened, "GG-42"),
            Leg(ItineraryRef.Format(7), "implement", NominationEndings.Opened, "GG-43"));

        var rows = Rows.Itineraries(state);

        await Assert.That(rows.Count).IsEqualTo(3)
            .Because("a header and two flown legs. A finished plan is still a plan, and a "
                   + "surface that emptied when the work started would be one nobody could "
                   + "use to follow it.");

        await Assert.That(rows.Where(r => r.Flight.Length > 0).Select(r => r.Flight))
            .IsEquivalentTo((string[]) ["GG-42", "GG-43"])
            .Because("the flight is what a person follows to see the work, and it is the "
                   + "whole reason this tab is worth opening after the approval.");

        await Assert.That(PaneText.Itineraries(state)).IsEmpty()
            .Because("the table draws the rows; a sentence beside a full table would be one "
                   + "of them contradicting the other.");
    }

    [Test]
    public async Task A_dropped_leg_says_so_rather_than_vanishing()
    {
        // THE SEVENTH ENDING IS READ HERE AND NOWHERE ELSE, because the board
        // excludes these rows. A dropped leg that disappeared would make a
        // person's approved plan quietly shorter than they remember.
        var state = Showing(
            Leg(ItineraryRef.Format(7), "research", NominationEndings.Opened, "GG-42"),
            Leg(ItineraryRef.Format(7), "implement", NominationEndings.Dropped));

        var rows = Rows.Itineraries(state);

        await Assert.That(rows.Select(r => r.State)).Contains(NominationEndings.Dropped);

        await Assert.That(rows.Single(r => r.State == NominationEndings.Dropped).Flight)
            .IsEmpty()
            .Because("a dropped leg opened nothing, and a flight number there would be a "
                   + "claim about work that never started.");
    }

    [Test]
    public async Task A_tenant_with_no_plans_is_told_how_one_comes_to_exist()
    {
        var read = new AppState
        {
            ActiveTab = TabId.Itineraries,
            Itineraries = new BoardPage { Nominations = [], IncludedEnded = true },
        };

        await Assert.That(PaneText.Itineraries(read)).Contains("plan")
            .Because("no plans is the ordinary case rather than a fault - nothing proposes "
                   + "one until a `plan` flight runs - so the pane says how one comes to "
                   + "exist rather than suggesting something is wrong.");

        // AND BEFORE THE READ IT SAYS SOMETHING ELSE, because "nobody asked
        // yet" and "there are none" are different facts and a person acts on
        // them differently.
        await Assert.That(PaneText.Itineraries(new AppState())).IsNotEqualTo(
            PaneText.Itineraries(read));
    }
}
