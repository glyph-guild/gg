namespace Gg.Console;

/// <summary>
/// One work item as a list shows it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The url is held, and it was not.</b> "No url, and that is deliberate" was
/// right on its own terms: a flight is opened from a provider and an id, never
/// parsed out of a url - the rule <c>FlightIntent.Id</c> states - so the url was
/// one more customer string in the state dump <i>for no reader of the screen</i>.
/// That last clause was the whole condition, and it has changed: there is a
/// reader now, which is the key that opens the item where it lives.
/// </para>
/// <para>
/// <b>It still never becomes an intent.</b> What opens a flight is unchanged -
/// a provider and an id, declared - and this is only ever handed to a browser.
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

    /// <summary>Where a person would go to read it, or null where the reader gave none.</summary>
    public string? Url { get; init; }
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
/// What a tracker offers to narrow a listing by, or why it offered nothing.
/// </summary>
/// <remarks>
/// <para>
/// <b>Empty and unavailable are different, and both are drawn.</b> A project
/// with no iterations is a project where nobody should be offered a sprint;
/// a reader that does not declare the tool is a reader to go and look at.
/// <see cref="Why"/> being set is the second one, and the modal says it rather
/// than drawing three empty groups.
/// </para>
/// <para>
/// <b>Plain lists of plain strings, because they go straight back out.</b>
/// These are the values the filter arguments take, offered exactly as they will
/// be sent - a console that reshaped what it was handed would send something
/// the reader that offered it does not recognise.
/// </para>
/// </remarks>
public sealed record BrowseFacets
{
    public IReadOnlyList<string> AreaPaths { get; init; } = [];

    public IReadOnlyList<string> Iterations { get; init; } = [];

    public IReadOnlyList<string> States { get; init; } = [];

    /// <summary>Why there is nothing to choose from, already worded, or null.</summary>
    public string? Why { get; init; }
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
