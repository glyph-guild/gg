namespace Gg.Runner.Vcs;

/// <summary>
/// What a proposal is called, from the sentences a landing has to hand.
/// </summary>
/// <remarks>
/// <para>
/// <b>Here rather than inline at the landing, because it is a rule.</b> Three
/// sources ranked, a marker removed and a bound applied is a decision worth
/// being able to read on its own and to test without a flight - the argument
/// <c>RefNamedDestinationAdapter.ProposalUrl</c> already makes one layer down
/// about the one composition that file permits.
/// </para>
/// <para>
/// <b>The agent's account first, and it was never consulted.</b> A landing had
/// the admission's <c>Reason</c> in scope and used it: that member answers
/// <i>why was this allowed to land</i>, written for an audit trail from a
/// destination id and its obligations. Pull request 8629 was called
/// <i>"GG-118: Destination 'pull-request' requires 'in-scope', and it holds."</i>
/// </para>
/// <para>
/// <b>The account arrives shaped for a different job.</b>
/// <c>ExecutorPort.Clean</c> cuts a reason to the first paragraph and 280
/// characters and marks the cut, and that budget's own docstring defends it as
/// <i>the row somebody reads first</i>. A title is not a reason with a smaller
/// number on it, so the marker comes off and the first sentence is taken.
/// </para>
/// <para>
/// <b>Three tiers now, and the top one is the point.</b> An agent granted
/// <c>propose-landing</c> states a title through this platform's own tool, and
/// that is what a person reads. The account below it is what a flight gets when
/// the envelope withheld the move or the agent never called it - better than a
/// verdict, and still whatever the agent happened to write first.
/// </para>
/// </remarks>
public static class LandingTitle
{
    /// <summary>The most a title may be, before it stops being one.</summary>
    /// <remarks>
    /// Comfortably inside what either provider accepts, and chosen for the
    /// person rather than the api: a list of proposals is read at a glance and
    /// a title that wraps twice is one nobody finishes.
    /// </remarks>
    public const int MaxLength = 100;

    /// <summary>What <c>ExecutorPort.Clean</c> appends where it cut.</summary>
    /// <remarks>
    /// <b>Matched rather than shared.</b> The suffix lives inside a recorded
    /// string and therefore inside that fact's hash; importing the constant
    /// would tie a display rule to bytes that must not move. If the two ever
    /// disagree the title keeps the marker, which is visible and harmless -
    /// the failure mode of the alternative is a title cut at the wrong place.
    /// </remarks>
    private const string Marker = "… (the rest is in the transcript)";

    /// <summary>
    /// The title for one landing.
    /// </summary>
    /// <param name="flightNumber">What ties the proposal back to a record.</param>
    /// <param name="runReason">The agent's own account, or null when no loop ran.</param>
    /// <param name="fallback">The admission's sentence: poor, and never false.</param>
    /// <param name="proposed">
    /// What the agent asked for through the tool, or null when it was not
    /// granted the move or never called it.
    /// </param>
    /// <remarks>
    /// <b>The flight number leads all three.</b> An agent asked for wording, not
    /// for the record - and a proposal nobody can trace back to a flight is a
    /// branch nobody will ever delete.
    /// </remarks>
    public static string For(
        string flightNumber, string? runReason, string fallback,
        Gg.Contracts.LandingProposal? proposed = null)
    {
        // WHAT THE AGENT CHOSE, and the contract has already refused anything
        // that is not a title - one line, bounded, not blank - before it could
        // reach here, because the extractor throws on an answered call carrying
        // one the contract refuses.
        if (proposed?.Title is { Length: > 0 } stated)
        {
            return $"{flightNumber}: {stated.Trim()}";
        }

        var said = Sentence(runReason);

        return $"{flightNumber}: {(said is { Length: > 0 } ? said : fallback)}";
    }

    /// <summary>The first sentence of an account, bounded, or null.</summary>
    private static string? Sentence(string? reason)
    {
        var text = (reason ?? "").Trim();

        // A MARKER ABOUT THE RECORD, not part of what was said. In a title it
        // reads as an unfinished thought and points at a file the reader of a
        // pull request cannot open.
        var marked = text.IndexOf(Marker, StringComparison.Ordinal);
        if (marked >= 0)
        {
            text = text[..marked].TrimEnd();
        }

        if (text.Length == 0)
        {
            // EMPTY IS NOT SHORT. A loop that said nothing gives a title of
            // just the flight number, which says less than the fallback does.
            return null;
        }

        // A SENTENCE END IS A TERMINATOR FOLLOWED BY A SPACE, which is what
        // keeps `AskUserQuestionToolTests.cs:659` and `v1.2` from being ends.
        // A real summary ended `never reassigned.…`, where the period is
        // followed by the marker rather than by a space - so this finding one
        // is the ordinary case rather than the reliable one, and the bound
        // below is what actually holds most titles.
        var end = text.Length;
        for (var i = 0; i < text.Length - 1; i++)
        {
            if (text[i] is '.' or '!' or '?' && char.IsWhiteSpace(text[i + 1]))
            {
                end = i + 1;
                break;
            }
        }

        var first = text[..end].TrimEnd();

        if (first.Length <= MaxLength)
        {
            return first;
        }

        // CUT ON A WORD. Mid-word is the thing that reads as broken rather than
        // as long, and the ellipsis is the same courtesy the reason's own cut
        // pays: somebody has to be able to tell there was more.
        var space = first.LastIndexOf(' ', MaxLength - 1);

        return (space > 0 ? first[..space] : first[..(MaxLength - 1)]).TrimEnd(' ', ',', ';', ':') + "…";
    }
}
