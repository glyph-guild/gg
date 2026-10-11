using Gg.Client;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// The console's review offers every machine a person could send to, not only the ones
/// that declared a need.
/// </summary>
/// <remarks>
/// <para>
/// <b>Owner, 2026-10-11:</b> <i>"we should be able to send credentials adhoc."</i> A
/// review that listed only declared machines said "no machines need it" and stopped, so a
/// credential nothing declares yet - the first SonarCloud token - could not leave the
/// console at all.
/// </para>
/// <para>
/// <b>Asked for, never the default.</b> The CLI's broadcast still reaches only what the
/// tenant declared or a machine reported (ADR-0037 Decision 10: need is declared, never
/// inferred). The offer is a list a person picks from; a machine nobody declared is never
/// sent to unless somebody picked it.
/// </para>
/// </remarks>
public class AnyMachineCanBeOfferedACredentialTests
{
    private const string Locator = "local:analysis/sonarcloud";

    private static RunnerSummary ARunner(
        string id, string label, string state = RunnerStates.Idle, string? host = null) =>
        new() { RunnerId = id, Label = label, State = state, HostRunnerId = host };

    [Test]
    public async Task An_online_machine_nothing_declares_is_offered_when_everyone_is_asked_for()
    {
        var offered = CredentialAudience.For(
            Locator, [ARunner("r2", "vmlinux002")], profiles: [], everyone: true);

        var row = offered.Single();
        await Assert.That(row.Label).IsEqualTo("vmlinux002");
        await Assert.That(row.Declared).IsFalse();
        await Assert.That(row.Reported).IsFalse();
        await Assert.That(row.Reachable).IsTrue();
        await Assert.That(CredentialAudience.Wanted(row)).IsFalse()
            .Because("nothing declared it and it reported nothing, so it is offered, not wanted.");
    }

    [Test]
    public async Task The_broadcast_s_audience_is_unchanged()
    {
        var wanted = CredentialAudience.For(Locator, [ARunner("r2", "vmlinux002")], profiles: []);

        await Assert.That(wanted).IsEmpty()
            .Because("need is declared, never inferred: the CLI's broadcast must not start "
                   + "sending to every machine in the tenant.");
    }

    [Test]
    public async Task An_offline_machine_and_a_pool_member_are_not_offered()
    {
        var offered = CredentialAudience.For(
            Locator,
            [
                ARunner("r1", "vmlinux001", state: RunnerStates.Offline),
                ARunner("h", "pool-host"),
                ARunner("m", "pool-member-1", host: "h"),
            ],
            profiles: [],
            everyone: true);

        await Assert.That(offered.Select(r => r.Label)).IsEquivalentTo(new[] { "pool-host" })
            .Because("an offer is somewhere a send can arrive now: an offline machine holds "
                   + "nothing for later, and a member is reached through its host.");
    }
}
