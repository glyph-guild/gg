using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Console.Tests;

/// <summary>
/// The recorded tab is a table of facts over a pane that shows the one under the
/// cursor whole, and copying it takes all of them.
/// </summary>
/// <remarks>
/// <para>
/// <b>Asked for 2026-10-09, after "the recorded tab seems formatted weird".</b>
/// It was one word-wrapped Label. Measured in a pty on GG-1058 (an investigate
/// flight, eight facts): the landing's thirty-five lines of analysis filled the
/// pane, the five facts after it were pushed off the bottom of a view that
/// does not scroll, wrapped lines fell back to column zero under indented ones,
/// the columns had no headers, and the result sat third because eight facts
/// sharing one timestamp came out in the order of their kinds' names.
/// </para>
/// <para>
/// <b>The log tab's shape</b>: one row per fact, and the detail of the one under
/// the cursor in a pane that scrolls. <b>And copyable</b> (the owner's word):
/// `c` takes every fact with everything it carries, not the part on screen.
/// </para>
/// </remarks>
public class TheRecordedTabIsATableTests
{
    private const string Id = "019fe815-6136-7518-bb57-b06d6d3f411a";

    private static readonly DateTimeOffset At = new(2026, 10, 10, 1, 41, 20, TimeSpan.Zero);

    private const string Analysis =
        "Asked: close 2 open S6845 issues on develop.\n"
      + "\n"
      + "Finding: these aren't new regressions. The inventory documents 3 instances, "
      + "reviewed as interactive-in-practice but left unfixed.\n"
      + "- preview-profile-editor.component.html:73-82\n"
      + "- filter-menu.component.html:41-49";

    private static RecordedFact Fact(
        string kind, string disposition, Func<FactEnvelope, FactEnvelope> with) => new()
    {
        Disposition = disposition,
        RecordedAt = At.AddSeconds(1),
        Fact = with(new FactEnvelope
        {
            IdempotencyKey = $"{Id}:{kind}",
            Kind = kind,
            Digest = new string('a', 64),
            ObservedAt = At,
        }),
    };

    /// <summary>Four facts sharing one timestamp, as one loop ships them.</summary>
    private static FlightFacts Recorded() => new()
    {
        FlightNumber = "GG-42",
        Facts =
        [
            Fact(FactKinds.EnvironmentIdentity, EvidenceDispositions.Digest, f => f with
            {
                Environment = new EnvironmentIdentity
                {
                    HostFingerprint = "host-7f3a",
                    Locks = [],
                    Tools = [],
                    Provenance = "measured",
                },
            }),
            Fact(FactKinds.LandingProposal, EvidenceDispositions.Digest, f => f with
            {
                Landing = new LandingProposal
                {
                    Title = "Investigate 18492: add role=\"button\" to 2 residual sites",
                    Description = Analysis,
                },
            }),
            Fact(FactKinds.LoopOutcome, EvidenceDispositions.Inline, f => f with
            {
                Loop = new LoopOutcome
                {
                    LoopId = "investigate",
                    Outcome = "completed",
                    Reason = "Landing proposed. No files were edited.",
                    Executor = "frontier",
                    Attempts = 27,
                    DurationMs = 211416,
                    MovesUsed = ["Bash", "Read"],
                },
            }),
            Fact(FactKinds.LoopTranscript, EvidenceDispositions.Reference, f => f with
            {
                Transcript = new ArtifactReference
                {
                    Locator = "runner://transcripts/abc",
                    Bytes = 48213,
                    Scope = "loop",
                    MediaType = "application/x-ndjson",
                    Sha256 = new string('c', 64),
                },
            }),
        ],
    };

    private static AppState Opened(int selected = 0, FlightTab tab = FlightTab.Facts) => new()
    {
        Mode = UiMode.FlightDetail,
        FlightTab = tab,
        FactSelected = selected,
        FlightFacts = Recorded(),
        Flights = new FlightList
        {
            Flights =
            [
                new FlightSummary
                {
                    FlightId = Id,
                    FlightNumber = FlightRef.Format(42),
                    Name = "investigate 18492",
                    Intent = new FlightIntent { Kind = FlightIntentKinds.Text, Text = "look" },
                    CreatedAt = At,
                    RunnerProtocolVersion = 1,
                    FactVocabularyVersion = "0.42.0",
                    ConstitutionVersion = "1.0.0",
                    EnvelopeVersion = "v7",
                    Attempts = 1,
                    State = FlightStates.Landed,
                    Facts = [],
                },
            ],
        },
    };

    // ---- the table ----

    [Test]
    public async Task The_table_says_what_each_column_is()
    {
        await Assert.That(Rows.FactColumns).IsEquivalentTo(
            (string[])["at", "kind", "what it says", "kept as"]);
    }

