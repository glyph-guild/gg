using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// The question an agent asked, as the thing that crosses.
/// </summary>
/// <remarks>
/// <para>
/// <b>A fact, because the alternative is a field on the outcome.</b> Rule 7:
/// asking and finishing are two facts, not one state. An agent that asked and
/// then went on to finish is <c>completed</c> WITH a question recorded beside
/// it, and collapsing the two would make one clarifying question turn a
/// finished flight into a chore - which is how a feature gets switched off.
/// </para>
/// <para>
/// <b>Customer-adjacent prose, filtered like everything else.</b> It may quote
/// code, name a path, or paste an error, so it is classified on the runner's
/// machine and held under the tenant's ceiling before it crosses. What it may
/// never carry off the machine is an absolute path or a machine name, and that
/// is <c>FactCleanliness</c>' job rather than a rule written beside it.
/// </para>
/// <para>
/// <b>Refused rather than truncated.</b> A question cut in half is a question a
/// person cannot answer, and one that arrives looking answerable is worse than
/// one that was rejected: the flight would wait on a gate nobody could close.
/// </para>
/// </remarks>
public class LoopQuestionFactTests
{
    private const string Asked =
        "Two teams asked for opposite rounding rules and the ticket does not say which wins. "
      + "Accounts Receivable want half-to-even; Tax want half-up at 2dp. Which?";

    private static FactEnvelope Envelope(LoopQuestion question) => new()
    {
        IdempotencyKey = "q1",
        Kind = FactKinds.LoopQuestion,
        Digest = new string('a', 64),
        ObservedAt = DateTimeOffset.UnixEpoch,
        Question = question,
    };

    // ---- S25.3-01 ----

    [Test]
    public async Task It_crosses_as_a_fact_of_its_own_kind()
    {
        await Assert.That(FactKinds.All).Contains(FactKinds.LoopQuestion)
            .Because("a kind the vocabulary does not declare is refused at the edge, so a "
                   + "fact type nobody registered is one that can never arrive.");

        var envelope = Envelope(new LoopQuestion { Question = Asked });

        await Assert.That(FactEnvelope.Validate(envelope)).IsNull()
            .Because("its own kind and its own slot, the way a human account is marked as a "
                   + "person's words: there is no voice field on a fact, so what says whose "
                   + "words these are is the shape.");
    }

    [Test]
    public async Task A_question_above_the_ceiling_is_refused_rather_than_cut()
    {
        var enormous = new LoopQuestion { Question = new string('x', LoopQuestion.MaxQuestion + 1) };

        var refusal = LoopQuestion.Validate(enormous);

        await Assert.That(refusal).IsNotNull()
            .Because("a question cut in half is one a person cannot answer, and one that "
                   + "arrives looking answerable is worse than one that was rejected - the "
                   + "flight would wait on a gate nobody could close.");
        await Assert.That(refusal!).Contains(LoopQuestion.MaxQuestion.ToString())
            .Because("the refusal says what the ceiling is, so an agent that hit it can say "
                   + "less rather than guess.");

        await Assert.That(LoopQuestion.Validate(
                new LoopQuestion { Question = new string('x', LoopQuestion.MaxQuestion) }))
            .IsNull()
            .Because("exactly at the ceiling is under it. An off-by-one here refuses the "
                   + "longest question anybody is allowed to ask.");
    }

    // ---- S25.3-05 ----

    [Test]
    public async Task An_empty_question_is_refused_at_construction()
    {
        // WORSE THAN NONE. A blocked declaration with nothing in it opens a
        // gate a person cannot answer, and the flight then waits on somebody
        // who has been given no way to decide.
        foreach (var nothing in (string[])["", "   ", "\n\t "])
        {
            await Assert.That(LoopQuestion.Validate(new LoopQuestion { Question = nothing }))
                .IsNotNull()
                .Because($"'{nothing.Replace("\n", "\\n").Replace("\t", "\\t")}' is not a "
                       + "question, and recording it would open a gate with nothing in it.");
        }
    }

    // ---- S25.3-02 ----

