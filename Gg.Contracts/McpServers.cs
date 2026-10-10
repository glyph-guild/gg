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
/// <remarks>
/// On the contract fingerprint: the values travel in an envelope, so one added
/// here is a document an older reader refuses.
/// </remarks>
[VocabularyOf(VocabularyFingerprints.Contract)]
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
    /// <remarks>
    /// <b>Only the named ones</b>, because the lease carries what this loop may
    /// reach and not what the floor happens to define. A name with no definition
    /// is absent here; composition has already refused it.
    /// </remarks>
    public static IReadOnlyList<McpServer> For(Envelope composed, Loop loop)
    {
        ArgumentNullException.ThrowIfNull(composed);
        ArgumentNullException.ThrowIfNull(loop);

        if (loop.Mcp is not { Count: > 0 } uses || composed.McpServers is not { } defined)
        {
            return [];
        }

        return
        [
            .. uses
                .Select(use => defined.FirstOrDefault(
                    server => string.Equals(server.Key, use.Server, StringComparison.Ordinal)))
                .OfType<McpServer>(),
        ];
    }
}

/// <summary>What a server definition and a loop's use of one may say.</summary>
public static class McpRules
{
    /// <summary>
    /// The key gg serves its own tools under, which no external server may take.
    /// </summary>
    /// <remarks>
    /// A server under it would shadow the platform's tools, including the one an
    /// agent asks a person with. The runner's own server is named this in
    /// <c>NominationTool.Server</c>, which this package cannot reference.
    /// </remarks>
    public const string Reserved = "gg";

    /// <summary>Every tool a server has, now and after it grows.</summary>
    public const string Everything = "*";

    /// <summary>How a credential is referenced inside a value.</summary>
    public const string ReferenceOpen = "${credential:";

    private static readonly string[] SecretWords =
        ["authorization", "token", "secret", "password", "passwd", "apikey", "api_key", "api-key"];

    /// <summary>What is wrong with the document's servers or its loops' use of them, or null.</summary>
    public static string? Refuse(Envelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var keys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var server in envelope.McpServers ?? [])
        {
            if (!keys.Add(server.Key))
            {
                return $"Two MCP servers are defined under '{server.Key}' in 'mcp-servers:'. A loop "
                     + "names a server by its key, so two under one key would give it whichever was "
                     + "read second.";
            }

            if (Refuse(server) is { } refused)
            {
                return refused;
            }
        }

        foreach (var loop in envelope.Loops)
        {
            var named = new HashSet<string>(StringComparer.Ordinal);

            foreach (var use in loop.Mcp ?? [])
            {
                var at = $"Loop '{loop.Id}' 'mcp: {use.Server}'";

                if (!Named(use.Server))
                {
                    return $"Loop '{loop.Id}' names an MCP server '{use.Server}', which is not a key "
                         + "root could define: letters, digits, '-' and '_'.";
                }

                if (!named.Add(use.Server))
                {
                    return $"{at} is named twice.";
                }

                if (use.Allow.Count == 0)
                {
                    return $"{at} allows no tools. Write '{Everything}' for every tool, or name the "
                         + "ones it may call; a server named and then forbidden is a mistake.";
                }

                if (use.Allow.Contains(Everything, StringComparer.Ordinal) && use.Allow.Count > 1)
                {
                    return $"{at} allows '{Everything}' beside named tools. '{Everything}' is every "
                         + "tool already, so write it alone or name the tools.";
                }

                if (use.Allow.FirstOrDefault(tool => tool != Everything && !Tool(tool)) is { } bad)
                {
                    return $"{at} allows '{bad}', which is not a tool name. Name tools as the server "
                         + "does, without the 'mcp__' prefix.";
                }
            }
        }

