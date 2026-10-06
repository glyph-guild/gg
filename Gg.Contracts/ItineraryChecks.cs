namespace Gg.Contracts;

/// <summary>
/// What a leg of a checked itinerary would do if a plan flight proposed it now.
/// </summary>
/// <remarks>
/// <b>Closed, and refused on read when a word is unknown.</b> A fourth verdict would be a
/// control plane that learned an answer this client cannot explain; <see cref="ItineraryCheck.Validate"/>
/// says so rather than printing a word with no meaning attached.
/// </remarks>
[VocabularyOf(VocabularyFingerprints.Contract)]
public static class LegVerdicts
{
    /// <summary>Admission would open a flight for this leg.</summary>
    public const string Opens = "opens";

    /// <summary>The leg would stand on the board until somebody opens it.</summary>
    public const string Stands = "stands";

    /// <summary>A rule refuses the leg, and the reason is the one admission would record.</summary>
    public const string Refused = "refused";

    public static IReadOnlyList<string> All { get; } = [Opens, Stands, Refused];
}

/// <summary>
/// An itinerary written out leg by leg, to be checked rather than proposed.
/// </summary>
/// <remarks>
/// <para>
/// <b>A leg is a <see cref="FlightNomination"/></b>, because that is what a plan flight
/// proposes and what admission judges. A shape of its own would be a second thing to keep
/// agreeing with the first, and every rule a nomination already has would need writing twice.
/// </para>
/// <para>
/// <b>One intent for the plan, a subject for each leg.</b> That is how admission builds a leg's
/// flight: the plan's intent, with the leg's subject as what this piece of work is. A tracker
/// reference stays the payload; a plan about prose takes each subject as its text.
/// </para>
/// <para>
/// <b>Nothing in it is kept.</b> The control plane judges it and answers; the intent may be a
/// sentence somebody typed, and the boundary that is the product forbids storing it.
/// </para>
/// </remarks>
[PinnedId("9ff879b6-7d0e-4328-a3ca-a93f36c5cc44")]
public sealed record ItineraryDraft
{
    /// <summary>
    /// The planning work kind whose flight destination bounds the legs - the menu a plan flight
    /// of that kind would be held to.
    /// </summary>
    public required string Planner { get; init; }

    /// <summary>What the plan is about: a tracker reference, a link, or text.</summary>
    public required FlightIntent Intent { get; init; }

    /// <summary>The legs, in the order they were written. Order means nothing; <c>after</c> does.</summary>
    public required IReadOnlyList<FlightNomination> Legs { get; init; }

    /// <summary>
    /// The most legs one draft may carry.
    /// </summary>
    /// <remarks>
    /// A bound on the request, not a planning rule: a destination's own cap is the rule, and
    /// the check reports it. This only stops one request asking the control plane to judge an
    /// unbounded list.
    /// </remarks>
    public const int MaxLegs = 50;

    /// <summary>The diagnosis, or null when there is nothing wrong.</summary>
    public static string? Validate(ItineraryDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        if (string.IsNullOrWhiteSpace(draft.Planner))
        {
            return "A draft names the planning kind whose menu bounds its legs. Without one there "
                 + "is no destination to check them against.";
        }

        if (draft.Planner.Length > FlightNomination.MaxWorkKind)
        {
            return $"A planner is a work kind, at most {FlightNomination.MaxWorkKind} characters, "
                 + $"and this one is {draft.Planner.Length}.";
        }

        if (draft.Intent is null)
        {
            return "A draft's intent is what every leg's flight would be about, and it is missing.";
        }

        if (FlightIntent.Validate(draft.Intent) is { } badIntent)
        {
            return "A draft's intent is what every leg's flight would be about: " + badIntent;
        }

        if (draft.Legs is not { Count: > 0 } legs)
        {
            return "A draft has at least one leg. A plan of nothing has nothing to check.";
        }

        if (legs.Count > MaxLegs)
        {
            return $"A draft carries at most {MaxLegs} legs and this one has {legs.Count}.";
        }

        for (var i = 0; i < legs.Count; i++)
        {
            var leg = legs[i];

            // A SUBJECT ON EVERY LEG. A nomination without one is about the flight that made
            // it, which is right for a single piece of work and is how three legs became one
            // on GG-369 and GG-380. A draft is a plan by definition, so it can require it.
            if (string.IsNullOrWhiteSpace(leg.Subject))
            {
                return $"Leg {i + 1} ('{leg.WorkKind}') names no subject. Every leg of a plan "
                     + "says which piece of work it is - without one, every such leg is the same "
                     + "piece, and all but one are lost.";
            }

            // AND THE LEG'S OWN RULES, which are the nomination's: the reason, the bounds, a leg
            // following itself. One validator, so the sentence is the one a person already
            // knows from a refused nomination.
            if (FlightNomination.Validate(leg) is { } badLeg)
            {
                return $"Leg {i + 1}: {badLeg}";
            }
        }

        // NO TWO LEGS SHARE A KIND AND A SUBJECT, which is a leg's identity. One subject under
        // two kinds is legal - triaging a thing and implementing it are two legs about one.
        for (var i = 0; i < legs.Count; i++)
        {
            for (var j = i + 1; j < legs.Count; j++)
            {
                if (string.Equals(legs[i].WorkKind, legs[j].WorkKind, StringComparison.Ordinal)
                    && string.Equals(legs[i].Subject, legs[j].Subject, StringComparison.Ordinal))
                {
                    return $"Legs {i + 1} and {j + 1} are both '{legs[i].WorkKind}' about "
                         + $"'{legs[i].Subject}', so they are one piece of work written twice. "
                         + "Give them subjects that say how they differ, or drop one.";
                }
            }
        }

        return null;
    }
}

