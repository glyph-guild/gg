namespace Gg.Local;

/// <summary>
/// One work item, as much of it as a reader renders.
/// </summary>
/// <remarks>
/// <para>
/// <b>The field set is the one a deployment already proved useful.</b> Type,
/// state, title, description, acceptance criteria and tags are what the reader
/// running in production projects today, in the order a person reads them. That
/// is a measured set rather than a hopeful one, and matching it exactly is what
/// lets a binary-served reader replace an installed script without changing
/// what any agent sees.
/// </para>
/// <para>
/// <b>Nullable where a tracker may legitimately hold nothing.</b> An item with
/// no acceptance criteria is ordinary; an item with no title is a bug at the
/// tracker. The types say which is which so a reader does not have to invent an
/// empty string to stand for an absent field.
/// </para>
/// </remarks>
public sealed record WorkItem(
    string Id,
    string Type,
    string State,
    string Title,
    string? Description,
    string? AcceptanceCriteria,
    string? Tags,
    string? Url);

/// <summary>
/// One work item as a list shows it: enough to choose by, and no more.
/// </summary>
/// <remarks>
/// <b>Exactly <see cref="BrowseTool.Fields"/>, and deliberately not a
/// <see cref="WorkItem"/>.</b> The browse contract names five fields and
/// excludes description and body on purpose - a list that carried them would
/// make every page as expensive as reading everything on it. A separate type is
/// what keeps that decision from eroding one convenient property at a time.
/// </remarks>
public sealed record WorkItemSummary(
    string Id,
    string Title,
    string State,
    string Url,
    string? Updated);

/// <summary>
/// A page of work items, and how to ask for the next one.
/// </summary>
/// <param name="Items">This page, in the order the tracker returned them.</param>
/// <param name="NextCursor">
/// What to pass as <see cref="BrowseTool.Paging.Cursor"/> to continue, or null
/// when this page is the last. <b>Null is the end of the list</b> - not an
/// empty string, which a caller would pass back and receive the first page for.
/// </param>
public sealed record WorkItemPage(
    IReadOnlyList<WorkItemSummary> Items,
    string? NextCursor);

/// <summary>
/// Where work items are read from.
/// </summary>
/// <remarks>
/// <para>
/// <b>The seam, and it exists so the protocol is testable without a tracker.</b>
/// A server that could only be exercised against a live tracker is a server
/// nobody exercises: the JSON-RPC framing above this interface and the http
/// below it fail in completely different ways, and a test that has to reach a
/// network to see either cannot tell them apart.
/// </para>
/// <para>
/// <b>Two calls, because the browse contract names two.</b> Reading one item by
/// id is what a flight needs; a page of them is what a person choosing work
/// needs. A reader implementing only the first is the case
/// <see cref="BrowseTool.IsBrowsable"/> exists to report.
/// </para>
/// </remarks>
public interface IWorkItemSource
{
    /// <summary>The item, or null where the tracker has no such id.</summary>
    Task<WorkItem?> ReadAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>A page of items, oldest cursor semantics decided by the source.</summary>
    Task<WorkItemPage> BrowseAsync(
        string? cursor, int limit, CancellationToken cancellationToken = default);
}
