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

    /// <summary>A flight that published a preview, as the runner ships it.</summary>
    private static FlightFacts APreview() => new()
    {
        // THE FLIGHT THE MODAL IS OPEN ON. This said GG-268 under a modal open
        // on GG-42, and passed only because the tab drew whichever flight's
        // facts it held - the defect the last test in this class pins.
        FlightNumber = "GG-42",
        Facts =
        [
            new RecordedFact
            {
                Disposition = EvidenceDispositions.Inline,
                RecordedAt = new DateTimeOffset(2026, 9, 24, 17, 9, 53, TimeSpan.Zero),
                Fact = new FactEnvelope
                {
                    IdempotencyKey = "preview-1",
                    Kind = FactKinds.PreviewUrl,
                    Digest = new string('b', 64),
                    ObservedAt = new DateTimeOffset(2026, 9, 24, 17, 9, 52, TimeSpan.Zero),
                    Preview = new PreviewUrl
                    {
                        Url = "https://jdapp-01.goodgrief.dev",
                        Exposure = "jdapp",
                        Slot = "01",
                    },
                },
            },
        ],
    };

    [Test]
    public async Task A_preview_shows_the_address_a_person_is_meant_to_open()
    {
        // THE ONE FACT WHOSE WHOLE CONTENT IS THE THING TO DO NEXT. Every other
        // kind here describes what happened; this one is an instruction - go
        // and look at this - and a row that names the kind and says nothing has
        // told somebody a preview exists while withholding where.
        //
        // The default arm of FactSays answers "" and cannot fail, so a fact
        // nobody added a case for renders as a blank column rather than as an
        // error anybody would notice.
        var text = PaneText.Modal(Opened(APreview()));

        await Assert.That(text).Contains("https://jdapp-01.goodgrief.dev")
            .Because("a gate asking somebody to look at a preview cannot carry the address - "
                   + "the gate payload is assembled and discarded - so this pane is where a "
                   + "person finds it, and a blank here is a preview nobody can open.");
    }

    [Test]
    public async Task A_preview_names_the_slot_it_was_granted()
    {
        // SO IT CAN BE RECONCILED AGAINST AN INVENTORY. An address alone cannot
        // answer the question asked when a preview stops responding: whether
        // the slot is still held or already free.
        var text = PaneText.Modal(Opened(APreview()));

        await Assert.That(text).Contains("jdapp")
            .Because("the fact names the exposure and the slot as well as the address, and a "
                   + "renderer that drew only the address would throw that away.");
    }

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
            Opened(new FlightFacts { FlightNumber = "GG-42", Facts = [] }));

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

    /// <summary>What an investigate flight lands: a title, and the analysis beneath it.</summary>
    private static FlightFacts ALanding() => new()
    {
        FlightNumber = "GG-42",
        Facts =
        [
            new RecordedFact
            {
                Disposition = "digest",
                RecordedAt = new DateTimeOffset(2026, 10, 10, 1, 41, 20, TimeSpan.Zero),
                Fact = new FactEnvelope
                {
                    IdempotencyKey = "landing-1",
                    Kind = FactKinds.LandingProposal,
                    Digest = new string('c', 64),
                    ObservedAt = new DateTimeOffset(2026, 10, 10, 1, 41, 20, TimeSpan.Zero),
                    Landing = new LandingProposal
                    {
                        Title = "Investigate 18492: add role=button to 2 residual sites",
                        Description = "Asked: close 2 open issues.\n\nApproach: add role=button to both.",
                    },
                },
            },
        ],
    };

    [Test]
    public async Task Turning_to_the_tab_is_what_asks_for_its_facts()
    {
        // REPORTED FROM USE: the recorded tab showed no facts for any flight.
        // The read started only on ShowFlightFacts, which no key and no click
        // sends - `v` and the tab bar both reduce NextFlightTab - and the tab
        // said "press for its facts" about a press nobody could make.
        await Assert.That(FlightDetails.FactsOwed(Opened())).IsEqualTo("GG-42")
            .Because("a tab showing nothing it has not read owes the read, whichever way a "
                   + "person turned to it.");

        await Assert.That(FlightDetails.FactsOwed(Opened(Recorded()))).IsNull()
            .Because("facts already held for this flight are not asked for again.");

        await Assert.That(FlightDetails.FactsOwed(Opened() with { ReadInFlight = true })).IsNull()
            .Because("one read runs at a time and a second abandons the first, so asking while "
                   + "the story is still coming would drop the story.");

        await Assert.That(FlightDetails.FactsOwed(Opened() with { FlightTab = FlightTab.Details }))
            .IsNull()
            .Because("facts are large and rarely read, so only the tab that shows them asks.");
    }

    [Test]
    public async Task The_absence_names_no_key_that_does_not_exist()
    {
        var text = PaneText.Modal(Opened());

        await Assert.That(text).DoesNotContain("Press for its facts")
            .Because("no key sends ShowFlightFacts, so a sentence asking for that press sends "
                   + "a person looking for something that is not there.");
    }

    [Test]
    public async Task Another_flights_facts_are_never_drawn_under_this_one()
    {
        // ONE SLOT, MANY FLIGHTS. The facts are held with the flight they belong
        // to, and the tab drew them without reading it - so the second flight
        // opened showed the first one's record under its own title.
        var elsewhere = Recorded() with { FlightNumber = "GG-7" };

        var text = PaneText.Modal(Opened(elsewhere));

        await Assert.That(text).DoesNotContain("Custom.HAL")
            .Because("GG-7's proposal is not something GG-42 recorded.");

        await Assert.That(FlightDetails.FactsOwed(Opened(elsewhere))).IsEqualTo("GG-42")
            .Because("holding another flight's facts is holding none of this one's.");
    }

    [Test]
    public async Task A_landing_shows_its_title_and_the_analysis_beneath_it()
    {
        // AN INVESTIGATE FLIGHT'S WHOLE RESULT. Its destination opens nothing,
        // so the landing it proposed is the only place its analysis exists, and
        // a row naming the kind and nothing else showed a flight with no result.
        var text = PaneText.Modal(Opened(ALanding()));

        await Assert.That(text).Contains("Investigate 18492: add role=button to 2 residual sites");
        await Assert.That(text).Contains("Approach: add role=button to both.")
            .Because("the description is the analysis, and a title alone is a headline over "
                   + "nothing.");
    }
}
