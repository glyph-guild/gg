namespace Gg.Console;

/// <summary>
/// One row of the airspace tree: a folder, or a document and what is known
/// about it.
/// </summary>
/// <remarks>
/// <b>A record per row, for <c>Rows.cs</c>'s reason</b> — the values stay
/// checkable without a terminal, and the alignment is left to something that
/// can measure the screen.
/// </remarks>
/// <param name="Document">The name, indented to its depth. A folder ends in a slash.</param>
/// <param name="Basis">The version this was rendered from, or empty.</param>
/// <param name="State">What is known about it, or empty when nothing is.</param>
public sealed record AirspaceRow(string Document, string Basis, string State);

/// <summary>
/// The working copy as a tree, joined with everything known about it.
/// </summary>
/// <remarks>
/// <para>
/// <b>PURE, AND THE ORDER IS THE ORDER A CURSOR INDEXES.</b>
/// <c>Rows.cs</c>'s rule: row two means the second row ON THE SCREEN, so the
/// order lives here rather than in the view.
/// </para>
/// <para>
/// <b>TWO TIERS ON ONE COLUMN, and it must never dress one as the other.</b>
/// <c>uncommitted</c> is git's answer and needs no session: it says a file
/// moved since the pull that wrote it. A DIRECTION is the door's answer and
/// needs one: it says which way. The console computes neither and invents
/// neither — a pane that decided for itself which way a document moved would
/// be the second opinion ADR-0016 § 6 refused a permission model for.
/// </para>
/// <para>
/// <b>Folders are synthesised from the paths, not walked.</b>
/// <c>AirspaceTree.Read</c> already orders documents by full path ordinally,
/// which groups them; the directory rows are the distinct prefixes of those
/// paths. A <c>TreeView</c> would be new machinery with its own key-eating
/// risk — the reason <c>QuietTable</c> exists — for the same picture a flat
/// list indented by depth draws.
/// </para>
/// <para>
/// <b>Empty rather than a header over nothing.</b> A tree that is absent, one
/// that is empty, and one nobody has read are three different facts and each
/// keeps its own sentence in the pane; none of them is a table with no rows in
/// it.
/// </para>
/// </remarks>
public static class AirspaceRows
{
    /// <summary>Two spaces per level, which is what the documents use.</summary>
    private const string Step = "  ";

    public static IReadOnlyList<string> AirspaceColumns { get; } =
        ["document", "based on", "state"];

    /// <summary>The tree, folders and files, in the order it reads.</summary>
    public static IReadOnlyList<AirspaceRow> Tree(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.Estate?.Tree is not { Present: true } tree)
        {
            return [];
        }

        // EVERY PATH THE PANE HAS A ROW FOR, documents and unparseable files
        // alike. An unreadable file has no name and no version, and it is the
        // one row somebody has to act on before anything else can happen - so
        // leaving it out of the tree would be leaving it out of the place they
        // are looking.
        var paths = tree.Documents
            .Select(d => d.Path)
            .Concat(tree.Unreadable.Select(u => u.Path))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        if (paths.Count == 0)
        {
            return [];
        }

        var documents = tree.Documents.ToDictionary(d => d.Path, StringComparer.Ordinal);
        var unreadable = tree.Unreadable.ToDictionary(u => u.Path, StringComparer.Ordinal);
        var changed = state.Estate.Working?.Changes
            .ToDictionary(c => c.Path, StringComparer.Ordinal);

        var uncommitted = state.Estate.Uncommitted.ToHashSet(StringComparer.Ordinal);

        var rows = new List<AirspaceRow>();
        var shown = new HashSet<string>(StringComparer.Ordinal);

        foreach (var path in paths)
        {
            var parts = path.Split('/');

            // THE FOLDERS THIS PATH IS UNDER, each once and each before what
            // is in it. Ordering the paths ordinally is what makes "before"
            // fall out rather than needing a sort of its own.
            for (var depth = 0; depth < parts.Length - 1; depth++)
            {
                var folder = string.Join('/', parts[..(depth + 1)]);

                if (shown.Add(folder))
                {
                    rows.Add(new AirspaceRow(
                        string.Concat(Enumerable.Repeat(Step, depth)) + parts[depth] + "/",
                        "",
                        ""));
                }
            }

            var indent = string.Concat(Enumerable.Repeat(Step, parts.Length - 1));
            var leaf = parts[^1];

            if (unreadable.TryGetValue(path, out var broken))
            {
                // NAMED, AND SAYING WHAT IT COSTS. Apply refuses the whole
                // changeset over one of these rather than landing the rest,
                // because the rest is part of something somebody meant as a
                // whole - so the row says that and not just "unreadable".
                rows.Add(new AirspaceRow(
                    indent + leaf,
                    "",
                    "unreadable - stops every apply: " + Clean(broken.Diagnosis)));

                continue;
            }

            var document = documents[path];

            rows.Add(new AirspaceRow(
                indent + leaf,
                Clean(document.BasedOn ?? ""),
                Said(document, changed, uncommitted.Contains(path))));
        }

        return rows;
    }

    /// <summary>What is known about one document, weakest claim last.</summary>
    /// <remarks>
    /// <para>
    /// <b>NEVER APPLIED IS FIRST because it is the strongest.</b> A document
    /// with no <c>based-on</c> line has never been in the stream, so whether
    /// it differs from what is applied is not a question: everything about it
    /// is new.
    /// </para>
    /// <para>
    /// <b>Then the direction, when the door has answered.</b> It comes from
    /// the diff and nowhere else, which is the one place it is computed — from
    /// the comparator the door itself runs.
    /// </para>
    /// <para>
    /// <b>And git last, as its own weaker claim.</b> "Uncommitted" is not a
    /// direction and is never rendered as one: it says the file moved since
    /// the pull, which is worth knowing and is all that can be known with no
    /// session. Both can be true at once and both are said, because they
    /// answer different questions.
    /// </para>
    /// </remarks>
    private static string Said(
        AirspaceFile document,
        IReadOnlyDictionary<string, Gg.Client.DocumentChange>? changed,
        bool uncommitted)
    {
        var said = new List<string>();

        if (document.BasedOn is not { Length: > 0 })
        {
            said.Add("never applied");
        }

        if (changed?.TryGetValue(document.Path, out var change) is true)
        {
            said.Add(change.Field is { Length: > 0 } field
                ? $"{Clean(change.Direction)} ({Clean(field)})"
                : Clean(change.Direction));
        }

        if (uncommitted)
        {
            said.Add("uncommitted");
        }

        return string.Join(" · ", said);
    }

    private static string Clean(string value) => Gg.Contracts.ControlText.Strip(value);
}
