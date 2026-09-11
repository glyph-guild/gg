using Gg.Client;

namespace Gg.Console;

/// <summary>
/// Applies the working copy, and says per document what became of it.
/// </summary>
/// <remarks>
/// <para>
/// A composition-root function, like <see cref="ConsolePull"/> beside it.
/// </para>
/// <para>
/// <b>Per document, because that is what happened.</b> Apply is one flight per
/// changed document — one gate, one minted version, one attribution — so a
/// single "applied" line would collapse the two outcomes a person most needs
/// told apart: what landed, and what is now waiting on somebody. Unlike a pull,
/// which is counted, this is named.
/// </para>
/// <para>
/// <b>The refusals are the control plane's sentences, and one of them is
/// local.</b> An unreadable file stops every document before anything is sent,
/// because applying the rest would land part of a changeset somebody meant as a
/// whole; it arrives as a refusal naming the paths, and it is rendered rather
/// than reported.
/// </para>
/// </remarks>
public static class ConsoleApply
{
    /// <summary>
    /// What the apply came to, in lines, for a modal somebody reads.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>LINES, BECAUSE IT USED TO BE ONE ROW AND THE ROW LOST THE
    /// REMEDY.</b> This joined its per-document results with "; " and ran
    /// multi-line refusals through a flattener, because the activity slot was
    /// the only place it went. A refused apply then read "Nothing was applied:"
    /// with the reason off the right edge - and the reason is the whole of what
    /// somebody does next. Measured in the world: an apply refused for an
    /// undeclared name, twice reported as a key that did nothing.
    /// </para>
    /// <para>
    /// <b>The row still exists and still gets one line</b> - see
    /// <see cref="Summary"/> - but it is a summary of this rather than this
    /// squeezed into it.
    /// </para>
    /// </remarks>
    /// <param name="apply">
    /// The verb, as a function so this can be asserted without a control plane.
    /// </param>
    public static IReadOnlyList<string> Applied(Func<VerbResult> apply)
    {
        ArgumentNullException.ThrowIfNull(apply);

        try
        {
            if (apply() is not VerbResult.AirspaceApplied applied)
            {
                return ["Nothing was applied: the verb answered something other than an apply."];
            }

            if (applied.Value.Applied.Count == 0 && applied.Value.Retiring.Count == 0)
            {
                return ["Nothing to apply: the working copy matches the airspace."];
            }

            var said = new List<string>();

            foreach (var document in applied.Value.Applied)
            {
                // THE DIVERT FIRST, because it is the one a person has to act
                // on. Nothing was minted and the name still holds its old
                // version, so reporting it as applied would send them looking
                // for a change that is waiting at a gate.
                said.Add(document.Flight is { Length: > 0 } flight
                    ? $"{document.Name} widens {document.Widens} - {flight} awaits "
                    + $"{document.Awaiting}"
                    : document.Changed
                        ? $"{document.Name} applied as {document.Version}"
                        : $"{document.Name} unchanged, still {document.Version}");
            }

            // AN INTENT, NOT AN ACT. There is no delete verb: retiring a name
            // is applying a terminal version of it, which is its own gated
            // change - so a missing file is reported and never performed.
            foreach (var name in applied.Value.Retiring)
            {
                said.Add($"{name} is missing from the tree, which is its own gated change");
            }

            return said;
        }
        catch (EnvelopeRefusedException refused)
        {
            // THE CONTROL PLANE'S OWN SENTENCES, LINE FOR LINE. One bad file
            // stops every document and the paths are in the message; an
            // undeclared name is refused with the command that declares one.
            // Both were being run together into a row that showed the first
            // eighty columns.
            return Refused(refused.Message);
        }
        catch (Exception refused) when (refused is NotSignedInException
                                            or ProtocolTooOldException
                                            or HttpRequestException
                                            or IOException
                                            or UnauthorizedAccessException)
        {
            return Refused(refused.Message);
        }
    }

    /// <summary>A refusal, as its own lines under a heading.</summary>
    /// <remarks>
    /// <b>The heading is separate so the message keeps its own first line.</b>
    /// Prefixing "Nothing was applied: " onto a multi-line diagnosis buried
    /// the diagnosis's opening sentence in the middle of a row.
    /// </remarks>
    private static IReadOnlyList<string> Refused(string message) =>
    [
        NothingApplied,
        "",
        .. message
            .ReplaceLineEndings("\n")
            .Split('\n')
            .Select(line => line.TrimEnd())
            .SkipWhile(line => line.Length == 0),
    ];

    /// <summary>The one line the activity slot gets.</summary>
    /// <remarks>
    /// <para>
    /// <b>A summary, not a squeeze.</b> The slot is one row and that was never
    /// the problem - putting a whole report in it was. This answers the first
    /// line and nothing more, because the first line is the only part written
    /// to stand alone.
    /// </para>
    /// <para>
    /// <b>Bounded, and a test holds the bound</b> against the narrowest screen
    /// this console supports. Without it the same defect returns the first time
    /// somebody appends a clause here.
    /// </para>
    /// </remarks>
    public static string Summary(IReadOnlyList<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var first = lines.FirstOrDefault(line => line.Length > 0) ?? "";

        // THE REPORT IS THE PLACE TO READ IT, so a long first line is cut
        // rather than wrapped - a wrap here would be two rows in a one-row
        // slot, which is the defect wearing a different hat.
        return first.Length <= Fits ? first : first[..(Fits - 1)].TrimEnd() + "…";
    }

    /// <summary>What a refused apply always says first.</summary>
    private const string NothingApplied = "Nothing was applied.";

    /// <summary>
    /// How wide the activity row may be taken to be.
    /// </summary>
    /// <remarks>
    /// <c>PaneText.QuestionColumns</c>'s number and its reason: the narrowest
    /// screen anybody here has is eighty columns, and this leaves room for the
    /// chosen-repository clause the slot also carries.
    /// </remarks>
    private const int Fits = 64;

}
