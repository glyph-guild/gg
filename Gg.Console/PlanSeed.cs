namespace Gg.Console;

/// <summary>
/// The work item a plan session starts from: which tracker, which item, and what it is called.
/// </summary>
/// <param name="Provider">The tracker's reader key, such as <c>ado</c>.</param>
/// <param name="Id">The item's id in that tracker.</param>
/// <param name="Title">The item's title as the listing showed it, or null when unknown.</param>
public sealed record PlanSeed(string Provider, string Id, string? Title);
