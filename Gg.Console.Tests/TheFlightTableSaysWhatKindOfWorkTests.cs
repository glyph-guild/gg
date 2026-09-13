using Gg.Console;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// Which kind of work a flight is, in the table where a person chooses what to
/// look at.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every flight has one and no reader could see it.</b> A launch carries a
/// work kind — <c>FlightLaunchRequest.WorkKind</c>, defaulted to
/// <c>implement</c> when a caller names none — and it decides which envelope
/// governs the flight. The control plane has stored it all along
/// (<c>FlightSummaryDto.WorkKind</c>); it simply never reached the wire, so the
/// queue showed a name and a state for a triage and an implement alike.
/// </para>
/// <para>
/// <b>The name is not the kind, which is why the name is not enough.</b> The
/// <c>work</c> column carries what somebody called this flight; the kind is
/// what governs it. Two flights called "fix the import" can be a
/// <c>score-hal</c> and an <c>implement</c> and be bound by entirely different
/// obligations, and the console rendered them identically.
/// </para>
/// <para>
/// <b>Absence is rendered, and never guessed.</b> The control plane defaults a
/// missing kind to <c>implement</c> at CREATION, so every stored flight has
/// one — but a summary from a control plane that predates this member carries
/// none, and a console that filled that gap with <c>implement</c> would be
/// stating as fact something it was never told. The two repositories are not
/// upgraded in step; the reader has to say which it is looking at.
/// </para>
/// </remarks>
public class TheFlightTableSaysWhatKindOfWorkTests
{
    private static FlightSummary AFlight(string number, string? workKind) => new()
    {
        FlightId = "f-" + number,
        FlightNumber = number,
        Name = "fix the import",
        Intent = new FlightIntent { Kind = FlightIntentKinds.Text, Text = "work" },
        CreatedAt = DateTimeOffset.UnixEpoch,
        RunnerProtocolVersion = 1,
        FactVocabularyVersion = "0.30.0",
        ConstitutionVersion = "1.0.0",
        EnvelopeVersion = "v6",
        Attempts = 1,
        State = FlightStates.Landed,
        Facts = [],
        WorkKind = workKind,
    };

    private static AppState Listing(params FlightSummary[] flights) => new()
    {
        ActiveTab = TabId.Flights,
        Flights = new FlightList { Flights = flights },
    };

    [Test]
    public async Task The_kind_is_a_column_of_its_own()
    {
        // NOT FOLDED INTO `work`. That column is the flight's name, which a
        // person wrote; this one is what governs it, which an envelope decides.
        // Joining them into one cell would make a table that cannot be scanned
        // down either.
        await Assert.That(Rows.FlightColumns).Contains("kind");
    }

    [Test]
    public async Task Two_flights_with_one_name_are_told_apart_by_it()
    {
        // THE WHOLE POINT, IN THE SHAPE IT MATTERS. Same name, different
        // obligations, and until now the queue drew them identically.
        var rows = Rows.Flights(Listing(
            AFlight("GG-1", "score-hal"),
            AFlight("GG-2", "implement")));

        await Assert.That(rows.Single(r => r.Number == "GG-1").Kind).IsEqualTo("score-hal");
        await Assert.That(rows.Single(r => r.Number == "GG-2").Kind).IsEqualTo("implement");
    }

    [Test]
    public async Task A_flight_that_never_said_is_not_reported_as_implement()
    {
        // THE GUESS THAT WOULD BE WRONG EXACTLY WHEN IT MATTERED. `implement`
        // is what the control plane defaults an unstated kind to when a flight
        // is CREATED, so filling an absent one in here would be right by
        // coincidence for new flights and a fabrication for every summary from
        // a control plane that does not send this member yet.
        var rows = Rows.Flights(Listing(AFlight("GG-3", workKind: null)));

        await Assert.That(rows[0].Kind).IsNotEqualTo("implement")
            .Because("a console that answered `implement` would be stating as fact something no "
                   + "control plane told it, on the field that decides which envelope governs.");
        await Assert.That(rows[0].Kind).IsNotEmpty()
            .Because("an empty cell reads as a column that failed to load rather than as a fact "
                   + "nobody has.");
    }

    [Test]
    public async Task The_summary_carries_it_at_all()
    {
        // The contract half. A member the control plane cannot send is a column
        // that can only ever render its absence - which is what this console
        // would have shipped without it.
        await Assert.That(AFlight("GG-4", "triage").WorkKind).IsEqualTo("triage");
    }
}
