using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Gg.Contracts.Description;

/// <summary>
/// A reference to one flight: the number a person types, or the id a machine
/// holds.
/// </summary>
/// <remarks>
/// <para>
/// A flight number exists because <c>gg show &lt;flight&gt;</c> needs something a
/// person can type. A read surface accepting only uuids would make the number
/// decorative, so both forms resolve to the same flight - and the rule for
/// turning text into one lives HERE, in the contract, rather than once in gg
/// and once in the control plane.
/// </para>
/// <para>
/// Two implementations that agree today is the thing being avoided. They agree
/// until one of them starts accepting lowercase, or stops rejecting a
/// negative, and then a reference a person copied off one screen fails to
/// resolve on the next.
/// </para>
/// <para>
/// A bare integer is deliberately not a reference. It reads as a flight number
/// today and as an index or an offset the moment a list grows paging, and
/// accepting it now would be permanent.
/// </para>
/// <para>
/// It lives beside <see cref="ProtocolSurface"/> rather than among the wire
/// types because it never crosses the wire - the wire carries a string. What
/// it describes is the <c>{ref}</c> placeholder in
/// <c>/v1/flights/{ref}</c>, so the declaration of that path and the rule for
/// reading it are one artifact.
/// </para>
/// </remarks>
public sealed record FlightRef
{
    /// <summary>How a flight number is written wherever a person will see it.</summary>
    public const string Prefix = "GG-";

    private FlightRef(Guid? id, int? number)
    {
        Id = id;
        Number = number;
    }

    /// <summary>The flight id, when the reference was one. Never set with <see cref="Number"/>.</summary>
    public Guid? Id { get; }

    /// <summary>The flight number, when the reference was one. Never set with <see cref="Id"/>.</summary>
    public int? Number { get; }

    /// <summary>Renders a flight number the way a person will type it back.</summary>
    public static string Format(int number) =>
        Prefix + number.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Reads a reference, or refuses.
    /// </summary>
    /// <remarks>
    /// The prefix is accepted in any case because people type what is
    /// quickest, and rendered in one so a flight number looks the same
    /// everywhere it is printed.
    /// </remarks>
    public static bool TryParse(string? text, [NotNullWhen(true)] out FlightRef? reference)
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
            // whitespace. "GG--1" and "GG-1 000" are not flight numbers, and
            // the permissive default would have accepted both.
            if (!int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
            {
                return false;
            }

            reference = new FlightRef(null, number);
            return true;
        }

        // Exact "D" only. The permissive overload also reads braced and
        // hyphenless forms, which would make two spellings of one id resolve
        // while a third did not.
        if (!Guid.TryParseExact(trimmed, "D", out var id))
        {
            return false;
        }

        reference = new FlightRef(id, null);
        return true;
    }

    /// <summary>The canonical rendering, whichever form this reference is.</summary>
    public override string ToString() =>
        Id is { } id ? id.ToString() : Format(Number!.Value);
}
