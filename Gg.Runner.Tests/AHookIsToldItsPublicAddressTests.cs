using System.Diagnostics;
using Gg.Runner.Environments;

namespace Gg.Runner.Tests;

/// <summary>
/// A hook is told the address a person will open, not only the port to bind.
/// </summary>
/// <remarks>
/// <para>
/// <b>Because a stack has to build its own URLs, and it cannot build them from a port.</b>
/// <c>GG_PREVIEW_PORT</c> says where to listen, which is enough for a stack that only
/// serves pages. It is not enough for one that redirects: an application doing OIDC
/// constructs a redirect URI and hands it to an identity provider, and a URI built from
/// <c>localhost</c> and a port is one the provider rejects and the person never returns
/// from. The browser is at the tunnel's public hostname; the stack has to agree.
/// </para>
/// <para>
/// <b>gg already knows it and never said it.</b> <see cref="Exposures.ExposureServed.Url"/>
/// is built from the grant — <i>"built from the grant, never read back"</i> — and is the
/// same address the control plane stamps as <c>preview.url</c>. The hook was handed the
/// port from that same record and not the address beside it, so an environment that needed
/// its public origin had to guess, and could not.
/// </para>
/// <para>
/// <b>Absent stays absent.</b> A flight with no exposure has no public address, and a
/// variable set to an empty string is not the same answer as one nobody set — a shell
/// reading <c>${GG_PREVIEW_URL:-}</c> cannot tell "no preview" from "a preview at
/// nowhere". The same disposition <c>GG_PREVIEW_PORT</c> already takes.
/// </para>
/// </remarks>
public class AHookIsToldItsPublicAddressTests
{
    private static ProcessStartInfo AnInvocation() => new();

    [Test]
    public async Task The_public_address_is_placed_when_there_is_one()
    {
        var info = AnInvocation();

        StackScript.PlacePreview(info, port: 8080, url: "https://jdapp-3.goodgrief.dev");

        await Assert.That(info.Environment[StackScript.PreviewUrlVariable])
            .IsEqualTo("https://jdapp-3.goodgrief.dev")
            .Because("this is the origin the person's browser is at, and a stack that builds "
                   + "a redirect URI from anything else sends them somewhere they cannot "
                   + "come back from.");

        await Assert.That(info.Environment[StackScript.PreviewPortVariable]).IsEqualTo("8080")
            .Because("the port is still where it binds; the address is where it is reached, "
                   + "and a hook needs both because they are different facts.");
    }

    [Test]
    public async Task A_flight_with_no_exposure_is_told_nothing_rather_than_told_nowhere()
    {
        var info = AnInvocation();

        StackScript.PlacePreview(info, port: null, url: null);

        await Assert.That(info.Environment.ContainsKey(StackScript.PreviewUrlVariable)).IsFalse()
            .Because("a variable set to empty is not the answer 'there is no preview' - a shell "
                   + "reading ${GG_PREVIEW_URL:-} cannot tell that from a preview at nowhere.");

        await Assert.That(info.Environment.ContainsKey(StackScript.PreviewPortVariable)).IsFalse()
            .Because("the port takes the same disposition, and did before this.");
    }

    [Test]
    public async Task The_two_are_independent_because_a_grant_can_carry_one_without_the_other()
    {
        // A PORT WITHOUT AN ADDRESS is what every exposure that lets the provider's own
        // ingress decide produces: `LeasePreview.Port` is nullable and the hostname is
        // not. Neither may be invented from the other.
        var portOnly = AnInvocation();
        StackScript.PlacePreview(portOnly, port: 8080, url: null);

        await Assert.That(portOnly.Environment[StackScript.PreviewPortVariable]).IsEqualTo("8080");
        await Assert.That(portOnly.Environment.ContainsKey(StackScript.PreviewUrlVariable))
            .IsFalse();

        var urlOnly = AnInvocation();
        StackScript.PlacePreview(urlOnly, port: null, url: "https://jdapp-3.goodgrief.dev");

        await Assert.That(urlOnly.Environment[StackScript.PreviewUrlVariable])
            .IsEqualTo("https://jdapp-3.goodgrief.dev");
        await Assert.That(urlOnly.Environment.ContainsKey(StackScript.PreviewPortVariable))
            .IsFalse();
    }
}
