using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// Every flight this tenant has recently is on a surface, whether or not it
/// needs anybody.
/// </summary>
/// <remarks>
/// <para>
/// <b>MEASURED ON A REAL FLIGHT THAT VANISHED.</b> GG-52 was opened against the
/// live control plane to create a python script. A runner claimed it, the agent
/// tried to write the file, and the envelope's <c>loops.implement.moves</c> did
/// not declare <c>write</c> - so it was refused every time, asked a person
/// through <c>mcp__gg__ask_for_decision</c>, and stopped with
/// <c>loop.outcome: blocked</c>. Seven facts reached the control plane
/// including the question.
/// </para>
/// <para>
/// <b>And the console showed nothing.</b> The queue is flights NEEDING ME and
/// its three reasons are a waiting gate, two expired leases and a stranded
/// runner; this flight had none. The tenant's envelope declares no obligation
/// conditioned on <c>loop asked for a decision</c>, so no gate opened, the
/// machine obligation was satisfied - every path a flight touched is in scope
/// when it touched none - and the flight LANDED. A person looking at the
/// console was told <i>nothing needs you</i>, which was true and useless.
/// </para>
/// <para>
/// <b>The list was already in the model.</b> <c>AppState.Flights</c> holds every
/// flight the boot fetched - it is what makes an arrow key free - and nothing
/// rendered it. This is a pane over a read that already happened.
/// </para>
/// </remarks>
public class EveryRecentFlightIsVisibleTests
{
    private static FlightSummary AFlight(
        string number, string state, DateTimeOffset created, string? outcome = null) => new()
    {
        FlightId = $"01a0776a-cacb-76dc-b444-2b70318{number}",
        FlightNumber = number,
        Name = $"work for {number}",
        Intent = new FlightIntent { Kind = FlightIntentKinds.Text, Text = $"work for {number}" },
        CreatedAt = created,
        RunnerProtocolVersion = 1,
        FactVocabularyVersion = "0.25.0",
        ConstitutionVersion = "1.0.0",
        EnvelopeVersion = "v6",
        Attempts = 1,
        State = state,
        Facts = outcome is null
            ? []
            : [new FactEnvelope
              {
                  IdempotencyKey = $"{number}:loop.outcome",
                  Kind = FactKinds.LoopOutcome,
                  Digest = new string('c', 64),
                  ObservedAt = created,
                  Loop = new LoopOutcome
                  {
                      LoopId = "implement",
                      Outcome = outcome,
                      Reason = "I stopped without making changes.",
                      Executor = ExecutorRungs.Frontier,
                      Attempts = 10,
                      DurationMs = 62317,
                      MovesUsed = [LoopMoves.Read, LoopMoves.Write],
                  },
              }],
    };

    private static AppState Booted() => new()
    {
        Queue = [],
        Flights = new FlightList
        {
            Flights =
            [
                AFlight("GG-50", FlightStates.Landed, new DateTimeOffset(2026, 9, 6, 4, 28, 0, TimeSpan.Zero)),
                AFlight("GG-52", FlightStates.Landed, new DateTimeOffset(2026, 9, 6, 15, 51, 0, TimeSpan.Zero), LoopOutcomes.Blocked),
                AFlight("GG-51", FlightStates.Open, new DateTimeOffset(2026, 9, 6, 4, 29, 0, TimeSpan.Zero)),
            ],
        },
    };

    [Test]
    public async Task A_flight_that_needs_nobody_is_still_somewhere()
    {
        // THE DEFECT, both halves in one test. The queue is right to say
        // nothing needs you; the console was wrong to leave it at that.
        var state = Booted();

        await Assert.That(string.Join("\n", PaneText.QueueRows(state))).Contains("nothing needs you")
            .Because("no gate, no expired lease, no stranded runner - the queue is honest.");

        await Assert.That(PaneText.Flights(state)).Contains("GG-52", StringComparison.Ordinal)
            .Because("and it still ran, and a person still has to be able to find it.");
    }

