namespace Gg.Contracts.Tests;

/// <summary>
/// S53.1-01. A nomination may say what it is about, and one that says nothing
/// is about the flight it came from.
/// </summary>
/// <remarks>
/// <para>
/// <b>The one line that stops a pass from proposing more than one thing.</b>
/// The board supersedes per <c>(nominator, subject)</c>, and every nominator
/// built so far has exactly one subject - so an agent that nominated three
/// pieces of work from one flight wrote three rows and kept the last. That is
/// correct for a flight saying <i>what comes next</i> and wrong for a pass
/// saying <i>here are three pieces of work</i>, and the difference is whether
/// each nomination names what it is about.
/// </para>
/// <para>
/// <b>Absent means what it has always meant, asserted rather than assumed.</b>
/// Every nomination already made omits both members, so the reading of an
/// absence is the compatibility promise: it is about the flight it came from,
/// and nothing has to be taught anything to keep working.
/// </para>
/// </remarks>
public class ANominationMayNameItsSubjectTests
{
    private static FlightNomination Nomination(
        string? subject = null, string? version = null, string? itinerary = null) =>
        new()
        {
            WorkKind = "triage",
            Reason = "The item already names the root cause and the file to change.",
            Subject = subject,
            Version = version,
            Itinerary = itinerary,
        };

    [Test]
    public async Task A_nomination_that_names_none_is_about_the_flight_it_came_from()
    {
        // The whole of backward compatibility, in one assertion: what every
        // classifier ships today is still valid, and both members read absent
        // rather than empty.
        var asItAlwaysWas = new FlightNomination
        {
            WorkKind = "triage",
            Reason = "The item already names the root cause and the file to change.",
        };

        await Assert.That(FlightNomination.Validate(asItAlwaysWas)).IsNull();
        await Assert.That(asItAlwaysWas.Subject).IsNull();
        await Assert.That(asItAlwaysWas.Version).IsNull();
        await Assert.That(asItAlwaysWas.Itinerary).IsNull();
    }

    [Test]
    public async Task A_nomination_may_name_a_subject_of_its_own()
    {
        await Assert.That(FlightNomination.Validate(Nomination(subject: "ado#8412"))).IsNull();

        // A PIECE OF WORK, NOT A WORK ITEM. Some legs trace back to a tracker
        // and some do not, and a plan whose legs are sentences is the case
        // this has to survive - S53.7-02 is the walk that proves it.
        await Assert.That(FlightNomination.Validate(
            Nomination(subject: "write the migration note for the new nominator spelling"))).IsNull();
    }

    [Test]
    public async Task A_subject_is_an_identity_rather_than_a_sentence()
    {
        // SweepNomination's bound, and it is the same number rather than the
        // same magnitude: FlightNomination.MaxSubject IS
        // SweepNomination.MaxSubject, so the compiler holds the agreement and
        // there is nothing here to assert. A bound able to drift would make a
        // subject sayable by a sweep and refused from a flight; one const
        // cannot.
        var tooLong = FlightNomination.Validate(
            Nomination(subject: new string('x', FlightNomination.MaxSubject + 1)));

        await Assert.That(tooLong).IsNotNull();
        await Assert.That(tooLong!).Contains(FlightNomination.MaxSubject.ToString());
    }

    [Test]
    public async Task A_blank_subject_is_refused_rather_than_read_as_an_absence()
    {
        // The note's rule, on a new member: null says this is about the flight
        // it came from, and an empty string says it is about something that
        // was not named. Reading the second as the first would put a nameless
        // row on the same key as every other nameless row.
        foreach (var blank in (string[])["", "   ", "\t"])
        {
            await Assert.That(FlightNomination.Validate(Nomination(subject: blank))).IsNotNull()
                .Because($"'{blank}' is an attempt at a subject that produced nothing.");
        }
    }

    [Test]
    public async Task A_version_names_which_version_of_a_subject_it_is()
    {
        await Assert.That(FlightNomination.Validate(
            Nomination(subject: "ado#8412", version: "rev-3"))).IsNull();

        // A VERSION OF NOTHING. A subjectless nomination is about the flight it
        // came from, and that flight's version is not the nominator's to state
        // - so this is refused at the door rather than left for a store to
        // decide what it was a version of.
        var orphan = FlightNomination.Validate(Nomination(version: "rev-3"));

        await Assert.That(orphan).IsNotNull();
        await Assert.That(orphan!).Contains("subject");
    }

    [Test]
    public async Task A_nomination_may_name_the_itinerary_it_belongs_to()
    {
        await Assert.That(FlightNomination.Validate(
            Nomination(subject: "ado#8412", itinerary: "ITN-7"))).IsNull();

        await Assert.That(FlightNomination.Validate(
            Nomination(subject: "ado#8412", itinerary: Guid.NewGuid().ToString()))).IsNull();

        // AN UNPLANNED FLIGHT JOINS THE SAME ONE, and what it has to say is
        // "what should happen next" - the subjectless reading, keyed on the
        // flight it came from, which is distinct per flight and collapses
        // nothing. Legal on purpose.
        await Assert.That(FlightNomination.Validate(Nomination(itinerary: "ITN-7"))).IsNull();
    }

    [Test]
    public async Task An_itinerary_that_is_not_a_reference_is_refused()
    {
        // A plan naming an unparseable itinerary would quietly have minted a
        // second one instead of revising the one it meant - an absence and a
        // typo producing the same outcome, which is the failure worth refusing
        // rather than resolving.
        foreach (var notOne in (string[])["GG-7", "7", "itinerary-7", "ITN-", "ITN-x", ""])
        {
            await Assert.That(FlightNomination.Validate(Nomination(itinerary: notOne))).IsNotNull()
                .Because($"'{notOne}' names no itinerary, and minting a fresh one for it would "
                       + "be a revision that silently became a new plan.");
        }
    }
}
