namespace Gg.Console;

/// <summary>One change to a work item, as a row.</summary>
/// <remarks>
/// <b>Strings, and the time is the tracker's own spelling.</b> The contract
/// carries it that way for the reason <c>BrowseRow.Updated</c> does: a console
/// re-formatting somebody else's timestamp is a second opinion about a record
/// it does not own, and parsing one back is a second chance to disagree.
/// </remarks>
public sealed record WorkItemChangeRow
{
    public required string When { get; init; }

    public required string Who { get; init; }

    public required string What { get; init; }
}

/// <summary>
/// How asking a reader what has happened to an item ended.
/// </summary>
/// <remarks>
/// <para>
/// <b>Rows or a sentence</b>, which is <see cref="ItemOutcome"/>'s shape over a
/// table rather than over prose. The two cannot be one type: a history that
/// came back is a list a pane puts in columns, and a history that did not is a
/// line a person reads.
/// </para>
/// <para>
/// <b>Empty is not absent.</b> A tracker that answered and had nothing to say
/// is <see cref="Read"/> with no rows; a reader that does not declare the tool
/// is <see cref="Nothing"/>. An empty table claims the first when it may be the
/// second, which is the distinction every pane in this console draws.
/// </para>
/// </remarks>
public abstract record HistoryOutcome
{
    /// <summary>The reader answered, with however many rows it had.</summary>
    public sealed record Read(IReadOnlyList<WorkItemChangeRow> Changes) : HistoryOutcome;

    /// <summary>It did not, and this is why.</summary>
    public sealed record Nothing(string Why) : HistoryOutcome;
}

/// <summary>
/// How asking a reader what one item RECORDS ended.
/// </summary>
/// <remarks>
/// <b>Its own type for <see cref="HistoryOutcome"/>'s reason, and the same
/// shape.</b> An inventory that came back is a list a pane puts in columns; one
/// that did not is a line a person reads. And empty is not absent: a tracker
/// that answered and holds nothing beyond the seven a listing carries is
/// <see cref="Read"/> with no rows, where a reader that does not declare the
/// tool is <see cref="Nothing"/>.
/// </remarks>
public abstract record FieldsOutcome
{
    /// <summary>The reader answered, with however many fields it had.</summary>
    public sealed record Read(IReadOnlyList<Gg.Local.WorkItemField> Fields) : FieldsOutcome;

    /// <summary>It did not, and this is why.</summary>
    public sealed record Nothing(string Why) : FieldsOutcome;
}
