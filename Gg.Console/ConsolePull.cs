using Gg.Client;

namespace Gg.Console;

/// <summary>
/// Renders the estate into the working copy, and says what that came to in one
/// line.
/// </summary>
/// <remarks>
/// <para>
/// A composition-root function, like <see cref="ConsoleEstate"/>: the loop is
/// handed something it can call and never a write surface.
/// </para>
/// <para>
/// <b>Counts rather than a file list.</b> A whole estate is dozens of files and
/// the activity line is one line — and the pane below already lists the
/// documents, so repeating them here would push the thing a person came to read
/// off the screen.
/// </para>
/// <para>
/// <b>Two exceptions are answers rather than faults.</b> A dirty tree is git
/// behaving like git, and a refused read is the control plane saying no; both
/// carry sentences a person acts on, so they are rendered rather than reported.
/// Anything else is left to throw, because a console that caught everything
/// would turn a bug into a shrug.
/// </para>
/// </remarks>
public static class ConsolePull
{
    /// <summary>
    /// What the pull came to, in lines, for a modal somebody reads.
    /// </summary>
    /// <remarks>
    /// <b>LINES FOR THE APPLY'S REASON.</b> A pull's ordinary answer is a
    /// count and fits a row; its REFUSALS do not. It now declines to render
    /// over unapplied work and names every document, and the dirty refusal
    /// names every file — both of which ran off the right edge of a one-row
    /// slot, which is where the remedy lives.
    /// </remarks>
    /// <param name="pull">
    /// The verb, as a function so this can be asserted without a control plane
    /// — the same reason <c>ConsoleLoop.Opened</c> is public.
    /// </param>
    public static IReadOnlyList<string> Pulled(Func<VerbResult> pull)
    {
        ArgumentNullException.ThrowIfNull(pull);

        try
        {
            if (pull() is not VerbResult.AirspacePulled pulled)
            {
                return ["Nothing was pulled: the verb answered something other than a pull."];
            }

            var written = pulled.Value.Written.Count;
            var removed = pulled.Value.Removed.Count;

            // A NAME NO PATH CAN CARRY IS NAMED IN FULL, and it is the one
            // thing here that is not a count. An estate declared before the
            // name rule existed can hold one, and a file that is silently
            // absent is worse than a name that is named.
            var unwritable = pulled.Value.Unrepresentable.Count > 0
                ? " Not written, because no path can carry the name: "
                + string.Join(", ", pulled.Value.Unrepresentable) + "."
                : "";

            if (written == 0 && removed == 0)
            {
                return
                [
                    "The working copy already matches the airspace; nothing was written."
                  + unwritable,
                ];
            }

            // REMOVALS ARE SAID EVEN WHEN THERE ARE NONE OF THEM TO SAY, when
            // something was written: a document whose stream ended leaves a
            // file, and somebody who is not told one went will look for it
            // later.
            return [$"Pulled the airspace: {written} written, {removed} removed." + unwritable];
        }
        catch (DirtyWorkingCopyException dirty)
        {
            // GIT BEHAVING LIKE GIT. The files are what a person acts on -
            // commit them or discard them - and a refusal that said only "the
            // tree is dirty" would leave them hunting for which. A file per
            // line, because that is what a person reads down.
            return
            [
                "Nothing was pulled: these are uncommitted, and pull would overwrite them.",
                "",
                .. dirty.Paths.Select(path => "  " + path),
                "",
                "Commit or discard them first.",
            ];
        }
        catch (Exception refused) when (refused is NotSignedInException
                                            or ProtocolTooOldException
                                            or EnvelopeRefusedException
                                            or HttpRequestException
                                            or IOException
                                            or UnauthorizedAccessException)
        {
            return
            [
                "Nothing was pulled.",
                "",
                .. refused.Message
                    .ReplaceLineEndings("\n")
                    .Split('\n')
                    .Select(line => line.TrimEnd())
                    .SkipWhile(line => line.Length == 0),
            ];
        }
    }

    /// <summary>The one line the activity slot gets.</summary>
    /// <remarks>
    /// <b>ConsoleApply's, so the two reports are trimmed by one rule.</b> The
    /// bound it checks against is the narrowest screen this console supports,
    /// and a second copy of that arithmetic is a second thing to keep true.
    /// </remarks>
    public static string Summary(IReadOnlyList<string> lines) => ConsoleApply.Summary(lines);
}
