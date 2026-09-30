using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// A leg's sentence is cut to its cell, so the columns after it survive.
/// </summary>
/// <remarks>
/// <b>The table cannot do this itself.</b> Its style sets
/// <c>ExpandLastColumn</c> and no horizontal scroll bar, so a long cell in a
/// MIDDLE column takes the width it wants and pushes `state`, `flight` and
/// `since` off the pane. Measured on the first real itinerary, whose two legs
/// each ran past a hundred characters: the pane showed the sentences and hid
/// everything they were supposed to sit beside.
/// </remarks>
public class ALegsSentenceIsCutToFitTests
{
    private const string Plan = "itinerary:a2720c15-8575-702e-9300-7fca06fa0cde";

    private static AppState Showing(string reason) => new()
    {
        Itineraries = new BoardPage
        {
            Nominations =
            [
                new NominationSummary
                {
                    NominationId = Guid.NewGuid(),
                    Nominator = Plan,
                    Subject = "leg:implement@d0a04631313809f9",
                    Reason = reason,
                    Version = "abcdef",
                    WorkKind = "implement",
                    Mode = "auto",
                    State = NominationStates.Standing,
                    ItineraryNumber = "ITN-1",
                    MadeAt = DateTimeOffset.UnixEpoch,
                },
            ],
            IncludedEnded = true,
        },
        ItinerariesSelected = 0,
    };

    [Test]
    public async Task A_long_sentence_is_cut_and_says_it_was()
    {
        // The real one, verbatim off the first itinerary that ever minted.
        var leg = Rows.ItineraryLegs(Showing(
            "This is the core Phase 1 scope the item describes as one user-facing "
          + "capability: Admin (JDX) and Admin (JDX+) can cancel a workflow.")).Single();

        await Assert.That(leg.Reason.Length).IsEqualTo(Rows.LegReasonFits)
            .Because("the cell has a width and the sentence has to live inside it, or the "
                   + "three columns after it are drawn past the edge of the pane.");

        await Assert.That(leg.Reason).EndsWith("…")
            .Because("a cut nobody can see is a sentence a person will quote back as though "
                   + "it were the whole of what the agent wrote.");
    }

    [Test]
    public async Task A_short_sentence_is_left_exactly_alone()
    {
        var leg = Rows.ItineraryLegs(Showing("the parser")).Single();

        await Assert.That(leg.Reason).IsEqualTo("the parser")
            .Because("padding or an ellipsis on something that already fits would be this "
                   + "console editing what the agent said for no reason.");
    }

    [Test]
    public async Task A_sentence_with_a_line_break_stays_one_line()
    {
        // ORDINARY RATHER THAN RARE: the reason is prose an agent wrote, and
        // an agent writes paragraphs. A break drawn into a table cell paints
        // over the row beneath it.
        var leg = Rows.ItineraryLegs(Showing("first line\nsecond line")).Single();

        await Assert.That(leg.Reason).DoesNotContain("\n");
        await Assert.That(leg.Reason).IsEqualTo("first line second line");
    }

    [Test]
    public async Task A_leg_with_no_reason_is_blank_rather_than_an_ellipsis()
    {
        var leg = Rows.ItineraryLegs(Showing("")).Single();

        await Assert.That(leg.Reason).IsEmpty()
            .Because("absent is 'nobody said', and an ellipsis there would claim there were "
                   + "words that did not fit.");
    }

    [Test]
    public async Task The_cut_does_not_leave_a_space_before_the_ellipsis()
    {
        var leg = Rows.ItineraryLegs(Showing(new string('a', 40) + "     tail")).Single();

        await Assert.That(leg.Reason).DoesNotContain(" …");
    }
}
