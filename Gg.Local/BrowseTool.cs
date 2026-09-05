namespace Gg.Local;

/// <summary>
/// The tool a reader declares to say it can be browsed, and the shape it answers with.
/// </summary>
/// <remarks>
/// <para>
/// <b>A browser cannot discover a tool surface; it has to know what to call.</b>
/// An intent reader is whatever process an operator installed, and its tools are
/// discovered at runtime by an AGENT, which can read a description and decide.
/// A pane cannot. So a reader that wants to be browsable declares a tool with
/// this exact name, answering this exact shape - and one that does not is
/// reported as declared and not browsable rather than as a tracker with no work
/// in it.
/// </para>
/// <para>
/// <b>Declared here rather than discovered, which is <see cref="NominationTool"/>'s
/// move one noun over.</b> That names the platform's own tool in one place
/// because three things have to agree; this names a tool a THIRD PARTY
/// implements, so the name and the field set are a contract rather than an
/// internal constant. Renaming either is a breaking change to every reader
/// somebody wrote.
/// </para>
/// <para>
/// <b>It names no forge, and could not.</b> The fields are what any tracker has
/// - an id, a title, a state, a link, a time - and nothing here says which
/// tracker answers. That is the same rule <c>IntentConfiguration</c> states as
/// <i>"a command, never a forge"</i>, and it is why this is a shape rather than
/// a client.
/// </para>
/// </remarks>
public static class BrowseTool
{
    /// <summary>
    /// The tool's name, as a reader declares it in <c>tools/list</c>.
    /// </summary>
    /// <remarks>
    /// Snake case and unprefixed, matching how a reader declares
    /// <c>get_work_item</c> today - the prefix is the server key an operator
    /// chose, and belongs to the deployment rather than to this contract.
    /// </remarks>
    public const string Name = "list_work_items";

    /// <summary>
    /// The fields one listed item carries.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Five, and no body.</b> An issue's text is customer content that does
    /// not cross and is not needed to choose one: a person picking work reads a
    /// title and a state. The body is what <c>get_work_item</c> is for, on the
    /// runner, after a flight exists.
    /// </para>
    /// <para>
    /// <c>Url</c> is what makes a listing actionable without this binary
    /// knowing a forge - a person opens it, and a flight can be opened against
    /// it as a uri intent even when the reader's provider key is not one the
    /// control plane has seen.
    /// </para>
    /// </remarks>
    public static class Fields
    {
        /// <summary>The tracker's own identifier, as a flight's ticket intent spells it.</summary>
        public const string Id = "id";

        /// <summary>One line a person recognises the work by.</summary>
        public const string Title = "title";

        /// <summary>Whatever the tracker calls where this item is. Not interpreted.</summary>
        public const string State = "state";

        /// <summary>Where a person would go to read it.</summary>
        public const string Url = "url";

        /// <summary>When it last changed, so a listing can be ordered.</summary>
        public const string Updated = "updated";

        /// <summary>All five, for a reader asserting it answers them.</summary>
        public static IReadOnlyList<string> All { get; } = [Id, Title, State, Url, Updated];
    }

    /// <summary>
    /// What a caller may ask for, and what comes back beside the items.
    /// </summary>
    /// <remarks>
    /// <b>A cursor rather than a page number.</b> A tracker's own paging is
    /// opaque and its ordering is its business, so the caller hands back
    /// whatever it was given and never computes an offset - which is also what
    /// stops a listing renumbering itself while somebody reads it.
    /// </remarks>
    public static class Paging
    {
        /// <summary>Optional. The value a previous answer returned as <see cref="NextCursor"/>.</summary>
        public const string Cursor = "cursor";

        /// <summary>Optional. What the caller can usefully show at once.</summary>
        public const string Limit = "limit";

        /// <summary>Present when there is more; absent when the listing ended.</summary>
        public const string NextCursor = "nextCursor";

        /// <summary>The items, under one key so a reader's answer is one object.</summary>
        public const string Items = "items";
    }

    /// <summary>
    /// Whether a reader that listed these tools can be browsed.
    /// </summary>
    /// <remarks>
    /// <b>Asked once, from <c>tools/list</c>, and answered as a fact rather than
    /// probed.</b> Calling a tool that is not there to find out costs a launch
    /// and returns an error a person would read as "the tracker is empty". The
    /// distinction is <c>DeclaredAndAbsent</c> versus <c>ForgeUnreachable</c>,
    /// one noun over - and today it is the ordinary case, not the edge one: the
    /// only reader anybody has configured declares <c>get_work_item</c> and
    /// nothing else.
    /// </remarks>
    public static bool IsBrowsable(IReadOnlyList<string>? declaredTools) =>
        declaredTools is not null
        && declaredTools.Contains(Name, StringComparer.Ordinal);

    /// <summary>
    /// What to tell a person whose reader cannot be browsed.
    /// </summary>
    /// <remarks>
    /// It names the tool, because the person reading it is usually the operator
    /// who installed the reader, and "not browsable" without the missing name
    /// is a sentence they cannot act on.
    /// </remarks>
    public static string NotBrowsable(string providerKey) =>
        $"The reader for '{providerKey}' does not declare '{Name}', so its work cannot be "
      + "listed here. It can still read one item, which is what opening a flight against a "
      + "ticket needs. Browsing wants a reader that declares that tool.";
}
