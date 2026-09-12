using System.Text.Json;

namespace Gg.Local;

/// <summary>
/// What the provider's own meter last said about this plan.
/// </summary>
/// <remarks>
/// <para>
/// <b>Read, never derived, and that is the whole reason this type exists.</b>
/// A share needs a denominator; nothing on a machine reports a plan's token
/// ceiling. The tempting fix — measure the tokens, take a percentage out of
/// the executor, divide — is wrong: the meter's windows are FIXED and end at a
/// reset instant, while <see cref="AllowanceLedger"/> sums ROLLING ones, so
/// the division puts two different spans over each other and produces a
/// number that looks reasonable.
/// </para>
/// <para>
/// <b>So nothing is divided.</b> The meter's own utilisation is the
/// authoritative share, it needs no ceiling, and it is right on a machine
/// where nobody ever configured one.
/// </para>
/// <para>
/// <b>A guest, under the transcripts' rule.</b> That file belongs to another
/// tool and carries the account the plan belongs to. This takes two numbers
/// and an instant per window and retains nothing else — an allowance is a name
/// somebody chose and never an identity, and a reading crosses to a control
/// plane that must never learn whose account this is.
/// </para>
/// <para>
/// <b>Every absence is an absence, never a zero.</b> No file, no cache, a
/// shape this does not recognise, a window the meter did not mention: all of
/// them answer null. Nought percent reads as a plan nobody has touched, which
/// is the opposite of "nobody asked".
/// </para>
/// </remarks>
public sealed record MeteredShare
{
    /// <summary>The meter's share per window, 0 to 1. Absent windows are absent.</summary>
    public required IReadOnlyDictionary<string, double> Shares { get; init; }

    /// <summary>When each window the meter keeps resets.</summary>
    public required IReadOnlyDictionary<string, DateTimeOffset> Resets { get; init; }

    /// <summary>When the executor last asked. Absent when it never did.</summary>
    public DateTimeOffset? FetchedAt { get; init; }

    /// <summary>Nothing was readable.</summary>
    public static MeteredShare None { get; } = new()
    {
        Shares = new Dictionary<string, double>(StringComparer.Ordinal),
        Resets = new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal),
    };

    /// <summary>The meter's share of one window, or null where it said nothing.</summary>
    public double? Share(string window) =>
        Shares.TryGetValue(window, out var share) ? share : null;

    /// <summary>When that window resets, or null where the meter said nothing.</summary>
    public DateTimeOffset? ResetsAt(string window) =>
        Resets.TryGetValue(window, out var resets) ? resets : null;
}

/// <summary>Reads the executor's cached view of the provider's meter.</summary>
public static class AllowanceMeter
{
    /// <summary>The meter's name for this ledger's session window.</summary>
    private const string FiveHour = "five_hour";

    /// <summary>And for its week. A calendar anchor, not seven rolling days.</summary>
    private const string SevenDay = "seven_day";

    /// <summary>Where the executor keeps it, beside the transcripts directory.</summary>
    /// <remarks>
    /// <b>A sibling of <see cref="AllowanceLedger.DefaultRoot"/>, not inside
    /// it.</b> The transcripts live under <c>~/.claude/projects</c>; this is
    /// <c>~/.claude.json</c>, one level up and a file rather than a tree.
    /// </remarks>
    public static string DefaultPath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".claude.json");

    /// <summary>
    /// What the meter said, or <see cref="MeteredShare.None"/> when nothing could be read.
    /// </summary>
    /// <remarks>
    /// <b>Silent on every failure, deliberately.</b> Another tool owns this
    /// file and may change its shape without telling anybody. A throw here
    /// would take the token counts down with it, and those are measured from
    /// somewhere else and right regardless — the same argument the transcripts'
    /// malformed lines are stepped over under.
    /// </remarks>
    public static MeteredShare Read(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        try
        {
            if (!File.Exists(path))
            {
                return MeteredShare.None;
            }

            using var document = JsonDocument.Parse(File.ReadAllText(path));

            if (!document.RootElement.TryGetProperty("cachedUsageUtilization", out var cached)
                || cached.ValueKind != JsonValueKind.Object)
            {
                return MeteredShare.None;
            }

            var shares = new Dictionary<string, double>(StringComparer.Ordinal);
            var resets = new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal);

            if (cached.TryGetProperty("utilization", out var windows)
                && windows.ValueKind == JsonValueKind.Object)
            {
                Take(windows, FiveHour, AllowanceLedger.Session, shares, resets);
                Take(windows, SevenDay, AllowanceLedger.Week, shares, resets);
            }

            return new MeteredShare
            {
                Shares = shares,
                Resets = resets,

                // THE AGE, WHICH IS PART OF THE ANSWER. The real one on this
                // machine was seven hours old and described a window that had
                // already reset; a share with no age cannot be told from a
                // current one.
                FetchedAt = cached.TryGetProperty("fetchedAtMs", out var fetched)
                            && fetched.TryGetInt64(out var ms)
                    ? DateTimeOffset.FromUnixTimeMilliseconds(ms)
                    : null,
            };
        }
        catch (JsonException)
        {
            return MeteredShare.None;
        }
        catch (IOException)
        {
            return MeteredShare.None;
        }
        catch (UnauthorizedAccessException)
        {
            return MeteredShare.None;
        }
    }

    /// <summary>
    /// One window, if the meter has it and it is shaped the way this expects.
    /// </summary>
    /// <remarks>
    /// <b>A percentage on the way in, a fraction on the way out</b> — the
    /// contract's rule for a floor, applied to the same kind of number for the
    /// same reason: a fraction has one spelling and a percentage has three.
    /// </remarks>
    private static void Take(
        JsonElement windows, string metered, string ours,
        Dictionary<string, double> shares, Dictionary<string, DateTimeOffset> resets)
    {
        if (!windows.TryGetProperty(metered, out var window)
            || window.ValueKind != JsonValueKind.Object)
        {
            // Null is what the meter writes for a window this plan does not
            // have, and it is an absence rather than a nought.
            return;
        }

        if (window.TryGetProperty("utilization", out var used)
            && used.ValueKind == JsonValueKind.Number
            && used.TryGetDouble(out var percent))
        {
            shares[ours] = percent / 100.0;
        }

        if (window.TryGetProperty("resets_at", out var at)
            && at.ValueKind == JsonValueKind.String
            && at.TryGetDateTimeOffset(out var when))
        {
            resets[ours] = when;
        }
    }
}
