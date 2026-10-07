using Gg.Client;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// A pool member is named as out of reach, rather than silently absent from the list.
/// </summary>
/// <remarks>
/// <para>
/// <b>S64.5-07, and a limitation measured rather than feared.</b>
/// <c>RunnerRegistry.RedeemMemberAsync</c>'s insert names fifteen columns and
/// <c>profile</c> is not among them, so it is NULL for every member — which makes a
/// profile-derived audience structurally blind to exactly the machines ADR-0037's
/// amendment was written for: <i>"a tenant with a pool does not have one recipient, it
/// has however many members the pool happens to be running."</i>
/// </para>
/// <para>
/// <b>So Decision 5's host-to-member recursion is not an optimisation here, it is the
/// only path to a member</b> — and this slice does not build it. What the slice must do
/// instead is SAY so. An audience that quietly omitted every member would be a list a
/// person reads as complete, and the thing they would notice is a member failing a
/// flight days later.
/// </para>
/// <para>
/// <b>A member is identified by its host, not by its profile.</b>
/// <c>RunnerSummary.HostRunnerId</c> is documented as <i>"null is 'not a member', which
/// is what a laptop says, what a resident says"</i> — so the row that is present and
/// unreachable is distinguishable from the row that simply declared nothing.
/// </para>
/// <para>
/// <b>This is the falsifier the slice wrote down, and it is confirmed.</b> If a
/// tenant's fleet is mostly members, a declared audience reaches almost nobody and
/// <c>--to</c> stays the only useful form.
/// </para>
/// </remarks>
public class APoolMemberIsNotInADeclaredAudienceTests
{
    private const string Locator = "local:acme/widgets";

    private static RunnerSummary ARunner(
        string id, string label, string? profile = null, string? host = null) =>
        new()
        {
            RunnerId = id,
            Label = label,
            State = RunnerStates.Idle,
            Profile = profile,
            HostRunnerId = host,
        };

    private static FleetProfileState AProfile(string name, params string[] credentials) =>
        new()
        {
            Name = name,
            Version = "v1",
            AppliedAt = DateTimeOffset.UnixEpoch,
            Profile = new FleetProfile { Environment = name, Credentials = credentials },
        };

    [Test]
    public async Task A_member_of_a_host_in_the_audience_is_named_as_out_of_reach()
    {
        var audience = CredentialAudience.For(
            Locator,
            [
                ARunner("host", "vmlinux001", profile: "dev"),
                ARunner("member", "gg-pool-ui-2", host: "host"),
            ],
            [AProfile("dev", Locator)]);

        var member = audience.SingleOrDefault(a => a.Label == "gg-pool-ui-2");

        await Assert.That(member).IsNotNull()
            .Because("it is present and cannot be reached, which is a different state from absent - "
                   + "and the one a person has to be told about, because nothing else will tell "
                   + "them until a flight fails on it.");

        await Assert.That(member!.Reachable).IsFalse()
            .Because("no profile is recorded for a member, so a declared audience cannot route to "
                   + "one. Decision 5's host recursion is the answer and it is not built.");
    }

    [Test]
    public async Task And_the_row_says_which_host_would_have_to_pass_it_on()
    {
        var audience = CredentialAudience.For(
            Locator,
            [
                ARunner("host", "vmlinux001", profile: "dev"),
                ARunner("member", "gg-pool-ui-2", host: "host"),
            ],
            [AProfile("dev", Locator)]);

        var member = audience.Single(a => a.Label == "gg-pool-ui-2");

        await Assert.That(member.Through).IsEqualTo("vmlinux001")
            .Because("a person told a member is unreachable can do nothing with that; told WHICH "
                   + "host holds it, they can push to the host - which is the path that exists.");
    }

    [Test]
    public async Task A_member_whose_host_is_not_in_the_audience_is_left_out_entirely()
    {
        // NOT EVERY MEMBER IS THIS CREDENTIAL'S PROBLEM. A member under a host whose
        // profile declares nothing about this locator has no business in the list;
        // naming it would turn "here is who needs this" into "here is the fleet".
        var audience = CredentialAudience.For(
            Locator,
            [
                ARunner("host", "vmlinux001", profile: "prod"),
                ARunner("member", "gg-pool-ui-2", host: "host"),
            ],
            [AProfile("dev", Locator), AProfile("prod", "local:acme/other")]);

        await Assert.That(audience).IsEmpty()
            .Because("the host's profile does not declare this credential, so neither the host nor "
                   + "anything it runs is expected to have it.");
    }

    [Test]
    public async Task A_host_that_IS_reachable_is_not_marked_otherwise()
    {
        // THE SILENCE. A guard that marked everything unreachable would pass the test
        // above and make the whole feature useless, so the reachable case is asserted
        // beside it.
        var audience = CredentialAudience.For(
            Locator,
            [ARunner("host", "vmlinux001", profile: "dev")],
            [AProfile("dev", Locator)]);

        await Assert.That(audience[0].Reachable).IsTrue()
            .Because("a host under a declaring profile is exactly what a push can reach, and if "
                   + "this were false the audience would always be empty of anybody actionable.");

        await Assert.That(audience[0].Through).IsNull()
            .Because("nothing has to pass it on - it is the recipient.");
    }

    [Test]
    public async Task A_member_with_no_host_recorded_is_still_not_a_declared_recipient()
    {
        // A MEMBER WHOSE HOST IS UNKNOWN. HostRunnerId is how a member is recognised,
        // so a member from a control plane too old to send it looks like a laptop -
        // which is the honest reading, and it means "not in a declared audience"
        // rather than "unreachable".
        var audience = CredentialAudience.For(
            Locator,
            [ARunner("member", "gg-pool-ui-2")],
            [AProfile("dev", Locator)]);

        await Assert.That(audience).IsEmpty()
            .Because("nothing declares what it needs and nothing says whose member it is, so there "
                   + "is no derivation that reaches it - and inventing one would push a secret on "
                   + "a guess about a machine gg cannot describe.");
    }
}
