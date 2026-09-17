namespace Gg.Local;

/// <summary>
/// The tool a sweep pages its watch's query through, and the one field it adds
/// to a listed row.
/// </summary>
/// <remarks>
/// <para>
/// <b>Offered only by a server started with a query</b>, and it takes none. A
/// watch's filter is reviewed - any change to it is a widening - so the runner
/// binds it when it starts the reader for a sweep, and the agent pages through
/// it with <see cref="BrowseTool.Paging"/>'s cursor and limit. A tool that took
/// a query would let an agent sweep something nobody approved.
/// </para>
/// <para>
/// <b>Rows are <see cref="BrowseTool"/>'s, plus <see cref="Revision"/></b>,
/// because the executor keys each nomination on the item's version and asking
/// for it one item at a time would be a round trip per row.
/// </para>
/// </remarks>
public static class QueryTool
{
    /// <summary>The tool's name, unprefixed, as the reader declares it.</summary>
    public const string Name = "query_work_items";

    /// <summary>The item's revision, as the tracker numbers it.</summary>
    public const string Revision = "revision";
}
