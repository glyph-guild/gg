using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// The runners tab shows what each machine has and how much of it is in use.
/// </summary>
/// <remarks>
/// <para>
/// <b>Used OF LIMIT, never a percentage.</b> It was asked for with the limits,
/// and a bare percentage hides the thing worth knowing: a member at 90% of one
/// core and a host at 90% of sixteen are not the same situation, and only one of
/// them can take another flight.
/// </para>
/// <para>
/// <b>Placed before the wide columns, because this table is already over
/// budget.</b> Measured by driving the real console at 176 columns: `last heard`
/// is clipped to two characters and `lacks` prints whole credential references.
/// So these two go where they stay visible while the right-hand side goes on
/// clipping as it already does - a column that is only there on a wider
/// terminal is a column somebody cannot rely on.
/// </para>
/// <para>
/// <b>A stale figure is not drawn.</b> The contract carries
/// <c>MachineMeasuredAt</c> so a reader can refuse one, and this is the reader:
/// a number on screen means a machine said so recently, not that it said so
/// once. The state column already says <c>offline</c>, so nothing is lost by
/// the dash.
/// </para>
/// </remarks>
public class TheFleetShowsWhatEachMachineHasTests
{
    /// <summary>
    /// The wall clock, because the renderer reads it.
    /// </summary>
    /// <remarks>
    /// <b><c>PaneText.Age</c>'s precedent, and its reason.</b> Nothing at this
    /// layer carries a clock - an age on twenty rows is twenty subtractions and
    /// the renderer does them against <c>UtcNow</c> - so freshness is decided
    /// the same way, and these fixtures are offsets from now rather than fixed
    /// instants. The window is minutes wide, so no test here is racing it.
    /// </remarks>
    private static DateTimeOffset Now => DateTimeOffset.UtcNow;

    private const string Mine = "01a06572-0000-7000-8000-000000000001";

    private static AppState Fleet(params RunnerSummary[] runners) => new()
    {
        ActiveTab = TabId.Runners,
        LocalRunnerId = Mine,
        Runners = new RunnerList { Runners = runners },
    };

    private static RunnerSummary ARunner(
        string label = "vmlinux001",
        int? cpuLimit = 4000,
        int? cpuUsed = 250,
        long? memoryLimit = 16_366_796L * 1024,
        long? memoryUsed = 2_000_000_000,
        DateTimeOffset? measuredAt = null,
        string state = RunnerStates.Idle) => new()
    {
        RunnerId = Mine,
        Label = label,
        State = state,
        LastHeartbeatAt = Now,
        CpuMilliLimit = cpuLimit,
        CpuMilliUsed = cpuUsed,
        MemoryLimitBytes = memoryLimit,
        MemoryUsedBytes = memoryUsed,
        MachineMeasuredAt = measuredAt ?? Now.AddSeconds(-5),
    };

    private static string Cell(AppState state, string column)
    {
        var at = Rows.RunnerColumns.ToList().IndexOf(column);

        if (at < 0)
        {
            throw new InvalidOperationException(
                $"there is no '{column}' column: {string.Join(", ", Rows.RunnerColumns)}");
        }

        return Rows.RunnerCells(Rows.Runners(state)[0])[at];
    }

    [Test]
    public async Task Both_columns_are_there_and_the_cells_line_up_with_them()
    {
        await Assert.That(Rows.RunnerColumns).Contains("cpu");
        await Assert.That(Rows.RunnerColumns).Contains("memory");

        await Assert.That(Rows.RunnerCells(Rows.Runners(Fleet(ARunner()))[0]).Count)
            .IsEqualTo(Rows.RunnerColumns.Count)
            .Because("a cell list a different length from the column list is a table whose "
                   + "every heading is over the wrong column, which is the one mistake this "
                   + "pairing can make.");
    }

    [Test]
    public async Task They_sit_before_the_columns_that_clip()
    {
        var columns = Rows.RunnerColumns.ToList();

        await Assert.That(columns.IndexOf("cpu")).IsLessThan(columns.IndexOf("lacks"))
            .Because("driving the real console at 176 columns, `last heard` is already clipped "
                   + "to two characters - so a figure placed after `lacks` and `advertises` "
                   + "would be a figure nobody can see.");
        await Assert.That(columns.IndexOf("memory")).IsLessThan(columns.IndexOf("advertises"));
    }

