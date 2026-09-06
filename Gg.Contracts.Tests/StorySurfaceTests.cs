using System.Reflection;

namespace Gg.Contracts.Tests;

/// <summary>
/// The story's entry carries no shape a reader would have to branch on.
/// </summary>
/// <remarks>
/// <b>A ratchet over the type, not a test somebody edits.</b> The failure this
/// prevents is gradual: one convenient member holding "the bits this kind
/// needs", and the union the flight log already has is back, one property at a
/// time. <c>FlightNomination</c> is guarded the same way and for the same
/// reason.
/// </remarks>
public class StorySurfaceTests
{
    // ---- S32.1-02 ----

    /// <summary>What a story entry may be made of, and nothing else.</summary>
    private static readonly HashSet<Type> Allowed =
    [
        typeof(string), typeof(string[]), typeof(IReadOnlyList<string>),
        typeof(DateTimeOffset), typeof(int?), typeof(Actor),
    ];

    [Test]
    public async Task A_story_entry_holds_only_scalars_a_string_list_and_an_actor()
    {
        var offending = typeof(StoryEntry)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => !Allowed.Contains(p.PropertyType))
            .Select(p => $"{p.Name}: {p.PropertyType.Name}")
            .ToList();

        await Assert.That(offending).IsEmpty()
            .Because("a member able to hold a nested per-kind object is the union this shape "
                   + "exists to remove, and it arrives one convenient property at a time. "
                   + "Found: " + string.Join(", ", offending));
    }

    [Test]
    public async Task The_shape_check_can_tell_an_offending_member_from_an_allowed_one()
    {
        // The poison twin. Without it the assertion above also passes on a set
        // that considers every type allowed.
        await Assert.That(Allowed.Contains(typeof(string))).IsTrue();
        await Assert.That(Allowed.Contains(typeof(Dictionary<string, string>))).IsFalse()
            .Because("a dictionary is exactly the shape the flight log ships today.");
    }
}
