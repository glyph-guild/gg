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
    string? Updated,

    /// <summary>Where the tracker files it, or null where it says nothing.</summary>
    /// <remarks>
    /// <b>A filter a person cannot see the effect of is a filter they cannot
    /// trust.</b> Narrowing to a team and being shown the same undifferentiated
    /// list is indistinguishable from a filter that did not take, so what was
    /// filtered on is on the row.
    /// </remarks>
    string? AreaPath = null,

    /// <summary>The sprint it is in, or null where the tracker says nothing.</summary>
    string? Iteration = null);

/// <summary>
/// What a caller is asking to see, or null where it is asking for the default.
/// </summary>
/// <remarks>
/// <para>
/// <b>It narrows the QUERY, not the page.</b> Filtering what came back can only
/// filter what happened to come back, so a sprint whose work fell outside the
/// first page would read as an empty one - a box that is lying, which is what
/// the browse endings exist to stop.
/// </para>
/// <para>
/// <b>Every part is optional and absent means "do not narrow on this".</b> Not
/// "match nothing" and not "match everything explicitly": a filter with one
/// field set is the ordinary case, and a caller should not have to name the two
/// it does not care about.
/// </para>
/// <para>
/// <b>States REPLACE the default rather than adding to it.</b> The default list
/// excludes closed and removed work; a caller asking FOR closed work and being
/// handed that exclusion on top would get nothing, for ever, with nothing on
/// screen to say why.
/// </para>
/// </remarks>
public sealed record WorkItemFilter(
    string? AreaPath = null,
    string? Iteration = null,
    IReadOnlyList<string>? States = null)
{
    /// <summary>Whether this narrows anything at all.</summary>
    public bool Narrows =>
        AreaPath is { Length: > 0 }
        || Iteration is { Length: > 0 }
        || States is { Count: > 0 };
}

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
        string? cursor, int limit, WorkItemFilter? filter = null,
        CancellationToken cancellationToken = default);

    /// <summary>What has happened to one item, oldest first.</summary>
    /// <remarks>
    /// <b>Empty is an answer.</b> Nothing has happened to it yet, which is a
    /// thing to know rather than an error to report - the same distinction the
    /// browse page draws between an empty backlog and a refusal.
    /// </remarks>
    Task<IReadOnlyList<WorkItemChange>> HistoryAsync(
        string id, CancellationToken cancellationToken = default);
}

/// <summary>
/// One thing that happened to a work item.
/// </summary>
/// <remarks>
/// <para>
/// <b>A when, a who and a what, because that is what any tracker has.</b> Which
/// one spells them which way is the deployment's business - this names no forge,
/// for <see cref="BrowseTool"/>'s reason one file over.
/// </para>
/// <para>
/// <b>A field change and a comment are the same shape.</b> One says a state
/// moved and the other says somebody wrote a paragraph, and sorting them into
/// two lists would be deciding for a person which of the two they came for.
/// </para>
/// </remarks>
public sealed record WorkItemChange(DateTimeOffset When, string Who, string What);
