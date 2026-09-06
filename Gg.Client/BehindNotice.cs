using Gg.Contracts;

namespace Gg.Client;

/// <summary>
/// What gg makes of a notice saying it is behind.
/// </summary>
/// <remarks>
/// <para>
/// <b>The notice is composed on the control plane, and that is the whole
/// design.</b> One shape, three renderers — <c>gg doctor</c> turns it into a
/// check, the console puts it on the queue row, the tenant page shows a banner
/// — so the sentence travels rather than being written three times. Nothing
/// here rewords it.
/// </para>
/// <para>
/// <b>What this holds is the one thing a sender may not decide.</b>
/// <c>Blocking</c> is otherwise the control plane's call and a reader may never
/// promote a notice into a failure. Being behind is the exception, fixed at the
/// contract: rule 6 says it is reported and never refuses, and the protocol
/// floor's 426 stays the only thing in this design that stops anybody. A
/// control plane sending <c>Blocking = true</c> on this code is contradicting
/// the client rather than configuring it, and gg declines to be configured into
/// breaking somebody's build over a version number.
/// </para>
/// </remarks>
public static class BehindNotice
{
    /// <summary>Whether a code may never block, whatever its sender said.</summary>
    public static bool IsAdvisoryOnly(string? code) =>
        code is not null && TenantNoticeCodes.AdvisoryOnly.Contains(code, StringComparer.Ordinal);

    /// <summary>
    /// The notice as it should be acted on, with any promotion undone.
    /// </summary>
    /// <remarks>
    /// Returns the same instance when there is nothing to correct, so the
    /// common path allocates nothing and a reader can see that the usual case
    /// is untouched.
    /// </remarks>
    public static TenantNotice Advisory(TenantNotice notice)
    {
        ArgumentNullException.ThrowIfNull(notice);

        return notice.Blocking && IsAdvisoryOnly(notice.Code)
            ? notice with { Blocking = false }
            : notice;
    }
}
