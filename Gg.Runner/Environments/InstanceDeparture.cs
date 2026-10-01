using Gg.Contracts;

namespace Gg.Runner.Environments;

/// <summary>
/// Whether a flight gives its environment instance back when it leaves.
/// </summary>
/// <remarks>
/// <para>
/// <b>Its own type because two slices had an opinion and neither knew about the
/// other.</b> <see cref="InstanceHolds.BringsDownOnDeparture"/> answers for the
/// hold alone — an itinerary leg keeps what a sibling may need — and that is a
/// wire-level fact the contract should own. Whether this flight is serving a
/// preview somebody was asked to look at is a RUNNER-level fact, knowable only
/// here. Joining them in the contract would have the wire learn about exposures;
/// joining them at the call site is what produced GG-522.
/// </para>
/// <para>
/// <b>GG-522, and the order that caused it.</b> The agent built an image in the
/// daemon it was granted, ran it publishing <c>127.0.0.1:8080</c>, and fetched
/// the page to check its own change was being served. The preview worked. Then
/// the departure emptied the instance, the facts shipped, the branch pushed, the
/// address was published and the machine went out of service for twelve hours —
/// waiting for a person to review a 502.
/// </para>
/// <para>
/// <b>This adds a reason to KEEP an instance and takes none away.</b> A flight
/// serving no preview departs exactly as the hold vocabulary already said, which
/// is every flight in the field and the courtesy rule 3 describes. A test holds
/// the two in agreement across every hold, so they cannot drift into the shape
/// that caused this.
/// </para>
/// </remarks>
public static class InstanceDeparture
{
    /// <summary>
    /// Whether the instance is emptied as this flight leaves.
    /// </summary>
    /// <param name="hold">
    /// How the instance was held, from <see cref="InstanceHolds"/>.
    /// </param>
    /// <param name="holdsItsMachine">
    /// Whether this flight is holding its machine for a preview —
    /// <see cref="Exposures.TreeRetention.HoldsItsMachine"/>, which already means
    /// "this flight's environment must survive, because a person was asked to
    /// look at it". An instance is part of that environment.
    /// </param>
    /// <remarks>
    /// <b>The preview wins over the hold, not the other way round.</b> A flight
    /// hold says "nobody else needs this, so tidy up"; a preview says "somebody
    /// is about to open it". Tidying up is a courtesy and the reclaim on the next
    /// flight's way in is the guarantee, so the courtesy is what yields.
    /// </remarks>
    public static bool BringsDown(string? hold, bool holdsItsMachine) =>
        !holdsItsMachine && InstanceHolds.BringsDownOnDeparture(hold);
}
