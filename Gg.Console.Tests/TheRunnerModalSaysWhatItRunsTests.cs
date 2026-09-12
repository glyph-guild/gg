using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// The runner modal gains two views: the environments this runner is
/// associated with, and the members alongside it.
/// </summary>
/// <remarks>
/// <para>
/// <b>IN THE MODAL, AND SCOPED TO THE RUNNER.</b> A tenant-wide list of
/// environments answers "what does this tenant have"; the question a person has
/// with a runner under the cursor is "what does THIS machine run, and what else
/// runs it". Two top-level tabs answered the first question, which is not the
/// one that was asked.
/// </para>
/// <para>
/// <b>TWO WAYS A RUNNER IS ASSOCIATED, AND ONLY ONE OF THEM IS ON THE WIRE.</b>
/// A runner that ADVERTISES <c>environment=&lt;name&gt;</c> can claim work
/// there, and the labels cross on every heartbeat. A resident runner MAINTAINS
/// a pool — and nothing on the wire says which: <c>RunnerSummary</c> has no
/// pool, an attestation names a pool without naming the runner that made it,
/// and the pool name lives in a systemd unit's argument. So the maintaining
/// half is reported as a relationship and refuses to name a pool, which is the
/// same refusal <c>Suggestion</c> already makes about somebody else's host.
/// </para>
/// </remarks>
public class TheRunnerModalSaysWhatItRunsTests
{
    private static EnvironmentCharted Charted(string name) => new()
    {
        Name = name,
        Meaning = "ran here",
        Disposition = LabelDispositions.Measured,
        ChartedBy = "Kevin",
        ChartedAt = DateTimeOffset.Parse("2026-09-01T00:00:00Z"),
    };

    private static RunnerSummary Runner(
        string label, string state, string? flight = null, params string[] labels) => new()
        {
            RunnerId = "runner-" + label,
            Label = label,
            State = state,
            CurrentFlightNumber = flight,
            LastHeartbeatAt = DateTimeOffset.Parse("2026-09-12T14:22:01Z"),
            Labels = [.. labels.Select(l => new AdvertisedLabel
            {
                Name = l, Disposition = LabelDispositions.Measured,
            })],
        };

    /// <summary>A console with a fleet, a chart, a strategy and an attestation.</summary>
    private static AppState Fleet(int cursor) => new()
    {
        RunnerSelected = cursor,
        Chart = new EnvironmentChart
        {
            Environments = [Charted("aspire-payments"), Charted("staging")],
        },
        Strategies = new StrategyList
        {
            Strategies =
            [
                new EnvironmentStrategyState
                {
                    Name = "payments",
                    Version = "v3",
                    AppliedAt = DateTimeOffset.Parse("2026-09-02T00:00:00Z"),
                    Strategy = new EnvironmentStrategy
                    {
                        Kind = StrategyKinds.DockerHost,
                        Environment = "aspire-payments",
                        Inventory = new StrategyInventory
                        {
                            Pool = "gg-pool-payments", Size = 4, Warm = 2,
                        },
                        PullPoint = PullPoints.ResidentRunner,
                        Image = "example.test/pool@sha256:" + new string('a', 64),
                        Bounds = new StrategyBounds { PoolMax = 4 },
                    },
                },
            ],
        },
        Pools = new PoolLedger
        {
            Pools =
            [
                new PoolStatus
                {
                    Pool = "gg-pool-payments",
                    Action = PoolActions.Verify,
                    Outcome = PoolOutcomes.Verified,
                    MeasuredAt = DateTimeOffset.Parse("2026-09-12T14:22:01Z"),
                },
            ],
        },
        Runners = new RunnerList
        {
            // ORDERED AS Rows.Runners WILL ORDER THEM, so a cursor index here
            // means the same row the pane is on. None is ours or on this
            // machine, so the order is the fleet's own.
            Runners =
            [
                Runner("gg-pool-payments-1", RunnerStates.Idle,
                       labels: "environment=aspire-payments"),
                Runner("gg-pool-payments-2", RunnerStates.Busy, flight: "GG-1042",
                       labels: "environment=aspire-payments"),
                Runner("vmlinux001:maintain", RunnerStates.Idle),
                Runner("kev-laptop", RunnerStates.Idle, labels: "environment=dev"),
            ],
        },
    };

    [Test]
    public async Task The_modal_has_three_views_and_the_log_is_the_first()
    {
        await Assert.That(RunnerViews.All)
            .IsEquivalentTo((RunnerView[])
                [RunnerView.Log, RunnerView.Environments, RunnerView.Members]);

        await Assert.That(RunnerViews.Title(RunnerView.Log)).IsEqualTo("log")
            .Because("the log is what this modal has always shown, and it stays first so "
                   + "nothing changes for somebody who opens a runner and presses nothing.");

        await Assert.That(RunnerViews.Title(RunnerView.Environments)).IsEqualTo("environments");
        await Assert.That(RunnerViews.Title(RunnerView.Members)).IsEqualTo("members");
    }

