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