        return null;
    }

    /// <summary>Everything a definition says, as one string, so any change moves it.</summary>
    public static string Fingerprint(McpServer server)
    {
        ArgumentNullException.ThrowIfNull(server);

        static string Settings(IReadOnlyList<McpSetting>? settings) =>
            settings is null ? "-" : string.Join("\u0001", settings.Select(s => $"{s.Name}={s.Value}"));

        return string.Join(
            "\u0000",
            server.Key, server.Type ?? "-", server.Command ?? "-",
            server.Args is null ? "-" : string.Join("\u0001", server.Args),
            Settings(server.Env), server.Url ?? "-", Settings(server.Headers));
    }

    private static string? Refuse(McpServer server)
    {
        var at = $"MCP server '{server.Key}'";

        if (!Named(server.Key))
        {
            return $"{at}: a key is letters, digits, '-' and '_', because it becomes the prefix of "
                 + "its tools' names.";
        }

        if (string.Equals(server.Key, Reserved, StringComparison.Ordinal))
        {
            return $"{at}: '{Reserved}' is the key gg serves its own tools under, and a server "
                 + "under it would shadow them.";
        }

        if (server.Type is { } type && !McpTransports.All.Contains(type, StringComparer.Ordinal))
        {
            return $"{at} has type '{type}'. Expected one of: {string.Join(", ", McpTransports.All)}.";
        }

        var local = server.Command is { Length: > 0 };
        var remote = server.Url is { Length: > 0 };

        if (local == remote)
        {
            return $"{at} names {(local ? "both a command and a url" : "neither a command nor a url")}. "
                 + "A server is started from a command or reached at a url, and only one.";
        }

        if (local && (server.Headers is not null || server.Type is McpTransports.Http or McpTransports.Sse))
        {
            return $"{at} is started from a command, so it takes 'env', not 'headers', and its type "
                 + $"is '{McpTransports.Stdio}' or unsaid.";
        }

        if (remote && (server.Env is not null || server.Args is not null
                       || server.Type is not (McpTransports.Http or McpTransports.Sse)))
        {
            return $"{at} is reached at a url, so it says 'type: {McpTransports.Http}' (or "
                 + $"'{McpTransports.Sse}') and takes 'headers', not 'env' or 'args'.";
        }

        if (server.Args?.FirstOrDefault(a => a.Contains(ReferenceOpen, StringComparison.Ordinal)) is not null)
        {
            return $"{at} puts a credential in an argument. Arguments are on the command line, where "
                 + "every user on the host can read them; put it in 'env'.";
        }

        foreach (var (section, settings) in ((string, IReadOnlyList<McpSetting>?)[])
                 [("env", server.Env), ("headers", server.Headers)])
        {
            foreach (var setting in settings ?? [])
            {
                if (Refuse(setting) is { } refused)
                {
                    return $"{at} '{section}.{setting.Name}': {refused}";
                }
            }
        }

        return null;
    }

    private static string? Refuse(McpSetting setting)
    {
        if (setting.Name.Length == 0
            || setting.Name.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '_' or '-')))
        {
            return "a name is letters, digits, '_' and '-'.";
        }

        var references = 0;
        var rest = setting.Value;

        while (rest.IndexOf("${", StringComparison.Ordinal) is var open and >= 0)
        {
            rest = rest[open..];

            if (!rest.StartsWith(ReferenceOpen, StringComparison.Ordinal))
            {
                return $"only '{ReferenceOpen}<locator>}}' is substituted, and '{rest}' is not that.";
            }

            var close = rest.IndexOf('}', StringComparison.Ordinal);
            if (close < 0)
            {
                return $"'{rest}' opens a credential reference and never closes it.";
            }

            var locator = rest[ReferenceOpen.Length..close];
            if (Locator(locator) is { } bad)
            {
                return $"the reference '{locator}' {bad}";
            }

            references++;
            rest = rest[(close + 1)..];
        }

        var lowered = setting.Name.ToLowerInvariant();
        return references == 0 && SecretWords.Any(word => lowered.Contains(word, StringComparison.Ordinal))
            ? "its name says it holds a secret, and its value is written out. A document names where "
            + $"a credential is, as '{ReferenceOpen}<locator>}}', and never carries one."
            : null;
    }

    private static string? Locator(string locator)
    {
        if (locator.StartsWith(CredentialLocator.VaultScheme, StringComparison.Ordinal))
        {
            return locator.Length > CredentialLocator.VaultScheme.Length && !locator.Any(char.IsWhiteSpace)
                ? null
                : "is an empty or broken vault reference.";
        }

        return CredentialLocator.Validate(locator) is { } refused ? $"is not a locator: {refused}" : null;
    }

    private static bool Named(string key) =>
        key.Length > 0 && key.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

    private static bool Tool(string tool) =>
        tool.Length > 0
        && !tool.StartsWith("mcp__", StringComparison.Ordinal)
        && tool.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.');
}
