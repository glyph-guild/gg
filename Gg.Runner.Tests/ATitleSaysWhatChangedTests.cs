using Gg.Runner.Vcs;

namespace Gg.Runner.Tests;

/// <summary>
/// What a pull request is called, and where the sentence comes from.
/// </summary>
/// <remarks>
/// <para>
/// <b>Three sources, in the order a reader would want them.</b> The agent's own
/// account of what it did; then, when there was no loop, the admission's
/// sentence, which at least never claims anything false; and the flight number
/// in front of either, because that is what ties a proposal back to a record.
/// </para>
/// <para>
/// <b>The agent's account arrives shaped for a different job.</b>
/// <c>ExecutorPort.Clean</c> cuts it to the first paragraph and 280 characters
/// and marks the cut - a budget whose own docstring defends it as <i>the row
/// somebody reads first</i>. So the marker comes off and the first sentence is
/// taken: a title is not a reason with a smaller number on it.
/// </para>
/// </remarks>
public class ATitleSaysWhatChangedTests
{
    private const string Flight = "GG-118";

    private const string Verdict =
        "Destination 'pull-request' requires 'in-scope', and it holds.";

    [Test]
    public async Task It_is_what_the_agent_said_it_did()
    {
        // GG-118, exactly. Both sentences were in scope at the landing and the
        // one describing the change was the one not used.
        var title = LandingTitle.For(
            Flight,
            "Removed the last explicitly-typed local in the unit tests. It was added by "
          + "AB#18517 after the earlier sweep.",
            Verdict);

        await Assert.That(title)
            .IsEqualTo("GG-118: Removed the last explicitly-typed local in the unit tests.");
    }

    [Test]
    public async Task The_transcript_marker_is_not_part_of_the_sentence()
    {
        // `… (the rest is in the transcript)` is a note about the RECORD, added
        // where the reason was cut. In a title it reads as an unfinished
        // thought and points at a file the reader cannot open.
        var title = LandingTitle.For(
            Flight,
            "Scoped the retry to the transient cases… (the rest is in the transcript)",
            Verdict);

        await Assert.That(title).DoesNotContain("transcript");
        await Assert.That(title).IsEqualTo("GG-118: Scoped the retry to the transient cases");
    }

    [Test]
    public async Task A_sentence_that_runs_on_is_cut_on_a_word()
    {
        // A summary with no sentence end in it is ordinary - a real one ended
        // `never reassigned.…`, where the period is followed by the marker
        // rather than by a space. Cutting mid-word is the thing that reads as
        // broken rather than as long.
        var title = LandingTitle.For(Flight, new string('a', 40) + " " + new string('b', 200), Verdict);

        await Assert.That(title.Length).IsLessThanOrEqualTo(LandingTitle.MaxLength);
        await Assert.That(title).EndsWith("…");
        await Assert.That(title).DoesNotContain("bbb");
    }

    [Test]
    public async Task A_flight_with_no_loop_keeps_the_sentence_that_was_true()
    {
        // A push-and-preserve flight runs no loop, so there is no account of a
        // change to name it by. The admission's sentence is a poor title and an
        // honest one, and inventing something here would be the worse trade.
        var title = LandingTitle.For(Flight, runReason: null, Verdict);

        await Assert.That(title).IsEqualTo($"{Flight}: {Verdict}");
    }

    [Test]
    public async Task A_loop_that_said_nothing_falls_through_rather_than_titling_a_blank()
    {
        // An empty reason is not a short one. A title of just the flight number
        // says less than the verdict does.
        var title = LandingTitle.For(Flight, "   ", Verdict);

        await Assert.That(title).IsEqualTo($"{Flight}: {Verdict}");
    }

    [Test]
    public async Task The_flight_number_leads_it_whichever_sentence_won()
    {
        // The one thing every title has to carry: a proposal nobody can trace
        // back to a flight is a branch nobody will ever delete.
        await Assert.That(LandingTitle.For(Flight, "Did the thing.", Verdict)).StartsWith(Flight);
        await Assert.That(LandingTitle.For(Flight, null, Verdict)).StartsWith(Flight);
    }
}
