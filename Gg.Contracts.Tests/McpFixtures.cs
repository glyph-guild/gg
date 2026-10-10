namespace Gg.Contracts.Tests;

/// <summary>Documents for the MCP tests: a floor that defines servers and a kind that names them.</summary>
internal static class McpFixtures
{
    public const string Locator = "local:analysis/sonarcloud";

    public static McpServer Hosted(string authorization = "Bearer ${credential:" + Locator + "}") => new()
    {
        Key = "sonarqube",
        Type = McpTransports.Http,
        Url = "https://api.sonarcloud.io/mcp",
        Headers =
        [
            new McpSetting { Name = "Authorization", Value = authorization },
            new McpSetting { Name = "SONARQUBE_ORG", Value = "jdx" },
            new McpSetting { Name = "SONARQUBE_READ_ONLY", Value = "true" },
        ],
    };

    public static McpServer Local() => new()
    {
        Key = "sonar-local",
        Command = "dnx",
        Args = ["mcp-sonarqube@1.1.1", "--yes"],
        Env =
        [
            new McpSetting { Name = "SONARQUBE_TOKEN", Value = "${credential:" + Locator + "}" },
            new McpSetting { Name = "SONARQUBE_ORG", Value = "jdx" },
            new McpSetting { Name = "SONARQUBE_MCP_READ_ONLY", Value = "1" },
        ],
    };

    public static Loop Loop(IReadOnlyList<LoopMcp>? mcp = null, string obligation = "in-scope") => new()
    {
        Id = "investigate",
        Executor = ExecutorRungs.Frontier,
        Discharges = [obligation],
        Moves = [LoopMoves.Read, LoopMoves.Search],
        Budget = new LoopBudget { WallClock = "20m" },
        OnExhaustion = ExhaustionPolicies.HandoffToHuman,
        Mcp = mcp,
    };

    /// <summary>The floor, with whatever servers it defines.</summary>
    public static Envelope Root(
        IReadOnlyList<McpServer>? servers, IReadOnlyList<LoopMcp>? mcp = null, string obligation = "in-scope") => new()
    {
        Context = new ContextBinding { Scope = "**", Constitution = "1.0.0" },
        McpServers = servers,
        Obligations =
        [
            new Obligation
            {
                Id = obligation,
                Check = ObligationChecks.Machine,
                Rule = ObligationPredicates.NoFileOutsideScope,
            },
        ],
        Loops = [Loop(mcp, obligation)],
        Destinations =
        [
            new Destination { Id = "none", Kind = DestinationKinds.None, Requires = [obligation] },
        ],
    };

    /// <summary>A work kind whose one loop names the servers given.</summary>
    public static Envelope Kind(IReadOnlyList<LoopMcp>? mcp, IReadOnlyList<McpServer>? servers = null) =>
        Root(servers, mcp, obligation: "investigate-in-scope") with
        {
            Accepts = [SubjectKinds.Repository],
            Produces = [],
        };

    public static Composition Compose(Envelope root, Envelope kind) => EnvelopeComposition.Compose(
    [
        new EnvelopeLayer { Role = Roles.Root, Name = "root", Version = "v1", Document = root },
        new EnvelopeLayer
        {
            Role = Roles.WorkKind, Name = "investigate", Parent = "root", Version = "v1", Document = kind,
        },
    ]);

    public static IReadOnlyList<LoopMcp> Uses(string server, params string[] allow) =>
        [new LoopMcp { Server = server, Allow = allow.Length == 0 ? ["*"] : allow }];
}
