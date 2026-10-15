namespace Gg.Contracts;

/// <summary>
/// A work kind that names another as its parent and says only what it changes
/// (ADR-0041, slice seventy-seven).
/// </summary>
/// <remarks>
/// <para>
/// <b>Not an <see cref="Envelope"/>, because the language refuses a partial
/// one.</b> An envelope has four required members, and a child that restated
/// them would be a copy - the drift this type exists to end. So every member
/// here is optional except the parent, and absent always means "as my parent
/// has it".
/// </para>
/// <para>
/// <b>No root-governed member.</b> A child inherits its parent's context, and
/// the bounds, the floor's servers and learned advice are root's or the full
/// document's. A child that could name them would be a second place to set
/// what one place governs.
/// </para>
/// <para>
/// <b>Resolved before composition</b> by <see cref="WorkKindChain.Resolve"/>,
/// so the operator table never sees one: what reaches the composer is an
/// ordinary work kind.
/// </para>
/// </remarks>
[PinnedId("702c2ebf-cb1f-4ddf-b553-244d43469b9e")]
public sealed record EnvelopeExtension
{
    /// <summary>The work kind this one extends: one name, always.</summary>
    public required string Extends { get; init; }

    /// <summary>True when this kind is never flown itself. Not inherited.</summary>
    public bool? Abstract { get; init; }

    /// <summary>Replaces the parent's description when present.</summary>
    public string? Description { get; init; }

    /// <summary>Replaces the parent's brief when present.</summary>
    public string? Brief { get; init; }

    /// <summary>Replaces the parent's targeting when present.</summary>
    public string? Targeting { get; init; }

    /// <summary>Replaces the parent's hosts, whole, when present.</summary>
    public IReadOnlyList<string>? Hosts { get; init; }

    /// <summary>Replaces the parent's accepts, whole, when present.</summary>
    public IReadOnlyList<string>? Accepts { get; init; }

    /// <summary>Replaces the parent's produces, whole, when present.</summary>
    public IReadOnlyList<string>? Produces { get; init; }

    /// <summary>The families it consults, replacing its parent's, or null to inherit them.</summary>
    public IReadOnlyList<string>? Consults { get; init; }

    /// <summary>Appended after the parent's instructions.</summary>
    public IReadOnlyList<EnvelopeInstruction> Instructions
    {
        get => field ?? [];
        init;
    }

    /// <summary>Each replaces the parent's obligation of its id, whole, or is added.</summary>
    public IReadOnlyList<Obligation> Obligations
    {
        get => field ?? [];
        init;
    }

    /// <summary>Each replaces the parent's loop of its id, whole, or is added.</summary>
    public IReadOnlyList<Loop> Loops
    {
        get => field ?? [];
        init;
    }

    /// <summary>Each replaces the parent's destination of its id, whole, or is added.</summary>
    public IReadOnlyList<Destination> Destinations
    {
        get => field ?? [];
        init;
    }

    /// <summary>What this kind takes away from its parent, or null when nothing.</summary>
    public ExtensionRemovals? Remove { get; init; }

    /// <summary>Null when the shape is sound, or one diagnosis.</summary>
    /// <remarks>
    /// Shape only. Whether the RESULT is a valid envelope is the resolution's
    /// question, asked of the leaf, because an abstract base and a child that
    /// completes it are only valid together.
    /// </remarks>
    public static string? Validate(EnvelopeExtension extension)
    {
        ArgumentNullException.ThrowIfNull(extension);

        if (string.IsNullOrWhiteSpace(extension.Extends))
        {
            return "This work kind extends nothing. 'extends:' names the work kind it changes, "
                 + "and a change against nothing is not a document.";
        }

        if (extension.Extends.Any(char.IsWhiteSpace))
        {
            return $"'extends: {extension.Extends}' is not a work kind name: a name has no spaces.";
        }

        return null;
    }
}

/// <summary>What a child takes away from its parent, each by its name or its text.</summary>
/// <remarks>
/// A removal that matches nothing is refused at resolution, naming it: it is a
/// parent that changed under its child, and silence would leave the child's
/// author believing something is gone that is not there to remove.
/// </remarks>
[PinnedId("6d0e5e94-bdf9-40e6-821b-1fb6cd17f54d")]
public sealed record ExtensionRemovals
{
    /// <summary>Loop ids to remove.</summary>
    public IReadOnlyList<string> Loops
    {
        get => field ?? [];
        init;
    }

    /// <summary>Destination ids to remove.</summary>
    public IReadOnlyList<string> Destinations
    {
        get => field ?? [];
        init;
    }

    /// <summary>Obligation ids to remove.</summary>
    public IReadOnlyList<string> Obligations
    {
        get => field ?? [];
        init;
    }

    /// <summary>Instructions to remove, each by its exact text.</summary>
    public IReadOnlyList<string> Instructions
    {
        get => field ?? [];
        init;
    }
}

/// <summary>A chain resolved into one work kind, or why it could not be.</summary>
[PinnedId("acd22457-9eb7-48c2-be86-c2e220d9bc29")]
public sealed record WorkKindResolution
{
    /// <summary>The effective work kind, or null when refused.</summary>
    public Envelope? Effective { get; init; }

    /// <summary>What was wrong, naming the document, or null.</summary>
    public string? Refused { get; init; }
}