    [Test]
    public async Task Cpu_reads_as_cores_used_of_cores_allowed()
    {
        await Assert.That(Cell(Fleet(ARunner(cpuLimit: 4000, cpuUsed: 250)), "cpu"))
            .IsEqualTo("0.3/4")
            .Because("cores rather than millicores on screen, because a person reads cores - "
                   + "and one decimal rather than three, because a quarter of a core is the "
                   + "resolution anybody acts on.");
    }

    [Test]
    public async Task A_whole_number_of_cores_carries_no_decimal_point()
    {
        await Assert.That(Cell(Fleet(ARunner(cpuLimit: 2000, cpuUsed: 2000)), "cpu"))
            .IsEqualTo("2/2")
            .Because("`2.0/2.0` spends three characters on nothing in a table that has none "
                   + "to spare.");
    }

    [Test]
    public async Task A_machine_over_its_limit_says_so()
    {
        await Assert.That(Cell(Fleet(ARunner(cpuLimit: 1000, cpuUsed: 1400)), "cpu"))
            .IsEqualTo("1.4/1")
            .Because("a quota is enforced over a period so an average can land above it, and "
                   + "this is the one reading somebody scanning for a throttled machine is "
                   + "looking for - clamping it to 1/1 would hide exactly that.");
    }

    [Test]
    public async Task Memory_reads_as_a_size_of_a_size()
    {
        await Assert.That(Cell(Fleet(ARunner(
                memoryLimit: 16_366_796L * 1024, memoryUsed: 2_000_000_000)), "memory"))
            .IsEqualTo("1.9G/15.6G");
    }

    [Test]
    public async Task A_small_footprint_is_not_rounded_to_nothing()
    {
        // The real gg-pool-ui-1: 43 MiB in use, uncapped so bounded by the host.
        await Assert.That(Cell(Fleet(ARunner(
                memoryLimit: 16_366_796L * 1024, memoryUsed: 45_744_128)), "memory"))
            .IsEqualTo("43.6M/15.6G")
            .Because("measured on a real pool member - rounding that to 0.0G would report an "
                   + "idle container as using nothing, and the whole point of the column is "
                   + "telling apart a machine doing nothing from one nearly full.");
    }

    // ---- what it refuses to draw ----

    [Test]
    public async Task A_machine_that_has_not_said_shows_a_dash()
    {
        var silent = Fleet(ARunner(
            cpuLimit: null, cpuUsed: null, memoryLimit: null, memoryUsed: null,
            measuredAt: null));

        await Assert.That(Cell(silent, "cpu")).IsEqualTo("-");
        await Assert.That(Cell(silent, "memory")).IsEqualTo("-")
            .Because("a zero would say this machine has no cores and no memory, which is a "
                   + "claim rather than an absence.");
    }

    [Test]
    public async Task A_limit_without_a_reading_shows_the_limit_and_says_the_rest_is_unknown()
    {
        var half = Fleet(ARunner(cpuUsed: null, memoryUsed: null));

        await Assert.That(Cell(half, "cpu")).IsEqualTo("-/4")
            .Because("what a machine IS does not wait for a second look at what it is doing, "
                   + "so a first reading has the limits and no rate - and showing nothing at "
                   + "all would throw away the half that arrived.");
        await Assert.That(Cell(half, "memory")).IsEqualTo("-/15.6G");
    }

    [Test]
    public async Task A_reading_too_old_to_trust_is_not_drawn()
    {
        var stale = Fleet(ARunner(
            measuredAt: Now.AddMinutes(-10), state: RunnerStates.Offline));

        await Assert.That(Cell(stale, "cpu")).IsEqualTo("-")
            .Because("the contract carries MachineMeasuredAt so a reader can refuse a stale "
                   + "figure, and this is the reader: a number on screen has to mean a "
                   + "machine said so recently. The state column already says offline, so "
                   + "the dash costs nothing.");
        await Assert.That(Cell(stale, "memory")).IsEqualTo("-");
    }

    [Test]
    public async Task A_reading_from_the_last_couple_of_minutes_is_still_drawn()
    {
        var recent = Fleet(ARunner(measuredAt: Now.AddSeconds(-70)));

        await Assert.That(Cell(recent, "cpu")).IsEqualTo("0.3/4")
            .Because("the cadence is thirty seconds and a beat can be late, so the window has "
                   + "to be several of them - a column that blanked between reports would "
                   + "flicker on every tick.");
    }
}
