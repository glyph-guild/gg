using System.Reflection;
using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// <c>EnvelopeNarrowing</c> still has exactly one member.
/// </summary>
/// <remarks>
/// <para>
/// <b>S30.1-05, and it is a ratchet against this slice specifically.</b> The
/// request that started this asked for instructions <i>tied to a narrowing on
/// work kind</i>. They went on the work-kind layer instead, and the reason is
/// written on the type: a narrowing declares what it ADDS, never what it
/// changes, and there is no member for the latter because a type without one
/// cannot express the failure.
/// </para>
/// <para>
/// <b>A slice that adds text to a narrowing would be the slice that widened
/// it</b>, one member at a time and for a good reason each time. This is what
/// makes that a build failure rather than a review comment.
/// </para>
/// </remarks>
public class NarrowingSurfaceTests
{
    [Test]
    public async Task It_declares_exactly_one_member()
    {
        var members = typeof(EnvelopeNarrowing)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.Name != "EqualityContract")
            .Select(p => p.Name)
            .ToList();

        await Assert.That(members).IsEquivalentTo((string[])["Obligations"])
            .Because("a narrowing declares what it adds. Adding a second member is adding a "
                   + "way for a narrowing to change something, which is the shape the type "
                   + "exists to make unrepresentable. Found: " + string.Join(", ", members));
    }

    [Test]
    public async Task Instructions_did_not_land_here()
    {
        // NAMED, because this slice is the one that would have. A general
        // member count catches it; saying which member says why.
        await Assert.That(typeof(EnvelopeNarrowing).GetProperty("Instructions")).IsNull();
    }
}
