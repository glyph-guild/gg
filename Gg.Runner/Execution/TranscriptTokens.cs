using System.Text.Json;

namespace Gg.Runner.Execution;

/// <summary>
/// What one loop spent, from the executor's own accounting.
/// </summary>
/// <remarks>
/// <b>Four counts and no total.</b> How a provider weighs a cache read against
/// an output token is not published, so a total computed here would be a guess
/// wearing a decimal point — and whoever needs one is measuring against a
/// ceiling they configured, which has to be calibrated against the same
/// arithmetic. The components cross; the weighting is the reader's.
/// </remarks>
public sealed record SpentTokens
{
    /// <summary>Fresh input.</summary>
    public required long Input { get; init; }

    /// <summary>What the model produced, thinking included.</summary>
    public required long Output { get; init; }

    /// <summary>Input served from cache.</summary>
    public required long CacheRead { get; init; }

    /// <summary>Input written to cache.</summary>
    public required long CacheWrite { get; init; }
}

/// <summary>
/// Reads the spend off a headless run's result record.
/// </summary>
/// <remarks>
/// <para>
/// <b>A pure static over the stream, like <c>TranscriptDigest</c>.</b> The
/// executor's result record carries a <c>usage</c> block beside the three
/// fields <c>Result()</c> has always read; this reads that block and nothing
/// else.
/// </para>
/// <para>
/// <b>Null rather than four zeroes.</b> An attended session has no stream, and
/// a headless one can end without a result record — Article XI's case, where
/// the adapter cannot tell what happened. Zero would report both as a loop
/// that cost nothing, which is the answer somebody working to a budget would
/// not stop to question.
/// </para>
/// </remarks>
public static class TranscriptTokens
{
    /// <summary>What the run spent, or null when nothing said.</summary>
    public static SpentTokens? Spent(string transcript)
    {
        ArgumentNullException.ThrowIfNull(transcript);

        foreach (var line in transcript
                     .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                     .Reverse())
        {
            try
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;

                if (root.ValueKind is not JsonValueKind.Object
                    || !root.TryGetProperty("type", out var type)
                    || type.GetString() != "result")
                {
                    continue;
                }

                if (!root.TryGetProperty("usage", out var usage)
                    || usage.ValueKind is not JsonValueKind.Object)
                {
                    return null;
                }

                var spent = new SpentTokens
                {
                    Input = Count(usage, "input_tokens"),
                    Output = Count(usage, "output_tokens"),
                    CacheRead = Count(usage, "cache_read_input_tokens"),
                    CacheWrite = Count(usage, "cache_creation_input_tokens"),
                };

                return spent is { Input: 0, Output: 0, CacheRead: 0, CacheWrite: 0 }
                    ? null
                    : spent;
            }
            catch (JsonException)
            {
                continue;
            }
        }

        return null;
    }

    private static long Count(JsonElement usage, string member) =>
        usage.TryGetProperty(member, out var held)
        && held.ValueKind is JsonValueKind.Number
        && held.TryGetInt64(out var count)
        && count > 0
            ? count
            : 0;
}
