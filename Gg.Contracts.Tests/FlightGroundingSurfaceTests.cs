using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// A person may stop a flight that could still have been done, and must say
/// why.
/// </summary>
/// <remarks>
/// <para>
/// <b>ADR-0017 declared this exit and deliberately did not build it.</b>
/// <c>WorkKindExits.PersonOperated</c> carries the reason in full: "`gg ground`
/// is rare and every kind that motivated the slice finishes by admitting or by
/// going moot, so the verb lands later - declared now, under no pressure,
/// rather than invented in a hurry the day somebody needs it". This is that
/// day, and the thing bought by declaring it early is that the shape was
/// settled before anybody wanted it.
/// </para>
/// <para>
/// <b>It is the door <c>withdrawn</c> keeps being reached for instead.</b> A
/// flight nobody can serve is not a flight whose question stopped applying -
/// the work is still wanted and the fleet simply cannot take it. Withdrawing
/// it says something untrue about why it ended, and <c>withdrawn</c> is
/// documented as "the most reachable sentence in this vocabulary, and the one
/// that will be reached for whenever something is inconvenient". Without this
/// door that is the only door, so the pressure lands on the word least able to
/// carry it.
/// </para>
/// <para>
/// <b>One caller, and that is the whole distinction.</b> Withdrawal has two -
/// a person, and pool recovery, which withdraws the maintenance flight whose
/// pull point came back up. Grounding has exactly one and can never have
/// another: "a person stopped it" is a sentence no sweep can truthfully write,
/// and a system that could write it would be attributing its own convenience
/// to somebody.
/// </para>
/// </remarks>
public class FlightGroundingSurfaceTests
{
    private static Endpoint Grounding() =>
        ProtocolSurface.Endpoints.Single(e =>
            string.Equals(e.Path, "/v1/flights/{ref}/grounding", StringComparison.Ordinal));

    private static Endpoint Withdrawal() =>
        ProtocolSurface.Endpoints.Single(e =>
            string.Equals(e.Path, "/v1/flights/{ref}/withdrawal", StringComparison.Ordinal));

    [Test]
    public async Task The_door_is_declared_and_is_a_persons()
    {
        var door = Grounding();

        await Assert.That(door.Method).IsEqualTo("POST");
        await Assert.That(door.Audience).IsEqualTo(Audience.Developer)
            .Because("`a person stopped it' is the definition of this exit, not a policy on "
                   + "it - a runner reaching this door would be a runner deciding.");
        await Assert.That(door.Request).IsEqualTo(typeof(FlightGroundingRequest));
        await Assert.That(door.RequiredHeaders).Contains(ProtocolSurface.SessionHeader);
    }

    [Test]
    public async Task It_answers_exactly_as_withdrawing_does()
    {
        // THE SAME DOOR SHAPE, because the difference between these two is
        // which sentence is true and not how they behave. A caller that had to
        // learn two arrangements for one act would be a caller told the
        // distinction matters somewhere it does not.
        await Assert.That(Grounding().Statuses).IsEquivalentTo(Withdrawal().Statuses)
            .Because("no 200 - the answer is that the flight is over and what a caller does "
                   + "next is read it; 409 for a flight that has already ended, refused "
                   + "rather than allowed to appear to rewrite an ending that happened.");
    }

    [Test]
    public async Task The_request_says_why_and_says_nothing_about_who()
    {
        await Assert.That(ProtocolSurface.JsonMembers[typeof(FlightGroundingRequest)])
            .IsEquivalentTo((string[])["because"])
            .Because("Article XII derives the actor from the authenticated principal. A "
                   + "caller naming somebody else would be a caller choosing its own "
                   + "attribution, which is the one thing attribution may never be.");

        await Assert.That(typeof(FlightGroundingRequest)
            .GetProperty(nameof(FlightGroundingRequest.Because))!.PropertyType)
            .IsEqualTo(typeof(string))
            .Because("required and non-nullable. The reason is the only thing that survives "
                   + "to tell a later reader why work that could have been done was not.");
    }

    [Test]
    public async Task Grounding_is_declared_where_flights_are_governed()
    {
        await Assert.That(ProtocolSurface.GovernedPrefixes).Contains("/v1/flights");

        await Assert.That(Grounding().Path).StartsWith("/v1/flights/")
            .Because("grounding is done to a flight, so it is addressed as one - and {ref} "
                   + "resolves a uuid or a flight number through the one parser.");
    }

    [Test]
    public async Task The_two_doors_are_two_doors()
    {
        // NOT ONE DOOR WITH A FIELD. An `ending` parameter would put the choice
        // between two sentences in a caller's string, where nothing type-checks
        // it and every client has to be told which values exist. ADR-0017's
        // carried-open question was whether these collapse; two paths is the
        // answer written where it cannot be missed.
        await Assert.That(Grounding().Path).IsNotEqualTo(Withdrawal().Path);
        await Assert.That(typeof(FlightGroundingRequest))
            .IsNotEqualTo(typeof(FlightWithdrawalRequest))
            .Because("two records, so a caller that means one cannot accidentally send the "
                   + "other.");
    }

    [Test]
    public async Task It_is_in_the_vocabulary_and_carries_an_id()
    {
        await Assert.That(Vocabulary.Types).Contains(typeof(FlightGroundingRequest));

        await Assert.That(typeof(FlightGroundingRequest)
            .GetCustomAttributes(typeof(PinnedIdAttribute), inherit: false))
            .IsNotEmpty()
            .Because("every wire type carries a pinned id, so a rename is a rename and not a "
                   + "new type wearing an old name.");
    }
}
