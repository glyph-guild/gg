using System.Text.Json;
using Gg.Contracts;

namespace Gg.Runner.Execution;

/// <summary>
/// Turns a loop's event stream into the fact that crosses in its place.
/// </summary>
/// <remarks>
/// <para>
/// <b>A function of the text it is handed, and nothing else.</b> It reads no
/// file, starts no process and reaches no network - which is what makes "no
/// model is in this path" a property of the code rather than a promise. The
/// tempting implementation is one call away and would look like an improvement;
/// what it would actually do is turn the one artifact that crosses into an
/// injection surface, because the transcript can contain text addressed to a
/// model.
/// </para>
/// <para>
/// <b>Extraction, not summary.</b> Every field here is something the stream
/// says literally: a path on a tool call, a pattern on a search, the text of a
/// failure. Nothing is interpreted, so the same stream produces the same digest,
/// and digests are comparable across flights - which is the whole of Article
/// XIII's hardening.
/// </para>
/// <para>
/// <b>Bounded, because this crosses.</b> A digest is machine-comparable history
/// sized for diffing thirty flights, not for reading one. Long values are cut
/// and the cut is visible; long lists stop and say they stopped.
/// </para>
/// </remarks>
public static class TranscriptDigest
{
    /// <summary>How much of one value crosses.</summary>
    private const int MaxDetail = 240;

    /// <summary>How many of anything crosses.</summary>
    /// <remarks>
    /// A cap rather than a truncation nobody sees: when it bites, the list says
    /// so in its own last entry. A digest that silently stopped listing would
    /// read as a loop that stopped looking.
    /// </remarks>
    private const int MaxItems = 64;

    /// <summary>
    /// The digest for one loop's stream.
    /// </summary>
    /// <param name="transcript">The line-delimited events, as they were written.</param>
    /// <param name="loopId">Which loop, from the envelope.</param>
    /// <param name="treeRoots">
    /// Every spelling of the tree, so paths can be made relative to it.
    /// </param>
    /// <param name="outcome">Where it stopped, from <see cref="LoopOutcomes"/>.</param>
    /// <param name="refused">Moves the envelope did not allow.</param>
    public static LoopDigest Extract(
        string transcript,
        string loopId,
        IReadOnlyList<string> treeRoots,
        string outcome,
        IReadOnlyList<string> refused)
    {
        ArgumentNullException.ThrowIfNull(transcript);
        ArgumentNullException.ThrowIfNull(treeRoots);
        ArgumentNullException.ThrowIfNull(refused);

        // First-appearance order, deduplicated. The order is part of the value -
        // it is the sequence in which the loop looked at things.
        var read = new List<string>();
        var edited = new List<string>();
        var searches = new List<string>();
        var errors = new List<DigestError>();

        // Which tool a result belongs to. The failure text arrives on a separate
        // event from the call that caused it, and "something failed" without the
        // tool is a sentence nobody can act on.
        var toolById = new Dictionary<string, string>(StringComparer.Ordinal);

        var attempts = 0;

        foreach (var line in transcript.Split('\n'))
        {
            if (line.Length == 0)
            {
                continue;
            }

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(line);
            }
            catch (JsonException)
            {
                // A half-written last line is the ordinary case while a file is
                // still being appended to. Skipping it loses one event; throwing
                // would lose every signal before it.
                continue;
            }

            using (document)
            {
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (Turns(root) is { } turns)
                {
                    attempts = turns;
                }

                if (!root.TryGetProperty("message", out var message)
                    || message.ValueKind != JsonValueKind.Object
                    || !message.TryGetProperty("content", out var content)
                    || content.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var block in content.EnumerateArray())
                {
                    if (block.ValueKind != JsonValueKind.Object
                        || !block.TryGetProperty("type", out var type))
                    {
                        continue;
                    }

                    switch (type.GetString())
                    {
                        case "tool_use":
                            Use(block, treeRoots, toolById, read, edited, searches);
                            break;

                        case "tool_result":
                            Result(block, toolById, errors);
                            break;
                    }
                }
            }
        }

