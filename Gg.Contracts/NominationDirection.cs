namespace Gg.Contracts;

/// <summary>
/// Which way a change to a nominator's bound or budget goes, in one place.
/// </summary>
/// <remarks>
/// <para>
/// <b>Three arms mean the same thing wherever a nomination is bounded</b> — a
/// repository's registration, a watch's document, and whatever nominator comes
/// after them. Reach added to <c>opens:</c> is reach nobody granted; a gate
/// removed is a nomination that used to wait and now does not; a budget raised
/// or deleted is more spent with nobody watching.
/// </para>
/// <para>
/// <b>EXTRACTED WHEN THE SECOND CALLER ARRIVED, and the reason is written
/// history rather than taste.</b> Slice thirty-six paid for a direction rule
/// copied out of the composition table with <c>MayPerform</c>, and thirty-seven
/// paid again for <c>opens-as</c>. ADR-0022 § 5 names the failure shape:
/// <i>"a second menu shaped like `opens:` would have its own validation, its
/// own direction arms, and no way for a narrowing to reach it — which is how
/// one of two spellings stops agreeing."</i> Two comparators is that, arriving
/// through the arms instead of the members.
/// </para>
/// <para>
/// <b>The noun is the caller's</b>, because the sentence a reviewer reads has
/// to name what widened — <i>this repository's pull requests</i>, <i>this
/// watch's sweeps</i>. That is the only thing the two doors do not share, which
/// is why it is a parameter and not a second method.
/// </para>
/// </remarks>
public static class NominationDirection
{
    /// <summary>How the proposed bound widens the applied one, or null.</summary>
    /// <param name="whose">
    /// What widened, as a subject a sentence can be built on — <c>"this
    /// watch's sweeps"</c>. A reviewer reading "reach nobody granted" needs to
    /// know whose.
    /// </param>
    public static EnvelopeWidening? BoundWidening(
        Destination? applied, Destination? proposed, string whose)
    {
        var was = applied?.Opens ?? [];
        var now = proposed?.Opens ?? [];

        // A BOUND DELETED IS EVERY KIND RATHER THAN NONE, which is the arm a
        // comparator gets wrong by reading only the two lists: an absent menu
        // has nothing to subtract, so the lists compare as no change.
        if (applied is not null && proposed is null)
        {
            return new EnvelopeWidening
            {
                Field = "nominates",
                Because = $"{whose} were bounded to what they may open and now are not, "
                        + "which is every work kind rather than none.",
            };
        }

        if (now.Except(was, StringComparer.Ordinal).ToList() is { Count: > 0 } added)
        {
            return new EnvelopeWidening
            {
                Field = "nominates.opens",
                Because = $"{whose} may now open "
                        + $"{string.Join(", ", added.Order(StringComparer.Ordinal))}, which is "
                        + "reach nobody granted.",
            };
        }

        // A GATE REMOVED IS REACH ADDED, which is `opens-as`' own asymmetry:
        // putting a person in front of an opening takes reach away and needs
        // no approval, while taking one out means a nomination that used to
        // wait becomes a flight unattended.
        return applied is { } before
            && proposed is { } after
            && string.Equals(
                DestinationOpening.Of(before), DestinationOpening.Gated, StringComparison.Ordinal)
            && !string.Equals(
                DestinationOpening.Of(after), DestinationOpening.Gated, StringComparison.Ordinal)
            ? new EnvelopeWidening
            {
                Field = "nominates.opens-as",
                Because = $"a nomination from {whose} used to wait for somebody and now "
                        + "becomes a flight without one.",
            }
            : null;
    }

    /// <summary>
    /// How the proposed budget widens the applied one, or null.
    /// </summary>
    /// <remarks>
    /// <b>Compared as a rate, never as a count.</b> Five a day and five a week
    /// are the same number and five times apart, so a comparator reading
    /// <c>Flights</c> alone would let the faster one through ungated.
    /// </remarks>
    public static EnvelopeWidening? BudgetWidening(
        NominationBudget? applied, NominationBudget? proposed, string whose)
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
                Because = $"{whose} had a budget and now have none, and absent means "
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
                Because = $"{whose} may now open {proposed.Flights} flights per "
                        + $"{proposed.Window} where they could open {applied.Flights} per "
                        + $"{applied.Window}, which is a faster rate and so more tokens spent "
                        + "with nobody watching.",
            }
            : null;
    }
}

