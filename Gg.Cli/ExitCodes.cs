using Gg.Client;

namespace Gg.Cli;

/// <summary>
/// What a script reads when <c>gg</c> exits.
/// </summary>
/// <remarks>
/// <para>
/// <b>A pure function over the result, for the reason <c>Keymap.Resolve</c> is
/// pure.</b> The mapping is the deliverable - a script that cannot tell "you were
/// told no" from "we do not know yet" will treat both as failure and stop - and a
/// mapping that only exists inside top-level statements is one nothing can
/// assert.
/// </para>
/// <para>
/// <b>From sysexits.h, which is the family this binary already used.</b> 69 is
/// <c>EX_UNAVAILABLE</c> and has meant "the control plane could not be reached or
/// refused the protocol" since slice two. The two below join it rather than
/// starting a second scheme.
/// </para>
/// </remarks>
public static class ExitCodes
{
    /// <summary>It worked, and the answer is on stdout.</summary>
    public const int Ok = 0;

    /// <summary>
    /// <c>EX_USAGE</c>. Everything <c>gg</c> says no to.
    /// </summary>
    /// <remarks>
    /// A malformed reference, no session, a decision the control plane refused.
    /// What they share is that the caller <b>was answered</b> - and every one of
    /// them already returned this, so a decision refused by the control plane
    /// joining them is not a new class of failure.
    /// </remarks>
    public const int Refused = 64;

    /// <summary>
    /// <c>EX_TEMPFAIL</c>. Nobody said no.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The submission was accepted and has not become visible within the bound. It
    /// may land after this. The right response is to <b>look again</b> - and
    /// specifically not to submit a second time, which on the decision endpoint is
    /// answered "nothing is waiting on a decision" for work that succeeded.
    /// </para>
    /// <para>
    /// <b>Distinct from <see cref="Refused"/> on purpose.</b> One non-zero for both
    /// would make a slow worker indistinguishable from a rejection, which is the
    /// failure the whole submit-and-observe change exists to avoid.
    /// </para>
    /// </remarks>
    public const int NotYetVisible = 75;

    /// <summary><c>EX_UNAVAILABLE</c>. The control plane could not be used at all.</summary>
    public const int Unavailable = 69;

    /// <summary>
    /// The code for a result that was produced rather than thrown.
    /// </summary>
    /// <remarks>
    /// Only an observed result can be anything but <see cref="Ok"/>: every other
    /// verb either produced its answer or threw, and a thrown refusal is mapped
    /// where it is caught.
    /// </remarks>
    public static int For(VerbResult result) => result switch
    {
        VerbResult.Decided decided => decided.Value.Observation.State switch
        {
            ObservationStates.Decided => Ok,
            ObservationStates.Refused => Refused,
            ObservationStates.NotYetVisible => NotYetVisible,
            // A state this version does not know is not a success. The vocabulary
            // is closed, so this is unreachable today - and it fails closed rather
            // than reporting zero for something nobody has read.
            _ => Refused,
        },
        _ => Ok,
    };
}
