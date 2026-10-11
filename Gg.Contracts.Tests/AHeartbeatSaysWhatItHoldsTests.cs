using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// A heartbeat says which credentials its machine holds, and the fleet read carries what
/// each machine last said (slice seventy-three, S73.1-01).
/// </summary>
/// <remarks>
/// <para>
/// <b>Owner, 2026-10-11:</b> <i>"We should also be able to go to any credential and see
/// which machines it has already been pushed to."</i> Nothing recorded that. ADR-0037
/// Decision 11 asks for the machine's own acknowledgement - <i>"Locators only"</i> - and
/// this is it.
/// </para>
/// <para>
/// <b>What a machine reports, not a log of pushes.</b> A push log would still say a
/// credential is somewhere after the machine lost it, and would miss one placed by hand.
/// </para>
/// <para>
/// <b>Never a vault reference.</b> The only way to ask a vault whether it has one is to
/// read it, so a machine cannot honestly say it holds one - <c>Holds</c> already answers
/// false for every vault reference for that reason.
/// </para>
/// </remarks>
public class AHeartbeatSaysWhatItHoldsTests
{
    [Test]
    public async Task Both_halves_are_declared_on_the_wire()
    {
        await Assert.That(ProtocolSurface.JsonMembers[typeof(RunnerHeartbeat)]).Contains("holds");
        await Assert.That(ProtocolSurface.JsonMembers[typeof(RunnerSummary)]).Contains("holds");
    }

    [Test]
    public async Task Absent_is_never_reported_rather_than_holds_nothing()
    {
        var older = new RunnerHeartbeat { Labels = [] };
        var row = new RunnerSummary { RunnerId = "r", Label = "l", State = "idle" };

        await Assert.That(older.Holds).IsNull()
            .Because("an older runner says nothing, and nothing is not 'holds none'.");
        await Assert.That(row.Holds).IsNull();
    }

    [Test]
    public async Task What_is_reported_is_locators_only_sorted_and_once()
    {
        var reported = CredentialLocator.Reportable(
        [
            "local:analysis/sonarcloud",
            "keyvault://acme-fleet.vault.example.net/repo-read",
            "local:jdx/jdnext",
            "local:analysis/sonarcloud",
        ]);

        await Assert.That(reported).IsEquivalentTo(
            new[] { "local:analysis/sonarcloud", "local:jdx/jdnext" });
        await Assert.That(reported[0]).IsEqualTo("local:analysis/sonarcloud");
    }

    [Test]
    public async Task Something_that_is_not_a_locator_is_left_out()
    {
        // A FILE NAME THAT IS NOT A LOCATOR is a store's own business - a lock, a
        // half-written temp - and reporting it would name something no credential is.
        var reported = CredentialLocator.Reportable(["local:jdx/jdnext", "not a locator", ""]);

        await Assert.That(reported).IsEquivalentTo(new[] { "local:jdx/jdnext" });
    }
}
