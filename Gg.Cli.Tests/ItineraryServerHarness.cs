using System.Text.Json;
using Gg.Client;

namespace Gg.Cli.Tests;

/// <summary>Drives <see cref="ItineraryToolServer"/> over its real line protocol, against a temp drafts directory.</summary>
internal sealed class ItineraryServerHarness : IDisposable
{
    private readonly List<string> _requests = [];
    private int _next = 1;

    internal string Root { get; } = Directory.CreateTempSubdirectory("gg-itinerary-").FullName;

    internal ItineraryDrafts Drafts => new(Root);

    /// <summary>The control plane's planning reads, as this test wants them answered.</summary>
    internal FakePlanningReads Reads { get; } = new();

    internal ItineraryServerHarness Call(string tool, object? arguments = null)
    {
        _requests.Add(JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["id"] = _next++,
            ["method"] = "tools/call",
            ["params"] = new Dictionary<string, object?> { ["name"] = tool, ["arguments"] = arguments ?? new { } },
        }));
        return this;
    }

    internal ItineraryServerHarness Method(string method)
    {
        _requests.Add(JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["id"] = _next++,
            ["method"] = method,
            ["params"] = new Dictionary<string, object?> { ["protocolVersion"] = "2024-11-05" },
        }));
        return this;
    }

    /// <summary>Every answer, in order, as parsed documents.</summary>
    internal async Task<IReadOnlyList<JsonElement>> RunAsync(string draft = "draft")
    {
        using var input = new StringReader(string.Join('\n', _requests) + "\n");
        await using var output = new StringWriter();

        await ItineraryToolServer.RunAsync(input, output, Drafts, draft, Reads);

        return [.. output.ToString()
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => JsonDocument.Parse(line).RootElement.Clone())];
    }

    /// <summary>A tool result's text, and whether it was an error.</summary>
    internal static (string Text, bool IsError) Result(JsonElement answer)
    {
        var result = answer.GetProperty("result");
        return (result.GetProperty("content")[0].GetProperty("text").GetString()!,
                result.GetProperty("isError").GetBoolean());
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
