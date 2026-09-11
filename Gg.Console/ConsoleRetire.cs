using Gg.Client;

namespace Gg.Console;

/// <summary>
/// Retires the names the tree no longer holds, and says per name what became
/// of each.
/// </summary>
/// <remarks>
/// <para>
/// A composition-root function, like <see cref="ConsoleApply"/> and
/// <see cref="ConsolePull"/> beside it.
/// </para>
/// <para>
/// <b>IN LINES, FOR THE APPLY'S REASON.</b> One gated flight per name, so the
/// report is per name by construction — and the row it would otherwise be
/// squeezed into is one row. The outcome modal reads it.
/// </para>
/// <para>
/// <b>NOTHING IS GONE WHEN THIS RETURNS, and every line says so.</b> The
/// retirement door has no 200: a document that stops applying removes every
/// constraint in it at once, so it is a widening by construction and always
/// rides a gate. A report that said "retired" would be describing something
/// that has not happened, and somebody reading it would assume a constraint
/// had stopped attaching while it is still attaching.
/// </para>
/// <para>
/// <b>One name at a time, and a refusal stops the rest.</b> Unlike an apply,
/// these are independent acts rather than one changeset — but a refusal part
/// way through is a state worth stopping in rather than pressing on, because
/// the likeliest refusal is a session or a permission problem that applies to
/// all of them.
/// </para>
/// </remarks>
public static class ConsoleRetire
{
    /// <param name="retire">
    /// The verb, per name, as a function so this can be asserted without a
    /// control plane.
    /// </param>
    /// <param name="names">The names the changeset reported as missing.</param>
    public static IReadOnlyList<string> Retired(
        Func<string, VerbResult> retire, IReadOnlyList<string> names)
    {
        ArgumentNullException.ThrowIfNull(retire);
        ArgumentNullException.ThrowIfNull(names);

        if (names.Count == 0)
        {
            return
            [
                "Nothing to retire: every name this airspace holds still has a document in "
              + "the tree.",
            ];
        }

        var said = new List<string>();

        foreach (var name in names)
        {
            try
            {
                if (retire(name) is not VerbResult.NameRetired retired)
                {
                    said.Add($"{name}: the verb answered something other than a retirement.");
                    continue;
                }

                said.Add(retired.Value.Flight is { Length: > 0 } flight
                    ? $"{name}: flight {flight} awaits {retired.Value.Awaiting} - the name "
                    + $"still governs, in force as {retired.Value.Version}"

                    // NO 200 EXISTS ON THIS DOOR, so this is a control plane
                    // that changed rather than a retirement that finished -
                    // reported plainly instead of being asserted away.
                    : $"{name}: retired with no gate, which this build did not expect - "
                    + $"in force as {retired.Value.Version}");
            }
            catch (Exception refused) when (refused is EnvelopeRefusedException
                                                or NotSignedInException
                                                or ProtocolTooOldException
                                                or HttpRequestException
                                                or IOException
                                                or UnauthorizedAccessException)
            {
                said.Add($"{name}: not retired - " + refused.Message);
                said.Add("");
                said.Add("Stopped here, so any name below this one was not asked about. The "
                       + "likeliest reason a retirement is refused applies to all of them.");

                return said;
            }
        }

        said.Add("");
        said.Add("NOTHING IS GONE YET. Each of these rides a gate, and until it opens the "
               + "name governs every flight that starts.");

        return said;
    }

    /// <summary>The one line the activity slot gets.</summary>
    public static string Summary(IReadOnlyList<string> lines) => ConsoleApply.Summary(lines);
}
