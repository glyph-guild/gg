using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// S53.1-02. An itinerary resolves by its number and by its id, and nothing
/// resolves one as a flight.
/// </summary>
/// <remarks>
/// <para>
/// <b>Its own prefix, decided by the owner on 2026-09-28.</b> The alternative
/// was a letter hung off <c>GG-</c>, and the reason it is not is mechanical:
/// <see cref="FlightRef.TryParse"/> reads everything after <c>GG-</c> as an
/// integer, so a <c>GG-</c>-prefixed itinerary number would parse as a flight
/// number and a read surface handed one would go looking for a flight. The two
/// vocabularies are disjoint by construction instead.
/// </para>
/// <para>
/// <b>Both forms, for the flight number's reason.</b> A person who approved a
/// plan asks about it later by the thing they saw on the screen, and a read
/// surface accepting only uuids would make the number decorative.
/// </para>
/// </remarks>
public class AnItineraryRefIsNotAFlightRefTests
{
    [Test]
    public async Task What_it_renders_it_parses_back()
    {
        foreach (var number in (int[])[0, 1, 7, 42, 1042, int.MaxValue])
        {
            await Assert.That(ItineraryRef.TryParse(ItineraryRef.Format(number), out var parsed))
                .IsTrue();
            await Assert.That(parsed!.Number).IsEqualTo(number);
            await Assert.That(parsed.Id).IsNull();
        }
    }

    [Test]
    public async Task A_uuid_is_a_reference_too()
    {
        var id = Guid.NewGuid();

        await Assert.That(ItineraryRef.TryParse(id.ToString(), out var parsed)).IsTrue();
        await Assert.That(parsed!.Id).IsEqualTo(id);
        await Assert.That(parsed.Number).IsNull();
        await Assert.That(parsed.ToString()).IsEqualTo(id.ToString());
    }

    [Test]
    public async Task A_reference_is_a_number_or_an_id_and_never_both()
    {
        ItineraryRef.TryParse(ItineraryRef.Format(7), out var byNumber);
        ItineraryRef.TryParse(Guid.NewGuid().ToString(), out var byId);

        await Assert.That(byNumber!.Id is null && byNumber.Number is not null).IsTrue();
        await Assert.That(byId!.Id is not null && byId.Number is null).IsTrue();
    }

    [Test]
    public async Task Nothing_reads_an_itinerary_as_a_flight()
    {
        // THE WHOLE REASON THE PREFIX IS ITS OWN. Under GG- this assertion
        // could not be written: every itinerary number would be a well-formed
        // flight number, and a read surface would answer 404 on a good day and
        // somebody else's flight on a bad one.
        foreach (var number in (int[])[0, 1, 7, 1042])
        {
            var itinerary = ItineraryRef.Format(number);

            await Assert.That(FlightRef.TryParse(itinerary, out var asFlight)).IsFalse()
                .Because($"'{itinerary}' is an itinerary, and a flight reference that accepted "
                       + "it would send a read looking for the wrong noun.");
            await Assert.That(asFlight).IsNull();
        }
    }

    [Test]
    public async Task Nothing_reads_a_flight_as_an_itinerary()
    {
        // The same rule in the other direction, which is the half that would be
        // skipped: the prefixes must BOTH be refused by the other parser, or
        // one of the two surfaces silently accepts a reference to the other
        // noun.
        foreach (var number in (int[])[0, 1, 42, 1042])
        {
            var flight = FlightRef.Format(number);

            await Assert.That(ItineraryRef.TryParse(flight, out var asItinerary)).IsFalse()
                .Because($"'{flight}' is a flight, and an itinerary read that accepted it would "
                       + "answer about a group that does not exist.");
            await Assert.That(asItinerary).IsNull();
        }
    }

    [Test]
    public async Task An_id_resolves_the_same_itinerary_the_number_does()
    {
        // Both forms resolve to ONE itinerary: this type carries whichever
        // arrived, and the surface that resolves it branches on which. What is
        // asserted here is that neither form loses anything on the way through.
        var id = Guid.NewGuid();

        ItineraryRef.TryParse(id.ToString(), out var byId);
        ItineraryRef.TryParse("  " + ItineraryRef.Format(7).ToLowerInvariant() + "\n", out var byNumber);

        await Assert.That(byId!.Id).IsEqualTo(id);
        await Assert.That(byNumber!.Number).IsEqualTo(7);

        // Read in any case, written in one - a plan's number looks the same
        // everywhere it is printed, whatever somebody typed.
        await Assert.That(byNumber.ToString()).IsEqualTo(ItineraryRef.Format(7));
    }

    [Test]
    public async Task Things_that_are_not_a_reference_are_refused()
    {
        // FlightRef's list, on this prefix, and for its reasons: a bare integer
        // reads as a number today and as an index the moment a list gains
        // paging, and NumberStyles.None is what refuses a sign and a thousands
        // separator.
        foreach (var text in (string?[])[null, "", "   ", "ITN-", "ITN-x", "ITN--1", "7", "itn7",
                                         "ITN-7-", "not-a-uuid", "ITN-1 000"])
        {
            await Assert.That(ItineraryRef.TryParse(text, out var parsed)).IsFalse()
                .Because($"'{text}' is not an itinerary reference.");
            await Assert.That(parsed).IsNull()
                .Because("a refused parse must not hand back half a reference.");
        }
    }
}
