using Gg.Contracts;

namespace Gg.Console;

/// <summary>
/// The badge in the top right: which gg this is, and whether a newer one
/// exists.
/// </summary>
/// <remarks>
/// <para>
/// <b>Pure, and outside the view, because the view cannot be constructed
/// without a terminal.</b> That is the lesson <c>ConsoleTheme</c> carries
/// about colours mixed into <c>ConsoleScreen</c> - they were beyond the reach
/// of any test and shipped inverted. The same is true of a string somebody has
/// to read.
/// </para>
/// <para>
/// <b>The control plane decides what "behind" means, not this.</b> It holds
/// <c>Gg:CurrentVersion</c> and sends a <c>binary</c> notice when a caller is
/// behind; a console comparing versions itself would be a second answer to
/// that question, and the two would disagree the first time somebody moved
/// one. What is here is only whether such a notice arrived.
/// </para>
/// <para>
/// <b>And it stays advisory.</b> <c>binary</c> is on
/// <c>TenantNoticeCodes.AdvisoryOnly</c> - being behind is reported and may
/// never stop a person working - so the corner says so and does nothing else.
/// </para>
/// </remarks>
public static class Corner
{
    /// <summary>Whether the control plane has said a newer gg exists.</summary>
    public static bool UpdateWaiting(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return state.Notices.Any(
            notice => string.Equals(
                notice.Code, TenantNoticeCodes.Binary, StringComparison.Ordinal));
    }

    /// <summary>What the corner reads.</summary>
    /// <remarks>
    /// <b>A space either side</b>, because the badge sits on the frame's own
    /// row and letters touching a border read as damage to it.
    /// </remarks>
    public static string Badge(string informational, bool updateWaiting)
    {
        ArgumentNullException.ThrowIfNull(informational);

        var waiting = updateWaiting ? " · update" : "";

        return $" gg {Stamped(informational)}{waiting} ";
    }

    /// <summary>
    /// The version, and the first eight of the commit behind it.
    /// </summary>
    /// <remarks>
    /// <b>Every build carries a commit, released or not</b> - measured: a
    /// released 0.48.0 binary reports <c>+50b2518c…</c> exactly as a local
    /// build does - so there is nothing here to tell a development build from
    /// a release and this does not pretend otherwise. Eight characters because
    /// forty is noise in a corner and eight is enough to find the commit.
    /// </remarks>
    private static string Stamped(string informational)
    {
        var halves = informational.Split('+');

        return halves is [var number, { Length: > 0 } commit, ..]
            ? $"{number}+{commit[..Math.Min(8, commit.Length)]}"
            : halves[0];
    }
}
