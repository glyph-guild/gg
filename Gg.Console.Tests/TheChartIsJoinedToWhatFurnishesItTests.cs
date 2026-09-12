using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// One row per charted environment, and what is known about each.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE CHART IS THE LIST, and nothing else is.</b> A strategy names the
/// environment it furnishes and a runner advertises one as a label, so both
/// mention environments - but neither enumerates them, and a charted name
/// nothing furnishes appears in neither. A pane built from the strategies
/// would show a tenant a shorter list than the one their envelopes are
/// refused against.
/// </para>
/// <para>
/// <b>NOTHING HERE IS MEASURED, and the columns must not imply it was.</b>
/// <c>warm</c> is a number somebody typed in a document; the attestation is
/// the pull point's own word about a POOL. The count of containers actually
/// running is on neither, so no column claims it.
/// </para>
/// </remarks>
public class TheChartIsJoinedToWhatFurnishesItTests
{
    private static EnvironmentCharted Charted(string name, string? meaning = "ran here") =>
        new()
        {
            Name = name,
            Meaning = meaning,
            Disposition = meaning is null ? LabelDispositions.Stated : LabelDispositions.Measured,
            ChartedBy = "Kevin",
            ChartedAt = DateTimeOffset.Parse("2026-09-01T00:00:00Z"),
        };

    private static EnvironmentStrategyState Strategy(
        string name, string version, string environment, string pool, int warm, int size) =>
        new()
        {
            Name = name,
            Version = version,
            AppliedAt = DateTimeOffset.Parse("2026-09-02T00:00:00Z"),
            Strategy = new EnvironmentStrategy
            {
                Kind = StrategyKinds.DockerHost,
                Environment = environment,
                Inventory = new StrategyInventory { Pool = pool, Size = size, Warm = warm },
                PullPoint = PullPoints.ResidentRunner,
                Image = "example.test/pool@sha256:" + new string('a', 64),
                Bounds = new StrategyBounds { PoolMax = size },
            },
        };

    private static PoolStatus Attested(
        string pool, string outcome, string? diagnosis = null, DateTimeOffset? probed = null) =>
        new()
        {
            Pool = pool,
            Action = PoolActions.Verify,
            Outcome = outcome,
            MeasuredAt = DateTimeOffset.Parse("2026-09-12T14:22:01Z"),
            ScopeProbedAt = probed,
            Diagnosis = diagnosis,
        };

    private static AppState Loaded() => new()
    {
        Chart = new EnvironmentChart
        {
            Environments = [Charted("aspire-payments"), Charted("staging", meaning: null)],
        },
        Strategies = new StrategyList
        {
            Strategies =
                [Strategy("payments", "v3", "aspire-payments", "gg-pool-payments", warm: 2, size: 4)],
        },
        Pools = new PoolLedger
        {
            Pools = [Attested("gg-pool-payments", PoolOutcomes.Verified,
                              probed: DateTimeOffset.Parse("2026-09-12T14:22:00Z"))],
        },
    };

    [Test]
    public async Task Every_charted_name_is_a_row_whether_or_not_anything_furnishes_it()
    {
        var rows = EnvironmentRows.Environments(Loaded());

        await Assert.That(rows.Select(r => r.Environment))
            .IsEquivalentTo((string[])["aspire-payments", "staging (stated)"])
            .Because("the chart is what an envelope is refused against, so a name nothing "
                   + "furnishes is exactly the row somebody needs to see - it is why their "
                   + "flight waits.");
    }

    [Test]
    public async Task A_name_with_no_registered_meaning_says_so_beside_itself()
    {
        // THE DISPOSITION TRAVELS WITH THE NAME, which is the contract's own
        // rule about it: the lie hazard was never the claim, it is a claim
        // wearing measurement's clothes. Folded into the cell rather than given
        // a column, which is what Rows.Advertised already does one pane over.
        var rows = EnvironmentRows.Environments(Loaded());

        await Assert.That(rows.Single(r => r.Environment.StartsWith("staging", StringComparison.Ordinal))
                .Environment)
            .IsEqualTo("staging (stated)");

        await Assert.That(rows.Single(r => r.Environment == "aspire-payments").Environment)
            .IsEqualTo("aspire-payments")
            .Because("measured is the ordinary case, so it costs no words.");
    }

