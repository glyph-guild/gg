namespace Gg.Contracts;

/// <summary>
/// Whether a re-registration gives a repository's pull requests more reach.
/// </summary>
/// <remarks>
/// <para>
/// <b>The asymmetry is the whole point, and it is <c>EnvelopeDirection</c>'s.</b>
/// More reach is what a reviewer must be shown; less is somebody deciding to
/// spend less, and a gate in front of that is how a review practice gets
/// abandoned. So a widening is reported and a tightening is silence.
/// </para>
/// <para>
/// <b>Written by hand, because an operator in a composition table is not a
/// direction rule.</b> Thirty-six learned that from <c>may-perform</c> and
/// thirty-seven paid for it again with <c>opens-as</c>: how two documents merge
/// and which of them grants more are different questions, and a comparator
/// derived from the first answers the second wrong in at least one direction.
/// </para>
/// <para>
/// <b>Two of these arms exist because absence means something.</b> A budget
/// removed is unbounded, which is the largest widening this member can express
/// — and a comparator that only compared two numbers would read a deletion as
/// no change at all. A window lengthened for the same count is less reach, and
/// one shortened is more, because the rate is what bounds a loop and not the
/// numerator.
/// </para>
/// </remarks>
public static class RepositoryDirection
{
    /// <summary>How this registration widens the one in force, or null.</summary>
    public static EnvelopeWidening? Widening(
        RegisterRepositoryRequest applied, RegisterRepositoryRequest proposed)
    {
        ArgumentNullException.ThrowIfNull(applied);
        ArgumentNullException.ThrowIfNull(proposed);

        // A BOUND WHERE THERE WAS NONE IS A TIGHTENING, not a widening, and it
        // is worth stating rather than falling out of the comparison. Absent
        // means "what it opens today" — an un-kinded flight under the tenant's
        // envelope — so naming a menu can only narrow what was already
        // reachable. Removing one goes the other way and is caught below.
        //
        // SHARED WITH THE WATCH'S COMPARATOR rather than written twice. The
        // three arms below mean the same thing wherever a nomination is bounded,
        // and a second copy is the way two direction rules come to disagree —
        // which is what `MayPerform` cost thirty-six and `opens-as` cost
        // thirty-seven. Only the noun in the sentence is this door's.
        if (NominationDirection.BoundWidening(
                applied.Nominates, proposed.Nominates, "this repository's pull requests")
            is { } bound)
        {
            return bound;
        }

        return NominationDirection.BudgetWidening(
            applied.Budget, proposed.Budget, "this repository's pull requests");
    }

}