    [Test]
    public async Task The_view_turns_and_comes_back_round()
    {
        var at = RunnerView.Log;

        foreach (var expected in (RunnerView[])
                 [RunnerView.Environments, RunnerView.Members, RunnerView.Log])
        {
            at = RunnerViews.Next(at);
            await Assert.That(at).IsEqualTo(expected);
        }
    }

    [Test]
    public async Task An_advertised_environment_is_a_row_and_the_others_are_not()
    {
        // THE CURSOR IS ON gg-pool-payments-1, which advertises exactly one
        // charted name. `staging' is charted and this runner has nothing to do
        // with it, so it is not this runner's business.
        var rows = EnvironmentRows.Environments(Fleet(cursor: 0));

        await Assert.That(rows.Select(r => r.Environment))
            .IsEquivalentTo((string[])["aspire-payments"]);

        var row = rows.Single();

        await Assert.That(row.Strategy).IsEqualTo("payments@v3");
        await Assert.That(row.Pool).IsEqualTo("gg-pool-payments");
        await Assert.That(row.Wants).IsEqualTo("2 of 4");
        await Assert.That(row.Attested).IsEqualTo(PoolOutcomes.Verified);
    }

    [Test]
    public async Task The_members_are_the_runners_that_share_its_environments()
    {
        var rows = EnvironmentRows.Members(Fleet(cursor: 0));

        await Assert.That(rows.Select(r => r.Member))
            .IsEquivalentTo((string[])["gg-pool-payments-1", "gg-pool-payments-2"]);

        // AND THE ONE UNDER THE CURSOR IS MARKED, because a list of peers that
        // does not say which one you opened is a list you have to count.
        await Assert.That(rows.Single(r => r.Member == "gg-pool-payments-1").Here).IsNotEmpty();
        await Assert.That(rows.Single(r => r.Member == "gg-pool-payments-2").Here).IsEmpty();

        await Assert.That(rows.Select(r => r.Member)).DoesNotContain("kev-laptop")
            .Because("it advertises environment=dev, which this runner has nothing to do "
                   + "with - and which nobody charted either.");
    }

    [Test]
    public async Task A_maintaining_runner_says_so_and_refuses_to_name_a_pool()
    {
        // THE CURSOR IS ON vmlinux001:maintain. The suffix is a fact about gg's
        // own packaging - Suggestion makes the same argument about it - so the
        // relationship is reportable. WHICH pool is not: RunnerSummary has no
        // pool, an attestation names a pool without naming the runner that made
        // it, and the name lives in a systemd unit's argument.
        var state = Fleet(cursor: 2);

        await Assert.That(EnvironmentRows.Environments(state)).IsEmpty();

        var said = RunnerDetails.EnvironmentAbsence(state);

        await Assert.That(said).Contains("maintains", StringComparison.Ordinal);
        await Assert.That(said).DoesNotContain("gg-pool-payments", StringComparison.Ordinal)
            .Because("naming the tenant's only pool here would be a guess that reads as a "
                   + "fact, and it would be wrong the moment a tenant has two. Said: " + said);
    }

    [Test]
    public async Task A_runner_that_advertises_nothing_charted_says_that_instead()
    {
        // THE CURSOR IS ON kev-laptop, which advertises environment=dev - a name
        // nobody charted. It is not a maintaining runner and it furnishes no
        // charted environment, and those are different sentences.
        var state = Fleet(cursor: 3);

        await Assert.That(EnvironmentRows.Environments(state)).IsEmpty();

        var said = RunnerDetails.EnvironmentAbsence(state);

        await Assert.That(said).DoesNotContain("maintains", StringComparison.Ordinal);
        await Assert.That(said).Contains("charted", StringComparison.Ordinal)
            .Because("the remedy is to chart the name it already advertises, which is a "
                   + "different act from bringing a machine up. Said: " + said);
    }

    [Test]
    public async Task An_unread_chart_is_not_an_empty_one()
    {
        var state = Fleet(cursor: 0) with { Chart = null };

        await Assert.That(EnvironmentRows.Environments(state)).IsEmpty();

        await Assert.That(RunnerDetails.EnvironmentAbsence(state))
            .Contains("not read", StringComparison.Ordinal)
            .Because("nobody asked, which is not the same as a runner that furnishes "
                   + "nothing - and the remedy is a refresh rather than a chart entry.");
    }

    [Test]
    public async Task The_two_top_level_tabs_are_gone()
    {
        // WHERE THIS LANDED FIRST, AND IT WAS THE WRONG PLACE. The chart and the
        // members were built as two tabs on the bar; what was asked for was the
        // runner's own view of them. The tabs go rather than sit beside this,
        // because two answers to "what environments are there" - one tenant-wide
        // and one runner-scoped - is how a person learns to trust neither.
        foreach (var name in Tabs.All.Select(Tabs.Name))
        {
            await Assert.That(name).IsNotEqualTo("Environments");
            await Assert.That(name).IsNotEqualTo("Members");
        }
    }
}