/// <summary>
/// A work kind and the children below it, resolved top down into the one work
/// kind composition sees (ADR-0041 Decisions 2 and 3).
/// </summary>
/// <remarks>
/// <para>
/// <b>An entry is replaced whole, never merged member by member.</b> A loop,
/// destination or obligation the child names replaces its parent's entry of
/// that id, so a reader finds one loop in one document. Lists replace whole for
/// the same reason, and a scalar the child names wins.
/// </para>
/// <para>
/// <b>Instructions append.</b> The child's guidance is written to be read after
/// its parent's; a parent's sentence the child must not keep is removed by its
/// exact text.
/// </para>
/// </remarks>
public static class WorkKindChain
{
    /// <summary>The most work kinds one chain may hold, its top included.</summary>
    /// <remarks>
    /// Every case ADR-0041 names needs two. A bound can be raised in a sentence;
    /// a chain nobody can read cannot be lowered without breaking someone.
    /// </remarks>
    public const int MaxDepth = 3;

    /// <summary>
    /// Resolves <paramref name="top"/> and the children below it, in order from
    /// the one that extends it to the leaf.
    /// </summary>
    public static WorkKindResolution Resolve(
        string topName,
        Envelope top,
        IReadOnlyList<(string Name, EnvelopeExtension Extension)> chain)
    {
        ArgumentNullException.ThrowIfNull(top);
        ArgumentNullException.ThrowIfNull(chain);

        if (chain.Count + 1 > MaxDepth)
        {
            return Refuse($"'{chain[^1].Name}' sits {chain.Count + 1} work kinds deep, and a "
                        + $"chain holds at most {MaxDepth}.");
        }

        var effective = top;
        var parent = topName;

        foreach (var (name, extension) in chain)
        {
            if (EnvelopeExtension.Validate(extension) is { } broken)
            {
                return Refuse($"'{name}': {broken}");
            }

            if (!string.Equals(extension.Extends, parent, StringComparison.Ordinal))
            {
                return Refuse($"'{name}' extends '{extension.Extends}', and the work kind above "
                            + $"it in this chain is '{parent}'.");
            }

            if (Applied(effective, name, extension) is not { } next)
            {
                return Refuse(Unmatched(effective, name, extension)!);
            }

            effective = next;
            parent = name;
        }

        var leaf = chain.Count == 0 ? topName : chain[^1].Name;

        return Envelope.Validate(effective) is { } invalid
            ? Refuse($"'{leaf}', resolved over its chain, is not a valid work kind: {invalid}")
            : new WorkKindResolution { Effective = effective };
    }

    private static WorkKindResolution Refuse(string why) => new() { Refused = why };

    /// <summary>One child applied over its parent, or null when a removal matches nothing.</summary>
    private static Envelope? Applied(Envelope parent, string name, EnvelopeExtension child)
    {
        if (Unmatched(parent, name, child) is not null)
        {
            return null;
        }

        var removed = child.Remove ?? new ExtensionRemovals();

        return parent with
        {
            // ABSTRACT IS WHAT A DOCUMENT SAYS ABOUT ITSELF: a child of an
            // abstract base is flyable unless it says otherwise.
            Abstract = child.Abstract,
            Description = child.Description ?? parent.Description,
            Brief = child.Brief ?? parent.Brief,
            Targeting = child.Targeting ?? parent.Targeting,
            Hosts = child.Hosts ?? parent.Hosts,
            Accepts = child.Accepts ?? parent.Accepts,
            Produces = child.Produces ?? parent.Produces,
            Consults = child.Consults ?? parent.Consults,
            Instructions =
            [
                .. parent.Instructions.Where(i => !removed.Instructions.Contains(i.Text, StringComparer.Ordinal)),
                .. child.Instructions,
            ],
            Obligations = Replaced(parent.Obligations, removed.Obligations, child.Obligations, o => o.Id),
            Loops = Replaced(parent.Loops, removed.Loops, child.Loops, l => l.Id),
            Destinations = Replaced(parent.Destinations, removed.Destinations, child.Destinations, d => d.Id),
        };
    }

    /// <summary>The first removal that names nothing in the parent, as a refusal, or null.</summary>
    private static string? Unmatched(Envelope parent, string name, EnvelopeExtension child)
    {
        if (child.Remove is not { } removed)
        {
            return null;
        }

        foreach (var (what, asked, present) in ((string, IReadOnlyList<string>, IEnumerable<string>)[])
            [("loop", removed.Loops, parent.Loops.Select(l => l.Id)),
             ("destination", removed.Destinations, parent.Destinations.Select(d => d.Id)),
             ("obligation", removed.Obligations, parent.Obligations.Select(o => o.Id)),
             ("instruction", removed.Instructions, parent.Instructions.Select(i => i.Text))])
        {
            var have = present.ToHashSet(StringComparer.Ordinal);

            if (asked.FirstOrDefault(a => !have.Contains(a)) is { } missing)
            {
                return $"'{name}' removes the {what} '{missing}', and its parent has no such "
                     + $"{what}. The parent changed under it, or the removal was never true; "
                     + "either way the child's author should know.";
            }
        }

        return null;
    }

    /// <summary>The parent's entries less the removed, each child entry replacing its id or added.</summary>
    private static List<T> Replaced<T>(
        IReadOnlyList<T> parent,
        IReadOnlyList<string> removed,
        IReadOnlyList<T> child,
        Func<T, string> id)
    {
        var named = child.ToDictionary(id, StringComparer.Ordinal);
        var kept = parent
            .Where(entry => !removed.Contains(id(entry), StringComparer.Ordinal))
            .Select(entry => named.TryGetValue(id(entry), out var replacement) ? replacement : entry)
            .ToList();

        var present = kept.Select(id).ToHashSet(StringComparer.Ordinal);
        kept.AddRange(child.Where(entry => !present.Contains(id(entry))));

        return kept;
    }
}
