using System.Globalization;
using System.Text.Json;

namespace Gg.Local;

/// <summary>
/// What one window of a subscription has been spent on, and out of what.
/// </summary>
/// <remarks>
/// <b>Named apart from the wire type on purpose.</b> <c>Gg.Contracts</c> has
/// an <c>AllowanceWindow</c> and this project cannot reference it — the
/// charter one file up: no package reference, no wire type, because a
/// filesystem convention must not ship in the artifact a customer audits. Two
/// types with one name in two namespaces is the shape that makes a reader
/// check which one they are holding, so the measurement is a
/// <c>MeasuredWindow</c> and the thing that crosses is the window.
/// </remarks>
/// <remarks>
/// <para>
/// <b>The fraction is absent rather than zero when no limit was configured.</b>
/// Nothing on a machine reports a subscription's ceiling, so the denominator is
/// a number somebody typed and may simply not be there. A window that answered
/// 0% would read as plenty left.
/// </para>
/// </remarks>
public sealed record MeasuredWindow
{
    /// <summary>Which window this is — <c>session</c> or <c>week</c>.</summary>
    public required string Kind { get; init; }

    /// <summary>Every token counted inside it, unweighted.</summary>
    public required long Tokens { get; init; }

    /// <summary>When the window opened.</summary>
    public required DateTimeOffset Since { get; init; }

    /// <summary>The ceiling somebody configured, or absent when nobody did.</summary>
    public long? Limit { get; init; }

    /// <summary>Spent over the ceiling, or absent when there is no ceiling.</summary>
    public double? Fraction =>
        Limit is > 0 ? Tokens / (double)Limit.Value : null;
}

/// <summary>
/// One machine's reading of the allowance it spends from.
/// </summary>
/// <remarks>
/// <b>A name, never an identity.</b> <see cref="Name"/> is the string a person
/// put in their own configuration file. It is not an account id, an email, an
/// organisation uuid or a token, and nothing here can resolve one — this
/// project's charter rule, stated for credentials and applying unchanged: it
/// carries which thing to use, never the thing.
/// </remarks>
public sealed record MeasuredAllowance
{
    /// <summary>What its owner calls it.</summary>
    public required string Name { get; init; }

    /// <summary>When this machine looked.</summary>
    public required DateTimeOffset MeasuredAt { get; init; }

    /// <summary>One per window.</summary>
    public required IReadOnlyList<MeasuredWindow> Windows { get; init; }
}

/// <summary>
/// The ceilings a person declared, per window.
/// </summary>
/// <remarks>
/// <para>
/// <b>A string, parsed here, for the reason <c>Configuration</c> gives.</b>
/// Every member of the configuration file is the string its variable carries,
/// so that a value from a file and a value from the environment reach one
/// parser and one refusal.
/// </para>
/// <para>
/// <b>An unreadable entry is dropped rather than refused.</b> A wrong ceiling
/// costs a wrong percentage on a screen; a refusal would cost the whole reading,
/// including the token counts, which are right regardless. <see cref="Refusal"/>
/// says what was dropped so the mistake is visible where somebody can fix it.
/// </para>
/// </remarks>
public sealed record AllowanceLimits
{
    private readonly IReadOnlyDictionary<string, long> _ceilings;

    private AllowanceLimits(IReadOnlyDictionary<string, long> ceilings, string? refusal)
    {
        _ceilings = ceilings;
        Refusal = refusal;
    }

    /// <summary>Nobody said. Every window reports no fraction.</summary>
    public static AllowanceLimits None { get; } =
        new(new Dictionary<string, long>(StringComparer.Ordinal), refusal: null);

    /// <summary>What could not be read, named, or absent when all of it could.</summary>
    public string? Refusal { get; }

