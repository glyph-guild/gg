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

/// <summary>One document as the working copy holds it.</summary>
/// <remarks>
/// The precondition travels with it, because pull wrote it into the file and
/// apply has to state it back — that is the whole of optimistic concurrency
/// here.
/// </remarks>
public sealed record TreeDocument
{
    public required string Name { get; init; }

    /// <summary>One of <see cref="Roles"/>, taken from where the file sits.</summary>
    public required string Role { get; init; }

    /// <summary>Slash-separated, relative to the working copy.</summary>
    public required string Path { get; init; }

    /// <summary>What the file says it was rendered from, or null.</summary>
    public string? BasedOn { get; init; }

    public Envelope? Envelope { get; init; }

    public EnvelopeNarrowing? Narrowing { get; init; }

    public EnvironmentStrategy? Strategy { get; init; }
}

/// <summary>A file that sits where a document goes and does not read as one.</summary>
public sealed record UnreadableDocument
{
    public required string Path { get; init; }

    public required string Diagnosis { get; init; }
}

/// <summary>What the working copy holds.</summary>
public sealed record TreeRead
{
    public required IReadOnlyList<TreeDocument> Documents { get; init; }

    /// <summary>
    /// Files that should be documents and are not.
    /// </summary>
    /// <remarks>
    /// Named rather than skipped. Applying the rest in silence would land a
    /// partial changeset somebody believed was whole, which is the silent no-op
    /// class this product exists to name.
    /// </remarks>
    public required IReadOnlyList<UnreadableDocument> Unreadable { get; init; }

    /// <summary>Whether the tree exists at all.</summary>
    /// <remarks>
    /// Load-bearing: an absent tree is somebody standing in the wrong
    /// directory, and reading it as "retire everything" would make apply a verb
    /// nobody could safely run.
    /// </remarks>
    public required bool Present { get; init; }
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

            if (Rendered(root, document.Role, document.Name, text, document.Version) is { } path)
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
            if (Rendered(
                    root, Roles.Strategy, strategy.Name,
                    EnvelopeText.Render(strategy.Strategy), strategy.Version) is { } path)
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
    private static string? Rendered(
        string root, string role, string name, string text, string version)
    {
        if (AirspaceNames.Invalid(name) is not null)
        {
            return null;
        }

        // THE PRECONDITION IS PULL'S TO WRITE. The renderer works from a model
        // and a model has no version in it - deliberately, because the stored
        // form is the idempotence key. Pull knows which version it rendered, so
        // pull states it, and apply refuses when the stream has moved past it.
        text = $"based-on: {version}\n{text}";

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

    /// <summary>Reads every document the working copy holds.</summary>
    /// <remarks>
    /// <b>Only files this tool would have written.</b> A path is a document when
    /// it sits in a role's directory and reads back as a name; anything else is
    /// somebody's notes and is not this tool's business.
    /// </remarks>
    public static TreeRead Read(string root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var estate = Path.Combine(root, Directory);
        if (!System.IO.Directory.Exists(estate))
        {
            return new TreeRead { Documents = [], Unreadable = [], Present = false };
        }

        var documents = new List<TreeDocument>();
        var unreadable = new List<UnreadableDocument>();

        foreach (var file in System.IO.Directory
            .EnumerateFiles(estate, "*", SearchOption.AllDirectories)
            .OrderBy(f => f, StringComparer.Ordinal))
        {
            var relative = Path.GetRelativePath(root, file).Replace(
                Path.DirectorySeparatorChar, '/');
            var inside = relative[(Directory.Length + 1)..];

            if (AirspaceNames.NameFrom(inside) is not { } document)
            {
                continue;
            }

            var text = File.ReadAllText(file);
            var (parsed, diagnosis) = Parse(document.Role, document.Name, relative, text);

            if (parsed is not null)
            {
                documents.Add(parsed);
            }
            else
            {
                unreadable.Add(new UnreadableDocument { Path = relative, Diagnosis = diagnosis! });
            }
        }

        return new TreeRead { Documents = documents, Unreadable = unreadable, Present = true };
    }

    /// <summary>
    /// Which documents differ from what the estate holds — the ones to submit.
    /// </summary>
    /// <remarks>
    /// <b>Compared by rendering both sides.</b> The text is what a person edited
    /// and the model is what the stream holds, so re-rendering the stream's model
    /// puts them in one form — and an unchanged document is not submitted at all.
    /// The control plane would answer "nothing changed", correctly, at the cost
    /// of a round trip per document per apply; at estate scale that is the
    /// difference between a verb a person runs and one they avoid.
    /// </remarks>
    public static IReadOnlyList<TreeDocument> Changed(TreeRead tree, AirspaceEstate estate)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(estate);

        var held = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var document in estate.Documents)
        {
            if (document.Narrowing is { } narrowing)
            {
                held[document.Name] = EnvelopeText.Render(narrowing);
            }
            else if (document.Envelope is { } envelope)
            {
                held[document.Name] = EnvelopeText.Render(envelope);
            }
        }

