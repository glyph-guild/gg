using System.Reflection;
using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// A ratchet on the shape, because the pressure runs one way.
/// </summary>
/// <remarks>
/// <para>
/// <b>Rule 3, structurally.</b> The agent may ask; it may not be granted a
/// move, a path, a budget or a destination by asking. Every field somebody will
/// want to add here makes the question more useful and makes it configuration
/// an agent writes - and an agent that could widen its own envelope by
/// describing what it needs is the failure this whole design is arranged
/// against.
/// </para>
/// <para>
/// <b>A ratchet rather than a test somebody edits.</b> The same shape
/// <c>LeaseFeedback</c> holds travelling the other way: what crosses is a
/// value, never a permission. Adding a member here should feel like editing a
/// guard, because it is.
/// </para>
/// </remarks>
public class LoopQuestionSurfaceTests
{
    private static IReadOnlyList<string> Members() =>
        [.. typeof(LoopQuestion)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => !string.Equals(p.Name, "EqualityContract", StringComparison.Ordinal))
            .Select(p => p.Name)
            .Order(StringComparer.Ordinal)];

    [Test]
    public async Task It_carries_the_question_and_nothing_else()
    {
        await Assert.That(Members()).IsEquivalentTo(new[] { nameof(LoopQuestion.Question) })
            .Because("one member. Every addition anybody will propose - what the agent "
                   + "thinks it needs, which files, how long, where it should land - turns a "
                   + "question into a request for permission. Found: "
                   + string.Join(", ", Members()));
    }

    [Test]
    public async Task It_carries_no_field_that_could_widen_anything()
    {
        // NAMED, as well as counted. The count above is the ratchet; this says
        // what the ratchet is protecting, so somebody reading a failure knows
        // which line they crossed rather than only that they crossed one.
        foreach (var widening in (string[])
            ["Moves", "Move", "Scope", "Paths", "Path", "Budget", "WallClock",
             "Destination", "Requires", "Obligations", "Approver", "Envelope"])
        {
            await Assert.That(Members().Contains(widening, StringComparer.Ordinal)).IsFalse()
                .Because($"'{widening}' would let an agent ask for a permission rather than "
                       + "for a decision, and the envelope is the only place a permission "
                       + "comes from.");
        }
    }

    [Test]
    public async Task It_names_no_flight_and_no_person()
    {
        // Correlation is the flight this fact is recorded on, and who answers
        // is the envelope's obligation to name. A question that chose its own
        // approver would be an agent routing its own escalation.
        foreach (var pointing in (string[])
            ["FlightId", "Flight", "LeaseId", "Lease", "Approver", "AskedOf", "Assignee"])
        {
            await Assert.That(Members().Contains(pointing, StringComparer.Ordinal)).IsFalse()
                .Because($"'{pointing}' would make the agent the one deciding who is "
                       + "interrupted.");
        }
    }

    [Test]
    public async Task The_scan_can_see_a_member_so_the_absences_mean_something()
    {
        // The liveness anchor. A reflection read that returned nothing would
        // satisfy every assertion above.
        await Assert.That(Members()).IsNotEmpty();
        await Assert.That(Members()).Contains(nameof(LoopQuestion.Question));
    }
}
