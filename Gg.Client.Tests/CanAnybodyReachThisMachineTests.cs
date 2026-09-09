using Gg.Client;

namespace Gg.Client.Tests;

/// <summary>
/// The doctor says whether anything outside could reach this machine.
/// </summary>
/// <remarks>
/// <para>
/// <b>The gap this fills was reported as a network problem.</b> Watching a
/// runner failed with "Both ends spoke and no route between them was found",
/// and the cause was that this console had no STUN server configured — so it
/// offered only HOST candidates, its own address on its own LAN, which nothing
/// on another network can reach. Nothing anywhere said so until the moment a
/// handshake had already failed.
/// </para>
/// <para>
/// <b>Beside the <c>channel</c> check rather than inside it.</b> That one
/// deliberately uses no ICE servers — its own remark says a check needing STUN
/// "would fail on an aeroplane and teach somebody to ignore it" — because what
/// it tests is the SCTP association on this binary. This is the other question:
/// not "can a channel open at all" but "could anyone reach me to open one".
/// </para>
/// <para>
/// <b>Not blocking, and fixable.</b> A machine that cannot be reached still
/// flies flights, takes work and lands them; what it cannot do is let somebody
/// watch a runner from elsewhere. And the remedy is a variable on this machine,
/// which is the definition of fixable.
/// </para>
/// </remarks>
public class CanAnybodyReachThisMachineTests
{
    [Test]
    public async Task Nowhere_to_ask_is_named_with_the_variable_that_fixes_it()
    {
        var check = Doctor.ReachableCheck(stunServers: [], reflexive: []);

        await Assert.That(check.Name).IsEqualTo(DoctorChecks.Reachable);
        await Assert.That(check.Passed).IsFalse();
        await Assert.That(check.Blocking).IsFalse()
            .Because("a machine nobody can reach still flies flights, takes work and lands "
                   + "them. Blocking would stop a fleet over one feature.");
        await Assert.That(check.Fixable).IsTrue()
            .Because("it is a variable on this machine, which is the definition of fixable.");
        await Assert.That(check.Fix).IsNotNull();
        await Assert.That(check.Fix!).Contains("GG_STUN_SERVERS");
    }

    [Test]
    public async Task A_reflexive_candidate_is_the_thing_that_means_reachable()
    {
        // NOT "IS IT CONFIGURED". A typo, a server that is down, a network that
        // eats UDP - all of them leave the variable set and the machine
        // unreachable, and a check that only read configuration would pass on
        // every one of them.
        var check = Doctor.ReachableCheck(
            stunServers: ["stun:stun.example:3478"], reflexive: ["203.0.113.7:54321"]);

        await Assert.That(check.Passed).IsTrue();
        await Assert.That(check.Detail).Contains("203.0.113.7")
            .Because("the address is this machine's own and seeing it is how somebody knows "
                   + "the answer came from outside. Said: " + check.Detail);
    }

    [Test]
    public async Task A_server_that_answered_nothing_is_not_the_same_as_none_configured()
    {
        // TWO DIFFERENT REMEDIES. One is "set the variable"; the other is
        // "the server you named did not answer", and sending somebody to set
        // something already set is how a check teaches people to ignore it.
        var check = Doctor.ReachableCheck(
            stunServers: ["stun:stun.example:3478"], reflexive: []);

        await Assert.That(check.Passed).IsFalse();
        await Assert.That(check.Fixable).IsTrue();
        await Assert.That(check.Detail).Contains("stun:stun.example:3478")
            .Because("naming what it asked is the difference between a typo and an outage. "
                   + "Said: " + check.Detail);
        await Assert.That(check.Fix!.Contains("Set GG_STUN_SERVERS", StringComparison.Ordinal))
            .IsFalse()
            .Because("it is already set; the remedy is a different one. Fix: " + check.Fix);
    }
}
