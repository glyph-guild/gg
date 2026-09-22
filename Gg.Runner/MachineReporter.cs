using Gg.Contracts;
using Gg.Local;

namespace Gg.Runner;

/// <summary>
/// Measures what this machine has, no more often than the figure means
/// anything.
/// </summary>
/// <remarks>
/// <para>
/// <b>The cadence is here rather than in the loop, and here it is load-bearing
/// twice.</b> It saves the walk, as the allowance reporter's does - a beat is
/// seconds and nobody needs a cpu average per second - and it also DEFINES the
/// figure: both sources are cumulative counters, so the interval between two
/// looks is the interval the rate is averaged over. Reading more often would
/// not give a better answer, it would give a different question.
/// </para>
/// <para>
/// <b>It maps a local measurement onto the record that crosses.</b>
/// <c>Gg.Local</c> cannot reference the wire contract, so the two shapes are
/// separate by construction and this is the one place they meet - the same
/// division <see cref="AllowanceReporter"/> describes.
/// </para>
/// <para>
/// <b>Nothing here identifies anybody.</b> Four numbers about silicon: no path,
/// no hostname, no account, nothing a customer wrote. The data boundary is not
/// in question on this route, and that is worth saying rather than assuming.
/// </para>
/// </remarks>
/// <param name="measure">What this machine reads about itself.</param>
/// <param name="cadence">How often it is worth looking.</param>
public sealed class MachineReporter(
    Func<DateTimeOffset, MeasuredMachine> measure, TimeSpan cadence)
{
    private DateTimeOffset? _last;

    /// <summary>
    /// How often a machine looks at itself.
    /// </summary>
    /// <remarks>
    /// Thirty seconds, which is what the console refreshes on - so a pane never
    /// draws a figure older than its own tick, and the interval a reader is
    /// shown is the interval they are watching.
    /// </remarks>
    public static readonly TimeSpan Cadence = TimeSpan.FromSeconds(30);

    /// <summary>The real one, reading this machine's own files.</summary>
    public static MachineReporter OfThisMachine() =>
        new(MachineMeter.OfThisMachine().Read, Cadence);

    /// <summary>
    /// A reading if one is due and there is anything to say, or null.
    /// </summary>
    public MachineReading? Read(DateTimeOffset now)
    {
        if (_last is { } when && now - when < cadence)
        {
            return null;
        }

        _last = now;

        var measured = measure(now);

        // A READING WITH EVERY FIGURE ABSENT SAYS ONLY THAT SOMEBODY LOOKED. A
        // machine with no /proc and no cgroup - a developer's Mac, until
        // somebody writes the interop for it - would otherwise buy a request
        // every thirty seconds to report nothing.
        if (measured.CpuMilliLimit is null
            && measured.CpuMilliUsed is null
            && measured.MemoryLimitBytes is null
            && measured.MemoryUsedBytes is null)
        {
            return null;
        }

        return new MachineReading
        {
            MeasuredAt = measured.MeasuredAt,

            // SECONDS, because a duration is not a JSON type and this one is
            // never sub-second: it is a cadence apart by construction.
            OverSeconds = measured.Over is { } over ? (int)Math.Round(over.TotalSeconds) : null,
            CpuMilliLimit = measured.CpuMilliLimit,
            CpuMilliUsed = measured.CpuMilliUsed,
            MemoryLimitBytes = measured.MemoryLimitBytes,
            MemoryUsedBytes = measured.MemoryUsedBytes,
        };
    }
}
