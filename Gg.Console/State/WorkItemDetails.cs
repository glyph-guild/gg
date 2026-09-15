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

    /// <summary>
    /// The heading over the history region.
    /// </summary>
    /// <remarks>
    /// <b>The word the flight modal already uses for the same thing.</b> Both
    /// are a list of what happened in the order it happened, and a console that
    /// called one a log and the other "what has happened to it" asked a person
    /// to learn that those are one idea. Short enough to be a tab, too.
    /// </remarks>
    public const string HistoryTitle = "Log";

    /// <summary>
    /// The item this modal is about, or null where the cursor is on nothing.
    /// </summary>
    public static BrowseRow? Item(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        // WHAT THE MODAL WAS OPENED ABOUT, else the row under the cursor. The
        // first is set only by a flight naming its ticket, which is an item
        // that is usually nowhere on the page somebody browsed.
        if (state.WorkItemRow is { } held)
        {
            return held;
        }

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
        Item(state) is not { } item
            ? "The work item"

            // THE DASH NEEDS SOMETHING ON BOTH SIDES OF IT. An item opened from
            // a flight was never listed, so this console has its id and no
            // title - and `18490 — ` reads as a title the tracker lost rather
            // than as one nobody here ever had.
            : ControlText.Strip(item.Title) is { Length: > 0 } named
                ? $"{ControlText.Strip(item.Id)} — {named}"
                : ControlText.Strip(item.Id);

    /// <summary>What the reader said about it, as prose.</summary>
    public static string Said(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        // WAITING IS NOT EMPTY. The modal opens on the keypress now and the
        // reader answers into it, so the gap between the two has to say what
        // it is - an empty pane reads as an item with nothing written on it.
        if (state.WorkItemSaid is not { Length: > 0 })
        {
            return "Reading what this item says. The console stays up while the tracker "
                 + "answers.";
        }

        return state.WorkItemSaid;
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

    /// <summary>The heading over the fields tab.</summary>
    public const string FieldsTitle = "What the tracker records";

    /// <summary>The columns the fields table declares.</summary>
    public static IReadOnlyList<string> FieldColumns { get; } = ["field", "value"];

    /// <summary>
    /// Everything the tracker records about this item: the named ones first,
    /// then whatever else it sent.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The seven first, in the contract's order.</b> They are the ones this
    /// console can promise and the ones a person came for, and putting them
    /// where <c>BrowseTool.Fields</c> lists them means a reader of both sees
    /// one ordering rather than two.
    /// </para>
    /// <para>
    /// <b>Then everything else, in the tracker's order, under the tracker's own
    /// names.</b> Not alphabetised: a tracker groups related fields and sorting
    /// scatters them, so what arrives is what somebody looking at the same item
    /// in the tracker's own UI sees. Not renamed either - a person who wants to
    /// know what <c>Microsoft.VSTS.Scheduling.StoryPoints</c> is will search
    /// for that, and a friendlier label would be a word only this console uses.
    /// </para>
    /// <para>
    /// <b>And nothing is dropped for looking like noise.</b> The owner chose
    /// that explicitly: a field this console decided not to show is one nobody
    /// can discover is there.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<Gg.Local.WorkItemField> AllFields(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (Item(state) is not { } item)
        {
            return [];
        }

        // UNLESS IT NEVER CAME FROM A LISTING. The seven are a listing's
        // columns; an item opened from a flight was never listed, so emitting
        // them would put `title` on screen as a blank row directly above the
        // tracker's own System.Title with the title in it - an absence this
        // console invented, reading as one it lost.
        if (state.WorkItemRow is not null)
        {
            return [.. state.WorkItemFields.Select(f => new Gg.Local.WorkItemField(
                ControlText.Strip(f.Name), ControlText.Strip(f.Value)))];
        }

        // THE NAMED SEVEN ARE ALWAYS ROWS, even the ones the tracker said
        // nothing for. Fields() above leaves an absent value out, because a
        // caption over nothing reads as a tracker that answered emptily - but
        // this table is the inventory, and a row missing from an inventory
        // reads as a field that does not exist.
        List<Gg.Local.WorkItemField> rows =
        [
            new(Gg.Local.BrowseTool.Fields.Id, ControlText.Strip(item.Id)),
            new(Gg.Local.BrowseTool.Fields.Title, ControlText.Strip(item.Title)),
            new(Gg.Local.BrowseTool.Fields.State, ControlText.Strip(item.State)),
            new(Gg.Local.BrowseTool.Fields.Url, ControlText.Strip(item.Url ?? "")),
            new(Gg.Local.BrowseTool.Fields.Updated, ControlText.Strip(item.Updated ?? "")),
            new(Gg.Local.BrowseTool.Fields.AreaPath, ControlText.Strip(item.Where ?? "")),
            new(Gg.Local.BrowseTool.Fields.Iteration, ControlText.Strip(item.Sprint ?? "")),
        ];

        // WHAT WAS ASKED ABOUT THIS ITEM, else whatever the row happened to
        // carry. Two conformant shapes: a reader answers `get_work_item_fields`
        // for the item somebody opened, or - finding them cheap - hangs them on
        // a listed item. The per-item answer wins where there is one, because
        // it is the one asked about THIS item and the row's may be a page old.
        var inventory = state.WorkItemFields.Count > 0 ? state.WorkItemFields : item.Fields;

        // STRIPPED AT THE BOUNDARY LIKE EVERY OTHER EXTERNAL STRING. These are
        // a tracker's words arriving through a child process, and the console
        // rule is that control sequences come out at ingress rather than at
        // render - but a name is also a key, so both halves are cleaned.
        rows.AddRange(inventory.Select(f => new Gg.Local.WorkItemField(
            ControlText.Strip(f.Name), ControlText.Strip(f.Value))));

        return rows;
    }

    /// <summary>Why the fields table has nothing but the seven, or nothing.</summary>
    /// <remarks>
    /// <b>Three absences, as everywhere else in this modal.</b> A reader that
    /// does not declare the verb, a tracker that answered and records nothing
    /// more, and an inventory with rows in it are three different facts - and
    /// an empty table under the seven claims the second when it may be the
    /// first. The reader's own words are preferred where there are any, because
    /// it already said what was wrong and naming the missing tool is the thing
    /// an operator can act on.
    /// </remarks>
    public static string FieldsAbsence(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.WorkItemFields.Count > 0 || Item(state) is not { Fields.Count: 0 })
        {
            return "";
        }

        return state.WorkItemFieldsSaid is { Length: > 0 } why
            ? ControlText.Strip(why)
            : "This tracker records nothing about this item beyond the fields a listing is "
            + "built from.";
    }

    /// <summary>The history, as rows.</summary>
    public static IReadOnlyList<WorkItemChangeRow> Changes(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return state.WorkItemChanges;
    }

    /// <summary>
    /// What the change under the cursor says, for the pane beneath the table.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The log's problem, one modal over.</b> <c>WorkItemChangeRow.What</c>
    /// is a sentence a tracker wrote — a state transition with the reason
    /// somebody typed after it — and a cell shows as much of it as the column
    /// happens to be wide. The table stays scannable and the prose goes where
    /// prose fits.
    /// </para>
    /// <para>
    /// <b>An absence answers with the absence.</b> The three cases this modal
    /// already distinguishes do not collapse here: a reader that could not be
    /// asked says so in its own words, and a tracker with nothing to report
    /// says that. A blank pane would claim the second when it may be the
    /// first.
    /// </para>
    /// </remarks>
    public static string ChangeDetail(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var changes = Changes(state);

        if (changes.Count == 0)
        {
            return HistoryAbsence(state);
        }

        // CLAMPED, BECAUSE THE CURSOR OUTLIVES THE LIST. A history read again
        // is a different length, and a cursor past the end would render
        // nothing - which reads as a pane that broke rather than a list that
        // got shorter.
        return changes[Math.Clamp(state.WorkItemSelected, 0, changes.Count - 1)].What;
    }

    /// <summary>
    /// What the change under the cursor says, broken to the width it is shown at.
    /// </summary>
    /// <remarks>
    /// The log's pane one modal over, and the same reasoning: a Label clips and
    /// this text is prose because a cell could not hold it. See
    /// <see cref="FlightDetails.LogDetailLines"/>.
    /// </remarks>
    public static IReadOnlyList<string> ChangeDetailLines(AppState state, int width) =>
        FlightDetails.Lines(ChangeDetail(state), width);

    /// <summary>The heading over the pane that holds what a cell cannot.</summary>
    public const string ChangeDetailTitle = "What the change says";

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
