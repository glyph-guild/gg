namespace Gg.Local;

/// <summary>
/// The tool an airspace agent asks what applying the working copy would change with, through the
/// verb a person would type (<c>gg airspace diff</c>). Reads only; granted at launch.
/// </summary>
public static class AirspaceDiffTool
{
    /// <summary>The server key, and therefore the tool-name prefix.</summary>
    public const string Server = NominationTool.Server;

    /// <summary>The tool, as the server declares it.</summary>
    public const string Name = "diff_airspace";

    /// <summary>The tool as the agent sees it, and as a launch grants it.</summary>
    public const string Qualified = $"mcp__{Server}__{Name}";
}
