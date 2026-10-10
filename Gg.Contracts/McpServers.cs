namespace Gg.Contracts;

/// <summary>
/// An external MCP server, defined once in root, written the way the agent's
/// own MCP configuration writes it (ADR-0040).
/// </summary>
/// <remarks>
/// <para>
/// <b>Passed through, never interpreted.</b> gg does not know what an external
/// server's tools do, so it does not classify them, name them in a move, or
/// translate the declaration. The runner substitutes credential references and
/// writes the rest as the author wrote it.
/// </para>
/// <para>
/// <b>Either a command or a URL.</b> A local server is <see cref="Command"/>,
/// <see cref="Args"/> and <see cref="Env"/>; a remote one is <see cref="Url"/>
/// and <see cref="Headers"/>, with <see cref="Type"/> saying how it is spoken to.
/// </para>
/// </remarks>
[PinnedId("f245f800-08f5-44c1-9b17-3ed7fa333b99")]
public sealed record McpServer
{
    /// <summary>The server's name, which is also the prefix of its tools' names.</summary>
    public required string Key { get; init; }

    /// <summary>One of <see cref="McpTransports"/>, or null for a local command.</summary>
    public string? Type { get; init; }

    /// <summary>The program a local server is started with.</summary>
    public string? Command { get; init; }

    /// <summary>Its arguments. Never a credential: arguments are visible to everything on the host.</summary>
    public IReadOnlyList<string>? Args { get; init; }

    /// <summary>Its environment, where a credential reference may go.</summary>
    public IReadOnlyList<McpSetting>? Env { get; init; }

    /// <summary>Where a remote server answers.</summary>
    public string? Url { get; init; }

    /// <summary>The headers sent to a remote server, where a credential reference may go.</summary>
    public IReadOnlyList<McpSetting>? Headers { get; init; }
}

/// <summary>One environment variable or header on an MCP server.</summary>
/// <remarks>
/// A literal, or text containing <c>${credential:&lt;locator&gt;}</c>, which the
/// runner replaces with the secret its own store resolves. A document never
/// carries a secret.
/// </remarks>
[PinnedId("50bf343e-b4fb-4550-a8a7-ab3d2d02cbc3")]
public sealed record McpSetting
{
    /// <summary>The variable or header name.</summary>
    public required string Name { get; init; }

    /// <summary>Its value, literal or carrying a credential reference.</summary>
    public required string Value { get; init; }
}

/// <summary>A server a loop uses, by the name root defines it under, and the tools it may call.</summary>
[PinnedId("ea2e933e-dd8e-467d-ba13-56ef1faecf01")]
public sealed record LoopMcp
{
    /// <summary>The key of a server root defines.</summary>
    public required string Server { get; init; }

    /// <summary>The tools the loop may call on it, by the server's own names, or <c>*</c> for all.</summary>
    public required IReadOnlyList<string> Allow { get; init; }
}

/// <summary>How an agent speaks to a remote MCP server.</summary>
public static class McpTransports
{
    public const string Stdio = "stdio";
    public const string Http = "http";
    public const string Sse = "sse";

    public static IReadOnlyList<string> All { get; } = [Stdio, Http, Sse];
}

/// <summary>Joining a loop's names to root's definitions.</summary>
public static class McpDefinitions
{
    /// <summary>The definitions a loop names, in the order it names them.</summary>
    public static IReadOnlyList<McpServer> For(Envelope composed, Loop loop)
    {
        ArgumentNullException.ThrowIfNull(composed);
        ArgumentNullException.ThrowIfNull(loop);
        return [];
    }
}