        return new LoopDigest
        {
            LoopId = loopId,
            // Read AND edited is the work, not the thinking. What is left is the
            // proxy for considered-and-ruled-out, which is the point of all this.
            FilesReadNotEdited = Bounded(read.Where(p => !edited.Contains(p, StringComparer.Ordinal))),
            FilesEdited = Bounded(edited),
            Searches = Bounded(searches),
            Errors = [.. errors.Take(MaxItems)],
            RefusedMoves = Bounded(refused),
            Attempts = attempts,
            StopReason = outcome,
        };
    }

    /// <summary>Records one tool call.</summary>
    private static void Use(
        JsonElement block,
        IReadOnlyList<string> treeRoots,
        Dictionary<string, string> toolById,
        List<string> read,
        List<string> edited,
        List<string> searches)
    {
        if (!block.TryGetProperty("name", out var name)
            || name.GetString() is not { Length: > 0 } tool)
        {
            return;
        }

        if (block.TryGetProperty("id", out var id) && id.GetString() is { Length: > 0 } callId)
        {
            toolById[callId] = tool;
        }

        if (!block.TryGetProperty("input", out var input) || input.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var path = Text(input, "file_path");
        if (path is not null)
        {
            var relative = Relative(path, treeRoots);

            // Anything that names a file and is not a read is a change to it.
            // Erring this way keeps a changed file out of the ruled-out list,
            // which is the mistake that would matter: it would report the work
            // as thinking.
            (string.Equals(tool, "Read", StringComparison.Ordinal) ? read : edited).Add(relative);
        }

        if (Text(input, "pattern") is { } pattern)
        {
            searches.Add(Short(pattern));
        }
    }

    /// <summary>Records a failure, against the tool that produced it.</summary>
    private static void Result(
        JsonElement block, Dictionary<string, string> toolById, List<DigestError> errors)
    {
        if (!block.TryGetProperty("is_error", out var flag)
            || flag.ValueKind != JsonValueKind.True)
        {
            return;
        }

        var source = block.TryGetProperty("tool_use_id", out var id)
                  && id.GetString() is { Length: > 0 } callId
                  && toolById.TryGetValue(callId, out var tool)
            ? tool
            : "unknown";

        errors.Add(new DigestError { Source = source, Detail = Short(Content(block)) });
    }

    /// <summary>A result's text, whichever shape it arrived in.</summary>
    private static string Content(JsonElement block)
    {
        if (!block.TryGetProperty("content", out var content))
        {
            return "";
        }

        if (content.ValueKind == JsonValueKind.String)
        {
            return content.GetString() ?? "";
        }

        if (content.ValueKind != JsonValueKind.Array)
        {
            return "";
        }

        // A result is sometimes a list of blocks rather than a string, and a
        // digest that only understood one of the two would report no detail for
        // half the failures.
        foreach (var part in content.EnumerateArray())
        {
            if (part.ValueKind == JsonValueKind.Object && Text(part, "text") is { } text)
            {
                return text;
            }
        }

        return "";
    }

    /// <summary>The turn count, when this event carries one.</summary>
    private static int? Turns(JsonElement root) =>
        root.TryGetProperty("num_turns", out var turns) && turns.ValueKind == JsonValueKind.Number
            ? turns.GetInt32()
            : null;

    private static string? Text(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value)
        && value.ValueKind == JsonValueKind.String
        && value.GetString() is { Length: > 0 } text
            ? text
            : null;

    /// <summary>
    /// A path relative to the tree.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An absolute path is a machine detail crossing a boundary, and a digest
    /// carrying <c>/home/runner/...</c> is not comparable with one carrying
    /// <c>/work/...</c> - which quietly ends the cross-flight comparison this
    /// exists for.
    /// </para>
    /// <para>
    /// <b>More than one root, because a tree has more than one name.</b> Found
    /// against a real agent: on macOS the tree is handed over as
    /// <c>/var/folders/…</c> and the agent reports every path as
    /// <c>/private/var/folders/…</c>, because one is a symlink to the other. A
    /// single-spelling comparison matched nothing, left the paths absolute, and
    /// would have shipped this machine's directory layout inside the one fact
    /// that is supposed to compare across machines.
    /// </para>
    /// <para>
    /// The longest matching root wins, so a nested tree does not lose its
    /// prefix to a shorter one that also matches.
    /// </para>
    /// </remarks>
    private static string Relative(string path, IReadOnlyList<string> treeRoots)
    {
        var clean = ControlText.Strip(path) ?? "";

        var matched = treeRoots
            .Where(r => !string.IsNullOrEmpty(r))
            .Select(r => r.TrimEnd('/'))
            .Where(r => clean.StartsWith(r + "/", StringComparison.Ordinal))
            .OrderByDescending(r => r.Length)
            .FirstOrDefault();

        return matched is not null ? clean[(matched.Length + 1)..] : clean.TrimStart('/');
    }

    /// <summary>
    /// Stripped and cut, in that order.
    /// </summary>
    /// <remarks>
    /// <b>Stripping happens here, in the runner, before the digest.</b> Doing it
    /// on the far side would mean the stored bytes disagree with the hash that
    /// proves what they were, and a control plane holding an escape sequence is
    /// one that can drive a terminal.
    /// </remarks>
    private static string Short(string value)
    {
        var clean = (ControlText.Strip(value) ?? "").ReplaceLineEndings(" ").Trim();

        return clean.Length <= MaxDetail ? clean : clean[..MaxDetail] + "…";
    }

    /// <summary>
    /// The first <see cref="MaxItems"/>, deduplicated, saying so when it cuts.
    /// </summary>
    /// <remarks>
    /// A list that silently stopped would read as a loop that stopped looking,
    /// which is a different thing and the one somebody would act on.
    /// </remarks>
    private static IReadOnlyList<string> Bounded(IEnumerable<string> values)
    {
        var distinct = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var value in values)
        {
            if (value.Length > 0 && seen.Add(value))
            {
                distinct.Add(value);
            }
        }

        return distinct.Count <= MaxItems
            ? distinct
            : [.. distinct.Take(MaxItems), $"… and {distinct.Count - MaxItems} more"];
    }
}
