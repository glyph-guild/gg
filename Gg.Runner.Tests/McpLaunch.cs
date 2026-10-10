using System.Text.Json;
using Gg.Contracts;
using Gg.Runner.Execution;

namespace Gg.Runner.Tests;

/// <summary>What a launch says about MCP servers, read the way the agent binary reads it.</summary>
internal static class McpLaunch
{
    public const string Locator = "local:analysis/sonarcloud";
    public const string Secret = "squ_the-users-own-token";

    public static McpServer Hosted() => new()
    {
        Key = "sonarqube",
        Type = McpTransports.Http,
        Url = "https://api.sonarcloud.io/mcp",
        Headers =
        [
            new McpSetting { Name = "Authorization", Value = "Bearer ${credential:" + Locator + "}" },
            new McpSetting { Name = "SONARQUBE_ORG", Value = "jdx" },
        ],
    };

    public static McpServer LocalServer() => new()
    {
        Key = "sonar-local",
        Command = "dnx",
        Args = ["mcp-sonarqube@1.1.1", "--yes"],
        Env =
        [
            new McpSetting { Name = "SONARQUBE_TOKEN", Value = "${credential:" + Locator + "}" },
            new McpSetting { Name = "SONARQUBE_ORG", Value = "jdx" },
        ],
    };

    public static ExecutorRequest Request(
        IReadOnlyList<McpServer> servers, IReadOnlyList<LoopMcp> uses, string? provider = null) => new()
    {
        WorkingDirectory = "/tmp/gg-tree",
        LoopId = "investigate",
        IntentProvider = provider,
        IntentId = provider is null ? null : "26",
        Moves = [LoopMoves.Read, LoopMoves.Search],
        WallClock = TimeSpan.FromMinutes(20),
        TranscriptPath = "/tmp/gg-transcript.ndjson",
        McpServers = servers,
        Mcp = uses,
    };

    public static IReadOnlyList<LoopMcp> Uses(string server, params string[] allow) =>
        [new LoopMcp { Server = server, Allow = allow.Length == 0 ? ["*"] : allow }];

    public static string? Resolve(string locator) =>
        string.Equals(locator, Locator, StringComparison.Ordinal) ? Secret : null;

    /// <summary>The value after <c>--mcp-config</c>, which must be a path.</summary>
    public static string ConfigPath(IReadOnlyList<string> arguments) =>
        arguments[arguments.ToList().IndexOf("--mcp-config") + 1];

    /// <summary>The configuration's text, read from the file the launch names.</summary>
    /// <remarks>Left in place, because a test may read it more than once; it is a temporary file.</remarks>
    public static string ConfigText(IReadOnlyList<string> arguments) =>
        File.ReadAllText(ConfigPath(arguments));

    /// <summary>The configuration the agent binary is handed, read from its file.</summary>
    public static JsonElement Servers(IReadOnlyList<string> arguments)
    {
        var path = ConfigPath(arguments);
        var text = File.ReadAllText(path);
        File.Delete(path);
        using var document = JsonDocument.Parse(text);
        return document.RootElement.GetProperty("mcpServers").Clone();
    }

    /// <summary>The tools on the allow-list.</summary>
    public static IReadOnlyList<string> Allowed(IReadOnlyList<string> arguments)
    {
        var list = arguments.ToList();
        var at = list.IndexOf("--allowedTools");
        return at < 0 ? [] : [.. list.Skip(at + 1).TakeWhile(a => !a.StartsWith("--", StringComparison.Ordinal))];
    }
}