    /// <summary><c>session=88000,week=2400000</c>, or whatever a person typed.</summary>
    public static AllowanceLimits Read(string? declared)
    {
        if (string.IsNullOrWhiteSpace(declared)) { return None; }

        var ceilings = new Dictionary<string, long>(StringComparer.Ordinal);
        var dropped = new List<string>();

        foreach (var entry in declared.Split(',', StringSplitOptions.RemoveEmptyEntries
                                                 | StringSplitOptions.TrimEntries))
        {
            var split = entry.Split('=', 2, StringSplitOptions.TrimEntries);

            if (split.Length is 2
                && split[0].Length > 0
                && long.TryParse(split[1], NumberStyles.None, CultureInfo.InvariantCulture, out var ceiling)
                && ceiling > 0)
            {
                ceilings[split[0]] = ceiling;
                continue;
            }

            dropped.Add(entry);
        }

        return new AllowanceLimits(
            ceilings,
            dropped.Count is 0
                ? null
                : $"allowance-limits: could not read {string.Join(", ", dropped)}. "
                + "Each entry is a window and a whole number of tokens, "
                + "like session=88000,week=2400000.");
    }

    /// <summary>The ceiling for one window, or absent.</summary>
    public long? For(string window) =>
        _ceilings.TryGetValue(window, out var ceiling) ? ceiling : null;
}

/// <summary>
/// What this machine has spent, summed from the executor's own transcripts.
/// </summary>
/// <remarks>
/// <para>
/// <b>A guest in another tool's directory, and both rules here follow from
/// that.</b> Claude Code writes one JSON record per assistant message under
/// <c>~/.claude/projects/&lt;project&gt;/&lt;session&gt;.jsonl</c>, each carrying a UTC
/// timestamp, a model and a <c>usage</c> block. There is no supported reading
/// of a subscription's remaining percentage anywhere on the machine, so this is
/// where the number comes from.
/// </para>
/// <para>
/// <b>It extracts five values and retains nothing else.</b> Those files are the
/// customer's code, their prompts and their agent's reasoning, and a reading
/// crosses to a control plane that must never see any of it. A line that will
/// not parse is stepped over in SILENCE — a diagnostic quoting the line it
/// could not read would carry content out through the one path nobody tests.
/// </para>
/// <para>
/// <b>Windows are bounded by timestamps.</b> A record also carries
/// <c>apiBlockIndex</c>, which reads exactly like a five-hour session marker and
/// is not one: measured on a real machine, blocks 0 through 3 all began within
/// two minutes of each other inside one session. Summing by it reports four
/// windows where there is one.
/// </para>
/// <para>
/// <b>Every count, unweighted.</b> Input, output, cache read and cache creation
/// are added together. How a provider weighs one against another is not
/// published, so a weighting here would be a guess wearing a decimal point —
/// and the ceiling is configured against this same total, which is the only
/// thing that makes a fraction mean anything.
/// </para>
/// <para>
/// <b>Interactive work counts too, and that is the point.</b> The directory
/// holds every session on this machine, not only the ones a runner started. A
/// reading that saw only fleet work would tell a person their allowance was
/// untouched on the morning they spent it themselves.
/// </para>
/// </remarks>
public static class AllowanceLedger
{
    /// <summary>The rolling window a provider resets most often.</summary>
    public const string Session = "session";

    /// <summary>The longer one.</summary>
    public const string Week = "week";

    /// <summary>How far back <see cref="Session"/> reaches.</summary>
    public static readonly TimeSpan SessionLength = TimeSpan.FromHours(5);

    /// <summary>How far back <see cref="Week"/> reaches.</summary>
    public static readonly TimeSpan WeekLength = TimeSpan.FromDays(7);

