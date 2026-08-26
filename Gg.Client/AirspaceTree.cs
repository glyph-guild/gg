using Gg.Contracts;

namespace Gg.Client;

/// <summary>The estate as one read: every document a tenant has in force.</summary>
/// <remarks>
/// Two reads rather than one, because strategies have their own door and their
/// own shape. Assembling them here keeps the tree's writer with one input.
/// </remarks>
public sealed record AirspaceEstate
{
    public required IReadOnlyList<NamedEnvelopeState> Documents { get; init; }

    public required IReadOnlyList<EnvironmentStrategyState> Strategies { get; init; }
}

/// <summary>What a pull did to the tree.</summary>
public sealed record TreeWritten
{
    /// <summary>Paths written, relative to the working copy and slash-separated.</summary>
    public required IReadOnlyList<string> Written { get; init; }

    /// <summary>Paths removed because the estate no longer holds them.</summary>
    public required IReadOnlyList<string> Removed { get; init; }

    /// <summary>
    /// Names the estate holds that no path can carry.
    /// </summary>
    /// <remarks>
    /// The read-side answer for an estate declared before the name rule existed.
    /// Every name check fires at declare and a declare-time rule never
    /// re-examines a stored row, so a legacy name can outlive it — and a file
    /// that cannot be written back is worse than a name that is named.
    /// </remarks>
    public required IReadOnlyList<string> Unrepresentable { get; init; }
}

/// <summary>
/// The working copy: the estate rendered as files, and nothing else.
/// </summary>
/// <remarks>
/// <para>
/// <b>The tree is a rendering.</b> ADR-0016's position is that the stream is the
/// record and the repository is convenience, so this writes what the stream holds
/// and removes what it no longer holds — and touches nothing it did not render.
/// A file pull invents is the tree quietly becoming a second source of truth,
/// which is what a path-to-name manifest was refused for.
/// </para>
/// <para>
/// <b>One directory, named in the ADR.</b> Documents live under
/// <c>airspace/</c> so the repository can hold a README, a CI config and
/// whatever else a team keeps, without the tool having an opinion about them.
/// </para>
/// </remarks>
public static class AirspaceTree
{
    /// <summary>The directory the estate renders into, relative to the working copy.</summary>
    public const string Directory = "airspace";

    /// <summary>Renders the estate into the tree and returns what changed.</summary>
    public static TreeWritten Write(string root, AirspaceEstate estate)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(estate);

        var written = new List<string>();
        var unrepresentable = new List<string>();

        foreach (var document in estate.Documents)
        {
            var text = document.Narrowing is { } narrowing
                ? EnvelopeText.Render(narrowing)
                : document.Envelope is { } envelope ? EnvelopeText.Render(envelope) : null;

            if (text is null)
            {
                continue;
            }

            if (Rendered(root, document.Role, document.Name, text) is { } path)
            {
                written.Add(path);
            }
            else
            {
                unrepresentable.Add(document.Name);
            }
        }

        foreach (var strategy in estate.Strategies)
        {
            if (Rendered(root, Roles.Strategy, strategy.Name, EnvelopeText.Render(strategy.Strategy))
                is { } path)
            {
                written.Add(path);
            }
            else
            {
                unrepresentable.Add(strategy.Name);
            }
        }

        return new TreeWritten
        {
            Written = [.. written.OrderBy(p => p, StringComparer.Ordinal)],
            Removed = Prune(root, written),
            Unrepresentable = [.. unrepresentable.OrderBy(n => n, StringComparer.Ordinal)],
        };
    }

    /// <summary>
    /// Which of the estate's files are uncommitted, as paths a person can act on.
    /// </summary>
    /// <remarks>
    /// <b>Scoped to the estate's own directory.</b> Somebody mid-edit on a README
    /// has nothing to do with whether the estate can be re-rendered, and refusing
    /// on it would make the tool's business the whole repository's business.
    /// </remarks>
    public static IReadOnlyList<string> Dirty(string root)
    {
        ArgumentNullException.ThrowIfNull(root);

        return
        [
            .. Git.Status(root, Directory)
                // Porcelain v1: two status columns, a space, then the path.
                .Select(line => line.Length > 3 ? line[3..].Trim('"') : line)
                .Where(path => path.Length > 0)
                .OrderBy(path => path, StringComparer.Ordinal),
        ];
    }

    /// <summary>
    /// Writes one document, or answers null when its name cannot be a path.
    /// </summary>
    private static string? Rendered(string root, string role, string name, string text)
    {
        if (AirspaceNames.Invalid(name) is not null)
        {
            return null;
        }

        var relative = $"{Directory}/{AirspaceNames.PathFor(role, name)}";
        var absolute = Path.Combine(root, Path.Combine(relative.Split('/')));

        System.IO.Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);

        // WRITTEN ONLY WHEN THE BYTES DIFFER, so a second pull does not touch a
        // file's modification time and a watching build does not rebuild the
        // world every time somebody re-reads their own estate.
        if (!File.Exists(absolute)
            || !string.Equals(File.ReadAllText(absolute), text, StringComparison.Ordinal))
        {
            File.WriteAllText(absolute, text);
        }

        return relative;
    }

    /// <summary>
    /// Removes documents the estate no longer holds, and nothing else.
    /// </summary>
    /// <remarks>
    /// <b>Only files this tool would have written.</b> A path is pruned when it
    /// sits in a role's directory, ends in the rendered extension, and reads back
    /// as a name — so a README, a note, or a nested folder somebody keeps there
    /// survives. Deleting those would be the tool claiming the repository, which
    /// is the opposite of the ADR's zero-magic commitment.
    /// </remarks>
    private static IReadOnlyList<string> Prune(string root, IReadOnlyList<string> written)
    {
        var estate = Path.Combine(root, Directory);
        if (!System.IO.Directory.Exists(estate))
        {
            return [];
        }

        var kept = written.ToHashSet(StringComparer.Ordinal);
        var removed = new List<string>();

        foreach (var file in System.IO.Directory.EnumerateFiles(
            estate, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, file).Replace(
                Path.DirectorySeparatorChar, '/');

            if (kept.Contains(relative))
            {
                continue;
            }

            // Is this a path THIS tool renders? NameFrom answers over the
            // estate-relative part, so a file nobody could have rendered - a
            // README, a nested folder, an unknown extension - is left alone.
            var inside = relative[(Directory.Length + 1)..];
            if (AirspaceNames.NameFrom(inside) is null)
            {
                continue;
            }

            File.Delete(file);
            removed.Add(relative);
        }

        return [.. removed.OrderBy(p => p, StringComparer.Ordinal)];
    }
}
