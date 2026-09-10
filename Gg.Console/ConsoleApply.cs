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
    /// <summary>What the apply came to, as one line for the activity slot.</summary>
    /// <param name="apply">
    /// The verb, as a function so this can be asserted without a control plane.
    /// </param>
    public static string Applied(Func<VerbResult> apply)
    {
        ArgumentNullException.ThrowIfNull(apply);

        try
        {
            if (apply() is not VerbResult.AirspaceApplied applied)
            {
                return "Nothing was applied: the verb answered something other than an apply.";
            }

            if (applied.Value.Applied.Count == 0 && applied.Value.Retiring.Count == 0)
            {
                return "Nothing to apply: the working copy matches the airspace.";
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

            return string.Join("; ", said);
        }
        catch (EnvelopeRefusedException refused)
        {
            // ONE BAD FILE STOPS EVERY DOCUMENT, and the paths are in the
            // sentence the control plane or the tree read composed. A person
            // told only that the apply failed has to go and find which.
            return "Nothing was applied: " + Flatten(refused.Message);
        }
        catch (Exception refused) when (refused is NotSignedInException
                                            or ProtocolTooOldException
                                            or HttpRequestException
                                            or IOException
                                            or UnauthorizedAccessException)
        {
            return "Nothing was applied: " + Flatten(refused.Message);
        }
    }

    /// <summary>
    /// One line, because the activity slot is one line.
    /// </summary>
    /// <remarks>
    /// The unreadable-file refusal is composed multi-line - a heading and then
    /// a path per line - which is right for a terminal verb and wrong for a
    /// status row. Flattened rather than truncated, so the paths survive: they
    /// are the part somebody acts on.
    /// </remarks>
    private static string Flatten(string message) =>
        string.Join(" ", message.Split(
            ['\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}