        foreach (var strategy in estate.Strategies)
        {
            held[strategy.Name] = EnvelopeText.Render(strategy.Strategy);
        }

        return
        [
            .. tree.Documents.Where(d =>
                !held.TryGetValue(d.Name, out var current)
                || !string.Equals(Render(d), current, StringComparison.Ordinal)),
        ];
    }

    /// <summary>
    /// Which names the tree no longer holds — the intents to retire.
    /// </summary>
    /// <remarks>
    /// <b>An absent tree retires nothing.</b> There is no delete verb, so a
    /// missing file is an intent somebody has to mean — and a person who ran
    /// apply in the wrong directory has not asked to retire their whole
    /// airspace. Reading it that way would make this a verb nobody could safely
    /// run.
    /// </remarks>
    public static IReadOnlyList<string> Retiring(TreeRead tree, AirspaceEstate estate)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(estate);

        if (!tree.Present)
        {
            return [];
        }

        var present = tree.Documents.Select(d => d.Name).ToHashSet(StringComparer.Ordinal);

        return
        [
            .. estate.Documents.Select(d => d.Name)
                .Concat(estate.Strategies.Select(s => s.Name))
                // Root cannot be retired - a tenant with no floor is ungoverned -
                // so its absence from a tree is never an intent, whatever it looks
                // like.
                .Where(name => !string.Equals(name, Roles.Root, StringComparison.Ordinal))
                .Where(name => !present.Contains(name))
                .OrderBy(name => name, StringComparer.Ordinal),
        ];
    }

    /// <summary>The document as text, for comparison against what is held.</summary>
    private static string Render(TreeDocument document) =>
        document.Narrowing is { } narrowing ? EnvelopeText.Render(narrowing)
        : document.Envelope is { } envelope ? EnvelopeText.Render(envelope)
        : document.Strategy is { } strategy ? EnvelopeText.Render(strategy)
        : string.Empty;

    /// <summary>Parses one file by the role its path gave it.</summary>
    private static (TreeDocument? Document, string? Diagnosis) Parse(
        string role, string name, string path, string text)
    {
        if (string.Equals(role, Roles.Narrowing, StringComparison.Ordinal))
        {
            var parsed = EnvelopeYaml.ParseNarrowing(text);
            return parsed.Narrowing is { } narrowing
                ? (Document(name, role, path, parsed.BasedOn) with { Narrowing = narrowing }, null)
                : (null, parsed.Diagnosis ?? "This does not read as a narrowing.");
        }

        if (string.Equals(role, Roles.Strategy, StringComparison.Ordinal))
        {
            var parsed = EnvelopeYaml.ParseStrategy(text);
            return parsed.Strategy is { } strategy
                ? (Document(name, role, path, parsed.BasedOn) with { Strategy = strategy }, null)
                : (null, parsed.Diagnosis ?? "This does not read as a strategy.");
        }

        var read = EnvelopeYaml.Parse(text);
        return read.Envelope is { } document
            ? (Document(name, role, path, read.BasedOn) with { Envelope = document }, null)
            : (null, read.Diagnosis ?? "This does not read as an envelope.");
    }

    private static TreeDocument Document(string name, string role, string path, string? basedOn) =>
        new() { Name = name, Role = role, Path = path, BasedOn = basedOn };
}

/// <summary>One document an apply submitted, and what came of it.</summary>
public sealed record AppliedDocument
{
    public required string Name { get; init; }

    public required string Path { get; init; }

    public required string Version { get; init; }

    public required bool Changed { get; init; }

    /// <summary>The flight a widening rides, when it diverted.</summary>
    public string? Flight { get; init; }

    /// <summary>Who the gate awaits, when it diverted.</summary>
    public string? Awaiting { get; init; }

    /// <summary>The field the widening named, when it diverted.</summary>
    public string? Widens { get; init; }
}

/// <summary>What applying the working copy came to.</summary>
public sealed record EstateApplied
{
    public required IReadOnlyList<AppliedDocument> Applied { get; init; }

    /// <summary>
    /// Names the tree no longer holds.
    /// </summary>
    /// <remarks>
    /// Reported, never acted on here: retiring is a terminal version and needs
    /// its own apply, so an intent shows up as an intent until somebody means
    /// it.
    /// </remarks>
    public required IReadOnlyList<string> Retiring { get; init; }
}

/// <summary>One document's change, in lines and direction.</summary>
public sealed record DocumentChange
{
    public required string Name { get; init; }

    public required string Path { get; init; }

    /// <summary><c>tightening</c> or <c>widening</c> — what decides whether it gates.</summary>
    public required string Direction { get; init; }

    /// <summary>The field that widens, when one does.</summary>
    public string? Field { get; init; }

    /// <summary>Why it could not be shown to tighten.</summary>
    public string? Because { get; init; }
}

/// <summary>What the working copy would change.</summary>
public sealed record EstateDiff
{
    public required IReadOnlyList<DocumentChange> Changes { get; init; }

    public required IReadOnlyList<string> Retiring { get; init; }

    public required IReadOnlyList<string> Unreadable { get; init; }
}
