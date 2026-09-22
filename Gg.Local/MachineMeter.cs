using System.Globalization;

namespace Gg.Local;

/// <summary>
/// What a machine has, and how much of it is being used.
/// </summary>
/// <remarks>
/// <b>Every member may be absent, and absence is the answer</b> rather than the
/// absence of one. A machine that cannot measure its memory says nothing about
/// its memory; a zero there would be a report of a machine with none. The one
/// number almost every machine can give is its core count, because the runtime
/// knows it everywhere.
/// </remarks>
public sealed record MeasuredMachine
{
    /// <summary>When this machine looked.</summary>
    public required DateTimeOffset MeasuredAt { get; init; }

    /// <summary>
    /// The interval <see cref="CpuMilliUsed"/> is an average over, or null when
    /// there is no figure yet.
    /// </summary>
    public TimeSpan? Over { get; init; }

    /// <summary>
    /// How much cpu this machine may use, in thousandths of a core.
    /// </summary>
    /// <remarks>
    /// <b>Millicores, as a scheduler expresses them.</b> A cgroup quota is
    /// genuinely fractional - half a core is a legal limit - and
    /// <c>ProcessorCount</c> rounds such a quota UP to one, so whole cores
    /// cannot carry the answer.
    /// </remarks>
    public int? CpuMilliLimit { get; init; }

    /// <summary>How much of it was used over <see cref="Over"/>, in the same unit.</summary>
    /// <remarks>
    /// It can exceed <see cref="CpuMilliLimit"/> briefly - a quota is enforced
    /// over a period, and an average taken across period boundaries can land
    /// above it. Reported as measured rather than clamped, because a number
    /// quietly held at its ceiling is a number that cannot show a machine being
    /// throttled.
    /// </remarks>
    public int? CpuMilliUsed { get; init; }

    /// <summary>The ceiling in bytes: the cgroup's, or what is installed.</summary>
    public long? MemoryLimitBytes { get; init; }

    /// <summary>
    /// What is in use in bytes, not counting what the kernel would give back.
    /// </summary>
    public long? MemoryUsedBytes { get; init; }
}

/// <summary>
/// Reads this machine's cpu and memory, from whichever of them it is subject
/// to.
/// </summary>
/// <remarks>
/// <para>
/// <b>Of the thing that would be THROTTLED.</b> A pool member is a container on
/// a host, and both are runners that report for themselves - so this reads the
/// cgroup when it is in one and the machine when it is not, and neither ever
/// reports the other's figures. That is one rule, and it is the only one that
/// gives both a meaning a reader can act on.
/// </para>
/// <para>
/// <b>Files, not interop.</b> Everything here is a read of <c>/proc</c> or
/// <c>/sys/fs/cgroup</c>, so it is Native-AOT-safe with no P/Invoke and no
/// side-car - and side-cars are the way this codebase has previously shipped
/// nothing at all. What a platform has no file for, it does not answer.
/// </para>
/// <para>
/// <b>cgroup v2 only, deliberately.</b> Measured on the fleet: the host and its
/// members are all unified v2. A v1 container would find none of these files
/// and fall through to <c>/proc</c>, which is the HOST's figures presented as
/// the container's - so v1 is detected by the absence of
/// <c>cgroup.controllers</c> and answers nothing rather than something wrong.
/// </para>
/// <para>
/// <b>The cpu figure needs two looks.</b> Both sources are cumulative counters,
/// so a rate is a difference over an elapsed time and the first reading has
/// neither. The limits do not wait for it.
/// </para>
/// </remarks>
/// <param name="read">A file's contents, or null when there is no such file.</param>
/// <param name="cores">
/// How many processors the runtime can see. This honours a cgroup cpu quota and
/// a processor affinity, so it is a real limit even where no file states one.
/// </param>
public sealed class MachineMeter(Func<string, string?> read, Func<int> cores)
{
    private const string Controllers = "/sys/fs/cgroup/cgroup.controllers";
    private const string CpuMax = "/sys/fs/cgroup/cpu.max";
    private const string CpuStat = "/sys/fs/cgroup/cpu.stat";
    private const string MemoryMax = "/sys/fs/cgroup/memory.max";
    private const string MemoryCurrent = "/sys/fs/cgroup/memory.current";
    private const string MemoryStat = "/sys/fs/cgroup/memory.stat";
    private const string ProcStat = "/proc/stat";
    private const string MemInfo = "/proc/meminfo";

