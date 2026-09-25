using System.Globalization;

namespace Gg.Console;

/// <summary>
/// Where the console's time went, when somebody asked.
/// </summary>
/// <remarks>
/// <para>
/// <b>Set <c>GG_TIMING</c> to a path and the console says what it spent.</b>
/// One line per phase - what it was, how long it took, and how many calls it
/// made when that is the thing that explains the duration. It sits beside
/// <see href="GG_STATE_DUMP"/> in <c>ConsoleEnvironment</c> and is the same
/// kind of thing: a hook a person turns on, reproduces with, and hands back.
/// </para>
/// <para>
/// <b>Off costs nothing, which is what makes it safe to leave in.</b> Every
/// console boots through these call sites, so <see cref="Off"/> takes no clock
/// reading, formats no string and opens no file - and <see cref="Asked"/> lets
/// a call site skip work that only a measurement would need.
/// </para>
/// <para>
/// <b>Appended as it goes, because the thing being chased is a hang.</b> A
/// person whose console has stopped responding kills it, and anything held in
/// memory until exit is exactly the evidence that would be lost. A short line
/// appended per phase is microseconds against the milliseconds it measures.
/// </para>
/// <para>
/// <b>And it may never throw.</b> A full disk or an unwritable path must not
/// end a session - taking the console down to report on the console is the
/// worst outcome this could have, so every write is swallowed.
/// </para>
/// </remarks>
public sealed class Timings
{
    private readonly Action<string>? _write;
    private readonly Lock _gate = new();

    private Timings(Action<string>? write) => _write = write;

    /// <summary>The one nobody asked for, which does nothing at all.</summary>
    public static Timings Off { get; } = new(null);

    /// <summary>
    /// The one the call sites use, set once where the console is composed.
    /// </summary>
    /// <remarks>
    /// <b>Ambient because the phases are static and deep.</b> Threading a
    /// recorder through <c>LoadAsync</c>, <c>ForTabAsync</c> and the screen
    /// would change the signature of everything between the composition root
    /// and the measurement, for a hook that is off in every session but the one
    /// being diagnosed. <c>GG_STATE_DUMP</c> is read the same way and for the
    /// same reason.
    /// </remarks>
    public static Timings Active { get; set; } = Off;

    /// <summary>Whether anybody is listening.</summary>
    /// <remarks>
    /// <b>Asked before work only a measurement needs.</b> A call site that
    /// would count something, or name something, to report it can check this
    /// and skip - the recording itself is already free when off.
    /// </remarks>
    public bool Asked => _write is not null;

    /// <summary>One that hands each line to <paramref name="write"/>.</summary>
    public static Timings Writing(Action<string> write)
    {
        ArgumentNullException.ThrowIfNull(write);
        return new Timings(write);
    }

    /// <summary>
    /// What <c>GG_TIMING</c> asked for, or <see cref="Off"/>.
    /// </summary>
    /// <remarks>
    /// <b>Whitespace is off, not a path.</b> A variable set to spaces is
    /// somebody clearing it, and creating a file named " " would be a
    /// surprising way to find that out.
    /// </remarks>
    public static Timings For(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Off;
        }

        var where = path.Trim();

        return Writing(line =>
        {
            // APPEND, so a second console against the same file adds to it
            // rather than truncating what the first one recorded.
            File.AppendAllText(where, line + Environment.NewLine);
        });
    }

    /// <summary>Records a phase that has already finished.</summary>
    /// <param name="reads">
    /// How many calls it made, when that is what explains the duration. Absent
    /// means the column does not apply - a render makes none, and printing a
    /// zero would invite somebody to read it as a measurement.
    /// </param>
    public void Took(string phase, TimeSpan took, int? reads = null)
    {
        if (_write is not { } write)
        {
            return;
        }

        var line = string.Create(
            CultureInfo.InvariantCulture,
            $"{DateTimeOffset.UtcNow:HH:mm:ss.fff}  {phase,-28} {took.TotalMilliseconds,8:F1}ms")
            + (reads is { } many
                ? string.Create(CultureInfo.InvariantCulture, $"  reads={many}")
                : string.Empty);

        // UNDER A LOCK, because the boot measures phases from several tasks at
        // once and two half-lines interleaved would be unreadable exactly when
        // somebody needs to read them.
        lock (_gate)
        {
            try
            {
                write(line);
            }
            catch (Exception)
            {
                // Said by the absence of a line, which is the only safe way for
                // a diagnostic to fail.
            }
        }
    }

    /// <summary>
    /// Measures a block, recording when it leaves.
    /// </summary>
    /// <remarks>
    /// <b>The shape every call site uses.</b> A stopwatch started and stopped
    /// by hand is one the early return forgets to stop, and the early return is
    /// the path worth measuring.
    /// </remarks>
    public IDisposable Measure(string phase, int? reads = null) =>
        _write is null ? Nothing.AtAll : new Span(this, phase, reads);

    private sealed class Nothing : IDisposable
    {
        public static readonly Nothing AtAll = new();

        public void Dispose()
        {
        }
    }

    private sealed class Span(Timings timings, string phase, int? reads) : IDisposable
    {
        private readonly long _from = System.Diagnostics.Stopwatch.GetTimestamp();

        public void Dispose() =>
            timings.Took(phase, System.Diagnostics.Stopwatch.GetElapsedTime(_from), reads);
    }
}
