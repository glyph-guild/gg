namespace Gg.Local;

/// <summary>
/// The tool an agent states what its proposal should be called with.
/// </summary>
/// <remarks>
/// <para>
/// <b>The third tool on this platform's own server, and its own whole name.</b>
/// One move granting two tools would be the prefix grant
/// <see cref="WorkItemProposalTool"/> refuses arriving by another route: every
/// tool this platform later adds would be granted retroactively, for every
/// envelope in force, with nothing in the record marking the day it changed.
/// </para>
/// <para>
/// <b>It exists because a title cut out of prose is not one the agent
/// chose.</b> The alternative considered first was a convention inside the
/// closing summary, and this repository already has the evidence against it: a
/// <c>CONSIDERED:</c> line has been asked for in an airspace document for
/// weeks, is cut by the reason's own first-paragraph rule before it reaches a
/// fact, and is read by nothing on either side.
/// </para>
/// </remarks>
public static class LandingProposalTool
{
    /// <summary>The platform's own server, shared with the two proposals.</summary>
    public const string Server = NominationTool.Server;

    /// <summary>The bare name, as <c>tools/list</c> declares it.</summary>
    public const string Name = "propose_landing";

    /// <summary>How it arrives in the stream, and what the launch grants.</summary>
    public const string Qualified = $"mcp__{Server}__{Name}";

    /// <summary>The argument carrying what the proposal should be called.</summary>
    public const string TitleArgument = "title";

    /// <summary>The argument carrying what it should say beyond its title.</summary>
    public const string DescriptionArgument = "description";
}
