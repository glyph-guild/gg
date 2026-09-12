using Gg.Contracts;
using Gg.Local;

namespace Gg.Runner;

/// <summary>
/// Measures what this machine has spent, no more often than it needs to.
/// </summary>
/// <remarks>
/// <para>
/// <b>The cadence is here rather than in the loop, because what it protects is
/// the WALK.</b> A beat is seconds; reading an allowance opens every transcript
/// on the machine. The loop asks on every beat and is told nothing most times,
/// and the measurement itself is not taken — a reporter that measured each time
/// and discarded the answer would cost exactly what this exists to save.
/// </para>
/// <para>
/// <b>It maps a local measurement onto the record that crosses.</b>
/// <c>Gg.Local</c> cannot reference the wire contract — no package reference
/// and no wire type, so a filesystem convention never ships in the artifact a
/// customer audits — so the two shapes are separate by construction and this is
/// the one place they meet.
/// </para>
/// <para>
/// <b>Nothing here resolves anything.</b> The allowance is a NAME, taken from
/// the machine's own configuration file; no account, no credential and no
/// identifier for either crosses, and nothing in the path that produces this
/// record could obtain one.
/// </para>
/// </remarks>
public sealed class AllowanceReporter(Func<MeasuredAllowance?> measure, TimeSpan cadence)
{
    private readonly Func<MeasuredAllowance?> _measure = measure;
    private readonly TimeSpan _cadence = cadence;

    private DateTimeOffset? _last;

    /// <summary>How often a machine looks at what it has spent.</summary>
    /// <remarks>
    /// Five minutes: long enough that the walk is nothing beside the work, and
    /// short enough that a reading is never the reason a decision was wrong.
    /// Nothing enforces anything on a reading yet, so the cost of being a few
    /// minutes behind is a percentage on a screen.
    /// </remarks>
    public static readonly TimeSpan Cadence = TimeSpan.FromMinutes(5);

    /// <summary>
    /// One for a machine that named an allowance, or null for one that did not.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Null is the ordinary answer.</b> A machine whose owner has not named
    /// an allowance reports none — naming one is how somebody agrees to lend
    /// it, and a default derived from the hostname would put a machine into a
    /// fleet's accounting without anybody saying so.
    /// </para>
    /// <para>
    /// <b>This one reads a real clock, and the constructor above does not.</b>
    /// A composition root is where the ambient answers are allowed to be
    /// ambient; the window arithmetic itself is tested through the constructor
    /// with time handed in.
    /// </para>
    /// </remarks>
    public static AllowanceReporter? For(string? name, string? limits) =>
        string.IsNullOrWhiteSpace(name)
            ? null
            : new AllowanceReporter(
                () => AllowanceLedger.Read(
                    name,
                    AllowanceLedger.DefaultRoot(),
                    AllowanceLimits.Read(limits),
                    DateTimeOffset.UtcNow),
                Cadence);

    /// <summary>The reading to post, or null when it is not time or there is none.</summary>
    public Task<AllowanceReading?> ReadAsync(DateTimeOffset now)
    {
        if (_last is { } last && now - last < _cadence)
        {
            return Task.FromResult<AllowanceReading?>(null);
        }

        _last = now;

        var measured = _measure();

        if (measured is null)
        {
            return Task.FromResult<AllowanceReading?>(null);
        }

        return Task.FromResult<AllowanceReading?>(new AllowanceReading
        {
            Allowance = measured.Name,
            MeasuredAt = measured.MeasuredAt,
            Windows =
            [
                .. measured.Windows.Select(w => new AllowanceWindow
                {
                    Kind = w.Kind,
                    Since = w.Since,

                    // THE FOUR COUNTS, NOT THE TOTAL. Both sides derive the
                    // total the same way from the same members, which is what
                    // stops a machine and a control plane disagreeing about
                    // how much of a ceiling is gone.
                    InputTokens = w.InputTokens,
                    OutputTokens = w.OutputTokens,
                    CacheReadTokens = w.CacheReadTokens,
                    CacheWriteTokens = w.CacheWriteTokens,
                    Limit = w.Limit,

                    // AND WHAT THE PROVIDER SAID, which the control plane
                    // cannot get any other way: it has no transcripts, no
                    // meter and no ceiling of its own.
                    Reported = w.Reported,
                    ResetsAt = w.ResetsAt,
                    ReportedAt = w.ReportedAt,
                }),
            ],
        });
    }
}
