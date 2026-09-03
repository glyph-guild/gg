using Gg.Contracts;
using Gg.Runner.Execution;

namespace Gg.Runner.Tests;

/// <summary>
/// A shortened reason says it was shortened, however it was shortened.
/// </summary>
/// <remarks>
/// <para>
/// <b>The rule is right and one of its two cuts breaks it.</b>
/// <c>ExecutorPort</c> keeps a reason short on purpose — <i>"it is the row
/// somebody reads first… a real agent's closing summary runs to paragraphs with
/// code blocks in it; that is the transcript's job"</i> — and says the cut must be
/// visible: <i>"the cut is marked rather than silent — somebody has to be able to
/// tell there was more."</i>
/// </para>
/// <para>
/// <b>Only the length cut is marked.</b> Cutting at the first paragraph break
/// drops everything after it and says nothing, so a reason that was one paragraph
/// of many is indistinguishable from a reason that was complete.
/// </para>
/// <para>
/// <b>Found on a live flight, in its worst form.</b> A blocked agent wrote a
/// lead-in ending in a colon and then listed what blocked it. What the control
/// plane recorded was the lead-in alone:
/// <i>"I'm blocked before I can start — two independent blockers, neither of which
/// I can work around:"</i> — which reads as a complete, if unhelpful, answer. The
/// two blockers were only in a transcript file on the runner host, and finding
/// them took an SSH session.
/// </para>
/// </remarks>
public class EveryCutIsMarkedTests
{
    /// <summary>The reason as it reaches a fact, for a given closing summary.</summary>
    private static string ReasonFor(string summary) =>
        ExecutorRun.Completed(
            "implement", summary, attempts: 1, took: TimeSpan.FromSeconds(1),
            movesUsed: [LoopMoves.Read]).Reason;

    [Test]
    public async Task A_reason_cut_at_a_paragraph_says_there_was_more()
    {
        // THE DEFECT, in the shape that produced it: a lead-in, then the
        // substance. Dropping the substance silently is worse than dropping it
        // loudly, because the lead-in reads like an answer.
        var reason = ReasonFor(
            "I'm blocked before I can start — two independent blockers:\n\n"
          + "**1. There is no repository in this working tree.**\n\n"
          + "**2. I can't read the work item.**");

        await Assert.That(reason).Contains("transcript")
            .Because("the file's own rule is that the cut is marked rather than silent, and a "
                   + "paragraph cut drops more than a length cut ever does.");
    }

    [Test]
    public async Task A_reason_short_enough_to_stand_alone_is_left_exactly_as_written()
    {
        // THE ANCHOR. Most reasons are one line and complete, and marking those
        // would put a footnote on every flight that went fine.
        const string Whole = "Edited src/orders.py and the tests pass.";

        await Assert.That(ReasonFor(Whole)).IsEqualTo(Whole);
    }

    [Test]
    public async Task A_reason_cut_for_length_still_says_so()
    {
        // Unchanged behaviour, asserted so fixing the paragraph cut cannot
        // quietly take the marker off the one that already had it.
        await Assert.That(ReasonFor(new string('x', ExecutorPort_MaxReasonLength + 50)))
            .Contains("transcript");
    }

    [Test]
    public async Task The_reason_still_fits_the_row_it_is_read_in()
    {
        // The bound is the whole point of shortening. A marker that pushed a
        // reason past it would trade one defect for another.
        var reason = ReasonFor(new string('x', 400) + "\n\nand more after that");

        await Assert.That(reason.Length).IsLessThanOrEqualTo(
            ExecutorPort_MaxReasonLength + 40)
            .Because("280 characters plus a short marker is still a row somebody reads first.");
    }

    /// <summary>Mirrors the constant so the tests read at the size they assert.</summary>
    private const int ExecutorPort_MaxReasonLength = 280;
}
