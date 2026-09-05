using Gg.Contracts.Description;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// What <c>gg show</c> says about a loop that stopped to ask.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nothing in this feature works if a person reads <i>blocked</i> as
/// <i>broken</i>.</b> The whole of it is an agent saying <i>I should not decide
/// this</i>, and a surface that files that beside a crash teaches everybody to
/// ignore the state - which is how the tier gets switched off rather than
/// argued with.
/// </para>
/// <para>
/// <b>And the loop fact was rendered nowhere.</b> Before this class,
/// <c>gg show</c> printed <c>loop.outcome</c> and a timestamp: not the outcome,
/// not the reason, not how long it ran. Four of the eleven fact slots have a
/// branch in the renderer and the rest print their kind. So "the 280-character
/// reason is the only thing a person gets" was generous - a person got the word
/// <c>loop.outcome</c>.
/// </para>
/// <para>
/// <b>Step 0 measured what the truncation costs.</b> Four real runs against a
/// deliberately undecidable ticket: every one of them put the situation in the
/// first paragraph and the decision in the second, and <c>ExecutorRun.Clean</c>
/// keeps the first paragraph and cuts at 280. The question is not subject to
/// that cut and must not be - it is the field a person reads while deciding.
/// </para>
/// </remarks>
public class BlockedRenderingTests
{
    /// <summary>
    /// A closing summary as step 0 measured them: the situation first, and the
    /// part somebody needs below the cut.
    /// </summary>
    private const string TruncatedReason =
        "I'm blocked: both the Write and Edit calls to src/rounding.py were rejected by the "
      + "permission layer, which flags that path as a sensitive file. I didn't try to route "
      + "around it via shell. The decision I made: go with the Tax team's half-up at 2d";

    /// <summary>
    /// Three lines because an agent laid it out over three, and the contract
    /// keeps a question as prose for exactly this reason.
    /// </summary>
    private const string Question =
        "The ticket records two teams asking for different rounding rules and does not say "
      + "which wins.\n"
      + "Tax asks for half-up at 2dp, citing the 2024 filing guidance.\n"
      + "Billing asks for half-even, citing the reconciliation job that already assumes it.\n"
      + "Which should I implement?";

    private static FlightSummary AFlightWith(params FactEnvelope[] facts) => new()
    {
        FlightId = "019fe815-6136-7518-bb57-b06d6d3f411a",
        FlightNumber = FlightRef.Format(42),
        Name = "round the invoice totals",
        Intent = new FlightIntent { Kind = FlightIntentKinds.Text, Text = "look at it" },
        CreatedAt = new DateTimeOffset(2026, 9, 4, 12, 0, 0, TimeSpan.Zero),
        RunnerProtocolVersion = 1,
        FactVocabularyVersion = "0.19.0",
        ConstitutionVersion = "1.0.0",
        EnvelopeVersion = "none",
        Attempts = 1,
        Facts = facts,
    };

    private static FactEnvelope AnOutcome(string outcome, string reason) => new()
    {
        IdempotencyKey = "k1",
        Kind = FactKinds.LoopOutcome,
        Digest = new string('a', 64),
        ObservedAt = new DateTimeOffset(2026, 9, 4, 12, 5, 0, TimeSpan.Zero),
        Loop = new LoopOutcome
        {
            LoopId = "implement",
            Outcome = outcome,
            Reason = reason,
            Executor = "claude-code",
            Attempts = 6,
            DurationMs = 41_200,
            MovesUsed = ["read", "edit"],
        },
    };

    private static FactEnvelope AQuestion(string asked) => new()
    {
        IdempotencyKey = "k2",
        Kind = FactKinds.LoopQuestion,
        Digest = new string('b', 64),
        ObservedAt = new DateTimeOffset(2026, 9, 4, 12, 4, 0, TimeSpan.Zero),
        Question = new LoopQuestion { Question = asked },
    };

    private static string Shown(params FactEnvelope[] facts) =>
        VerbOutput.ToText(new VerbResult.Flight(AFlightWith(facts)));

    /// <summary>
    /// One field of a fact, by its label rather than by its content.
    /// </summary>
    /// <remarks>
    /// <b>By the label's whole column</b>, because "outcome" also occurs in the
    /// kind line above it - <c>loop.outcome</c> - and a search that matched
    /// there would assert about the fact's NAME while reading as an assertion
    /// about its value. The first draft of this class did exactly that and one
    /// of its tests passed against a renderer with no outcome field at all.
    /// </remarks>
    private static string Field(string text, string label) =>
        text.Split('\n').Single(l => l.StartsWith("    " + label + " ", StringComparison.Ordinal));

    // ---- S25.6-02: blocked is a state, not a failure ----

    [Test]
    public async Task Blocked_is_rendered_as_waiting_on_a_person()
    {
        var text = Shown(AnOutcome(LoopOutcomes.Blocked, TruncatedReason));

        await Assert.That(text).Contains(LoopOutcomes.Blocked)
            .Because("the vocabulary's own word is what a script and a person share, so the "
                   + "sentence explains it rather than replacing it.");
        await Assert.That(text).Contains("not broken")
            .Because("a person reading this at 2am decides whether to page somebody. The word "
                   + "'blocked' alone reads as an incident, and this tier is worth nothing if "
                   + "the first thing everybody learns is to ignore it.");
        await Assert.That(text).Contains("aiting on a person")
            .Because("it says who unblocks it. A state nobody knows how to leave is one "
                   + "people route around.");
    }