/// <summary>
/// Which way a change to a watch goes: four arms of its own, three shared.
/// </summary>
/// <remarks>
/// <b>Written out per member, because an operator in the composition table is
/// not a direction rule.</b> Thirty-six learned that from <c>MayPerform</c> and
/// thirty-seven paid for it again with <c>opens-as</c>.
/// </remarks>
public static class WatchDirection
{
    /// <summary>How this watch widens the one in force, or null.</summary>
    public static EnvelopeWidening? Widening(WatchDocument applied, WatchDocument proposed)
    {
        ArgumentNullException.ThrowIfNull(applied);
        ArgumentNullException.ThrowIfNull(proposed);

        const string Whose = "this watch's sweeps";

        if (NominationDirection.BoundWidening(applied.Nominates, proposed.Nominates, Whose)
            is { } bound)
        {
            return bound;
        }

        // ANY CHANGE AT ALL, AND THE SENTENCE SAYS WHY. A filter is a query in
        // the shape's own language, and whether one returns a superset of
        // another is not a question this side can answer - so "looser" and
        // "tighter" are indistinguishable here. The conservative answer is the
        // only sound one: a comparator that guessed would be wrong in the
        // UNGATED direction about half the time, with nobody able to say which
        // half.
        //
        // THE SENTENCE IS LOAD-BEARING. An author narrowing a filter and being
        // sent to a reviewer will read a bare "this widens" as a bug and work
        // around it; saying that the comparison cannot be made is what makes
        // the gate legible.
        if (!string.Equals(applied.Filter, proposed.Filter, StringComparison.Ordinal))
        {
            return new EnvelopeWidening
            {
                Field = "filter",
                Because = "this watch asks the shape something different, and whether the new "
                        + "query returns more than the old one cannot be answered from here - "
                        + "a filter is a query in the shape's own language. So every change "
                        + "to one is reviewed, including a narrowing.",
            };
        }

        // A SHORTER PERIOD IS MORE SWEEPS, which is more spent and more
        // nominated. Decidable, unlike the filter, so it is compared rather
        // than assumed.
        if (EnvelopeDurations.TryParse(applied.Trigger.Every, out var was)
            && EnvelopeDurations.TryParse(proposed.Trigger.Every, out var now)
            && now < was)
        {
            return new EnvelopeWidening
            {
                Field = "trigger.every",
                Because = $"this watch sweeps every {proposed.Trigger.Every} where it swept "
                        + $"every {applied.Trigger.Every}, which is more passes and so more "
                        + "nominated with nobody watching.",
            };
        }

        return BoundsWidening(applied.Bounds, proposed.Bounds, Whose);
    }

    /// <summary>How the proposed bounds widen the applied ones, or null.</summary>
    private static EnvelopeWidening? BoundsWidening(
        WatchBounds? applied, WatchBounds? proposed, string whose)
    {
        if (NominationDirection.BudgetWidening(applied?.Budget, proposed?.Budget, whose)
            is { } budget)
        {
            return budget;
        }

        // ABSENT IS UNBOUNDED, TWICE OVER. Deleting a cap raises it to every
        // subject the shape has, and deleting the hours opens the whole day -
        // both the omission arm the budget has one member up.
        if (applied?.CapPerPass is { } cap
            && (proposed?.CapPerPass is null || proposed.CapPerPass > cap))
        {
            return new EnvelopeWidening
            {
                Field = "bounds.cap-per-pass",
                Because = proposed?.CapPerPass is { } raised
                    ? $"{whose} may now report {raised} subjects a pass where they could "
                      + $"report {cap}."
                    : $"{whose} were capped at {cap} subjects a pass and now are not, and "
                      + "absent means every subject the shape has.",
            };
        }

        if (applied?.ActiveHours is not { } hours)
        {
            return null;
        }

        if (proposed?.ActiveHours is not { } proposedHours)
        {
            return new EnvelopeWidening
            {
                Field = "bounds.active-hours",
                Because = $"{whose} were confined to {hours} and now are not, and absent "
                        + "means always.",
            };
        }

        // COMPARED AS A SPAN rather than by endpoint, because a window that
        // wraps midnight has a later opening than closing and endpoint
        // comparison reads that as negative.
        return Span(hours) is { } before && Span(proposedHours) is { } after && after > before
            ? new EnvelopeWidening
            {
                Field = "bounds.active-hours",
                Because = $"{whose} may now run {proposedHours} where they ran {hours}, "
                        + "which is more of the day.",
            }
            : null;
    }

    /// <summary>How long a window is open, wrapping midnight, or null when unreadable.</summary>
    private static TimeSpan? Span(string hours)
        => EnvironmentStrategy.ParseActiveHours(hours) is { } window
            ? window.ClosesUtc > window.OpensUtc
                ? window.ClosesUtc - window.OpensUtc
                : TimeSpan.FromDays(1) - (window.OpensUtc - window.ClosesUtc)
            : null;
}
