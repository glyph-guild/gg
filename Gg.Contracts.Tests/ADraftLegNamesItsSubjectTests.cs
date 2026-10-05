using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// <b>S61.4-01</b> - a draft itinerary names a subject on every leg, and no two of its legs
/// share a kind and a subject.
/// </summary>
/// <remarks>
/// <para>
/// <b>The door the collapse can be refused at.</b> GG-369 and GG-380 each proposed three legs
/// and kept one, because a leg with no subject takes its identity from the flight that
/// proposed it - so three legs were one piece of work three times. An agent's nomination
/// cannot be made to carry a subject without breaking every single-piece nomination; a draft
/// a person writes leg by leg can.
/// </para>
/// <para>
/// <b>Kind and subject, not subject alone.</b> Triaging a thing and implementing it are two
/// legs about one, and the ordering rule already refuses an <c>after</c> that names a subject
/// two legs share. What is refused here is the pair, which is a leg's identity.
/// </para>
/// <para>
/// <b>A leg is a <see cref="FlightNomination"/>, and its own validator runs.</b> The draft adds
/// two rules a lone nomination cannot have; every bound a nomination already has - the reason,
/// the length of a subject, a leg following itself - is that type's, unchanged.
/// </para>
/// </remarks>
public class ADraftLegNamesItsSubjectTests
{
    private static FlightNomination Leg(string kind, string? subject, string? after = null) => new()
    {
        WorkKind = kind,
        Reason = "the ticket names this piece on its own",
        Subject = subject,
        After = after,
    };

    private static ItineraryDraft Draft(params FlightNomination[] legs) => new()
    {
        Planner = "plan",
        Intent = new FlightIntent { Kind = "text", Text = "three fixes in one bug" },
        Legs = legs,
    };

    [Test]
    public async Task A_draft_of_distinct_legs_is_valid()
    {
        await Assert.That(ItineraryDraft.Validate(Draft(
            Leg("implement", "the icon"),
            Leg("implement", "the padding"),
            Leg("review", "the icon, reviewed", after: "the icon")))).IsNull();
    }

    [Test]
    public async Task A_leg_with_no_subject_is_refused_naming_its_kind_and_place()
    {
        var because = ItineraryDraft.Validate(Draft(
            Leg("implement", "the icon"),
            Leg("implement", null)));

        await Assert.That(because).IsNotNull();
        await Assert.That(because!).Contains("Leg 2");
        await Assert.That(because!).Contains("'implement'")
            .Because("a refusal has to say which leg, or a person counts by hand.");
    }

    [Test]
    public async Task Two_legs_with_one_kind_and_subject_are_refused_naming_both()
    {
        var because = ItineraryDraft.Validate(Draft(
            Leg("implement", "the icon"),
            Leg("review", "the padding"),
            Leg("implement", "the icon")));

        await Assert.That(because).IsNotNull();
        await Assert.That(because!).Contains("Legs 1 and 3");
        await Assert.That(because!).Contains("'the icon'");
    }

    [Test]
    public async Task One_subject_under_two_kinds_is_allowed()
    {
        await Assert.That(ItineraryDraft.Validate(Draft(
            Leg("triage", "the icon"),
            Leg("implement", "the icon")))).IsNull()
            .Because("triaging a thing and implementing it are two legs about one.");
    }

    [Test]
    public async Task A_legs_own_rules_are_the_nominations()
    {
        var because = ItineraryDraft.Validate(Draft(
            Leg("implement", "the icon", after: "the icon")));

        await Assert.That(because).IsNotNull();
        await Assert.That(because!).Contains("which is its own subject")
            .Because("a leg is a FlightNomination and its validator's sentence is the one a "
                   + "person reads - a second wording would be a second rule.");
    }

    [Test]
    public async Task A_draft_names_a_planner_and_at_least_one_leg()
    {
        await Assert.That(ItineraryDraft.Validate(Draft())).IsNotNull();
        await Assert.That(ItineraryDraft.Validate(Draft(Leg("implement", "x")) with { Planner = " " }))
            .IsNotNull();
    }

    [Test]
    public async Task A_draft_is_bounded()
    {
        var legs = Enumerable.Range(1, ItineraryDraft.MaxLegs + 1)
            .Select(i => Leg("implement", $"piece {i}"))
            .ToArray();

        await Assert.That(ItineraryDraft.Validate(Draft(legs))).IsNotNull();
    }
}
