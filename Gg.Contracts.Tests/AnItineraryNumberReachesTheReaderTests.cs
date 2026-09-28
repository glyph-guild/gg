using System.Reflection;
using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// A leg carries the number of the plan it belongs to, because its nominator
/// carries only the id.
/// </summary>
/// <remarks>
/// <para>
/// <b>WITHOUT THIS THE NUMBER IS UNREACHABLE TO A READER.</b> A leg's
/// nominator is <c>itinerary:{id}</c> - a uuid, and rightly: a nominator that
/// carried the number would be a key that moves if the allocator is ever
/// rebuilt, and would read as a different plan in a different tenant. But the
/// thing a person typed is <c>ITN-7</c>, and a surface holding only the id
/// cannot show them what they approved.
/// </para>
/// <para>
/// <b><see cref="NominationSummary.FlightNumber"/>'s shape, member for
/// member</b>, and for its reason: the number is minted in a context of its
/// own and rendered by the side that read it, because a second place deciding
/// the format would drift from the first. Null wherever the row is not a leg,
/// exactly as that one is null wherever there is no flight.
/// </para>
/// <para>
/// <b>Not the id beside it.</b> The nominator already carries that, and a
/// second copy is a second thing to disagree with the first - the rule this
/// record already holds about what a person scans.
/// </para>
/// </remarks>
public class AnItineraryNumberReachesTheReaderTests
{
    [Test]
    public async Task A_leg_may_carry_the_number_of_its_plan()
    {
        var members = typeof(NominationSummary)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToList();

        await Assert.That(members).Contains(nameof(NominationSummary.ItineraryNumber))
            .Because("a reader holding only itinerary:{uuid} cannot show a person the ITN- "
                   + "number they approved, so the plans surface would render a uuid or "
                   + "nothing.");
    }

    [Test]
    public async Task It_is_absent_on_every_row_that_is_not_a_leg()
    {
        // OPTIONAL, AND ABSENT MEANS "not a leg" rather than "a leg whose plan
        // has no number" - which cannot happen, because an itinerary is minted
        // with its number in one statement.
        var board = new NominationSummary
        {
            NominationId = Guid.Empty,
            Nominator = "flight:" + Guid.Empty,
            Subject = "flight:" + Guid.Empty,
            Version = "one",
            WorkKind = "research",
            Mode = "auto",
            State = NominationStates.Standing,
            MadeAt = DateTimeOffset.UnixEpoch,
        };

        await Assert.That(board.ItineraryNumber).IsNull();
    }

    [Test]
    public async Task It_is_rendered_by_the_one_thing_that_renders_one()
    {
        // A STRING ALREADY RENDERED, on FlightNumber's terms: the side that
        // read the row renders it, and a console that built its own would be a
        // second place deciding the format. ItineraryRef.Format is that one
        // place, and OnlyOneThingRendersAnItineraryNumberTests holds it.
        var leg = new NominationSummary
        {
            NominationId = Guid.Empty,
            Nominator = "itinerary:" + Guid.Empty,
            Subject = "leg:research@abc",
            Version = "abc",
            WorkKind = "research",
            Mode = "auto",
            State = NominationStates.Standing,
            MadeAt = DateTimeOffset.UnixEpoch,
            ItineraryNumber = ItineraryRef.Format(7),
        };

        await Assert.That(leg.ItineraryNumber).IsEqualTo("ITN-7");
    }

    [Test]
    public async Task The_summary_carries_no_second_copy_of_the_id()
    {
        // The nominator already says which plan. A member holding the uuid
        // beside it would be a second answer able to disagree with the first,
        // and a thing a person scans should not be mostly provenance.
        var members = typeof(NominationSummary)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToList();

        foreach (var second in (string[])["ItineraryId", "Itinerary", "PlanId"])
        {
            await Assert.That(members.Contains(second, StringComparer.Ordinal)).IsFalse()
                .Because($"'{second}' is already in the nominator, and two spellings of one "
                       + "plan is how a surface starts showing the wrong one.");
        }
    }
}