    /// <summary>
    /// What a tick of <c>/proc/stat</c> is worth in microseconds.
    /// </summary>
    /// <remarks>
    /// <c>USER_HZ</c> is 100 on every Linux this runs on and is not
    /// configurable without rebuilding the kernel. Reading it properly means
    /// <c>sysconf(_SC_CLK_TCK)</c>, which is interop for a constant - and the
    /// cgroup path, which every container takes, reports microseconds directly
    /// and does not come through here at all.
    /// </remarks>
    private const long MicrosecondsPerTick = 10_000;

    private long? _busyMicroseconds;
    private DateTimeOffset? _lookedAt;

    /// <summary>The real thing, reading this machine's own files.</summary>
    public static MachineMeter OfThisMachine() =>
        new(
            path =>
            {
                try
                {
                    return File.Exists(path) ? File.ReadAllText(path) : null;
                }
                catch (Exception)
                {
                    // A FILE THAT REFUSES IS A FILE THAT IS NOT THERE. These
                    // live in synthetic filesystems that can answer EIO or
                    // EACCES depending on the sandbox, and a machine that
                    // cannot read its own meter must report less rather than
                    // fail a heartbeat.
                    return null;
                }
            },
            () => Environment.ProcessorCount);

    /// <summary>What this machine has, as of now.</summary>
    public MeasuredMachine Read(DateTimeOffset now)
    {
        var inACgroup = read(Controllers) is { Length: > 0 };

        var limit = CpuLimit(inACgroup ? read(CpuMax) : null);
        var installed = Installed();

        var (used, over) = Busy(now, inACgroup);

        return new MeasuredMachine
        {
            MeasuredAt = now,
            Over = over,
            CpuMilliLimit = limit,
            CpuMilliUsed = used,
            MemoryLimitBytes = MemoryLimit(inACgroup ? read(MemoryMax) : null, installed),
            MemoryUsedBytes = inACgroup ? InUse() ?? Resident() : Resident(),
        };
    }

    /// <summary>
    /// The quota if there is one, and the machine's cores if there is not.
    /// </summary>
    /// <remarks>
    /// <b>An absent or unlimited quota is not an absent limit.</b> Measured on
    /// the fleet: the root cgroup has no <c>cpu.max</c> and every pool member
    /// says <c>max</c>, so what actually bounds them is the machine, and
    /// answering nothing would hide that they are uncapped.
    /// </remarks>
    private int? CpuLimit(string? cpuMax)
    {
        if (cpuMax?.Trim().Split(' ') is [var quota, var period, ..]
            && long.TryParse(quota, CultureInfo.InvariantCulture, out var each)
            && long.TryParse(period, CultureInfo.InvariantCulture, out var per)
            && per > 0
            && each > 0)
        {
            return (int)(each * 1000 / per);
        }

        var seen = cores();

        return seen > 0 ? seen * 1000 : null;
    }

    /// <summary>The rate since the last look, and what it covers.</summary>
    private (int? Used, TimeSpan? Over) Busy(DateTimeOffset now, bool inACgroup)
    {
        var busy = inACgroup
            ? Microseconds(read(CpuStat), "usage_usec")
            : Ticks(read(ProcStat));

        var was = _busyMicroseconds;
        var when = _lookedAt;

        _busyMicroseconds = busy;
        _lookedAt = now;

        if (busy is not { } microseconds || was is not { } before || when is not { } last)
        {
            return (null, null);
        }

        var over = now - last;

        // A COUNTER ONLY GOES UP. A fall is a new counter - a container
        // recreated under the same runner - and a negative rate is worse than
        // none. Nothing divides by no time either.
        if (microseconds < before || over <= TimeSpan.Zero)
        {
            return (null, null);
        }

        var spent = microseconds - before;
        var elapsed = (long)(over.TotalMilliseconds * 1000);

        return elapsed > 0 ? ((int)(spent * 1000 / elapsed), over) : (null, null);
    }

