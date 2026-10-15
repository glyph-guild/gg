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
    public static ReferenceDocument Build() => throw new NotImplementedException();

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
