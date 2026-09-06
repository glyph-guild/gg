using System.Reflection;

namespace Gg.Contracts.Tests;

/// <summary>
/// A nomination names a work kind and gives a reason, and carries nothing that
/// could widen anything.
/// </summary>
/// <remarks>
/// <para>
/// <b>A RATCHET RATHER THAN A TEST, and the distinction is the point.</b> The
/// pressure on this type is one direction only: every field somebody will want
/// to add - a move it needs, a scope it should get, a budget, a destination, an
/// approver who could wave it through - makes the nomination more useful and
/// makes it configuration an agent writes. A reason able to name a move would
/// be unreviewable policy arriving one sentence at a time.
/// </para>
/// <para>
/// <b>The shape LeaseFeedback holds, travelling the other way.</b> That record
/// carries a rejection into a flight and is asserted to hold nothing that could
/// widen what the flight may do - <i>advice, never authority</i>. This one
/// carries an agent's request out of a flight, and the same rule applies for
/// the same reason: what a classifier hands back must be a value, not a
/// permission.
/// </para>
/// <para>
/// <b>Two members, enumerated exactly.</b> A test that only forbade a list of
/// bad names would pass on a third member nobody thought of, which is how a
/// type grows.
/// </para>
/// </remarks>
public class FlightNominationSurfaceTests
{
    [Test]
    public async Task It_names_a_kind_a_reason_and_a_note_and_nothing_else()
    {
        var members = typeof(FlightNomination)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToList();

        await Assert.That(members).IsEquivalentTo(new[]
        {
            nameof(FlightNomination.WorkKind),
            nameof(FlightNomination.Reason),
            nameof(FlightNomination.Note),
        });
    }

    [Test]
    public async Task It_holds_nothing_that_could_widen_anything()
    {
        var members = typeof(FlightNomination)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToList();

        foreach (var forbidden in (string[])
            ["Moves", "Scope", "Obligations", "Destination", "Destinations", "Budget",
             "WallClock", "Requires", "Approver", "Opens", "Accepts", "Produces", "Executor",
             "Environment", "Repository", "Layer"])
        {
            await Assert.That(members.Contains(forbidden, StringComparer.Ordinal)).IsFalse()
                .Because($"'{forbidden}' on a nomination would let an agent ask for the regime "
                       + "it runs under rather than only which kind of work this is - which is "
                       + "the option this whole design rejected.");
        }
    }

    [Test]
    public async Task It_names_no_flight_and_no_parent()
    {
        // RULE 7 IN THE TYPE. The two flights are related by sharing an intent
        // and by nothing else, so the fact that opens one may not carry a
        // pointer to the other - and a field for it here is the first place
        // somebody would add one.
        var members = typeof(FlightNomination)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToList();

        foreach (var pointer in (string[])["FlightId", "Flight", "Parent", "ParentFlightId",
                                           "OpenedFlightId", "Intent"])
        {
            await Assert.That(members.Contains(pointer, StringComparer.Ordinal)).IsFalse()
                .Because($"'{pointer}' would be the reference ADR-0019 section 1 forbids, "
                       + "arriving as a convenience on the fact rather than as a decision.");
        }
    }

    [Test]
    public async Task A_nomination_without_a_note_is_unchanged()
    {
        // S30.3-01. Optional, so every nomination made before this reads back
        // identically and no classifier has to be taught anything to keep
        // working. Null rather than empty: a classifier with nothing to add and
        // one that wrote an empty note are the same state, and there is no such
        // thing as deliberately handing the next agent a blank page.
        var nomination = new FlightNomination
        {
            WorkKind = "implement",
            Reason = "The item names the file and the expected behaviour.",
        };

        await Assert.That(nomination.Note).IsNull();
        await Assert.That(FlightNomination.Validate(nomination)).IsNull();
    }

    [Test]
    public async Task A_note_is_bounded_at_what_a_real_one_measured()
    {
        // S30.0-02 MEASURED THIS RATHER THAN GUESSING IT. Three real triage
        // runs wrote notes of 728, 774 and 833 characters - the same magnitude
        // as the reason beside it, whose own remark records a measured ~700 and
        // bounds at 2000. So the bound is the same number for the same reason,
        // and the symmetry is a property rather than a coincidence.
        await Assert.That(FlightNomination.MaxNote).IsEqualTo(FlightNomination.MaxReason);

        var over = new FlightNomination
        {
            WorkKind = "implement",
            Reason = "A reason.",
            Note = new string('a', FlightNomination.MaxNote + 1),
        };

        var diagnosis = FlightNomination.Validate(over);

        await Assert.That(diagnosis).IsNotNull();
        await Assert.That(diagnosis!).Contains(
            FlightNomination.MaxNote.ToString(null as IFormatProvider));
        await Assert.That(diagnosis!).Contains(
            (FlightNomination.MaxNote + 1).ToString(null as IFormatProvider))
            .Because("naming both numbers is what lets an author shorten it without "
                   + "guessing how much - and it is never truncated into safety, "
                   + "because half a note reads as a whole one.");
    }

    [Test]
    public async Task A_note_that_says_nothing_is_refused_rather_than_carried()
    {
        // An empty or blank note is a classifier that produced a field instead
        // of declining to fill one, and it would render a fenced block in the
        // next agent's prompt with nothing in it - an attribution to somebody
        // who said nothing.
        foreach (var blank in (string[])["", "   ", "\t"])
        {
            var nomination = new FlightNomination
            {
                WorkKind = "implement",
                Reason = "A reason.",
                Note = blank,
            };

            await Assert.That(FlightNomination.Validate(nomination)).IsNotNull()
                .Because("null is how a nomination says it has nothing to add; "
                       + "an empty string is how it says it forgot.");
        }
    }
}