    [Test]
    public async Task It_is_cleaned_as_prose_and_refused_rather_than_altered()
    {
        // WHAT FactCleanliness ACTUALLY DOES, which is not what this criterion
        // was written believing. It refuses TERMINAL CONTROL SEQUENCES - and
        // refuses rather than cleans, because the digest was computed over the
        // fact as it was produced and altering it here would make what is
        // stored disagree with the hash that proves what it was.
        //
        // Absolute paths and machine names are NOT filtered, here or anywhere
        // else in the fact pipeline: `loop.outcome.reason` and
        // `nomination.reason` carry the same exposure today, and inventing a
        // scrubber for one prose field and not the others would be a
        // half-measure on a boundary. Recorded in the slice as its own finding.
        var withControl = new LoopQuestion
        {
            Question = "I am stuck on \u001b[31mthis\u001b[0m and cannot decide",
        };

        await Assert.That(FactCleanliness.Unclean(Envelope(withControl))).IsNotNull()
            .Because("a fact arriving dirty came from a runner that is misconfigured or "
                   + "modified, and the refusal names the field so somebody can act on it.");

        await Assert.That(FactCleanliness.Unclean(Envelope(
                new LoopQuestion { Question = Asked })))
            .IsNull()
            .Because("the liveness twin: a check that refused everything would satisfy the "
                   + "line above and would stop every question crossing.");
    }

    [Test]
    public async Task Line_breaks_are_the_agents_own_and_survive()
    {
        // PROSE, like a person's account and unlike a path. A question laid out
        // over three lines is a question somebody wrote to be read, and
        // flattening it would make the one field a person reads while deciding
        // harder to read.
        var laidOut = new LoopQuestion
        {
            Question = "I am stuck between two rules:\n\n- half-to-even\n- half-up at 2dp",
        };

        await Assert.That(FactCleanliness.Unclean(Envelope(laidOut))).IsNull();
    }

    // ---- S25.3-03 ----

    [Test]
    public async Task It_is_marked_as_the_agents_words_by_its_category()
    {
        // A flight family: it measures the episode rather than a tree or a
        // standing thing, and every work kind that runs a loop can produce one.
        // Categorised rather than vetoed, because there is no subject that
        // could rule out an agent asking a question.
        await Assert.That(FactCategories.Of(FactKinds.LoopQuestion))
            .IsEqualTo(FactCategories.Flight);
    }

    [Test]
    public async Task The_seed_renders_it_as_the_agents_own_words()
    {
        // S25.3-03, and the seed is where it matters most: whoever reads a
        // resumption is picking up work somebody else did, and the one thing
        // they must not do is mistake an assertion for something a machine
        // measured. The seed already marks a person's account that way - "their
        // words, a human assertion" - and this is the same marking for the
        // other kind of assertion in the document.
        var seed = TakeSeedComposer.Compose(
            "GG-42",
            "019fe815-6136-7518-bb57-b06d6d3f411a",
            digest: null,
            account: "I stopped because I could not decide.",
            priorQuestion: new LoopQuestion { Question = Asked });

        var rendered = TakeSeedComposer.Render(seed);

        await Assert.That(rendered).Contains(Asked)
            .Because("a question the next attempt cannot see is a question it asks again.");
        await Assert.That(rendered.Contains("asked", StringComparison.OrdinalIgnoreCase)).IsTrue()
            .Because("the section says what it is. An unlabelled block of prose in a document "
                   + "of measurements reads as a measurement.");
        await Assert.That(rendered.Contains("its own words", StringComparison.OrdinalIgnoreCase)
                       || rendered.Contains("agent", StringComparison.OrdinalIgnoreCase)).IsTrue()
            .Because("marked as an agent's assertion, the way a person's account is marked as "
                   + "theirs - so it never borrows a measurement's authority.");
    }

    [Test]
    public async Task A_seed_with_no_question_renders_no_section_for_one()
    {
        // The liveness twin. A heading printed unconditionally would tell every
        // resuming agent that its predecessor asked something, and it would
        // then go looking for a question that was never asked.
        var seed = TakeSeedComposer.Compose(
            "GG-42", "019fe815-6136-7518-bb57-b06d6d3f411a",
            digest: null, account: "I finished.");

        await Assert.That(TakeSeedComposer.Render(seed)
                .Contains("asked", StringComparison.OrdinalIgnoreCase))
            .IsFalse();
    }

    [Test]
    public async Task The_vocabulary_knows_the_type()
    {
        // The registration a missing entry makes a runtime halt rather than a
        // build error, which is why it is asserted rather than trusted.
        await Assert.That(Vocabulary.Types).Contains(typeof(LoopQuestion));
    }
}
