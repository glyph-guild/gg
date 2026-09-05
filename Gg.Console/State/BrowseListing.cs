namespace Gg.Console;

/// <summary>
/// One work item as a list shows it.
/// </summary>
/// <remarks>
/// <para>
/// <b>No url, and that is deliberate.</b> The browse contract carries one, but
/// a flight is opened from a provider and an id - never parsed out of a url,
/// which is the rule <c>FlightIntent.Id</c> already states - so holding it here
/// would put one more customer string in the state dump for no reader of the
/// screen.
/// </para>
/// <para>
/// <b>The title IS held, and it is customer content.</b> Choosing work without
/// titles is choosing by number. What keeps that safe is where
/// <c>ConsoleData.BundleFrom</c> already puts it: the bundle takes the whole
/// state and reads almost none of it.
/// </para>
/// </remarks>
public sealed record BrowseRow
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string State { get; init; }

    /// <summary>When the tracker last saw it change, as the tracker spells it.</summary>
    public string? Updated { get; init; }
}

/// <summary>
/// What one tracker answered, or why it did not.
/// </summary>
/// <remarks>
/// <para>
/// <b>Items and <see cref="Absence"/> are the two answers, and empty is not the
/// same as absent.</b> A tracker with no work in it returns no items and no
/// absence: it answered, and the answer was nothing. A reader that could not be
/// asked returns an absence in the reader's own words.
/// </para>
/// <para>
/// <b>Flattened to a sentence on purpose.</b> <see cref="BrowseOutcome"/> is a
/// record hierarchy, and a hierarchy in <c>AppState</c> is a polymorphic
/// serialisation problem in a source-generated, AOT-published context. By the
/// time it reaches state the decision is made and what is left is what to draw.
/// </para>
/// </remarks>
public sealed record BrowseListing
{
    /// <summary>Which tracker this is. On the screen, never implied.</summary>
    public required string ProviderKey { get; init; }

    public IReadOnlyList<BrowseRow> Items { get; init; } = [];

    /// <summary>What to ask for next, or null when this is the whole list.</summary>
    public string? NextCursor { get; init; }

    /// <summary>Why there are no items, already worded, or null.</summary>
    public string? Absence { get; init; }
}

/// <summary>
/// A flight somebody asked for, waiting on an answer about a duplicate.
/// </summary>
/// <remarks>
/// <b>The provider and id are carried, not re-derived.</b> The answer comes on
/// a later keystroke, and by then the list may have scrolled or been read
/// again - resolving the selection twice would open a flight for whatever is
/// under the cursor now rather than what the question was about.
/// </remarks>
public sealed record PendingFlight
{
    public required string Provider { get; init; }

    public required string Id { get; init; }

    /// <summary>Why this is being asked, in the words the check produced.</summary>
    public required string Why { get; init; }
}
