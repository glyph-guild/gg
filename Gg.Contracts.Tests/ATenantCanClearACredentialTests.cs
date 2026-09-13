using System.Reflection;
using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// A tenant can tell its runners to forget a credential, and what travels is a
/// reference like everything else.
/// </summary>
/// <remarks>
/// <para>
/// <b>The other half of placing one, and the half that has to work with nobody
/// present.</b> Putting a credential on a machine is a person's act over an
/// introduced channel, inside a flight. Taking one back is revocation, and
/// revocation that needs somebody at a terminal is revocation that happens on
/// Monday. So this rides the heartbeat, which is where signalling already
/// rides: <i>a runner gains no new loop, no long poll and no listening socket,
/// so its outbound-only posture survives by construction rather than by
/// care.</i>
/// </para>
/// <para>
/// <b>Article VIII is untouched, and this is the direction that was always
/// safe.</b> A locator is a reference - the control plane already holds them
/// and already sends them on a lease. Naming one to be dropped is strictly less
/// than naming one to be used.
/// </para>
/// <para>
/// <b>It grants nothing a hostile control plane did not already have.</b> The
/// worst it can do is make flights fail for want of a credential - and a
/// control plane that wanted that outcome could simply stop granting leases, or
/// grant them naming credentials nobody registered. The failure is now legible
/// either way: <c>credential-unreadable</c> names the locator and the machine.
/// </para>
/// </remarks>
public class ATenantCanClearACredentialTests
{
    [Test]
    public async Task A_beat_can_name_credentials_this_runner_should_no_longer_hold()
    {
        var member = typeof(HeartbeatAccepted).GetProperty("Forget");

        await Assert.That(member).IsNotNull();

        await Assert.That(member!.PropertyType).IsEqualTo(typeof(IReadOnlyList<string>))
            .Because("locators, which are references. Anything richer here would be a "
                   + "second shape for a credential on a path that already has one.");
    }

    [Test]
    public async Task Absent_when_there_is_nothing_to_forget()
    {
        // THE RULE ITS TWO SIBLINGS ALREADY FOLLOW: "absent when empty rather
        // than an empty list, so an idle fleet's heartbeat body is byte-for-byte
        // what it was and the two repositories stay free to upgrade out of
        // step." A fleet where nothing was revoked must not pay a member.
        var member = typeof(HeartbeatAccepted).GetProperty("Forget")!;

        await Assert.That(new NullabilityInfoContext().Create(member).WriteState)
            .IsEqualTo(NullabilityState.Nullable);

        await Assert.That(new HeartbeatAccepted { NextHeartbeatSeconds = 15 }.Forget).IsNull()
            .Because("a runner one version behind reads this response exactly as it did "
                   + "before, and a runner ahead of its control plane reads absence as "
                   + "nothing to do rather than as everything to drop.");
    }

    [Test]
    public async Task It_is_declared_like_every_other_member_of_the_beat()
    {
        await Assert.That(ProtocolSurface.JsonMembers[typeof(HeartbeatAccepted)])
            .Contains("forget")
            .Because("a member nobody wrote down is a member nobody can audit, and this "
                   + "one tells a machine to destroy something.");
    }

    [Test]
    public async Task A_locator_is_too_short_to_be_a_credential()
    {
        // WHAT KEEPS THIS A REFERENCE RATHER THAN A STRING. The runner validates
        // each one before acting, by the contract's own rule, and the rule is
        // shaped so a pasted token does not fit: "bounded so a pasted credential
        // does not fit. Provider tokens are longer than this, and a repository
        // slug is very much shorter."
        await Assert.That(CredentialLocator.Validate("local:acme/widgets")).IsNull();

        await Assert.That(CredentialLocator.Validate(
                "ghp_" + new string('x', 200)))
            .IsNotNull()
            .Because("the charset and the bound are what stop a secret travelling in a "
                   + "member named for references.");
    }
}
