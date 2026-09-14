using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Console.Tests;

/// <summary>
/// A fourth tab: what the flight recorded, beside what happened to it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Three tabs answered three questions and left the biggest one out.</b>
/// Details is what this flight IS, Gate is what waits on a person, Log is what
/// the control plane DID to it. None of them is what the runner shipped - and
/// that is the half a customer audits.
/// </para>
/// <para>
/// <b>A claim and a measurement must not read the same.</b> The loop line a
/// person sees is the agent's own prose, cut to its first paragraph; the
/// recorded proposal that either backs it or does not had no reader anywhere.
/// A flight that said it scored an item and recorded nothing looked exactly
/// like one that did.
/// </para>
/// <para>
/// <b>Fetched when the tab is opened, not at boot.</b> Logs are held for every
/// flight because the boot already pays for them; facts are large and rarely
/// read, so another request per flight at launch is the wrong trade. The read
/// goes through <c>BackgroundReads</c>, which is the established path - a task
/// owned outside every UI lifetime, folded in on a tick - so no new exception
/// to <i>a UI session may not start anything or block</i> is asked for here.
/// </para>
/// </remarks>
public class AFactsTabShowsWhatWasRecordedTests
{
    private const string Id = "019fe815-6136-7518-bb57-b06d6d3f411a";

    private static readonly DateTimeOffset At =
        new(2026, 9, 14, 17, 9, 52, TimeSpan.Zero);

    /// <summary>
    /// The modal open on a flight, with the facts tab showing.
    /// </summary>
    /// <remarks>
    /// <b>A state carrying facts and no flight renders nothing at all</b>, which
    /// is the console refusing to caption one flight's evidence with another's
    /// name - the rule the log tab's fixture records one tab over.
    /// </remarks>
    private static AppState Opened(FlightFacts? facts = null) => new()
    {
        Mode = UiMode.FlightDetail,
        FlightTab = FlightTab.Facts,
        FlightFacts = facts,
        Flights = new FlightList
        {
            Flights =
            [
                new FlightSummary
                {
                    FlightId = Id,
                    FlightNumber = FlightRef.Format(42),
                    Name = "score the work item",
                    Intent = new FlightIntent
                    {
                        Kind = FlightIntentKinds.Ticket,
                        Provider = "a-tracker",
                        Id = "18119",
                    },
                    CreatedAt = At,
                    RunnerProtocolVersion = 1,
                    FactVocabularyVersion = "0.30.0",
                    ConstitutionVersion = "1.0.0",
                    EnvelopeVersion = "v7",
                    Attempts = 1,
                    State = FlightStates.Open,
                    Facts = [],
                },
            ],
        },
        Story = new FlightStory
        {
            FlightId = Id,
            FlightNumber = FlightRef.Format(42),
            Stage = FlightStages.Worked,
            State = FlightStates.Open,
            Entries = [],
        },
    };

    private static FlightFacts Recorded() => new()
    {
        FlightNumber = "GG-42",
        Facts =
        [
            new RecordedFact
            {
                Disposition = "digest",
                RecordedAt = new DateTimeOffset(2026, 9, 14, 17, 9, 53, TimeSpan.Zero),
                Fact = new FactEnvelope
                {
                    IdempotencyKey = "p-1",
                    Kind = FactKinds.WorkItemProposal,
                    Digest = new string('a', 64),
                    ObservedAt = new DateTimeOffset(2026, 9, 14, 17, 9, 52, TimeSpan.Zero),
                    Proposal = new WorkItemProposal
                    {
                        Operation = WorkItemOperations.Field,
                        Target = "18119",
                        Reason = "scored against the rubric",
                        Fields = [new WorkItemFieldEdit { Path = "Custom.HAL", Value = "4" }],
                    },
                },
            },
        ],
    };

    [Test]
    public async Task The_cycle_reaches_it_and_comes_back_round()
    {
        // A cycle that stopped at the last tab would make the fourth one a place
        // somebody reaches and cannot leave by the key that got them there -
        // which is the reason the third one is already in this loop.
        var seen = new List<FlightTab>();
        var state = new AppState { FlightTab = FlightTab.Details };

        for (var i = 0; i < 4; i++)
        {
            state = Reducer.Reduce(state, Command.NextFlightTab);
            seen.Add(state.FlightTab);
        }

        await Assert.That(seen).IsEquivalentTo((FlightTab[])
            [FlightTab.Gate, FlightTab.Log, FlightTab.Facts, FlightTab.Details])
            .Because($"four presses visit all four and return. Saw: {string.Join(", ", seen)}");
    }

    [Test]
    public async Task It_shows_the_kind_the_budget_and_what_the_fact_says()
    {
        var text = PaneText.Modal(Opened(Recorded()));

        await Assert.That(text).Contains(FactKinds.WorkItemProposal, StringComparison.Ordinal);
        await Assert.That(text).Contains("digest", StringComparison.Ordinal)
            .Because("which budget held it is the answer to why a row has no content, and "
                   + "a reader who is not told reads an empty row as a defect.");
        await Assert.That(text).Contains("Custom.HAL", StringComparison.Ordinal)
            .Because("the field a scoring flight set is the whole of what it produced. "
                   + "This is the assertion the log could never make.");
    }

    [Test]
    public async Task Waiting_says_so_and_a_flight_that_recorded_nothing_says_that_instead()
    {
        // TWO ABSENCES, NOT ONE. A read still in flight and a flight that shipped
        // nothing are different answers, and a tab rendering both as blank would
        // make a pending read look like an empty record.
        var waiting = PaneText.Modal(Opened());

        await Assert.That(waiting.Length).IsGreaterThan(20)
            .Because($"a blank pane reads as a broken tab. Said: {waiting}");

        var none = PaneText.Modal(
            Opened(new FlightFacts { FlightNumber = "GG-1", Facts = [] }));

        await Assert.That(none).IsNotEqualTo(waiting)
            .Because("'nothing was recorded' is a different sentence from 'not read yet'.");
    }

    [Test]
    public async Task Opening_the_tab_is_a_read_rather_than_a_session_ending_command()
    {
        // The browse work settled this shape: a read folds in beside the console
        // on a task owned outside the session, so the screen is never taken away
        // and given back for a question about a flight already on it.
        await Assert.That(ShellCommands.Reads).Contains(Command.ShowFlightFacts)
            .Because("a command that ended the session to read facts would blink the "
                   + "terminal for a pane that is already open.");

        await Assert.That(ShellCommands.Handled).DoesNotContain(Command.ShowFlightFacts)
            .Because("a shell command whose effect is also a state change has two effects.");
    }
}
