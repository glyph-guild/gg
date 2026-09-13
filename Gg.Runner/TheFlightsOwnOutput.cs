using System.Text.Json;
using System.Text.Json.Serialization;
using Gg.Runner.Execution;

namespace Gg.Runner;

/// <summary>
/// What this flight's agent has been saying, read from the live view.
/// </summary>
/// <remarks>
/// <para>
/// <b>Not the journal, and measured rather than assumed.</b> Step 0 found the
/// pool host's runner is a systemd unit and concluded the tail should be
/// <c>journalctl -u &lt;unit&gt;</c>. Looking at what that journal actually holds
/// says otherwise: the runner's own narration and its crash traces, and nothing
/// an agent said — the resident unit runs <c>MaintainLoop</c> and never flies a
/// flight at all. The agent's text is in the live view, as structured NDJSON,
/// and always has been.
/// </para>
/// <para>
/// <b>Scoped to ONE FLIGHT, which is the part that matters.</b> A journal is the
/// machine's, so tailing it would hand somebody every flight that machine is
/// running — including other people's. This names one file, and the narrowing is
/// the file path rather than a filter somebody applies.
/// <para>
/// <b>WHICH flight is asked at read time now</b>, by the object that holds the
/// lease — see <c>WhatThisRunnerSays</c>. It used to be chosen when the session
/// was built, which was the same narrowing while a session existed for one
/// flight, and is no answer at all for a watcher who attached while the machine
/// was idle. One at a time, the one this machine is running now.
/// </para>
/// </para>
/// <para>
/// <b>The same file the local console tails.</b> <c>LiveStream</c> is ADR-0007
/// case 1 — same machine, no relay — and a remote hand-flight is that at a
/// distance. One writer, one format, two readers; a second transport for the
/// same bytes is how the two come to disagree about what a flight said.
/// </para>
/// <para>
/// <b>An absent file is an empty tail, not a failure.</b> A flight that has just
/// been claimed has written nothing yet, and a person asking then should be told
/// "nothing so far" rather than shown an error about a path.
/// </para>
/// </remarks>
public sealed class TheFlightsOwnOutput(string path) : IReadOnlyLog
{
    public TailRead Tail(int howMany)
    {
        if (howMany <= 0 || !File.Exists(path))
        {
            return new TailRead([], false);
        }

        string[] raw;

        try
        {
            // SHARED, because the runner is appending to this file while a
            // person reads it. An exclusive open would fail exactly when
            // somebody was watching a busy flight, which is the only time
            // anybody asks.
            using var stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);

            raw = reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        }
        catch (IOException)
        {
            // A read that could not happen is not a flight that said nothing,
            // and the distinction is this system's named worst failure. The
            // caller gets a line saying so rather than an empty tail.
            return new TailRead(["(this runner could not read its own live view)"], false);
        }

        var rendered = new List<string>(Math.Min(howMany, raw.Length));

        foreach (var line in raw.TakeLast(howMany))
        {
            rendered.Add(Render(line));
        }

        return new TailRead(rendered, raw.Length > howMany);
    }

    /// <summary>
    /// One NDJSON entry as one line a person can read.
    /// </summary>
    /// <remarks>
    /// <b>The kind is kept, because it is what tells Claude's own words from a
    /// tool call.</b> A tail that rendered only the text would show a person
    /// prose and tool names in one undifferentiated column, and the whole reason
    /// to watch is to see which is which.
    /// </remarks>
    private static string Render(string entry)
    {
        try
        {
            if (JsonSerializer.Deserialize(entry, LiveTailJson.Default.LiveLine) is { } line)
            {
                return $"{line.Kind}: {line.Text}";
            }
        }
        catch (JsonException)
        {
            // A HALF-WRITTEN LAST LINE IS THE ORDINARY CASE, not a corruption:
            // the runner appends while this reads. Showing it raw is better than
            // dropping it, because the next read will have the whole of it.
        }

        return entry;
    }
}

/// <summary>Reading the live view, which this side only ever writes.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(LiveLine))]
internal sealed partial class LiveTailJson : JsonSerializerContext;
