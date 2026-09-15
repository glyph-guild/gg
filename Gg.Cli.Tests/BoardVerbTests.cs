using Gg.Client;
using Gg.Contracts;

namespace Gg.Cli.Tests;

/// <summary>
/// `gg board` shows what is waiting to become a flight, and answers one.
/// </summary>
/// <remarks>
/// <para>
/// <b>The same door the console uses, so the two cannot disagree about what a
/// decision is.</b> A CLI that posted its own shape would be a second way to
/// open a nomination, and the second way is always the one that skips a rule —
/// which is `gg decide`'s argument one noun later, and the reason there is no
/// `gg approve`.
/// </para>
/// <para>
/// <b>THE REASON IS A REQUIRED ARGUMENT, on `gg ground`'s shape.</b> The door
/// refuses a blank one, so an optional flag here would only move the refusal to
/// a round trip later — and the sentence it would refuse with is about a field,
/// where this one can say what the reason is for. A nomination somebody opened
/// or declined with no sentence tells whoever finds it that a person decided
/// and nothing about what they were thinking.
/// </para>
/// <para>
/// <b>Two words, and they are the two a person can cause.</b> Superseding is
/// the board's, withdrawal is the world's, lapsing is the clock's and refusal
/// is the rules'; a verb that accepted one of those would let somebody record
/// that the clock did what they did. So the outcome is not a free string here
/// any more than it is at the door.
/// </para>
/// </remarks>
public class BoardVerbTests
{
    private const string Id = "01a0792a-5e1f-7030-a5d8-52fd66e510b0";

    private static NominationSummary AStandingRow() => new()
    {
        NominationId = Guid.Parse(Id),
        Nominator = "flight:019260e0-1f6d-7a1e-9b53-6f2f4c9d0a11",
        Subject = "flight:019260e0-1f6d-7a1e-9b53-6f2f4c9d0a11",
        Version = "an-idempotency-key",
        WorkKind = "research-27",
        Mode = DestinationOpening.Gated,
        State = NominationStates.Standing,
        MadeAt = new DateTimeOffset(2026, 9, 15, 9, 0, 0, TimeSpan.Zero),
        IntentKey = "https://example.test/issues/41",
    };

    [Test]
    public async Task The_board_is_a_verb_with_no_arguments()
    {
        var parsed = CliArgs.Parse(["board"]);

        await Assert.That(parsed).IsTypeOf<CliAction.Board>();
        await Assert.That(((CliAction.Board)parsed).Ended).IsFalse()
            .Because("what needs somebody is what a person came to see, and a page that "
                   + "included every superseded row would bury it.");
    }

    [Test]
    public async Task Ended_rows_are_asked_for_rather_than_filtered_out_afterwards()
    {
        var parsed = (CliAction.Board)CliArgs.Parse(["board", "--all"]);

        await Assert.That(parsed.Ended).IsTrue()
            .Because("the page says whether it included them, so asking is the only way to "
                   + "tell 'nothing was declined' from 'declined rows were not shown'.");
    }

    [Test]
    public async Task Opening_and_declining_both_take_an_id_and_a_sentence()
    {
        var opened = (CliAction.BoardDecide)CliArgs.Parse(
            ["board", "open", Id, "this one blocks the release"]);

        await Assert.That(opened.Nomination).IsEqualTo(Id);
        await Assert.That(opened.Outcome).IsEqualTo(NominationEndings.Opened);
        await Assert.That(opened.Because).IsEqualTo("this one blocks the release");

        var declined = (CliAction.BoardDecide)CliArgs.Parse(
            ["board", "decline", Id, "not this week"]);

        await Assert.That(declined.Outcome).IsEqualTo(NominationEndings.Declined);
        await Assert.That(declined.Because).IsEqualTo("not this week");
    }

    [Test]
    public async Task The_verb_carries_the_ending_word_rather_than_a_word_of_its_own()
    {
        // ONE VOCABULARY. `open` and `decline` are what a person types; what
        // travels is the ending the row will carry, and minting a second pair
        // of words here would be two spellings to keep agreeing - the day they
        // stop is the day the CLI sends a word no row can record.
        var opened = (CliAction.BoardDecide)CliArgs.Parse(["board", "open", Id, "why"]);

        await Assert.That(NominationDecisions.All).Contains(opened.Outcome);
    }