    [Test]
    public async Task The_strategy_the_pool_and_what_it_wants_come_off_the_document()
    {
        var row = EnvironmentRows.Environments(Loaded()).Single(r => r.Environment == "aspire-payments");

        await Assert.That(row.Strategy).IsEqualTo("payments@v3");
        await Assert.That(row.Pool).IsEqualTo("gg-pool-payments");

        // `wants' RATHER THAN `warm'. Both numbers are declared - how many the
        // strategy keeps ready, and how many the pool may hold - and nothing in
        // this console has counted a container. A column called `warm' would
        // read as a measurement, which is this week's recurring defect.
        await Assert.That(row.Wants).IsEqualTo("2 of 4");
    }

    [Test]
    public async Task A_charted_name_nothing_furnishes_says_nothing_rather_than_zero()
    {
        var row = EnvironmentRows.Environments(Loaded())
            .Single(r => r.Environment.StartsWith("staging", StringComparison.Ordinal));

        foreach (var (column, cell) in new[]
        {
            ("strategy", row.Strategy),
            ("pool", row.Pool),
            ("wants", row.Wants),
            ("attested", row.Attested),
            ("measured", row.Measured),
        })
        {
            await Assert.That(cell).IsEmpty()
                .Because($"nobody has said, and `{column}' printing a zero or a dash that reads "
                       + "as one would be an answer where there is no question yet.");
        }
    }

    [Test]
    public async Task The_attestation_is_joined_on_the_pool_the_strategy_names()
    {
        var row = EnvironmentRows.Environments(Loaded()).Single(r => r.Environment == "aspire-payments");

        await Assert.That(row.Attested).IsEqualTo(PoolOutcomes.Verified);
        await Assert.That(row.Measured).IsEqualTo("2026-09-12 14:22:01Z")
            .Because("the outcome without its instant is not health: a verified from four "
                   + "hours ago and one from four seconds ago are different facts.");
    }

    [Test]
    public async Task Only_the_verify_attestation_fills_the_row()
    {
        // REFRESH AND RESET ARE ACTS, AND VERIFY IS THE OBSERVATION. The ledger
        // carries the latest of each per pool, so a row taking whichever came
        // last would read `reset - verified' as the pool's health when what it
        // means is that somebody rebuilt a container.
        var state = Loaded() with
        {
            Pools = new PoolLedger
            {
                Pools =
                [
                    Attested("gg-pool-payments", PoolOutcomes.Verified),
                    new PoolStatus
                    {
                        Pool = "gg-pool-payments",
                        Action = PoolActions.Reset,
                        Outcome = PoolOutcomes.Failed,
                        MeasuredAt = DateTimeOffset.Parse("2026-09-12T16:00:00Z"),
                        Diagnosis = "the daemon answered 409",
                    },
                ],
            },
        };

        var row = EnvironmentRows.Environments(state).Single(r => r.Environment == "aspire-payments");

        await Assert.That(row.Attested).IsEqualTo(PoolOutcomes.Verified);
    }

    [Test]
    public async Task An_unread_chart_is_no_rows_rather_than_an_empty_one()
    {
        // THREE ABSENCES, AND THE TABLE IS NOT WHERE THEY ARE TOLD APART. A
        // chart nobody read, one that is empty, and one that could not be read
        // are three different facts; each keeps its own sentence in the pane,
        // and none of them is a table with a header over nothing.
        await Assert.That(EnvironmentRows.Environments(new AppState())).IsEmpty();

        await Assert.That(EnvironmentRows.Environments(
                new AppState { Chart = new EnvironmentChart { Environments = [] } }))
            .IsEmpty();
    }

    [Test]
    public async Task The_rows_are_in_the_charts_own_name_order()
    {
        // A CURSOR INDEXES THIS ORDER, so it lives here rather than in the view -
        // Rows.cs's rule. Ordinal by name, because a chart read twice must put
        // the same name under the same cursor.
        var state = Loaded() with
        {
            Chart = new EnvironmentChart
            {
                Environments = [Charted("zulu"), Charted("alpha"), Charted("mike")],
            },
        };

        await Assert.That(EnvironmentRows.Environments(state).Select(r => r.Environment))
            .IsEquivalentTo((string[])["alpha", "mike", "zulu"]);
    }

    [Test]
    public async Task The_columns_are_the_row_in_the_same_order()
    {
        await Assert.That(EnvironmentRows.EnvironmentColumns)
            .IsEquivalentTo((string[])
                ["environment", "strategy", "pool", "wants", "attested", "measured"]);
    }
}
