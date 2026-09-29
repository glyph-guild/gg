namespace Gg.Console.Tests;

using Gg.Contracts;

/// <summary>
/// The tab groups by what proposed the work, so it shows something before any
/// itinerary exists.
/// </summary>
/// <remarks>
/// <para>
/// <b>Measured on the live tenant, 2026-09-29: the tab is empty and correctly
/// so.</b> It filtered on <c>ItineraryNumber is { Length: > 0 }</c>, and across
/// thirty nominations not one carries an itinerary — twelve are
/// <c>flight:</c>, seventeen <c>person:</c>, one <c>watch:</c>, and none
/// <c>itinerary:</c>. **No itinerary has ever been minted**: one is created only
/// when a nomination names a `subject`, and no agent sets one (good-grief#619).
/// So every row was discarded and the pane drew nothing.
/// </para>
/// <para>
/// <b>The nominator is the grouping that exists today and survives the fix.</b>
/// A pass and its nominations are already related through it — GG-407 nominated,
/// GG-408 opened — and when itineraries do start minting the nominator simply
/// becomes <c>itinerary:{id}</c>, so the same grouping renders plans with no
/// second implementation.
/// </para>
/// <para>
/// <b>A header row per group, children indented.</b> Repeating the group on
/// every row is what the previous shape did, and it reads as a table of legs
/// rather than as plans with work under them.
/// </para>
/// </remarks>
public class TheItinerariesTabGroupsByNominatorTests
{
    private static NominationSummary From(
        string nominator, string kind, string? flight = null, string? ending = null,
        int madeAtHour = 9) => new()
    {
        NominationId = Guid.NewGuid(),
        Nominator = nominator,
        Subject = "about",
        Version = "abcdef",
        WorkKind = kind,
        Mode = "auto",
        State = ending ?? NominationStates.Standing,
        Ending = ending,
        FlightNumber = flight,
        ItineraryNumber = null,
        MadeAt = new DateTimeOffset(2026, 9, 29, madeAtHour, 0, 0, TimeSpan.Zero),
    };

    private static AppState Showing(params NominationSummary[] rows) => new()
    {
        ActiveTab = TabId.Itineraries,
        Itineraries = new BoardPage { Nominations = [.. rows], IncludedEnded = true },
    };

    [Test]
    public async Task A_pass_that_nominated_is_shown_with_its_work_under_it()
    {
        var pass = "flight:01a0ebd7-29b2-75c9-b691-84a8547c039d";

        var rows = Rows.Itineraries(Showing(
            From(pass, "implement", flight: "GG-408", ending: NominationEndings.Opened)));

        await Assert.That(rows.Count).IsEqualTo(2)
            .Because("a header for what proposed the work and one row under it for the work - "
                   + "which is the shape a person asked for and the shape the old one could "
                   + "never draw, because it required an itinerary that has never existed.");

        await Assert.That(rows[0].Kind).IsEmpty()
            .Because("the header names the nominator and nothing else; a kind on it would "
                   + "claim the pass is itself a piece of work.");
        await Assert.That(rows[1].Kind).Contains("implement");
        await Assert.That(rows[1].Flight).IsEqualTo("GG-408");
    }

    [Test]
    public async Task A_nomination_with_no_itinerary_is_still_shown()
    {
        // THE WHOLE POINT. Every row on the live tenant is in this state, and
        // the old filter dropped all of them.
        var rows = Rows.Itineraries(Showing(
            From("watch:jdx-triage", "review", flight: "GG-362",
                 ending: NominationEndings.Opened)));

        await Assert.That(rows.Count).IsGreaterThan(0)
            .Because("thirty nominations exist and none carries an itinerary number, so a tab "
                   + "that requires one shows a tenant nothing it has done.");
    }

    [Test]
    public async Task Two_passes_are_two_groups_and_do_not_interleave()
    {
        var one = "flight:" + Guid.NewGuid();
        var two = "flight:" + Guid.NewGuid();

        var rows = Rows.Itineraries(Showing(
            From(one, "implement", madeAtHour: 9),
            From(two, "review", madeAtHour: 10),
            From(one, "score-hal", madeAtHour: 9)));

        await Assert.That(rows.Count).IsEqualTo(5)
            .Because("two headers and three pieces of work.");

        // EACH GROUP WHOLE. A person reading a plan reads it in one place, and
        // interleaved rows would make two plans look like one.
        var headers = rows.Select((r, i) => (r, i))
            .Where(x => x.r.Kind.Length == 0)
            .Select(x => x.i)
            .ToList();

        await Assert.That(headers.Count).IsEqualTo(2);
        await Assert.That(headers[1] - headers[0]).IsGreaterThan(1)
            .Because("a header is followed by its own work before the next header starts.");
    }
}