    [Test]
    public async Task A_decision_with_no_sentence_is_refused_before_it_is_sent()
    {
        var refusal = ((CliAction.Unknown)CliArgs.Parse(["board", "open", Id])).Message;

        await Assert.That(refusal).Contains("why")
            .Because("the door refuses a blank reason, and a person who typed the wrong "
                   + $"thing should be told by the thing they typed it into. Said: {refusal}");
    }

    [Test]
    public async Task A_word_a_person_cannot_cause_is_not_a_board_verb()
    {
        var refusal = ((CliAction.Unknown)CliArgs.Parse(
            ["board", "lapse", Id, "it sat too long"])).Message;

        await Assert.That(refusal).Contains("open");
        await Assert.That(refusal).Contains("decline")
            .Because("the refusal names what a person CAN cause, because whoever typed the "
                   + $"other word has to decide which half was wrong. Said: {refusal}");
    }

    [Test]
    public async Task A_nomination_is_named_by_the_id_the_board_prints()
    {
        // NOT A FLIGHT REFERENCE. A nomination has no number and may never have
        // a flight, so there is no GG-42 for it - and accepting one here would
        // send a string the door cannot resolve and answer 404 for a reason
        // that reads like the row is gone.
        var refusal = ((CliAction.Unknown)CliArgs.Parse(
            ["board", "open", "GG-42", "because"])).Message;

        await Assert.That(refusal).Contains("id")
            .Because($"the message has to say what to type instead. Said: {refusal}");
    }

    [Test]
    public async Task It_is_in_the_usage_beside_the_verbs_that_answer_things()
    {
        var usage = ((CliAction.Unknown)CliArgs.Parse(["nonsense"])).Message;

        await Assert.That(usage).Contains("gg board");
    }

    [Test]
    public async Task A_standing_row_renders_as_a_row_and_says_what_it_waits_for()
    {
        var text = VerbOutput.ToText(new VerbResult.Board(new BoardPage
        {
            Nominations = [AStandingRow()],
            IncludedEnded = false,
        }));

        await Assert.That(text).Contains("research-27");
        await Assert.That(text).Contains(DestinationOpening.Gated)
            .Because("gated is the word that says this one is waiting for a person, and it "
                   + "is the only thing on the line a reader can act on.");
        await Assert.That(text).Contains(Id[..8])
            .Because("the id is what `gg board open` takes, so the listing has to print "
                   + "enough of it to type.");
        await Assert.That(text).Contains("https://example.test/issues/41")
            .Because("the work kind says which rules would apply and this says which piece "
                   + "of work - and a board is a list somebody scans to choose.");
    }

    [Test]
    public async Task A_row_about_free_text_says_so_rather_than_leaving_the_line_out()
    {
        // ABSENT IS AN ANSWER, SO IT IS SAID. A missing line reads as a row
        // that forgot to say what it is about; "free text" says that nobody
        // can name the thing, which is true of the intent and also happens to
        // be why nothing can count it.
        var text = VerbOutput.ToText(new VerbResult.Board(new BoardPage
        {
            Nominations = [AStandingRow() with { IntentKey = null }],
            IncludedEnded = false,
        }));

        await Assert.That(text).Contains("free text");
    }

    [Test]
    public async Task An_ended_row_shows_its_ending_and_the_sentence_with_it()
    {
        var text = VerbOutput.ToText(new VerbResult.Board(new BoardPage
        {
            Nominations =
            [
                AStandingRow() with
                {
                    State = NominationEndings.Declined,
                    Ending = NominationEndings.Declined,
                    Because = "we are not spending a flight on this one",
                    EndedAt = new DateTimeOffset(2026, 9, 15, 10, 0, 0, TimeSpan.Zero),
                },
            ],
            IncludedEnded = true,
        }));

        await Assert.That(text).Contains(NominationEndings.Declined);
        await Assert.That(text).Contains("we are not spending a flight on this one")
            .Because("the sentence is the only place the reason exists, and whoever reads an "
                   + "ended row is asking why rather than what.");
    }

    [Test]
    public async Task An_empty_board_answers_rather_than_printing_a_header_over_nothing()
    {
        var text = VerbOutput.ToText(new VerbResult.Board(new BoardPage
        {
            Nominations = [],
            IncludedEnded = false,
        }));

        await Assert.That(text).DoesNotContain("nominator")
            .Because("a header over nothing reads as a query that failed, which is the "
                   + "answer `gg gates` gives one noun later.");
        await Assert.That(text).IsNotEmpty();
    }
}
