using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// The endpoint a resident runner uses to give a pool member an identity of its
/// own.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why it has to exist.</b> A member is about to be the thing that runs a
/// flight, which means it registers, advertises and claims — and registering is
/// authenticated. The only mechanism that has ever worked bakes a copied
/// developer <c>session.json</c> into an image (<c>scripts/pool-e2e.sh</c>), which
/// no tenant can operate: a session lasts twelve hours and carries the whole
/// developer surface.
/// </para>
/// <para>
/// <b>The resident mints; the member redeems.</b> The resident runner is already
/// an authenticated, attributed identity holding a long-lived runner token, so it
/// is the one thing on that host entitled to ask for a member's credential. This
/// deliberately amends <c>RunnerRegistered</c>'s <i>"it cannot register another
/// runner"</i> — for one narrow case, gated on the caller being a resident.
/// </para>
/// <para>
/// <b>A nonce rather than the token.</b> <c>GET /containers/gg-pool-*/json</c> is
/// reachable through the scope proxy, so anything placed in a member's
/// environment is readable by an inspect for the life of the container. What goes
/// in is single-use and worthless once spent; the member exchanges it for the real
/// credential over its own connection.
/// </para>
/// <para>
/// <b>Declared here first, because this repository defines the protocol and the
/// control plane conforms to it.</b> A runner-audience route the surface does not
/// declare is an unaudited way for a runner to reach the control plane — which is
/// the stated reason <c>/v1/pools</c> is a governed prefix at all.
/// </para>
/// </remarks>
public class MemberCredentialSurfaceTests
{
    /// <summary>
    /// By exact path, because the surface already carries other credential
    /// routes and a Contains match found three of them.
    /// </summary>
    private static Endpoint At(string path) =>
        ProtocolSurface.Endpoints.Single(e =>
            string.Equals(e.Method, "POST", StringComparison.Ordinal)
            && string.Equals(e.Path, path, StringComparison.Ordinal));

    private static Endpoint Mint() => At("/v1/pools/{pool}/members/{member}/credential");

    private static Endpoint Redeem() => At("/v1/pools/members/redeem");

    [Test]
    public async Task A_resident_runner_can_mint_a_credential_for_one_member()
    {
        var mint = Mint();

        await Assert.That(mint.Path).IsEqualTo("/v1/pools/{pool}/members/{member}/credential")
            .Because("the pool and the member are both in the path: a credential is minted FOR "
                   + "one member, and nothing about it is a tenant-wide grant.");
        await Assert.That(mint.Audience).IsEqualTo(Audience.Runner);
        await Assert.That(mint.RequiredHeaders).Contains(ProtocolSurface.RunnerHeader)
            .Because("the resident's own runner token is what authorizes this. A session would "
                   + "put a person back in the loop on every warm.");
    }

    [Test]
    public async Task The_mint_can_refuse_a_caller_that_is_not_a_resident()
    {
        // 403 is the one that matters: a MEMBER presenting its own runner token
        // must not be able to mint another. Without that arm, one compromised
        // member mints an unbounded supply.
        await Assert.That(Mint().Statuses).Contains(403);
        await Assert.That(Mint().Statuses).Contains(401);
    }

    [Test]
    public async Task A_member_redeems_its_nonce_for_the_real_credential()
    {
        var redeem = Redeem();

        await Assert.That(redeem.Audience).IsEqualTo(Audience.Anonymous)
            .Because("a member has no credential YET - that is the whole point of redeeming. "
                   + "Requiring one here would be a bootstrap that cannot start.");
        await Assert.That(redeem.Statuses).Contains(409)
            .Because("a nonce is single-use, and the second attempt has to be told it is spent "
                   + "rather than handed a second credential.");
    }

    [Test]
    public async Task What_the_member_is_handed_carries_no_session_and_no_secret_of_ours()
    {
        // Article VIII at the one seam that would break it. The response is a
        // runner identity and nothing wider; a session token here would put a
        // developer's whole surface inside a container running customer code.
        var members = typeof(MemberCredentialIssued).GetProperties().Select(p => p.Name).ToList();

        await Assert.That(members).Contains(nameof(MemberCredentialIssued.RunnerId));
        await Assert.That(members).Contains(nameof(MemberCredentialIssued.ExpiresAt));

        foreach (var forbidden in (string[])["SessionToken", "Session", "PrincipalId", "Secret"])
        {
            await Assert.That(members).DoesNotContain(forbidden)
                .Because($"'{forbidden}' would hand a container more than the one identity it "
                       + "needs, and a member runs a customer's code.");
        }
    }

    [Test]
    public async Task The_credential_is_short_lived_and_says_so_in_its_own_shape()
    {
        // Thirty days is the RESIDENT's cadence, set by a person signing in. A
        // member is created and destroyed by machinery, so its credential should
        // outlive it by as little as possible - and reset revokes.
        await Assert.That(typeof(MemberCredentialIssued).GetProperty(
                nameof(MemberCredentialIssued.ExpiresAt)))
            .IsNotNull();
    }

    [Test]
    public async Task Both_routes_sit_under_the_governed_pools_prefix()
    {
        // THE ANCHOR. A runner-audience route outside a governed prefix is not
        // checked by ProtocolConformanceTests in the control plane, which is the
        // whole reason that prefix list exists.
        foreach (var endpoint in (Endpoint[])[Mint(), Redeem()])
        {
            await Assert.That(ProtocolSurface.GovernedPrefixes.Any(
                    p => endpoint.Path.StartsWith(p, StringComparison.Ordinal)))
                .IsTrue()
                .Because($"'{endpoint.Path}' would otherwise be an undeclared way into the "
                       + "control plane that nothing checks.");
        }
    }
}