    /// <summary>Cumulative busy microseconds out of a cgroup's cpu.stat.</summary>
    private static long? Microseconds(string? stat, string key) =>
        Field(stat, key) is { } value ? value : null;

    /// <summary>
    /// Cumulative busy microseconds out of <c>/proc/stat</c>'s first line.
    /// </summary>
    /// <remarks>
    /// Busy is everything that is not <c>idle</c> and not <c>iowait</c> - both
    /// of which are the machine waiting rather than working. Guest time is
    /// already counted inside user time, so summing every field would count it
    /// twice.
    /// </remarks>
    private static long? Ticks(string? stat)
    {
        if (stat is null)
        {
            return null;
        }

        foreach (var line in stat.Split('\n'))
        {
            if (!line.StartsWith("cpu ", StringComparison.Ordinal))
            {
                continue;
            }

            var fields = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            long total = 0;
            long waiting = 0;

            for (var at = 1; at < fields.Length && at <= 8; at++)
            {
                if (!long.TryParse(fields[at], CultureInfo.InvariantCulture, out var ticks))
                {
                    continue;
                }

                total += ticks;

                // Fields are user nice system idle iowait irq softirq steal;
                // the fourth and fifth are the two that are not work.
                if (at is 4 or 5)
                {
                    waiting += ticks;
                }
            }

            return (total - waiting) * MicrosecondsPerTick;
        }

        return null;
    }

    private long? MemoryLimit(string? memoryMax, long? installed) =>
        memoryMax is { } stated
        && long.TryParse(stated.Trim(), CultureInfo.InvariantCulture, out var bytes)
        && bytes > 0
            ? bytes

            // `max` parses as nothing, which is a container that may take the
            // whole machine - a limit, and the one every member of this fleet
            // has.
            : installed;

    private long? Installed() =>
        Field(read(MemInfo), "MemTotal:") is { } kilobytes ? kilobytes * 1024 : null;

    /// <summary>
    /// A cgroup's memory in use, without what the kernel would take back.
    /// </summary>
    /// <remarks>
    /// <b><c>memory.current</c> counts the page cache</b>, which is reclaimable
    /// under pressure - so a container that has merely read files reads as
    /// sitting at its ceiling. Subtracting <c>inactive_file</c> is what a
    /// working set means, and it is the difference between a number somebody can
    /// act on and one that cries wolf.
    /// </remarks>
    private long? InUse()
    {
        if (Field(read(MemoryCurrent), null) is not { } current)
        {
            return null;
        }

        var cache = Field(read(MemoryStat), "inactive_file") ?? 0;

        return Math.Max(0, current - cache);
    }

    /// <summary>
    /// What the machine has in use: total less AVAILABLE, never less free.
    /// </summary>
    /// <remarks>
    /// Free and available are different questions on Linux, and the gap between
    /// them is the cache. A host with a warm cache has almost no free memory and
    /// plenty available, so `free` would report every long-lived machine as
    /// nearly exhausted.
    /// </remarks>
    private long? Resident()
    {
        var info = read(MemInfo);

        return Field(info, "MemTotal:") is { } total && Field(info, "MemAvailable:") is { } spare
            ? (total - spare) * 1024
            : null;
    }

    /// <summary>
    /// The number on a key's line, or the whole file's number when there is no
    /// key.
    /// </summary>
    private static long? Field(string? body, string? key)
    {
        if (body is null)
        {
            return null;
        }

        if (key is null)
        {
            return long.TryParse(
                body.Trim(), CultureInfo.InvariantCulture, out var only) ? only : null;
        }

        foreach (var line in body.Split('\n'))
        {
            if (!line.StartsWith(key, StringComparison.Ordinal))
            {
                continue;
            }

            var fields = line.Split(
                [' ', '\t'], StringSplitOptions.RemoveEmptyEntries);

            if (fields.Length >= 2
                && long.TryParse(fields[1], CultureInfo.InvariantCulture, out var value))
            {
                return value;
            }
        }

        return null;
    }
}
