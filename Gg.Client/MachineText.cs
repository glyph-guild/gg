using System.Globalization;

namespace Gg.Client;

/// <summary>
/// What a machine has and how much of it is in use, as a person reads it.
/// </summary>
/// <remarks>
/// <para>
/// <b>One formatter, two renderers.</b> <c>gg runners</c> prints these and the
/// console's fleet table draws them, and this repository's rule for that is
/// "different renderers over one result type, never a second way to get the
/// data". Two implementations would be two answers to what <c>1.9G</c> means,
/// and they would agree until somebody changed one.
/// </para>
/// <para>
/// <b>Here rather than in the console, because the console can see this
/// assembly and not the other way round.</b> That is the only direction
/// available, and it is also the right one: the verb is the older surface.
/// </para>
/// <para>
/// <b>Used OF LIMIT, never a percentage.</b> The limits were the half that was
/// asked for, and a bare percentage hides what matters: a member at 90% of one
/// core and a host at 90% of sixteen are not the same situation, and only one
/// of them can take another flight.
/// </para>
/// <para>
/// <b>And absence is said, not skipped.</b> A machine that cannot measure
/// something says nothing about it, so a zero here would be a claim that it has
/// none.
/// </para>
/// </remarks>
public static class MachineText
{
    /// <summary>Nothing to say, which is not the same as nothing.</summary>
    public const string Absent = "-";

    /// <summary>
    /// How long a figure is worth drawing after it was measured.
    /// </summary>
    /// <remarks>
    /// <b>Four cadences.</b> A machine reports every thirty seconds and a beat
    /// can be late, so a window of one or two would blank the figure between
    /// reports and flicker on every tick. Two minutes rides out a slow beat and
    /// still means a number on screen is something a machine says, rather than
    /// something it said once.
    /// </remarks>
    public static readonly TimeSpan StillWorthDrawing = TimeSpan.FromMinutes(2);

    /// <summary>
    /// What this machine is using of what it may, in cores.
    /// </summary>
    /// <remarks>
    /// <b>Cores here, thousandths on the wire.</b> A cgroup quota is fractional
    /// so the wire cannot round it; a person reads cores, and a quarter of one
    /// is the resolution anybody acts on.
    /// </remarks>
    public static string Cpu(
        int? used, int? limit, DateTimeOffset? measuredAt, DateTimeOffset now) =>
        Fresh(measuredAt, now) ? Pair(Cores(used), Cores(limit)) : Absent;

    /// <summary>What this machine is using of what it may, as sizes.</summary>
    public static string Memory(
        long? used, long? limit, DateTimeOffset? measuredAt, DateTimeOffset now) =>
        Fresh(measuredAt, now) ? Pair(Size(used), Size(limit)) : Absent;

    /// <summary>
    /// Whether the reading behind a figure is recent enough to draw.
    /// </summary>
    /// <remarks>
    /// A machine that stopped reporting keeps its last figures - they are still
    /// true of the moment they name - and this is what stops a surface drawing
    /// them as though they were now. The state column already says
    /// <c>offline</c>, so the dash costs a reader nothing.
    /// </remarks>
    public static bool Fresh(DateTimeOffset? measuredAt, DateTimeOffset now) =>
        measuredAt is { } when && now - when < StillWorthDrawing;

    /// <summary>
    /// <c>used/limit</c>, with whichever half is missing said rather than
    /// skipped.
    /// </summary>
    /// <remarks>
    /// A first reading carries the limits and no rate, because both cpu sources
    /// are cumulative counters and one look is not a rate - so <c>-/4</c> is a
    /// real and common state, and collapsing it to a bare dash would throw away
    /// the half that arrived.
    /// </remarks>
    private static string Pair(string? used, string? limit) =>
        (used, limit) switch
        {
            (null, null) => Absent,
            (var u, null) => $"{u}/{Absent}",
            (null, var l) => $"{Absent}/{l}",
            var (u, l) => $"{u}/{l}",
        };

    private static string? Cores(int? milli) =>
        milli is { } thousandths ? Trimmed(thousandths / 1000.0) : null;

    /// <summary>
    /// Bytes in the largest unit that still leaves a readable number.
    /// </summary>
    /// <remarks>
    /// <b>Down to megabytes, because a pool member lives there.</b> Measured on
    /// the fleet: an idle member holds 43 MiB against a 15.6 GiB ceiling, and in
    /// gigabytes that is 0.0 - a container doing nothing and one using nothing
    /// are different facts, and this is the distinction the figure exists to
    /// draw.
    /// </remarks>
    private static string? Size(long? bytes) => bytes switch
    {
        null => null,
        >= 1024L * 1024 * 1024 => Trimmed(bytes.Value / (1024.0 * 1024 * 1024)) + "G",
        >= 1024L * 1024 => Trimmed(bytes.Value / (1024.0 * 1024)) + "M",
        >= 1024 => Trimmed(bytes.Value / 1024.0) + "K",
        _ => bytes.Value.ToString(CultureInfo.InvariantCulture),
    };

    /// <summary>
    /// One decimal, and none at all when it would be a zero.
    /// </summary>
    /// <remarks>
    /// <b>Away from zero rather than the default.</b> Banker's rounding turns a
    /// quarter of a core into 0.2, and these figures are read as "how much is
    /// left" - rounding toward the answer somebody hopes for is the wrong
    /// direction. And <c>2.0/2.0</c> spends three characters on nothing in a
    /// table that has none to spare.
    /// </remarks>
    private static string Trimmed(double value)
    {
        var rounded = Math.Round(value, 1, MidpointRounding.AwayFromZero);

        return rounded.ToString(
            rounded == Math.Floor(rounded) ? "0" : "0.0", CultureInfo.InvariantCulture);
    }
}
