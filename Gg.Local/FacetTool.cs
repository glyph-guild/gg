namespace Gg.Local;

/// <summary>
/// The tool a reader declares to say what there is to filter by.
/// </summary>
/// <remarks>
/// <para>
/// <b>A filter a person has to spell is a filter they get wrong once and
/// abandon.</b> An area path is a backslash-separated tree carrying a team's
/// own punctuation; a sprint is whatever somebody named a fortnight. Typed
/// wrong, both answer an empty listing that is indistinguishable from a sprint
/// with no work in it - which is the confusion <see cref="BrowseTool"/>'s
/// endings exist to prevent, arriving through a different door.
/// </para>
/// <para>
/// <b>A separate tool rather than an argument on <see cref="BrowseTool"/>.</b>
/// A reader that can list work and cannot enumerate a tree is a reader that
/// still browses perfectly well; folding the two together would make one
/// capability refuse for the other's sake, and every reader already deployed
/// would lose browsing to gain nothing.
/// </para>
/// <para>
/// <b>The values are what a query takes, not what a tree prints.</b> Whatever
/// shape a tracker keeps its nodes in, what comes back here is what can be
/// handed straight to <see cref="BrowseTool.Filters"/> - because a caller that
/// has to reshape a value it was offered is a caller that will reshape it
/// differently from the reader that offered it.
/// </para>
/// <para>
/// <b>Declared here because a THIRD PARTY implements it</b>, on
/// <see cref="BrowseTool"/>'s own terms: the name and the field set are a
/// contract, and renaming either breaks every reader somebody wrote.
/// </para>
/// </remarks>
public static class FacetTool
{
    /// <summary>The tool's name, as a reader declares it in <c>tools/list</c>.</summary>
    public const string Name = "list_work_item_facets";

    /// <summary>The three lists an answer carries.</summary>
    /// <remarks>
    /// <b>Plural, and each one keyed to what it narrows.</b> These are the
    /// values <see cref="BrowseTool.Filters"/> takes - offered here, chosen
    /// there - so a reader implementing both spells one thing once.
    /// </remarks>
    public static class Fields
    {
        /// <summary>Every area path a caller may narrow to, deepest included.</summary>
        public const string AreaPaths = "areaPaths";

        /// <summary>Every iteration a caller may narrow to.</summary>
        public const string Iterations = "iterations";

        /// <summary>Every state a caller may ask for, across whatever types there are.</summary>
        public const string States = "states";

        /// <summary>All three, for a reader asserting it answers them.</summary>
        public static IReadOnlyList<string> All { get; } = [AreaPaths, Iterations, States];
    }

    /// <summary>
    /// Whether a reader that listed these tools can say what there is to filter by.
    /// </summary>
    /// <remarks>
    /// Asked from <c>tools/list</c> and answered as a fact rather than probed,
    /// for <see cref="BrowseTool.IsBrowsable"/>'s reason: calling a tool that is
    /// not there costs a round trip whose only possible answer is an error.
    /// </remarks>
    public static bool IsOffered(IReadOnlyList<string>? declaredTools) =>
        declaredTools is not null
        && declaredTools.Contains(Name, StringComparer.Ordinal);

    /// <summary>
    /// What to tell a person whose reader cannot offer the choices.
    /// </summary>
    /// <remarks>
    /// It names the tool, because the person reading it is usually the operator
    /// who installed the reader. It also says what still works: browsing
    /// unfiltered is what browsing has always been, and a missing tool reported
    /// without that reads as a reader that is broken rather than one narrower
    /// than this pane wants.
    /// </remarks>
    public static string NotOffered(string providerKey) =>
        $"The reader for '{providerKey}' does not declare '{Name}', so there is nothing here to "
      + "pick a filter from. Its work can still be listed without a filter.";
}