/// <summary>A human check a leg's flight would wait on, and who answers it.</summary>
[PinnedId("38fbaae4-719b-40b2-b652-b488bfa46758")]
public sealed record LegGate
{
    public required string ObligationId { get; init; }

    /// <summary>Who the envelope names to answer it, or null when it names nobody.</summary>
    public string? Approver { get; init; }
}

/// <summary>
/// A destination requirement admission skips for this kind of work, because it can never
/// attach to it.
/// </summary>
/// <remarks>
/// <b>Passed over, not refused.</b> A requirement that never attached does not hold the
/// destination shut - so the leg lands, and the guard its author wrote does nothing for this
/// kind. Nothing else tells them.
/// </remarks>
[PinnedId("b0c68930-c11b-4a9b-a67b-90aaa8ef4464")]
public sealed record PassedOverRequirement
{
    public required string DestinationId { get; init; }

    public required string ObligationId { get; init; }

    /// <summary>Why, ending in the engine's own sentence for a rule that can never apply.</summary>
    public required string Because { get; init; }
}

/// <summary>What admission would do with one leg, and what its flight would face.</summary>
[PinnedId("14e8860f-2045-4a54-8f70-d4dedf49a927")]
public sealed record LegCheck
{
    public required string Subject { get; init; }

    public required string WorkKind { get; init; }

    /// <summary>One of <see cref="LegVerdicts"/>.</summary>
    public required string Verdict { get; init; }

    /// <summary>The sentence. For a refusal, the one admission would record, byte for byte.</summary>
    public required string Reason { get; init; }

    /// <summary>The envelope version the leg's flight would pin, once composition ran.</summary>
    public string? EnvelopeVersion { get; init; }

    /// <summary>A digest of what the leg's flight would compose.</summary>
    public string? EnvelopeDigest { get; init; }

    /// <summary>The documents that composition drew on, root first.</summary>
    /// <remarks>
    /// Null absorbed here: an absent key must not break a non-nullable promise, and a leg
    /// refused before composition has no layers to name.
    /// </remarks>
    public IReadOnlyList<string> EnvelopeLayers
    {
        get => field ?? [];
        init;
    }

    /// <summary>
    /// Every obligation the composed envelope declares, as <c>gg why</c> would say it - before
    /// any fact exists, so an unconditional rule attaches, one that reads a fact is not known
    /// yet, and one this kind can never feed is not attached and says why.
    /// </summary>
    public required IReadOnlyList<ObligationAttribution> Obligations { get; init; }

    /// <summary>The human checks the leg's flight would wait on.</summary>
    public required IReadOnlyList<LegGate> Gates { get; init; }

    /// <summary>Destination requirements admission would skip for this kind.</summary>
    public required IReadOnlyList<PassedOverRequirement> PassedOver { get; init; }

    /// <summary>
    /// Whether any machine could take the leg's flight now, priced the way <c>gg plan</c>
    /// prices a flight: the same compiler and the same matcher. Null when composition refused.
    /// </summary>
    public Checklist? Fleet { get; init; }
}

/// <summary>
/// What admission would do with every leg of a draft, and anything that refuses the plan whole.
/// </summary>
[PinnedId("11924987-afa1-46df-b4ac-e7247ab8df56")]
public sealed record ItineraryCheck
{
    /// <summary>The planning kind the legs were judged against, as the draft named it.</summary>
    public required string Planner { get; init; }

    /// <summary>The planner's flight destination, or null when it composes none.</summary>
    public string? DestinationId { get; init; }

    /// <summary>
    /// Why the whole plan would be refused - over the cap, impossible to order, or a planner
    /// that opens no flights - or null when it would not be. The legs are still judged.
    /// </summary>
    public string? Refused { get; init; }

    public required IReadOnlyList<LegCheck> Legs { get; init; }

