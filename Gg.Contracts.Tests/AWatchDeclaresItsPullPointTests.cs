using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// A watch nobody can perform is refused before it is applied.
/// </summary>
/// <remarks>
/// <para>
/// <b>S39.1-04.</b> ADR-0022 § 5 carries ADR-0015 § 5's rule unchanged: a watch
/// declares <i>its pull point, or it is refused at authoring</i> — a resident
/// runner beside the credential, the control plane itself where it can reach
/// the system of record, or the forge's own scheduler as the always-on party.
/// A strategy's version of this rule says why: <i>"a powered-off pool cannot
/// pull"</i>, and a watch nobody performs is a control that reads as running.
/// </para>
/// <para>
/// <b>`PullPoints` was closed at one and gains two, which costs a contract
/// version.</b> It is a fingerprinted vocabulary, so this is a deliberate
/// widening rather than an edit — and the two new values are legal for a
/// WATCH and not for a strategy, which is why each document validates its own
/// subset rather than the vocabulary narrowing for everybody.
/// </para>
/// </remarks>
public class AWatchDeclaresItsPullPointTests
{
    [Test]
    public async Task A_pull_point_nobody_declared_is_refused()
    {
        var refused = WatchDocument.Validate(
            AWatchDeclaresReferencesTests.AWatch() with { PullPoint = "  " });

        await Assert.That(refused).IsNotNull()
            .Because("a watch with no pull point is a control that reads as running and "
                   + "sweeps never - ADR-0015 section 5's rule, one noun over.");
    }

    [Test]
    public async Task A_pull_point_this_version_does_not_know_is_refused_and_named()
    {
        var refused = WatchDocument.Validate(
            AWatchDeclaresReferencesTests.AWatch() with { PullPoint = "somebodys-laptop" });

        await Assert.That(refused).IsNotNull();
        await Assert.That(refused!).Contains("somebodys-laptop");
    }

    [Test]
    [Arguments(PullPoints.ResidentRunner)]
    [Arguments(PullPoints.ControlPlane)]
    [Arguments(PullPoints.ForgeScheduler)]
    public async Task The_three_the_ADR_names_are_all_legal_for_a_watch(string pullPoint)
    {
        await Assert.That(WatchDocument.Validate(
            AWatchDeclaresReferencesTests.AWatch() with { PullPoint = pullPoint })).IsNull();
    }

    [Test]
    public async Task A_strategy_still_takes_only_the_resident_runner()
    {
        // THE WIDENING IS THE WATCH'S AND NOT EVERYBODY'S. A pool is warmed by
        // a runner on the managed host; the control plane cannot warm a
        // container and a forge's scheduler has never heard of one. So the
        // vocabulary holds three and each document validates the subset it can
        // actually be performed by - which is the shape `DestinationKinds`
        // already has, where a repository's bound accepts only `flight`.
        var strategy = new EnvironmentStrategy
        {
            Kind = StrategyKinds.DockerHost,
            Environment = "payments-ci",
            Inventory = new StrategyInventory { Pool = "payments", Size = 2, Warm = 1 },
            PullPoint = PullPoints.ControlPlane,
            Image = "registry.example/runner@sha256:" + new string('a', 64),
            Bounds = new StrategyBounds { PoolMax = 4 },
        };

        await Assert.That(EnvironmentStrategy.Validate(strategy)).IsNotNull()
            .Because("a strategy performed by the control plane is a pool nothing warms, so "
                   + "the value being legal for a watch must not make it legal here.");
    }
}