    [Test]
    public async Task Every_flight_the_boot_fetched_is_on_the_pane()
    {
        var pane = PaneText.Flights(Booted());

        foreach (var number in (string[])["GG-50", "GG-51", "GG-52"])
        {
            await Assert.That(pane).Contains(number, StringComparison.Ordinal)
                .Because($"{number} is in AppState.Flights and a pane that shows a subset of "
                       + "what was fetched is a pane that decides for a person. Pane:\n" + pane);
        }
    }

    [Test]
    public async Task Newest_first_because_recent_is_the_question()
    {
        var pane = PaneText.Flights(Booted());

        var rows = pane.Split('\n').Where(r => r.Contains("GG-", StringComparison.Ordinal)).ToList();

        await Assert.That(rows[0]).Contains("GG-52", StringComparison.Ordinal)
            .Because("a person opens this after doing something. Rows:\n" + string.Join("\n", rows));
        await Assert.That(rows[^1]).Contains("GG-50", StringComparison.Ordinal);
    }

    [Test]
    public async Task How_a_loop_ended_is_on_the_row_when_a_fact_says_so()
    {
        // WHAT WOULD HAVE ANSWERED THE QUESTION AT A GLANCE. GG-52's state is
        // `landed` and its loop was `blocked`: the flight reached an ending and
        // the work did not. A row carrying only the state reads as a success.
        var row = PaneText.Flights(Booted()).Split('\n')
            .Single(r => r.Contains("GG-52", StringComparison.Ordinal));

        await Assert.That(row).Contains(FlightStates.Landed, StringComparison.Ordinal);
        await Assert.That(row).Contains(LoopOutcomes.Blocked, StringComparison.Ordinal)
            .Because("landed and blocked are both true of this flight, and the second is the "
                   + "one somebody has to act on. Row: " + row);
    }

    [Test]
    public async Task A_flight_whose_loop_said_nothing_claims_nothing()
    {
        var row = PaneText.Flights(Booted()).Split('\n')
            .Single(r => r.Contains("GG-51", StringComparison.Ordinal));

        foreach (var outcome in LoopOutcomes.All)
        {
            await Assert.That(row).DoesNotContain(outcome, StringComparison.Ordinal)
                .Because("no loop.outcome fact reached this flight, so the row has nothing to "
                       + "say about how its loop ended - and inventing 'completed' would be the "
                       + "console answering for a runner. Row: " + row);
        }
    }

    [Test]
    public async Task A_fetch_that_failed_and_a_tenant_with_no_flights_read_differently()
    {
        // Article XI's shape on a read surface. "No flights" is a fact about a
        // tenant; a null list is a request that did not answer, and a person
        // seeing the first when the second happened stops looking.
        await Assert.That(PaneText.Flights(new AppState { Flights = null }))
            .Contains("could not", StringComparison.OrdinalIgnoreCase);

        await Assert.That(PaneText.Flights(new AppState { Flights = new FlightList { Flights = [] } }))
            .DoesNotContain("could not", StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task The_tab_is_open_before_anybody_asks_for_it()
    {
        // NO KEY, DELIBERATELY. Every letter that reads as 'flights' is taken -
        // f is freeze and fly-this, l is live - and a person who has to learn a
        // key to find out what their flights did will not learn it. Two
        // permanent tabs and one tab key is the whole discovery path.
        await Assert.That(Tabs.All).Contains(TabId.Flights);
        await Assert.That(Tabs.Title(new AppState(), TabId.Flights))
            .Contains("Flights", StringComparison.Ordinal);
        await Assert.That(Tabs.Next(new AppState())).IsEqualTo(TabId.Flights)
            .Because("one press of tab from where a console opens.");
        await Assert.That(Tabs.KeyFor(TabId.Flights)).IsNull()
            .Because("no key, deliberately: every letter that reads as 'flights' is taken.");
    }
}
