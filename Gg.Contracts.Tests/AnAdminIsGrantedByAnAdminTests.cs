using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// How somebody becomes an administrator of their tenant.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE FIRST ONE BOOTSTRAPS, AND AFTER THAT ONLY AN ADMINISTRATOR MAY
/// GRANT.</b> A tenant with no administrator cannot produce one any other way
/// — there is no seeding, no console, and deliberately no way to set the bit
/// from outside the protocol. So the first grant in a tenant is open to any
/// authenticated developer, and every grant after it is not.
/// </para>
/// <para>
/// <b>This is a weak rule on purpose and it is SELF-LIMITING, which is the
/// property that makes it acceptable rather than merely quick.</b> The hole is
/// open exactly once per tenant and closes on its own the moment it is used.
/// An endpoint that let any developer promote themselves forever would make
/// the privilege distinction decorative — the pane would have a gate anybody
/// could open, which is worse than having no gate, because it reads as one.
/// <c>MemberCredentialRedemption</c> is the precedent: anonymous by necessity,
/// single-use, and spent the moment it is redeemed.
/// </para>
/// <para>
/// <b>The last administrator cannot be revoked.</b> A tenant that removed its
/// own last one would be locked out of the surface the bit exists for, and the
/// only way back would be the bootstrap — which is to say, a stranger.
/// </para>
/// </remarks>
public class AnAdminIsGrantedByAnAdminTests
{
    [Test]
    public async Task Granting_is_a_persons_act_on_a_named_principal()
    {
        var granted = ProtocolSurface.Endpoints.SingleOrDefault(
            e => e.Path == "/v1/principals/{principalId}/admin" && e.Method == "POST");

        await Assert.That(granted).IsNotNull()
            .Because("the two repositories cannot reference each other, so this "
                   + "declaration is the only thing holding them together.");

        await Assert.That(granted!.Audience).IsEqualTo(Audience.Developer)
            .Because("a machine that could make somebody an administrator could make "
                   + "ITSELF one, and a runner's principal is the developer who "
                   + "registered it - so the runner audience would hand every machine "
                   + "its owner's authority to widen.");

        await Assert.That(granted.Request).IsNull()
            .Because("the principal is in the path and the grantor is on the credential. "
                   + "There is nothing left for a body to say, and a body would be a "
                   + "second place to name who is being promoted.");

        await Assert.That(granted.Statuses).Contains(403)
            .Because("once a tenant has one, only an administrator may grant.");
        await Assert.That(granted.Statuses).Contains(404)
            .Because("a principal this tenant does not have is not a fact worth "
                   + "confirming.");
    }

    [Test]
    public async Task Revoking_is_the_same_shape_and_can_refuse()
    {
        var revoked = ProtocolSurface.Endpoints.SingleOrDefault(
            e => e.Path == "/v1/principals/{principalId}/admin" && e.Method == "DELETE");

        await Assert.That(revoked).IsNotNull()
            .Because("a privilege that cannot be taken back is one nobody should grant.");

        await Assert.That(revoked!.Statuses).Contains(409)
            .Because("the LAST administrator cannot be revoked. A tenant that removed its "
                   + "own would be locked out of the surface the bit exists for, and the "
                   + "way back would be the bootstrap - which is to say, a stranger.");
    }

    [Test]
    public async Task The_prefix_is_governed_so_nothing_undeclared_serves_under_it()
    {
        await Assert.That(ProtocolSurface.GovernedPrefixes).Contains("/v1/principals")
            .Because("this is the only surface in the protocol that changes what one "
                   + "PERSON may do, so an undeclared route under it would be an "
                   + "unaudited way to hand somebody authority - which is the argument "
                   + "/v1/invitations came in on, applied to privilege rather than to "
                   + "membership.");
    }
}
