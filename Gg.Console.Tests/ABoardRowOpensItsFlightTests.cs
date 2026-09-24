using Gg.Console;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// A row that opened into a flight can take you to it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The question a person asks the moment after they answer.</b> Somebody
/// reads a nomination, says `open', and immediately wants to know what it
/// became - and until now the board could not say. The modal named the
/// subject, the nominator and the sentence, and stopped; the flight was on the
/// wire the whole time and the console dropped it.
/// </para>
/// <para>
/// <b>Only on a row that opened into something.</b> <c>FlightId</c> is null on
/// every standing row by definition - a nomination that nobody has answered
/// has opened into nothing - so the key is offered exactly where there is
/// somewhere to go. A key advertised against a row with no flight is a key
/// that does nothing, which is the rule the `take over' binding one file over
/// already states.
/// </para>
/// <para>
/// <b>And only when that flight is in the list.</b> The flights tab is paged,
/// so a nomination answered a fortnight ago names a flight this console has
/// not loaded. Moving the cursor to a row that is not there would leave it
/// somewhere arbitrary and look like the jump went wrong. The modal still
/// prints the number, which is the part a person can act on by typing it.
/// </para>
/// </remarks>
public class ABoardRowOpensItsFlightTests
{
    private static readonly DateTimeOffset At = new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid Opened = Guid.Parse("019fe815-6136-7518-bb57-b06d6d3f411a");

    private static NominationSummary ANomination(Guid? flight, string? number) => new()
    {
        NominationId = Guid.Parse("019fe900-0000-7000-8000-00000000da7a"),
        Nominator = "watch:nightly-triage",
        Subject = "tracker/4242",
        Version = "7",
        WorkKind = "review",
        Mode = "gated",
        State = flight is null ? "standing" : "opened",
        Ending = flight is null ? null : NominationEndings.Opened,
        Because = "names a customer and nobody owns it",
        MadeAt = At,
        FlightId = flight,
        FlightNumber = number,
    };

    private static FlightSummary AFlight(Guid id, string number) => new()
    {
        FlightId = id.ToString(),
        FlightNumber = number,
        State = "flying",
        Name = "what the nomination became",
        Intent = new FlightIntent { Kind = FlightIntentKinds.Text, Text = "review it" },
        CreatedAt = At,
        RunnerProtocolVersion = 1,
        FactVocabularyVersion = "0.25.0",
        ConstitutionVersion = "1.0.0",
        EnvelopeVersion = "none",
        Attempts = 1,
        Facts = [],
    };

    private static AppState Board(
        NominationSummary nomination, IReadOnlyList<FlightSummary>? flights = null) =>
        new()
        {
            ActiveTab = TabId.Board,
            BoardShowsEverybody = true,
            Board = new BoardPage
            {
                IncludedEnded = true,
                Nominations = [nomination],
            },
            Flights = new FlightList { Flights = flights ?? [] },
            BoardSelected = 0,
        };

    private static AppState InTheModal(AppState state) => state with { Mode = UiMode.BoardDetail };

    private static Command? Press(AppState state, char key) =>
        Keymap.Resolve(KeyStroke.Char(key), KeymapContext.For(InTheModal(state)));

    // ---- the modal says which flight ----

    [Test]
    public async Task The_modal_names_the_flight_the_row_opened_into()
    {
        // BY NUMBER AND NOT BY ID. Nothing in this product is named by a guid:
        // GG-42 is what every other surface prints and what a person types.
        var state = Board(ANomination(Opened, "GG-4211"));

        await Assert.That(PaneText.Modal(InTheModal(state))).Contains("GG-4211");
    }

    [Test]
    public async Task A_row_that_opened_into_nothing_names_no_flight()
    {
        // EVERY STANDING ROW, which is most of the board. A field reading
        // "none" would be a line per row saying nothing happened yet, on the
        // screen a person came to to make something happen.
        var state = Board(ANomination(flight: null, number: null));

        await Assert.That(PaneText.Modal(InTheModal(state))).DoesNotContain("GG-");
    }

    [Test]
    public async Task A_flight_nobody_has_numbered_yet_is_still_named()
    {
        // NOT MINTED IS NOT NOTHING. The number arrives from a perspective that
        // lags, so a row answered seconds ago has a flight and no number - and
        // a modal that said nothing would read as "it opened into nothing",
        // which is the one thing it did not do.
        var state = Board(ANomination(Opened, number: null));

        await Assert.That(PaneText.Modal(InTheModal(state))).Contains("not numbered yet");
    }

    // ---- and can take you there ----

    [Test]
    public async Task F_goes_to_the_flight()
    {
        var state = Board(ANomination(Opened, "GG-4211"), [AFlight(Opened, "GG-4211")]);

        await Assert.That(Press(state, 'f')).IsEqualTo(Command.GoToTheFlight);
    }

    [Test]
    public async Task And_is_not_offered_on_a_row_that_opened_into_nothing()
    {
        var state = Board(ANomination(flight: null, number: null));

        await Assert.That(Press(state, 'f')).IsNull();
    }

    [Test]
    public async Task And_is_not_offered_when_the_flight_is_not_on_this_page()
    {
        // THE PAGING CASE, and the reason the key is conditional rather than a
        // command that shrugs. Moving the cursor to a row that is not loaded
        // would leave it somewhere arbitrary and read as a jump gone wrong.
        var state = Board(ANomination(Opened, "GG-4211"), flights: []);

        await Assert.That(Press(state, 'f')).IsNull();
    }

    [Test]
    public async Task Going_to_it_puts_the_cursor_on_that_flight_and_closes_the_modal()
    {
        var other = Guid.Parse("019fe815-0000-7000-8000-00000000beef");

        // NEWEST FIRST is the flights tab's order, so the one wanted is second
        // - which is the whole point of asserting an index rather than zero.
        var state = InTheModal(Board(
            ANomination(Opened, "GG-4211"),
            [AFlight(Opened, "GG-4211"), AFlight(other, "GG-4300") with { CreatedAt = At.AddHours(1) }]));

        var after = Reducer.Reduce(state, Command.GoToTheFlight);

        await Assert.That(after.ActiveTab).IsEqualTo(TabId.Flights);
        await Assert.That(after.Mode).IsEqualTo(UiMode.Normal)
            .Because("a modal that stayed up over the tab it just sent you to would own the "
                   + "keyboard on a screen you were sent to use.");
        await Assert.That(Rows.Flights(after)[after.FlightSelected].FlightId)
            .IsEqualTo(Opened.ToString())
            .Because("the cursor has to land on the flight that was asked for rather than on "
                   + "whichever row happens to be first.");
    }
}