    [Test]
    public async Task One_row_per_fact_with_the_result_first()
    {
        var rows = Rows.Facts(Opened());

        await Assert.That(rows.Select(r => r.Kind)).IsEquivalentTo(
            (string[])
            [
                FactKinds.LandingProposal,
                FactKinds.LoopOutcome,
                FactKinds.EnvironmentIdentity,
                FactKinds.LoopTranscript,
            ])
            .Because("what the flight concluded and how its loop ended are what somebody opens "
                   + "this tab for, and facts shipped in one batch share a timestamp - so the "
                   + "order fell to their kinds' names and the landing came third.");
        await Assert.That(rows.All(r => !r.Says.Contains('\n'))).IsTrue()
            .Because("a cell is one line; prose belongs to the pane beneath.");
        await Assert.That(rows[0].Kept).IsEqualTo(EvidenceDispositions.Digest);
        await Assert.That(rows[0].At).IsEqualTo("01:41:20");
    }

    // ---- the pane beneath ----

    [Test]
    public async Task The_pane_shows_the_fact_under_the_cursor_whole()
    {
        var lines = FlightDetails.FactDetailLines(Opened(selected: 0), width: 60);
        var text = string.Join("\n", lines);

        await Assert.That(text).Contains("Investigate 18492");

        foreach (var said in new[] { "Asked: close 2 open S6845", "filter-menu.component.html:41-49" })
        {
            await Assert.That(text).Contains(said)
                .Because("every line of the analysis is there to scroll to - a pane that drew "
                       + "what fitted and dropped the rest is the defect this replaces.");
        }

        await Assert.That(lines.All(l => l.Length <= 60)).IsTrue()
            .Because("broken to the pane's width here, so nothing wraps back to column zero "
                   + "under an indented line.");
    }

    [Test]
    public async Task The_pane_says_what_kept_as_means()
    {
        var text = string.Join("\n", FlightDetails.FactDetailLines(Opened(selected: 3), width: 0));

        await Assert.That(text).Contains(EvidenceDispositions.Reference);
        await Assert.That(text).Contains("pointer")
            .Because("`reference` alone reads as a defect when the content is not there; what "
                   + "it means - it did not fit and does not reduce, so a pointer crossed - is "
                   + "the answer to why.");
    }

    [Test]
    public async Task A_fact_with_no_summary_still_shows_everything_it_carries()
    {
        // THE BLANK ROWS. environment.identity has no arm in the one-line
        // summary, so its row said nothing at all and there was nowhere else to
        // look. The pane carries every member the fact holds.
        var text = string.Join("\n", FlightDetails.FactDetailLines(Opened(selected: 2), width: 0));

        await Assert.That(text).Contains("host-7f3a");
        await Assert.That(text).DoesNotContain("null")
            .Because("an absent member is absent, not a word; the contract's two dozen empty "
                   + "slots would bury the three that are filled.");
    }

    // ---- moving ----

    [Test]
    public async Task On_this_tab_the_cursor_moves_over_the_facts()
    {
        var moved = Reducer.Reduce(Opened(), Command.SelectNext);

        await Assert.That(moved.FactSelected).IsEqualTo(1);
        await Assert.That(moved.LogSelected).IsEqualTo(0)
            .Because("the log's cursor belongs to the log tab, and moving it from here would "
                   + "move a place in a history nobody is looking at.");

        var far = Enumerable.Range(0, 9).Aggregate(Opened(), (s, _) => Reducer.Reduce(s, Command.SelectNext));

        await Assert.That(far.FactSelected).IsEqualTo(3);
    }

    [Test]
    public async Task On_the_log_tab_it_still_moves_the_log()
    {
        var moved = Reducer.Reduce(Opened(tab: FlightTab.Log), Command.SelectNext);

        await Assert.That(moved.FactSelected).IsEqualTo(0);
    }

    [Test]
    public async Task Opening_a_flight_starts_at_the_top_of_its_facts()
    {
        var shown = Reducer.FlightShown(Opened(selected: 3) with { Mode = UiMode.Normal });

        await Assert.That(shown.FactSelected).IsEqualTo(0)
            .Because("a cursor left where the last flight's facts ended points into facts it "
                   + "was never about.");
    }

    // ---- copying ----

    [Test]
    public async Task Copying_takes_every_fact_and_everything_it_carries()
    {
        // `c` COPIES PaneText.Modal, which on this tab is the linear reading of
        // the tab - so it must be all of it, not the row under the cursor.
        var copied = PaneText.Modal(Opened(selected: 3));

        foreach (var kind in new[]
                 {
                     FactKinds.LandingProposal, FactKinds.LoopOutcome,
                     FactKinds.EnvironmentIdentity, FactKinds.LoopTranscript,
                 })
        {
            await Assert.That(copied).Contains(kind);
        }

        await Assert.That(copied).Contains("filter-menu.component.html:41-49")
            .Because("the whole analysis, though the cursor is on another fact.");
        await Assert.That(copied).Contains("Landing proposed. No files were edited.");
        await Assert.That(copied).Contains("host-7f3a");
        await Assert.That(copied).Contains("runner://transcripts/abc");
    }
}