    /// <summary>The diagnosis, or null when every verdict is one this client knows.</summary>
    public static string? Validate(ItineraryCheck check)
    {
        ArgumentNullException.ThrowIfNull(check);

        foreach (var leg in check.Legs ?? [])
        {
            if (!LegVerdicts.All.Contains(leg.Verdict, StringComparer.Ordinal))
            {
                return $"The leg about '{leg.Subject}' has the verdict '{leg.Verdict}', which this "
                     + $"gg does not know - it knows {string.Join(", ", LegVerdicts.All)}. The "
                     + "control plane is newer than this client; update gg to read it.";
            }
        }

        return null;
    }
}

/// <summary>
/// What a planner's destination would accept for a leg: the planning tool server's three menus.
/// Slice sixty-three.
/// </summary>
/// <remarks>
/// <para>
/// <b>Answered from admission's own bounds</b>, so a menu never offers what the check then
/// refuses: the destination's <c>opens</c>, the registered repositories its repository bound
/// permits, and the charted environments its environment bound permits.
/// </para>
/// <para>
/// <b>A read of its own</b> rather than an empty check, because the check refuses a draft with
/// no legs and the menu is needed before the first leg exists.
/// </para>
/// </remarks>
[PinnedId("6a724446-a699-43d9-831f-e92090a2d2bf")]
public sealed record ItineraryMenu
{
    /// <summary>The planning kind the menu is for.</summary>
    public required string Planner { get; init; }

    /// <summary>The planner's flight destination, or null when it composes none.</summary>
    public string? DestinationId { get; init; }

    /// <summary>The work kinds a leg may name: the destination's <c>opens</c>.</summary>
    public required IReadOnlyList<string> WorkKinds { get; init; }

    /// <summary>The registered repositories, by name, a leg may name.</summary>
    public required IReadOnlyList<string> Repositories { get; init; }

    /// <summary>The charted environments a leg may name.</summary>
    public required IReadOnlyList<string> Environments { get; init; }

    /// <summary>
    /// Admission's sentence when the planner can open nothing - no destination, or one that
    /// opens no kind - or null when it can. A refusing menu offers nothing.
    /// </summary>
    public string? Refused { get; init; }

    /// <summary>The diagnosis, or null when the menu is one a client can offer from.</summary>
    public static string? Validate(ItineraryMenu menu)
    {
        ArgumentNullException.ThrowIfNull(menu);

        if (menu.Refused is { Length: > 0 }
            && (menu.WorkKinds is { Count: > 0 } || menu.Repositories is { Count: > 0 }
                || menu.Environments is { Count: > 0 }))
        {
            return "A menu that refuses offers nothing, and this one both refuses and offers - "
                 + "a client could offer a kind the control plane just said nothing opens.";
        }

        return null;
    }
}

/// <summary>
/// A plan a person proposes: the draft, and which agent acted for them. Slice sixty-five,
/// ADR-0038 Decision 5.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nothing else, and that is the point.</b> The conversation that produced a plan is not kept
/// (Decision 5): governance happens at the gate. A type with nowhere to put a transcript is the
/// strongest form of that, so a member added here is a diff somebody has to justify.
/// </para>
/// <para>
/// <b>Who proposed it is not here either</b>: the control plane takes the person from the
/// session, never from a body.
/// </para>
/// </remarks>
[PinnedId("f966327e-e6ba-42c0-bcb9-3aa163f4a4f1")]
public sealed record ItineraryProposal
{
    /// <summary>The longest <see cref="Via"/> may be: a label, not a place prose could travel.</summary>
    public const int MaxVia = 128;

    /// <summary>The plan, as <c>POST /v1/itineraries/check</c> takes it.</summary>
    public required ItineraryDraft Draft { get; init; }

    /// <summary>
    /// Which agent acted for the person - <c>claude-code (gg-itinerary)</c> - or null when they
    /// proposed by hand. A label that is recorded and printed, and grants nothing.
    /// </summary>
    public string? Via { get; init; }

    /// <summary>The diagnosis, or null when the proposal is well formed.</summary>
    public static string? Validate(ItineraryProposal proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);

        if (proposal.Draft is null)
        {
            return "A proposal carries the plan it proposes, and this one carries none.";
        }

        if (ItineraryDraft.Validate(proposal.Draft) is { } draft)
        {
            return draft;
        }

        if (proposal.Via is { Length: > MaxVia })
        {
            return $"'via' names the agent that acted, in at most {MaxVia} characters, and this is "
                 + $"{proposal.Via.Length}. It is a label: the conversation is not kept.";
        }

        return null;
    }
}

/// <summary>A proposed plan, waiting for its gate: its number, its pass, and who answers.</summary>
[PinnedId("5ac34a00-af4a-4a55-9ed4-583666e78097")]
public sealed record ItineraryProposed
{
    /// <summary>The plan's number, <c>ITN-n</c>.</summary>
    public required string Itinerary { get; init; }

    /// <summary>The pass flight that holds the plan's gate, <c>GG-n</c>. It is never leased.</summary>
    public required string Pass { get; init; }

    /// <summary>The human obligations the plan waits on, and who the envelope names to answer each.</summary>
    public required IReadOnlyList<LegGate> Gates { get; init; }
}
