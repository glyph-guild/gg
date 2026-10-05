using Gg.Contracts;
using Gg.Contracts.Authoring;

namespace Gg.Contracts.Tests;

/// <summary>
/// A scheme written into a document survives being rendered back, so a change to it is seen.
/// </summary>
/// <remarks>
/// <para>
/// <b>The third link in a three-link chain, and the first two were not enough.</b> 0.275.0
/// added <see cref="ExposureInventory.Scheme"/> and taught the connector to dial https;
/// 0.281.0's parser learned to read the key. A tenant could then write
/// <c>scheme: https</c>, have it accepted — and be told <c>no changes: the working copy
/// matches the airspace</c>.
/// </para>
/// <para>
/// <b>Measured on the live tenant, 2026-10-05, with a control.</b> Changing
/// <c>port: 8080</c> to <c>8081</c> reported <i>tightening</i>; adding <c>scheme: https</c>
/// reported <i>no changes</i>. A change nothing can see is a change nothing applies, so the
/// member was unreachable by a third route after being reachable by the first two.
/// </para>
/// <para>
/// <b>Because the comparison goes through the renderer.</b> <see cref="EnvelopeText.Render"/>
/// emitted size, hostnames, credentials and port, so a document carrying a scheme rendered
/// back to text identical to one without it. Round-tripping is what the diff trusts, and a
/// member the renderer drops is a member the diff cannot notice.
/// </para>
/// <para>
/// <b>Absent still renders nothing</b>, which is the rule the port already follows: a
/// document that named no scheme must not gain one by being written back, because that
/// changes what it means with nobody editing it — and every exposure in the field names
/// none.
/// </para>
/// </remarks>
public class TheSchemeSurvivesARoundTripTests
{
    private static Exposure AnExposure(string? scheme) => new()
    {
        Kind = "cloudflare-tunnel",
        Inventory = new ExposureInventory
        {
            Size = 8,
            Hostnames = "jdapp-{slot}.example.dev",
            Credentials = "local:exposure/jdapp-{slot}",
            Port = 8080,
            Scheme = scheme,
        },
    };

    [Test]
    public async Task A_rendered_scheme_comes_back_as_itself()
    {
        var rendered = EnvelopeText.Render(AnExposure(OriginSchemes.Https));

        await Assert.That(rendered).Contains("scheme: https")
            .Because("the diff compares through the renderer, so a member it drops is one no "
                   + "change to can ever be seen - which is `no changes` for an edit somebody "
                   + "just made.");

        var reparsed = EnvelopeYaml.ParseExposure(rendered);

        await Assert.That(reparsed.Diagnosis).IsNull();
        await Assert.That(reparsed.Exposure!.Inventory.Scheme).IsEqualTo(OriginSchemes.Https)
            .Because("a round trip that loses it is the same silence one step later.");
    }

    [Test]
    public async Task Two_documents_differing_only_by_scheme_do_not_render_the_same()
    {
        // THE DIFF'S WHOLE QUESTION, asked directly. This is what reported `no changes`
        // on the live tenant for an edit that had just been made.
        await Assert.That(EnvelopeText.Render(AnExposure(OriginSchemes.Https)))
            .IsNotEqualTo(EnvelopeText.Render(AnExposure(scheme: null)))
            .Because("if these render alike then adding a scheme is invisible, and a tenant is "
                   + "told there is nothing to apply.");
    }

    [Test]
    public async Task Absent_still_renders_nothing()
    {
        var rendered = EnvelopeText.Render(AnExposure(scheme: null));

        await Assert.That(rendered).DoesNotContain("scheme")
            .Because("a document that named no scheme must not gain one by being written back: "
                   + "that changes what it means with nobody editing it, and every exposure in "
                   + "the field names none.");
    }
}
