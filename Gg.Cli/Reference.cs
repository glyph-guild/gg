using System.Text.Json;
using System.Text.Json.Serialization;
using Gg.Console;
using Gg.Client;

namespace Gg.Cli;

/// <summary>
/// Every command and console key, in the shape docs.goodgrief.dev builds its
/// reference pages from.
/// </summary>
/// <remarks>
/// Read from the same usage list help prints and the same catalogue the help
/// page draws, so the published reference cannot describe a command or key
/// that this binary does not have.
/// </remarks>
public static class Reference
{
    public static ReferenceDocument Build() =>
        new(GgVersions.Binary, Commands(CliArgs.Usage), Keys());

    /// <summary>
    /// The usage lines, read back into commands.
    /// </summary>
    /// <remarks>
    /// A line that starts with <c>gg</c> is a command. An indented line is one
    /// of three things: an option (a usage and a summary, separated by a run of
    /// spaces), more of the command's usage (it starts with <c>[</c> or
    /// <c>-</c> and the command has no summary yet), or more of the summary.
    /// </remarks>
    internal static IReadOnlyList<ReferenceCommand> Commands(IEnumerable<string> lines)
    {
        var commands = new List<(string Usage, string Summary, List<ReferenceOption> Options)>();

        foreach (var line in lines)
        {
            if (line.StartsWith("gg", StringComparison.Ordinal))
            {
                var (usage, summary) = Split(line);
                commands.Add((usage, summary, []));
                continue;
            }

            if (commands.Count == 0)
            {
                continue;
            }

            var current = commands[^1];
            var trimmed = line.Trim();
            var (part, rest) = Split(trimmed);

            if (rest.Length > 0)
            {
                current.Options.Add(new ReferenceOption(part, rest));
            }
            else if (current.Summary.Length == 0 && trimmed is ['[' or '-', ..])
            {
                commands[^1] = current with { Usage = current.Usage + " " + trimmed };
            }
            else if (current.Summary.Length == 0 || current.Options.Count == 0)
            {
                commands[^1] = current with { Summary = Join(current.Summary, trimmed) };
            }
            else
            {
                var last = current.Options[^1];
                current.Options[^1] = last with { Summary = Join(last.Summary, trimmed) };
            }
        }

        return [.. commands.Select(c => new ReferenceCommand(c.Usage, c.Summary, c.Options))];
    }

    private static IReadOnlyList<ReferenceKey> Keys() =>
        [.. Keymap.Catalogue()
            .Where(entry => !entry.Binding.Untaught)
            .Select(entry => new ReferenceKey(
                entry.Mode.ToString(),
                entry.Mode == UiMode.Normal ? "" : PaneText.ModeHeading(entry.Mode),
                entry.Binding.Key.Name,
                entry.Binding.Description,
                entry.Binding.When is { Length: > 0 } when ? when : null))];

    /// <summary>A usage and its summary, split at the first run of two or more spaces.</summary>
    private static (string Usage, string Summary) Split(string line)
    {
        var at = line.IndexOf("  ", StringComparison.Ordinal);

        return at < 0 ? (line.Trim(), "") : (line[..at].Trim(), line[at..].Trim());
    }

    private static string Join(string first, string second) =>
        first.Length == 0 ? second : first + " " + second;

    public static string ToJson(ReferenceDocument document) =>
        JsonSerializer.Serialize(document, ReferenceJsonContext.Default.ReferenceDocument);
}

/// <summary>The whole reference, for one version of gg.</summary>
public sealed record ReferenceDocument(
    string Version,
    IReadOnlyList<ReferenceCommand> Commands,
    IReadOnlyList<ReferenceKey> Keys);

/// <summary>One command as help lists it, with the options listed under it.</summary>
public sealed record ReferenceCommand(
    string Usage,
    string Summary,
    IReadOnlyList<ReferenceOption> Options);

/// <summary>One option line under a command.</summary>
public sealed record ReferenceOption(string Usage, string Summary);

/// <summary>One console key, as the help page lists it.</summary>
/// <param name="Mode">The console mode the key belongs to.</param>
/// <param name="Heading">What the help page calls that mode; empty for the main screen.</param>
/// <param name="Key">The key, as the help page names it.</param>
/// <param name="Does">What pressing it does.</param>
/// <param name="When">The condition under which it applies, if any.</param>
public sealed record ReferenceKey(string Mode, string Heading, string Key, string Does, string? When);

/// <summary>Source-generated, because everything here must stay AOT-publishable.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ReferenceDocument))]
internal sealed partial class ReferenceJsonContext : JsonSerializerContext;
