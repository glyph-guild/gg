using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Gg.Contracts.Description;

/// <summary>
/// A reference to one itinerary: the number a person types, or the id a machine
/// holds.
/// </summary>
/// <remarks>
/// <para>
/// <b><see cref="FlightRef"/>'s shape, and for its reason.</b> A person who
/// approved a plan asks about it later by the thing they saw on the screen, so
/// both forms resolve to the same itinerary and the rule for turning text into
/// one lives HERE rather than once in gg and once in the control plane. Two
/// implementations that agree today is the failure being avoided.
/// </para>
/// <para>
/// <b>Its own prefix rather than a letter hung off <c>GG-</c>.</b> Owner's
/// decision, 2026-09-28. An itinerary is not a flight and nothing may read one
/// as one - and a <c>GG-</c>-prefixed itinerary number would parse as a flight
/// number in <see cref="FlightRef.TryParse"/>, which means a read surface
/// handed an itinerary would go looking for a flight and answer 404 on a good
/// day. The two vocabularies are disjoint by construction instead:
/// <c>ITN-7</c> is refused as a flight reference and <c>GG-7</c> is refused as
/// an itinerary one, each by the prefix the other does not have.
/// </para>
/// <para>
/// <b>An itinerary is a group of flights and is not itself flown</b>, so
/// nothing here resolves to a lease, a runner or an exit. What this describes
/// is the <c>{ref}</c> placeholder in <c>/v1/itineraries/{ref}</c>, so the
/// declaration of that path and the rule for reading it are one artifact.
/// </para>
/// </remarks>
public sealed record ItineraryRef
{
    /// <summary>How an itinerary number is written wherever a person will see it.</summary>
    public const string Prefix = "ITN-";

    private ItineraryRef(Guid? id, int? number)
    {
        Id = id;
        Number = number;
    }

    /// <summary>The itinerary id, when the reference was one. Never set with <see cref="Number"/>.</summary>
    public Guid? Id { get; }

    /// <summary>The itinerary number, when the reference was one. Never set with <see cref="Id"/>.</summary>
    public int? Number { get; }

    /// <summary>Renders an itinerary number the way a person will type it back.</summary>
    public static string Format(int number) =>
        Prefix + number.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Reads a reference, or refuses.
    /// </summary>
    /// <remarks>
    /// The prefix is accepted in any case because people type what is quickest,
    /// and rendered in one so an itinerary number looks the same everywhere it
    /// is printed - <see cref="FlightRef.TryParse"/>'s rules, member for member,
    /// because a person copying a reference off a screen should not have to know
    /// which of the two they are holding to know what is allowed to be in it.
    /// </remarks>
    public static bool TryParse(string? text, [NotNullWhen(true)] out ItineraryRef? reference)
    {
        reference = null;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        // Copied out of a terminal, a reference arrives with whatever came
        // with it.
        var trimmed = text.Trim();

        if (trimmed.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            var digits = trimmed[Prefix.Length..];

            // NumberStyles.None: no sign, no thousands separators, no
            // whitespace, exactly as a flight number is read.
            if (!int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
            {
                return false;
            }

            reference = new ItineraryRef(null, number);
            return true;
        }

        // Exact "D" only, for FlightRef's reason: the permissive overload also
        // reads braced and hyphenless forms, which would make two spellings of
        // one id resolve while a third did not.
        if (!Guid.TryParseExact(trimmed, "D", out var id))
        {
            return false;
        }

        reference = new ItineraryRef(id, null);
        return true;
    }

    /// <summary>The canonical rendering, whichever form this reference is.</summary>
    public override string ToString() =>
        Id is { } id ? id.ToString() : Format(Number!.Value);
}
