using Gg.Client;
using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Console.Tests;

/// <summary>
/// Why the selected flight is stopped, in the pane that shows it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The verb whose entire job is the question this console exists to ask.</b>
/// <c>gg why</c> answers <i>why is this flight stopped</i>; the queue's rows are
/// flights needing a person; and the wrapper on <c>ConsoleData</c> had no caller
/// anywhere, so a person looking at a stuck flight could read its facts, its log
/// and its credential and not the one answer that says what is holding it.
/// </para>
/// <para>
/// <b>Held for the SELECTED row and no other</b>, which is the shape
/// <c>TakeSeed</c> already has and the one rule 3 leaves available: a fetch per
/// queue row would be a request per row on every load, and a fetch when the
/// cursor moves would be I/O inside a UI session. So moving the cursor drops it,
/// and the pane says which nothing it is showing rather than going blank.
/// </para>
/// </remarks>
public class WhyReachesTheFlightPaneTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 5, 9, 0, 0, TimeSpan.Zero);

    private static FlightAttribution Stopped(string number) => new()
    {
        FlightNumber = number,
        EnvelopeVersion = "3",
        Obligations =
        [
            new ObligationAttribution
            {
                ObligationId = "widen-root",
                Attachment = "attached",
                Condition = "envelope widens",
                Because = "the proposal adds a repository the envelope does not name",
                Outcome = "waiting",
            },
        ],
        Halt = "waiting on platform-owner",
    };

    [Test]
    public async Task The_projection_puts_the_attribution_where_the_pane_reads_it()
    {
        var state = ConsoleProjection.Apply(
            new AppState(), new VerbResult.Why(Stopped(FlightRef.Format(42))));

        await Assert.That(state.Attribution).IsNotNull();
        await Assert.That(state.Attribution!.Halt).IsEqualTo("waiting on platform-owner");
    }

    [Test]
    public async Task The_flight_pane_says_what_is_holding_it()
    {
        var state = Selected() with { Attribution = Stopped(FlightRef.Format(1)) };

        var pane = PaneText.Flight(state);

        await Assert.That(pane).Contains("waiting on platform-owner")
            .Because("the halt is the sentence a person opened this pane to read.");
        await Assert.That(pane).Contains("widen-root")
            .Because("and which obligation is holding it, so the answer is actionable "
                   + "rather than a mood.");
        await Assert.That(pane).Contains("the proposal adds a repository")
            .Because("the reason is the control plane's own words - rendered, never "
                   + "computed, because a client that worked out for itself why an "
                   + "obligation attached would explain a verdict it did not produce.");
    }

    [Test]
    public async Task A_row_it_was_not_read_for_says_so_rather_than_going_blank()
    {
        // Rule 5. `not loaded for this row` and `nothing is holding this flight`
        // are opposite facts and an empty section is both of them.
        var pane = PaneText.Flight(Selected() with { Attribution = null });

        await Assert.That(pane).Contains("why")
            .Because("the section is present whether or not it was read.");
        await Assert.That(pane).Contains("not read for this row")
            .Because("a person has to be able to tell an unread row from a flight with "
                   + "nothing holding it, and the second one is good news.");
    }

    [Test]
    public async Task Moving_the_cursor_drops_an_answer_that_was_about_another_flight()
    {
        // The rule Detail already holds for the summary and the log: one
        // flight's detail under another flight's name is the worst of the three
        // answers, because it is the one a person cannot see is wrong. An
        // attribution is the most dangerous of them - it names a halt.
        var moved = Reducer.Detail(
            Selected() with { SelectedRow = 1, Attribution = Stopped(FlightRef.Format(1)) });

        await Assert.That(moved.Attribution).IsNull()
            .Because("it was read for the row the cursor has left.");
    }

    private static AppState Selected()
    {
        var rows = new[] { "a", "b" }.Select((id, at) => new QueueRow
        {
            FlightId = id,
            FlightNumber = FlightRef.Format(at + 1),
            Name = $"flight {id}",
            Reason = QueueReason.AwaitingDecision,
            Since = T0,
        }).ToList();

        return new AppState
        {
            Queue = rows,
            SelectedRow = 0,
            Flights = new FlightList { Flights = [.. rows.Select(Summary)] },
            Flight = Summary(rows[0]),
        };
    }

    private static FlightSummary Summary(QueueRow row) => new()
    {
        FlightId = row.FlightId,
        FlightNumber = row.FlightNumber,
        Name = row.Name,
        Intent = new FlightIntent { Kind = FlightIntentKinds.Text, Text = "why" },
        CreatedAt = T0,
        RunnerProtocolVersion = 1,
        FactVocabularyVersion = "0.1.0",
        ConstitutionVersion = "1.0.0",
        EnvelopeVersion = "3",
        Attempts = 1,
        Facts = [],
    };
}
