using Gg.Runner.Pools;

namespace Gg.Runner.Tests;

/// <summary>
/// Where a MEMBER reaches the control plane, which is not always where the host
/// reaches it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Found by the walk, and it would have been found in production the hard
/// way.</b> The resident runner was handing members its own control-plane
/// address. On a deployed host that is right — both are a public URL. On a
/// developer's machine, and in <c>pool-e2e.sh</c>, the host reaches the control
/// plane at <c>127.0.0.1:5199</c> and a container reaches the same service at
/// <c>host.docker.internal:5199</c>.
/// </para>
/// <para>
/// <b>Handing a container <c>127.0.0.1</c> does not fail loudly.</b> It points at
/// the container itself, so the member starts, tries to redeem, gets a connection
/// refused, and dies — while the pool counts a container that exists. That is the
/// silent-warm-member failure this whole slice has been closing, arriving by a
/// new route.
/// </para>
/// <para>
/// <b>Defaulting to the host's own address is right</b>, because on every real
/// deployment they are the same and asking an operator to state it twice invites
/// them to state it wrong. The override exists for the case where they genuinely
/// differ, and says so by name.
/// </para>
/// </remarks>
public class MemberAddressTests
{
    private const string Host = "http://127.0.0.1:5199";

    [Test]
    public async Task A_member_is_given_the_hosts_address_when_nothing_says_otherwise()
    {
        // The deployed case, and the one that must need no configuration: a
        // container and its host both reach a public control plane the same way.
        await Assert.That(MemberBootstrap.ControlPlaneFor(Host, reachableAs: null))
            .IsEqualTo(Host);
        await Assert.That(MemberBootstrap.ControlPlaneFor(Host, reachableAs: ""))
            .IsEqualTo(Host)
            .Because("an empty variable is an unset one, not an instruction to send members "
                   + "nowhere.");
    }

    [Test]
    public async Task A_member_is_given_the_address_containers_can_actually_reach()
    {
        // The local case. Without this the member redeems against itself.
        await Assert.That(MemberBootstrap.ControlPlaneFor(
                Host, reachableAs: "http://host.docker.internal:5199"))
            .IsEqualTo("http://host.docker.internal:5199");
    }

    [Test]
    public async Task The_variable_is_named_for_who_reads_it()
    {
        // A name is the whole documentation of an environment variable. This one
        // is about what a MEMBER can reach, not what the host prefers, and an
        // operator reading it in a unit file should not have to guess which.
        await Assert.That(MemberBootstrap.ReachableAsVariable)
            .IsEqualTo("GG_MEMBER_CONTROL_PLANE");
    }
}
