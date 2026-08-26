namespace Gg.Client;

/// <summary>
/// The working copy has edits nobody committed, so pull will not overwrite it.
/// </summary>
/// <remarks>
/// <b>A named refusal rather than a status code</b>, because the useful content
/// is the LIST: "the tree is dirty" sends a person to run <c>git status</c>
/// themselves, which is the tool making its problem theirs.
/// </remarks>
public sealed class DirtyWorkingCopyException(IReadOnlyList<string> paths)
    : Exception(Describe(paths))
{
    /// <summary>The estate files with uncommitted changes.</summary>
    public IReadOnlyList<string> Paths { get; } = paths;

    private static string Describe(IReadOnlyList<string> paths) =>
        $"{paths.Count} file(s) in the working copy have uncommitted changes, and pull "
      + "renders over what is there. Commit or discard them, then pull again:\n"
      + string.Join('\n', paths.Select(p => $"  {p}"));
}