    /// <summary>Where Claude Code keeps its transcripts.</summary>
    /// <remarks>
    /// Not a <see cref="LocalPaths"/> member: this is another tool's directory
    /// and gg does not own its layout. It is a parameter everywhere below so a
    /// test never reads somebody's real work.
    /// </remarks>
    public static string DefaultRoot() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".claude",
            "projects");

    /// <summary>Sum what this machine has spent.</summary>
    public static MeasuredAllowance Read(
        string name,
        string transcriptsRoot,
        AllowanceLimits limits,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(transcriptsRoot);
        ArgumentNullException.ThrowIfNull(limits);

        var opened = new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal)
        {
            [Session] = now - SessionLength,
            [Week] = now - WeekLength,
        };

        var counted = new Dictionary<string, long>(StringComparer.Ordinal)
        {
            [Session] = 0,
            [Week] = 0,
        };

        var earliest = now - WeekLength;

        foreach (var file in Transcripts(transcriptsRoot, earliest))
        {
            foreach (var (at, tokens) in Spends(file))
            {
                foreach (var window in counted.Keys.ToArray())
                {
                    if (at >= opened[window]) { counted[window] += tokens; }
                }
            }
        }

        return new MeasuredAllowance
        {
            Name = name,
            MeasuredAt = now,
            Windows =
            [
                .. counted.Select(one => new MeasuredWindow
                {
                    Kind = one.Key,
                    Tokens = one.Value,
                    Since = opened[one.Key],
                    Limit = limits.For(one.Key),
                }),
            ],
        };
    }

    /// <summary>
    /// The files that could hold a record inside the widest window.
    /// </summary>
    /// <remarks>
    /// <b>Skipped on the file's own timestamp, which is the whole of the
    /// incremental story.</b> A machine accumulates hundreds of these — 355 on
    /// the one this was written against — and a transcript untouched since
    /// before the window opened cannot contain a record inside it. Nothing is
    /// cached between calls, so there is no offset file to fall out of step
    /// with what it describes.
    /// </remarks>
    private static IEnumerable<string> Transcripts(string root, DateTimeOffset earliest)
    {
        if (!Directory.Exists(root)) { yield break; }

        IEnumerable<string> found;

        try
        {
            found = Directory.EnumerateFiles(root, "*.jsonl", SearchOption.AllDirectories);
        }
        catch (IOException) { yield break; }
        catch (UnauthorizedAccessException) { yield break; }

        foreach (var file in found)
        {
            DateTimeOffset written;

            try { written = File.GetLastWriteTimeUtc(file); }
            catch (IOException) { continue; }
            catch (UnauthorizedAccessException) { continue; }

            if (written >= earliest) { yield return file; }
        }
    }

    /// <summary>
    /// Every spend one transcript records: when, and how many tokens.
    /// </summary>
    /// <remarks>
    /// The only two values that leave this method. Anything that will not read
    /// is stepped over without a word — see the boundary rule on the class.
    /// </remarks>
    private static IEnumerable<(DateTimeOffset At, long Tokens)> Spends(string file)
    {
        IEnumerable<string> lines;

        try { lines = File.ReadLines(file); }
        catch (IOException) { yield break; }
        catch (UnauthorizedAccessException) { yield break; }

        foreach (var line in lines)
        {
            var spend = Spend(line);

            if (spend is { } one) { yield return one; }
        }
    }

    private static (DateTimeOffset At, long Tokens)? Spend(string line)
    {
        if (line.Length is 0) { return null; }

        JsonDocument? record = null;

        try
        {
            record = JsonDocument.Parse(line);

            var root = record.RootElement;

            if (root.ValueKind is not JsonValueKind.Object
                || !root.TryGetProperty("timestamp", out var stamped)
                || stamped.ValueKind is not JsonValueKind.String
                || !DateTimeOffset.TryParse(
                    stamped.GetString(),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var at)
                || !root.TryGetProperty("message", out var message)
                || message.ValueKind is not JsonValueKind.Object
                || !message.TryGetProperty("usage", out var usage)
                || usage.ValueKind is not JsonValueKind.Object)
            {
                return null;
            }

            var tokens = Count(usage, "input_tokens")
                       + Count(usage, "output_tokens")
                       + Count(usage, "cache_read_input_tokens")
                       + Count(usage, "cache_creation_input_tokens");

            return tokens is 0 ? null : (at, tokens);
        }
        catch (JsonException)
        {
            // DELIBERATELY SILENT. See the boundary rule on the class: this is
            // somebody else's file and the text that failed to parse may be
            // anything at all.
            return null;
        }
        finally
        {
            record?.Dispose();
        }
    }

    private static long Count(JsonElement usage, string member) =>
        usage.TryGetProperty(member, out var held)
        && held.ValueKind is JsonValueKind.Number
        && held.TryGetInt64(out var count)
        && count > 0
            ? count
            : 0;
}
