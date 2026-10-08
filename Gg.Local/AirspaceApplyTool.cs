namespace Gg.Local;

/// <summary>
/// The tool an airspace agent applies the working copy with, through the verb a person would type
/// (<c>gg airspace apply</c>), as the signed-in person.
/// </summary>
/// <remarks>
/// <b>Declared, never granted at launch.</b> Applying changes what every flight in the tenant is
/// governed by, so Claude Code asks the person before each call: its permission prompt is the
/// confirmation the console's own apply asks for.
/// </remarks>
public static class AirspaceApplyTool
{
    /// <summary>The server key, and therefore the tool-name prefix.</summary>
    public const string Server = NominationTool.Server;

    /// <summary>The tool, as the server declares it.</summary>
    public const string Name = "apply_airspace";

    /// <summary>The tool as the agent sees it - never on a launch's allow-list.</summary>
    public const string Qualified = $"mcp__{Server}__{Name}";

    /// <summary>Whether to declare the names the documents use and the airspace lacks.</summary>
    public const string DeclareNamesArgument = "declare_names";
}
