namespace Gg.Console;

/// <summary>
/// One row of the help tree: a group, or a key under one.
/// </summary>
/// <remarks>
/// <para>
/// <b>The shape a <c>TreeView</c> wants, built from the shape the model
/// keeps.</b> <see cref="HelpTree"/> answers in groups and keys; this is those
/// two flattened into the one node type a tree walks, so the widget needs no
/// opinion about which is which beyond <see cref="Group"/>.
/// </para>
/// <para>
/// <b>It carries the mode rather than a reference to the fold.</b> What is
/// open lives in <c>AppState.HelpFolds</c>, keyed by mode — so a node knows
/// which fold it is without holding one, and a tree rebuilt from scratch lands
/// on the same folds.
/// </para>
/// </remarks>
public sealed class HelpNode
{
    /// <summary>What the row reads as.</summary>
    public required string Text { get; init; }

    /// <summary>The group this row is, or the group it belongs to.</summary>
    public required UiMode Mode { get; init; }

    /// <summary>Whether this row is the group itself rather than a key under it.</summary>
    public required bool Group { get; init; }

    /// <summary>The keys under it. Empty for a key.</summary>
    public IReadOnlyList<HelpNode> Keys { get; init; } = [];

    public override string ToString() => Text;
}
