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
        var was = applied.Nominates?.Opens ?? [];
        var now = proposed.Nominates?.Opens ?? [];

        if (applied.Nominates is not null && proposed.Nominates is null)
        {
            return new EnvelopeWidening
            {
                Field = "nominates",
                Because = "this repository declared what its pull requests may open and now "
                        + "declares nothing, which is every work kind rather than none.",
            };
        }

        if (now.Except(was, StringComparer.Ordinal).ToList() is { Count: > 0 } added)
        {
            return new EnvelopeWidening
            {
                Field = "nominates.opens",
                Because = "this repository's pull requests may now open "
                        + $"{string.Join(", ", added.Order(StringComparer.Ordinal))}, which is "
                        + "reach arriving from a webhook nobody is watching.",
            };
        }

        // A GATE REMOVED IS REACH ADDED, which is `opens-as`' own asymmetry one
        // record over: putting a person in front of an opening takes reach
        // away and needs no approval; taking one out means a nomination that
        // used to wait now becomes a flight unattended.
        if (applied.Nominates is { } gatedBefore
            && proposed.Nominates is { } gatedAfter
            && string.Equals(
                DestinationOpening.Of(gatedBefore), DestinationOpening.Gated,
                StringComparison.Ordinal)
            && !string.Equals(
                DestinationOpening.Of(gatedAfter), DestinationOpening.Gated,
                StringComparison.Ordinal))
        {
            return new EnvelopeWidening
            {
                Field = "nominates.opens-as",
                Because = "a nomination from this repository used to wait for somebody and now "
                        + "becomes a flight without one.",
            };
        }

        return BudgetWidening(applied.Budget, proposed.Budget);
    }

    /// <summary>
    /// How the proposed budget widens the applied one, or null.
    /// </summary>
    /// <remarks>
    /// <b>Compared as a rate, never as a count.</b> Five a day and five a week
    /// are the same number and five times apart, so a comparator reading
    /// <c>Flights</c> alone would let the faster one through ungated.
    /// </remarks>
    private static EnvelopeWidening? BudgetWidening(
        NominationBudget? applied, NominationBudget? proposed)
    {
        // NEITHER IS UNBOUNDED TWICE OVER, which is no change.
        if (applied is null)
        {
            return null;
        }

        // ABSENT MEANS UNBOUNDED, so deleting a line raises the ceiling to
        // infinity. This is the arm a comparator gets wrong by omission: two
        // numbers cannot be compared when one of them is not there, and
        // "no change" is the answer that lets it through.
        if (proposed is null)
        {
            return new EnvelopeWidening
            {
                Field = "budget",
                Because = "this repository had a budget and now has none, and absent means "
                        + "unbounded - which is the largest widening this member can express.",
            };
        }

        if (!EnvelopeDurations.TryParse(applied.Window, out var before)
            || !EnvelopeDurations.TryParse(proposed.Window, out var after)
            || before <= TimeSpan.Zero || after <= TimeSpan.Zero)
        {
            // A WINDOW NOBODY CAN PARSE IS REPORTED RATHER THAN IGNORED.
            // Validation refuses one at authoring, so reaching here means a
            // document that got in another way - and reading it as no change
            // would be the silent half of a refusal that already happened.
            return new EnvelopeWidening
            {
                Field = "budget.window",
                Because = $"'{applied.Window}' and '{proposed.Window}' cannot both be read as "
                        + "durations, so whether this is more reach cannot be answered.",
            };
        }

        // THE RATE, as flights per unit of time. Compared by cross-multiplying
        // rather than by dividing, because integer division would call 5/24h
        // and 5/168h equal and floating point would make the answer depend on
        // how long the window happened to be.
        var widens = (long)proposed.Flights * (long)before.Ticks
                   > (long)applied.Flights * (long)after.Ticks;

        return widens
            ? new EnvelopeWidening
            {
                Field = "budget",
                Because = $"this repository's pull requests may now open {proposed.Flights} "
                        + $"flights per {proposed.Window} where they could open "
                        + $"{applied.Flights} per {applied.Window}, which is a faster rate and "
                        + "so more tokens spent with nobody watching.",
            }
            : null;
    }
}