    [Test]
    public async Task Blocked_does_not_borrow_the_words_of_a_failure()
    {
        // THE LIVENESS TWIN FOR THE SENTENCE ABOVE. A rendering that read
        // "blocked - the loop failed to decide" contains 'blocked' and
        // 'not broken' is easy to bolt on; what makes the state legible is
        // that it does not reach for the failure vocabulary at all.
        var text = Shown(AnOutcome(LoopOutcomes.Blocked, TruncatedReason));

        var line = Field(text, "outcome");

        await Assert.That(line).DoesNotContain("failed")
            .Because("an impasse and a crash need different people, which is the argument "
                   + "LoopOutcomes.Blocked is written on. The line is: " + line);
        await Assert.That(line).DoesNotContain("error")
            .Because("same reason, and 'error' is the word a reader scans for.");
    }

    [Test]
    public async Task Every_outcome_in_the_vocabulary_has_its_own_words()
    {
        // A SWEEP, so the fifth outcome inherits this rather than being
        // remembered. Distinctness is the assertion that matters: four values
        // rendering the same sentence is a table that compiles and says nothing.
        var sentences = new List<string>();

        foreach (var outcome in LoopOutcomes.All)
        {
            var text = Shown(AnOutcome(outcome, "it did a thing"));
            var line = Field(text, "outcome").Trim();

            await Assert.That(line).Contains(outcome);
            await Assert.That(line.Length).IsGreaterThan(("outcome     " + outcome).Length + 20)
                .Because($"'{outcome}' is a vocabulary value and this surface is for a person. "
                       + "The line is: " + line);
            sentences.Add(line);
        }

        await Assert.That(sentences.Distinct(StringComparer.Ordinal).Count())
            .IsEqualTo(LoopOutcomes.All.Count)
            .Because("four outcomes sharing one sentence is a lookup that always hits its "
                   + "default, and it renders as a table that works.");
    }

    [Test]
    public async Task An_outcome_outside_the_vocabulary_halts()
    {
        // ARTICLE XI, in Rendered(state)'s own shape one noun over. The
        // plausible default here is 'failed', and showing a state this build
        // does not understand as a failure is the precise confusion the
        // vocabulary exists to remove.
        var thrown = Assert.Throws<InvalidOperationException>(
            () => Shown(AnOutcome("abandoned", "it did a thing")));

        await Assert.That(thrown!.Message).Contains("abandoned")
            .Because("naming the value is what makes the halt actionable.");
    }

    // ---- S25.6-03: the reason is no longer all there is ----

    [Test]
    public async Task The_reason_reaches_a_person_at_all()
    {
        // IT DID NOT, BEFORE THIS. The renderer has a branch for four fact
        // slots out of eleven and loop.outcome was not one of them, so `gg show`
        // printed the kind and the timestamp and stopped.
        var text = Shown(AnOutcome(LoopOutcomes.Blocked, TruncatedReason));

        await Assert.That(text).Contains("permission layer")
            .Because("the 280 characters that survive ExecutorRun.Clean are the ones a person "
                   + "gets, and until now they were not rendered anywhere.");
        await Assert.That(text).Contains("claude-code")
            .Because("which rung ran it, beside how long. A reason with no run behind it is a "
                   + "sentence somebody has to take on trust.");
        await Assert.That(text).Contains("6 turn(s)");
    }

    [Test]
    public async Task The_question_survives_beside_the_truncated_reason()
    {
        // THE CRITERION. Step 0 measured four runs whose decision was in the
        // paragraph below the cut. The question is a different fact and is not
        // cut, so what a person gets is a truncated summary AND the whole ask.
        var text = Shown(
            AnOutcome(LoopOutcomes.Blocked, TruncatedReason), AQuestion(Question));

        foreach (var line in Question.Split('\n'))
        {
            await Assert.That(text).Contains(line)
                .Because("every line of it, not a prefix. A question cut in half is one "
                       + "nobody can answer, and one that arrives looking answerable is "
                       + "worse. Missing: " + line);
        }

        await Assert.That(text).Contains(TruncatedReason[^20..])
            .Because("beside, not instead of. The reason is what the loop said about its "
                   + "run and the question is what it needs; a surface that showed one "
                   + "would have a person guessing which.");
    }

    [Test]
    public async Task A_question_laid_out_over_lines_stays_under_its_label()
    {
        // PROSE IS PROSE, and the contract keeps the agent's line breaks on
        // purpose. A renderer that pastes them in raw puts every line after the
        // first at column zero, where it is indistinguishable from a new field -
        // so the layout has to carry the shape the field promised.
        var text = Shown(AQuestion(Question));

        await Assert.That(text).DoesNotContain("\nTax asks for half-up")
            .Because("flush-left continuation reads as a new field, and the next real field "
                   + "then reads as part of the question.");
        await Assert.That(text).Contains("\n                Tax asks for half-up")
            .Because("indented under the label it belongs to, which is what makes a "
                   + "three-line question one value rather than three rows.");
    }

    // ---- S25.6-04's client half ----

    [Test]
    public async Task A_question_with_no_outcome_beside_it_is_still_shown()
    {
        // RULE 3. A recorded question is not a gate: whether one opens is the
        // envelope's to say. An agent that asked and went on to finish is
        // `completed` with a question beside it, and a flight still running has
        // no outcome at all - both are ordinary, and in both the question is
        // the only thing on the flight that says a person is wanted.
        var text = Shown(AQuestion(Question));

        await Assert.That(text).Contains("Which should I implement?")
            .Because("an ungated question nobody can see is a question nobody answers.");
    }
}
