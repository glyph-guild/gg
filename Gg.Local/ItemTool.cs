namespace Gg.Local;

/// <summary>
/// The tool a reader declares to answer about ONE work item.
/// </summary>
/// <remarks>
/// <para>
/// <b>Beside <see cref="BrowseTool"/>, because it is the same kind of thing.</b>
/// This names a tool a THIRD PARTY implements, so the name and the argument are
/// a contract rather than an internal constant — renaming either is a breaking
/// change to every reader somebody wrote.
/// </para>
/// <para>
/// <b>The name was declared in one place and needed in three.</b> The tool
/// server spelled it, a reader declares it in <c>tools/list</c>, and now the
/// console asks for it by name; three spellings of one name is how one of them
/// stops agreeing, and the failure would be silent — a console that asked for a
/// tool nobody serves gets the same answer as one asking a reader that has none.
/// <c>NominationTool</c> makes exactly this argument for exactly this reason.
/// </para>
/// <para>
/// <b>It names no forge.</b> An id and a body is what any tracker has.
/// </para>
/// </remarks>
public static class ItemTool
{
    /// <summary>
    /// The tool, as a reader declares it.
    /// </summary>
    /// <remarks>
    /// Snake case and unprefixed, matching <see cref="BrowseTool.Name"/>: the
    /// prefix is the server key an operator chose and belongs to the deployment
    /// rather than to this contract.
    /// </remarks>
    public const string Name = "get_work_item";

    /// <summary>
    /// The tool that answers what has HAPPENED to one item.
    /// </summary>
    /// <remarks>
    /// <b>A second verb rather than more of the first.</b> What
    /// <see cref="Name"/> answers is described to an agent as an item's type,
    /// state, title, description and acceptance criteria, and widening that
    /// rendering would change what every agent already reading it sees. A
    /// history is a different question, so it is a different tool - and a reader
    /// that does not declare it still answers the first one.
    /// </remarks>
    public const string HistoryName = "get_work_item_history";

    /// <summary>
    /// The tool that answers what one item RECORDS - every field, not the seven
    /// a listing is built from.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A third verb, for <see cref="HistoryName"/>'s reason.</b> What
    /// <see cref="Name"/> answers is one rendering described to an agent, and
    /// widening it would change what every agent already reading it sees.
    /// </para>
    /// <para>
    /// <b>And per item, because a listing cannot afford it.</b>
    /// <c>BrowseTool.Fields</c> carries seven because a page of fifty rows
    /// costs fifty rows' worth of everything otherwise - and "everything"
    /// includes the body, which that contract says does not cross in a listing.
    /// One item a person has opened is the other case: its body has already
    /// crossed, so its fields give nothing away.
    /// </para>
    /// </remarks>
    public const string FieldsName = "get_work_item_fields";

    /// <summary>
    /// What one fields answer carries.
    /// </summary>
    /// <remarks>
    /// <b>An object of name to value, in the tracker's own order.</b> Not an
    /// array of pairs: a tracker's fields ARE a mapping, and the names are its
    /// own - which is the point, because they are what a person searches its UI
    /// for. Values may be any JSON, for <c>BrowseTool.Fields.Extra</c>'s
    /// reason: a story point is a number and an assignee is an object, and a
    /// contract demanding strings would ask a tracker to lie about what it
    /// holds.
    /// </remarks>
    public static class Inventory
    {
        /// <summary>The fields, under one key so the answer is one object.</summary>
        public const string Fields = "fields";
    }

    /// <summary>
    /// What one history answer carries.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Rows, because a history IS a table.</b> It crossed as one rendered
    /// block - three fields joined with two spaces - which flattened the shape
    /// at the last point anybody could still see it had one, and left a pane
    /// with a paragraph where it wanted columns. <see cref="BrowseTool"/>
    /// answers JSON for exactly this reason: a pane parses it.
    /// </para>
    /// <para>
    /// <b>The time is a string, as the tracker spells it.</b>
    /// <c>BrowseTool.Fields.Updated</c> already makes this choice: a reader
    /// re-formatting somebody else's timestamp is a reader having an opinion
    /// about a record it does not own, and a console parsing one back is two
    /// chances to disagree.
    /// </para>
    /// </remarks>
    public static class History
    {
        /// <summary>The changes, under one key so the answer is one object.</summary>
        public const string Changes = "changes";

        /// <summary>When it happened, as the tracker spells it.</summary>
        public const string When = "when";

        /// <summary>Who did it, as the tracker names them.</summary>
        public const string Who = "who";

        /// <summary>What changed, in the tracker's own words.</summary>
        public const string What = "what";

        /// <summary>All three fields of one row.</summary>
        public static IReadOnlyList<string> Fields { get; } = [When, Who, What];
    }

    /// <summary>What the caller names the item by.</summary>
    /// <remarks>
    /// <b>The tracker's own identifier, exactly as a listing spelled it.</b> A
    /// detail fetched for the wrong row is worse than no detail, and the row is
    /// the only thing the person chose.
    /// </remarks>
    public const string Id = BrowseTool.Fields.Id;

    /// <summary>
    /// Whether a reader that listed these tools can be asked about one item.
    /// </summary>
    /// <remarks>
    /// Asked once, from <c>tools/list</c>, and answered as a fact rather than
    /// probed — <see cref="BrowseTool.IsBrowsable"/>'s reason: calling a tool
    /// that is not there costs a launch and returns an error a person would
    /// read as something about the item.
    /// </remarks>
    public static bool IsReadable(IReadOnlyList<string>? declaredTools) =>
        declaredTools is not null
        && declaredTools.Contains(Name, StringComparer.Ordinal);

    /// <summary>Whether a reader that listed these tools can answer a history.</summary>
    public static bool HasHistory(IReadOnlyList<string>? declaredTools) =>
        declaredTools is not null
        && declaredTools.Contains(HistoryName, StringComparer.Ordinal);

    /// <summary>What to tell a person whose reader cannot answer a history.</summary>
    public static string NoHistory(string providerKey) =>
        $"The reader for '{providerKey}' does not declare '{HistoryName}', so what has "
      + "happened to this item can only be read at the tracker.";

    /// <summary>Whether a reader that listed these tools can answer an inventory.</summary>
    public static bool HasFields(IReadOnlyList<string>? declaredTools) =>
        declaredTools is not null
        && declaredTools.Contains(FieldsName, StringComparer.Ordinal);

    /// <summary>What to tell a person whose reader cannot answer an inventory.</summary>
    /// <remarks>
    /// It names the tool for <see cref="NoHistory"/>'s reason, and it says what
    /// is still there: the seven a listing carries are on the tab regardless,
    /// so this is a reader that answers less rather than a tab that is broken.
    /// </remarks>
    public static string NoFields(string providerKey) =>
        $"The reader for '{providerKey}' does not declare '{FieldsName}', so what else this "
      + "item records can only be read at the tracker. The fields a listing is built from are "
      + "above.";

    /// <summary>What to tell a person whose reader cannot answer about one item.</summary>
    /// <remarks>
    /// It names the tool, because the person reading it is usually the operator
    /// who installed the reader.
    /// </remarks>
    public static string NotReadable(string providerKey) =>
        $"The reader for '{providerKey}' does not declare '{Name}', so it can list work "
      + "without being able to say what any of it is about. Reading one item wants a reader "
      + "that declares that tool.";
}
