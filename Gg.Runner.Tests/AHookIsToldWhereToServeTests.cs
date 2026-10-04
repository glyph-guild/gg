using Gg.Runner.Environments;
using Gg.Runner.Exposures;

namespace Gg.Runner.Tests;

/// <summary>
/// gg tells a hook where to serve, because the address is fixed and the stack
/// is what moves.
/// </summary>
/// <remarks>
/// <para>
/// <b>S58.4-01, and it is the half that fixes a live defect.</b> The connector
/// forwards to <c>http://localhost:&lt;port&gt;</c>, written from the exposure
/// document — <c>jdapp.yaml</c> says <c>port: 8080</c> — and brought up ON THE
/// BEAT, once per machine. ADR-0033 Decision 8 names what that costs:
/// <c>ui-preview</c> also says 8080, the Angular app binds <b>4200</b>, and
/// traefik holds 443 and 8080, so a person opening the preview reaches traefik.
/// </para>
/// <para>
/// <b>The address cannot move, so the stack has to.</b> Re-dialling at whatever
/// a flight reported is forbidden by the loop's own comment — <i>"two connectors
/// on one token are replicas of one tunnel and the provider picks between them,
/// so a machine that re-dialled on every beat would race itself"</i> — so gg
/// says where to serve and the hook obeys.
/// </para>
/// <para>
/// <b>This is what <c>PREVIEW_PORT</c> was reaching for.</b> That is an untyped
/// string in a tenant document plus a sentence of prose, and <i>nothing in any
/// schema knows that name means a port</i>. <c>GG_PREVIEW_PORT</c> is the
/// platform's, placed rather than declared — the <c>GG_</c> prefix is the same
/// one <c>GG_IMAGE_DIGEST</c> and <c>GG_POOL_ENDPOINT</c> carry, and it says
/// whose value it is.
/// </para>
/// <para>
/// <b>Absent when there is no exposure</b>, which is every flight that serves
/// nobody. A port named without a tunnel behind it is a number a stack would
/// bind for no reason.
/// </para>
/// </remarks>
public class AHookIsToldWhereToServeTests
{
    [Test]
    public async Task A_hook_is_told_the_port_the_connector_dials()
    {
        var start = new System.Diagnostics.ProcessStartInfo();

        StackScript.PlacePreview(start, port: 8080);

        await Assert.That(start.Environment["GG_PREVIEW_PORT"]).IsEqualTo("8080")
            .Because("the connector forwards to localhost on this port and nothing else, so a "
                   + "stack that binds anywhere else serves a person whatever is already "
                   + "listening there - which on this tenant is traefik.");
    }

    [Test]
    public async Task A_flight_with_no_exposure_is_told_nothing()
    {
        var start = new System.Diagnostics.ProcessStartInfo();

        StackScript.PlacePreview(start, port: null);

        await Assert.That(start.Environment.ContainsKey("GG_PREVIEW_PORT")).IsFalse()
            .Because("every flight that serves nobody has no tunnel behind it, and a port "
                   + "named without one is a number a stack would bind for no reason.");
    }

    [Test]
    public async Task It_is_the_platforms_and_cannot_be_displaced()
    {
        // THE EXECUTOR'S ORDERING RULE, reused for its reason: gg granted this
        // slot, and a value arriving from the runner's own environment would
        // point a stack at a port no tunnel forwards to.
        var start = new System.Diagnostics.ProcessStartInfo();
        start.Environment["GG_PREVIEW_PORT"] = "4200";

        StackScript.PlacePreview(start, port: 8080);

        await Assert.That(start.Environment["GG_PREVIEW_PORT"]).IsEqualTo("8080");
    }

    [Test]
    public async Task The_served_slot_carries_the_port_it_was_dialled_with()
    {
        // IT USED TO DROP IT. ExposureServed carried the ORIGIN and not the
        // number inside it, so the one place that knows where the connector
        // forwards could not say it in a form a stack could bind. Parsing it
        // back out of the origin string would be a second source for one fact.
        var served = new ExposureServed
        {
            Exposure = "jdapp",
            Slot = "slot-1",
            Url = "https://preview.example.invalid",
            Origin = "http://localhost:8080",
            Port = 8080,
        };

        await Assert.That(served.Port).IsEqualTo(8080);
    }
}
