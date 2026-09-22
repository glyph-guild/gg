using Gg.Local;

namespace Gg.Cli.Tests;

/// <summary>
/// What a machine can say about its own cpu and memory, and what it refuses to
/// say.
/// </summary>
/// <remarks>
/// <para>
/// <b>Asked for 2026-09-22: track cpu and memory, including limits, and show
/// it in the runners tab.</b> Nothing in either repository measured a machine
/// before this - the one existing read of <c>ProcessorCount</c> goes into a
/// fingerprint and is never reported as a number - so every rule here is new
/// and this is where the arithmetic is held to account.
/// </para>
/// <para>
/// <b>Of the thing that would be THROTTLED.</b> A pool member is a container on
/// a host and both are runners; each reads its own cgroup when it is in one,
/// and the machine's own figures when it is not. So one rule covers both and
/// neither reports the other's numbers.
/// </para>
/// <para>
/// <b>Absence is a value here.</b> A machine that cannot measure something says
/// nothing about it, because a zero would be a claim - and a number that is
/// plausible and wrong is the worst thing this could produce. Measured on
/// vmlinux001 before any of this was written: the host's root cgroup has no
/// <c>cpu.max</c> at all and both pool members are uncapped, so "unlimited" is
/// the common case rather than the exception.
/// </para>
/// </remarks>
public class AMachineSaysWhatItHasTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 22, 8, 0, 0, TimeSpan.Zero);

    /// <summary>A machine whose files say what the dictionary says.</summary>
    private static MachineMeter Reading(Dictionary<string, string> files, int cores = 4) =>
        new(path => files.TryGetValue(path, out var body) ? body : null, () => cores);

    /// <summary>A pool member's shape, as measured on vmlinux001.</summary>
    private static Dictionary<string, string> AContainer(
        string cpuMax = "max 100000",
        string memoryMax = "max",
        long current = 46534656,
        long inactiveFile = 0,
        long cpuMicroseconds = 0) => new(StringComparer.Ordinal)
    {
        ["/sys/fs/cgroup/cgroup.controllers"] = "cpuset cpu io memory pids",
        ["/sys/fs/cgroup/cpu.max"] = cpuMax,
        ["/sys/fs/cgroup/cpu.stat"] =
            $"usage_usec {cpuMicroseconds}\nuser_usec 0\nsystem_usec 0\n",
        ["/sys/fs/cgroup/memory.max"] = memoryMax,
        ["/sys/fs/cgroup/memory.current"] = current.ToString(),
        ["/sys/fs/cgroup/memory.stat"] = $"anon 1000\ninactive_file {inactiveFile}\nslab 20\n",
        ["/proc/meminfo"] = "MemTotal:       16366796 kB\nMemAvailable:    9000000 kB\n",
    };

    /// <summary>A host's shape: no cpu.max on the root cgroup.</summary>
    private static Dictionary<string, string> AHost(
        long idleTicks = 1000, long busyTicks = 0) => new(StringComparer.Ordinal)
    {
        ["/proc/stat"] =
            $"cpu  {busyTicks} 0 0 {idleTicks} 0 0 0 0 0 0\ncpu0 1 2 3 4 5 6 7 0 0 0\n",
        ["/proc/meminfo"] = "MemTotal:       16366796 kB\nMemAvailable:    9000000 kB\n",
    };

    // ---- limits ----

    [Test]
    public async Task A_quota_is_the_limit_it_states()
    {
        var meter = Reading(AContainer(cpuMax: "200000 100000"));

        var read = meter.Read(T0);

        await Assert.That(read.CpuMilliLimit).IsEqualTo(2000)
            .Because("two hundred thousand microseconds of every hundred thousand is two "
                   + "cores, and millicores rather than a fraction because a quota is "
                   + "genuinely fractional and an integer cannot round the answer away.");
    }

    [Test]
    public async Task A_half_core_quota_survives_being_an_integer()
    {
        var read = Reading(AContainer(cpuMax: "50000 100000")).Read(T0);

        await Assert.That(read.CpuMilliLimit).IsEqualTo(500)
            .Because("half a core is why this is not expressed in whole ones - "
                   + "ProcessorCount rounds a fractional quota UP and would call this one.");
    }

    [Test]
    public async Task An_uncapped_container_reports_the_machines_cores()
    {
        var read = Reading(AContainer(cpuMax: "max 100000"), cores: 4).Read(T0);

        await Assert.That(read.CpuMilliLimit).IsEqualTo(4000)
            .Because("measured on vmlinux001: both pool members are uncapped, so what bounds "
                   + "them IS the host's four cores and saying nothing would hide that.");
    }

    [Test]
    public async Task A_host_with_no_cpu_max_at_all_reports_its_cores()
    {
        var read = Reading(AHost(), cores: 4).Read(T0);

        await Assert.That(read.CpuMilliLimit).IsEqualTo(4000)
            .Because("the root cgroup has no cpu.max file - measured, not assumed - so an "
                   + "absent file is a machine with no quota rather than a machine with "
                   + "nothing to say.");
    }

    [Test]
    public async Task An_unlimited_memory_max_falls_back_to_what_is_installed()
    {
        var read = Reading(AContainer(memoryMax: "max")).Read(T0);

        await Assert.That(read.MemoryLimitBytes).IsEqualTo(16366796L * 1024)
            .Because("`max` means this container may take the whole machine, which is a "
                   + "limit and not an absence.");
    }

    [Test]
    public async Task A_memory_max_that_is_a_number_is_the_limit()
    {
        var read = Reading(AContainer(memoryMax: "2147483648")).Read(T0);

        await Assert.That(read.MemoryLimitBytes).IsEqualTo(2147483648L);
    }

    // ---- what is used ----

    [Test]
    public async Task A_containers_memory_leaves_out_the_page_cache()
    {
        // 400 MiB accounted, 300 MiB of it reclaimable file cache.
        var read = Reading(AContainer(
            current: 419430400, inactiveFile: 314572800)).Read(T0);

        await Assert.That(read.MemoryUsedBytes).IsEqualTo(104857600L)
            .Because("memory.current counts page cache, which a kernel gives back under "
                   + "pressure - reporting it would show a container at its ceiling while it "
                   + "is nearly idle, which is exactly the plausible wrong number this whole "
                   + "reading has to avoid.");
    }

    [Test]
    public async Task A_hosts_memory_is_what_is_not_available()
    {
        var read = Reading(AHost()).Read(T0);

        await Assert.That(read.MemoryUsedBytes).IsEqualTo((16366796L - 9000000L) * 1024)
            .Because("MemAvailable rather than MemFree, for the same reason a container "
                   + "subtracts its cache: free is not the same as reclaimable, and a Linux "
                   + "host with a warm cache has almost no free memory and plenty available.");
    }

    // ---- cpu needs two looks ----

    [Test]
    public async Task The_first_reading_says_nothing_about_cpu_used()
    {
        var read = Reading(AContainer(cpuMicroseconds: 5_000_000)).Read(T0);

        await Assert.That(read.CpuMilliUsed).IsNull()
            .Because("a cumulative counter read once is not a rate, and the limits are worth "
                   + "reporting on their own while the interval is still being waited for.");
        await Assert.That(read.Over).IsNull();
        await Assert.That(read.CpuMilliLimit).IsNotNull();
    }

    [Test]
    public async Task Half_a_core_of_work_over_ten_seconds_reads_as_five_hundred_millicores()
    {
        var files = AContainer(cpuMicroseconds: 0);
        var meter = Reading(files);

        _ = meter.Read(T0);

        // Five seconds of cpu in ten seconds of wall clock.
        files["/sys/fs/cgroup/cpu.stat"] = "usage_usec 5000000\n";
        var second = meter.Read(T0.AddSeconds(10));

        await Assert.That(second.CpuMilliUsed).IsEqualTo(500);
        await Assert.That(second.Over).IsEqualTo(TimeSpan.FromSeconds(10));
    }

    [Test]
    public async Task Two_whole_cores_busy_reads_as_two_thousand_millicores()
    {
        var files = AContainer(cpuMax: "400000 100000", cpuMicroseconds: 0);
        var meter = Reading(files);

        _ = meter.Read(T0);
        files["/sys/fs/cgroup/cpu.stat"] = "usage_usec 20000000\n";
        var second = meter.Read(T0.AddSeconds(10));

        await Assert.That(second.CpuMilliUsed).IsEqualTo(2000)
            .Because("twenty seconds of cpu in ten seconds of wall clock is two cores, and "
                   + "a figure that can exceed one thousand is the whole reason this is not "
                   + "a percentage.");
    }

    [Test]
    public async Task A_hosts_cpu_comes_from_the_ticks_that_were_not_idle()
    {
        // One second of wall clock at 100Hz: 400 ticks across four cores, of
        // which 100 were busy - a quarter of one core.
        var files = AHost(idleTicks: 0, busyTicks: 0);
        var meter = Reading(files);

        _ = meter.Read(T0);
        files["/proc/stat"] = "cpu  100 0 0 300 0 0 0 0 0 0\n";
        var second = meter.Read(T0.AddSeconds(1));

        await Assert.That(second.CpuMilliUsed).IsEqualTo(1000)
            .Because("a hundred ticks of busy in one second is one whole core at 100Hz - "
                   + "idle and iowait are what a machine was NOT doing.");
    }

    [Test]
    public async Task A_counter_that_went_backwards_says_nothing()
    {
        var files = AContainer(cpuMicroseconds: 9_000_000);
        var meter = Reading(files);

        _ = meter.Read(T0);

        // A container restarted under the same runner: the counter resets.
        files["/sys/fs/cgroup/cpu.stat"] = "usage_usec 10000\n";
        var second = meter.Read(T0.AddSeconds(10));

        await Assert.That(second.CpuMilliUsed).IsNull()
            .Because("a cumulative counter only goes up, so a fall is a new counter rather "
                   + "than negative work - and a negative rate is worse than no rate.");
    }

    [Test]
    public async Task Two_readings_at_the_same_instant_say_nothing()
    {
        var files = AContainer(cpuMicroseconds: 1000);
        var meter = Reading(files);

        _ = meter.Read(T0);
        files["/sys/fs/cgroup/cpu.stat"] = "usage_usec 2000\n";

        await Assert.That(meter.Read(T0).CpuMilliUsed).IsNull()
            .Because("nothing divides by no time.");
    }

    // ---- a machine that cannot answer ----

    [Test]
    public async Task A_machine_with_no_proc_and_no_cgroup_says_only_its_cores()
    {
        var read = Reading([], cores: 10).Read(T0);

        await Assert.That(read.CpuMilliLimit).IsEqualTo(10000)
            .Because("the core count is the one thing the runtime knows everywhere, and it "
                   + "is a real limit.");
        await Assert.That(read.MemoryLimitBytes).IsNull();
        await Assert.That(read.MemoryUsedBytes).IsNull();
        await Assert.That(read.CpuMilliUsed).IsNull()
            .Because("this is a developer's Mac until somebody writes the sysctl for it, and "
                   + "four absences are honest where four zeroes would be a report of an "
                   + "idle machine with no memory.");
    }

    // ---- the real files, copied off the fleet ----

    [Test]
    public async Task The_hosts_own_files_read_the_way_they_are_written()
    {
        // VERBATIM FROM vmlinux001, 2026-09-22. A fixture somebody typed agrees
        // with whatever they believed; this one disagrees when a format is not
        // what it was thought to be. /proc/stat's first line has TWO spaces
        // after `cpu` and ten fields, of which idle is the fourth number and
        // iowait the fifth - which is the only thing standing between this and
        // reporting a machine as permanently busy.
        var files = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["/proc/stat"] = "cpu  2699710 61405 3823672 1694280489 273059 0 148624 0 0 0\n",
            ["/proc/meminfo"] =
                "MemTotal:       16366796 kB\nMemFree:          659892 kB\n"
              + "MemAvailable:   14764892 kB\n",
        };

        var read = Reading(files, cores: 4).Read(T0);

        await Assert.That(read.CpuMilliLimit).IsEqualTo(4000);
        await Assert.That(read.MemoryLimitBytes).IsEqualTo(16366796L * 1024);
        await Assert.That(read.MemoryUsedBytes).IsEqualTo((16366796L - 14764892L) * 1024)
            .Because("about 1.5 GiB in use of 15.6 GiB installed, which is what the box "
                   + "itself reports - and MemFree would have said 15.1 GiB were gone.");
    }

    [Test]
    public async Task A_pool_members_own_files_read_the_way_they_are_written()
    {
        // VERBATIM FROM gg-pool-ui-1 on vmlinux001, 2026-09-22. Note what the
        // real container says: uncapped on both, and its cache is 20 KiB of a
        // 43 MiB footprint.
        var files = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["/sys/fs/cgroup/cgroup.controllers"] =
                "cpuset cpu io memory hugetlb pids rdma misc dmem\n",
            ["/sys/fs/cgroup/cpu.max"] = "max 100000\n",
            ["/sys/fs/cgroup/cpu.stat"] =
                "usage_usec 180050593\nuser_usec 122365818\nsystem_usec 57684774\n",
            ["/sys/fs/cgroup/memory.max"] = "max\n",
            ["/sys/fs/cgroup/memory.current"] = "45764608\n",
            ["/sys/fs/cgroup/memory.stat"] = "anon 42504192\ninactive_file 20480\n",
            ["/proc/meminfo"] = "MemTotal:       16366796 kB\nMemAvailable:   14764892 kB\n",
        };

        var read = Reading(files, cores: 4).Read(T0);

        await Assert.That(read.CpuMilliLimit).IsEqualTo(4000)
            .Because("`max 100000` is a member nothing caps, so the host's four cores are "
                   + "what bounds it.");
        await Assert.That(read.MemoryLimitBytes).IsEqualTo(16366796L * 1024)
            .Because("and `max` memory means the same: the whole machine.");
        await Assert.That(read.MemoryUsedBytes).IsEqualTo(45764608L - 20480L);
    }

    [Test]
    public async Task What_it_read_is_stamped_with_when_it_looked()
    {
        var read = Reading(AContainer()).Read(T0);

        await Assert.That(read.MeasuredAt).IsEqualTo(T0)
            .Because("a reading is only worth anything beside the moment it was taken, which "
                   + "is what lets a stale one be refused rather than shown.");
    }
}
