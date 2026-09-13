using Gg.Contracts;

namespace Gg.Console;

/// <summary>
/// What the work item modal shows, in the shapes it shows them in.
/// </summary>
/// <remarks>
/// <para>
/// <b><see cref="FlightDetails"/>' rule, applied to the other thing this
/// console opens a modal about.</b> That file says it: split by what the
/// content IS rather than by where it lands — an identity is a heading, prose
/// somebody wrote is a document, the scalars are fields and the history is a
/// table. This modal had all four and drew them as one label with a rule of
/// dashes in the middle.
/// </para>
/// <para>
/// <b>The scalars come from the ROW, not from the prose.</b> The listing
/// carried every one of them, and pulling them back out of a rendering would be
/// parsing something nobody promised — the argument the browser key already
/// makes about the url. It also means a reader that could not be asked still
/// leaves fields worth showing: what a person picked is a thing they chose.
/// </para>
/// <para>
/// <b>Pure, so the modal can be asked what it holds without a terminal.</b>
/// </para>
/// </remarks>
public static class WorkItemDetails
{
    /// <summary>The heading over the prose region.</summary>
    public const string SaidTitle = "What it says";

    /// <summary>The heading over the history region.</summary>
    public const string HistoryTitle = "What has happened to it";

    /// <summary>
    /// The item this modal is about, or null where the cursor is on nothing.
    /// </summary>
    public static BrowseRow? Item(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return state.Browse is { Items.Count: > 0 } listing
            && state.BrowseSelected >= 0
            && state.BrowseSelected < listing.Items.Count
                ? listing.Items[state.BrowseSelected]
                : null;
    }

    /// <summary>
    /// The title bar: which item this is.
    /// </summary>
    /// <remarks>
    /// <b>The subject, like the flight modal's.</b> Two items with similar
    /// names behind a dialog are told apart by nothing, and the id is what a
    /// person carries to the tracker.
    /// </remarks>
    public static string Title(AppState state) =>
        Item(state) is { } item
            ? $"{ControlText.Strip(item.Id)} — {ControlText.Strip(item.Title)}"
            : "The work item";

    /// <summary>What the reader said about it, as prose.</summary>
    public static string Said(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return state.WorkItemSaid ?? "";
    }

    /// <summary>
    /// The scalars, each in a field a cursor can enter.
    /// </summary>
    /// <remarks>
    /// <b>The link is one of them.</b> The row carried a url all along and this
    /// modal offered to open it without ever showing it — a value a person
    /// cannot read is one they cannot send to anybody else.
    /// </remarks>
    public static IReadOnlyList<FlightField> Fields(AppState state)
    {
        if (Item(state) is not { } item)
        {
            return [];
        }

        List<FlightField> fields = [new("id", ControlText.Strip(item.Id))];

        Add(fields, "state", item.State);
        Add(fields, "where", item.Where);
        Add(fields, "sprint", item.Sprint);
        Add(fields, "updated", item.Updated);
        Add(fields, "link", item.Url);

        return fields;
    }

    /// <summary>A field, unless the tracker said nothing for it.</summary>
    /// <remarks>
    /// <b>An absent value is left out rather than shown blank.</b> A caption
    /// over nothing reads as a tracker that answered emptily, where the honest
    /// answer is that this tracker does not keep that.
    /// </remarks>
    private static void Add(List<FlightField> fields, string label, string? value)
    {
        if (value is { Length: > 0 } said)
        {
            fields.Add(new FlightField(label, ControlText.Strip(said)));
        }
    }

    /// <summary>The history, as rows.</summary>
    public static IReadOnlyList<WorkItemChangeRow> Changes(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return state.WorkItemChanges;
    }

    /// <summary>
    /// Why the history table is empty, or empty when it is not.
    /// </summary>
    /// <remarks>
    /// <b>Three answers, not two.</b> A reader that cannot be asked says so in
    /// its own words; a tracker that answered and had nothing to report says
    /// that; and a history with rows in it says nothing at all, because the
    /// table speaks. An empty table with no sentence claims the second when it
    /// may be the first.
    /// </remarks>
    public static string HistoryAbsence(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return state.WorkItemChanges.Count > 0
            ? ""
            : state.WorkItemHistorySaid is { Length: > 0 } why
                ? ControlText.Strip(why)
                : "Nothing has happened to this item yet.";
    }
}
