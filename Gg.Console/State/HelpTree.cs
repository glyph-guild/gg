namespace Gg.Console;

/// <summary>One fold of the help page: a mode, and the keys it teaches.</summary>
/// <remarks>
/// <b>Plain data, and serializable like everything else in the model.</b> The
/// console rebuilds its views from <see cref="AppState"/> after handing the
/// terminal away, so a group that existed only inside a widget would come back
/// missing.
/// </remarks>
public sealed record HelpGroup
{
    /// <summary>Which mode these keys belong to.</summary>
    public required UiMode Mode { get; init; }

    /// <summary>What the fold is called. Never empty — a fold is chosen by its name.</summary>
    public required string Heading { get; init; }

    /// <summary>The keys, in the order the catalogue declares them.</summary>
    public required IReadOnlyList<HelpKey> Keys { get; init; }
}

/// <summary>One key on the help page.</summary>
public sealed record HelpKey
{
    public required string Name { get; init; }

    public required string Description { get; init; }

    /// <summary>When it applies, or empty when it always does.</summary>
    public required string When { get; init; }

    /// <summary>
    /// Whether the catalogue marks it unteachable.
    /// </summary>
    /// <remarks>
    /// Carried so a test can assert none reaches here, rather than trusting a
    /// filter that a later edit could drop.
    /// </remarks>
    public required bool Untaught { get; init; }
}

/// <summary>
/// The help page's keys, grouped so a person can fold what they are not using.
/// </summary>
/// <remarks>
/// <para>
/// <b>The same catalogue the flat page renders.</b> <c>HelpKeys</c> already
/// grouped by mode and threw the grouping away into a string. This keeps it,
/// so a tree can fold it and a test can count it — and the two pages cannot
/// teach different consoles, because there is one source.
/// </para>
/// <para>
/// <b>What is OPEN is here rather than in the widget</b>, for the reason the
/// records above are plain: views are rebuilt from the model and a fold
/// remembered only by a TreeView would open itself every time the terminal
/// came back.
/// </para>
/// </remarks>
public static class HelpTree
{
    /// <summary>Every mode that teaches at least one key, in catalogue order.</summary>
    public static IReadOnlyList<HelpGroup> Keys() =>
    [
        .. Keymap.Catalogue()
            .Where(entry => !entry.Binding.Untaught)
            .GroupBy(entry => entry.Mode)
            .Select(group => new HelpGroup
            {
                Mode = group.Key,
                Heading = Heading(group.Key),
                Keys =
                [
                    .. group.Select(entry => new HelpKey
                    {
                        Name = entry.Binding.Key.Name,
                        Description = entry.Binding.Description,
                        When = entry.Binding.When ?? "",
                        Untaught = entry.Binding.Untaught,
                    }),
                ],
            })

            // A MODE WHOSE KEYS ARE ALL UNTAUGHT IS NOT A FOLD. An empty one is
            // a promise of something behind it, and opening it to find nothing
            // is worse than never offering it.
            .Where(group => group.Keys.Count > 0),
    ];

    /// <summary>Whether a group starts open.</summary>
    /// <remarks>
    /// <b>Only the always-available keys.</b> They are the answer to "what can
    /// I do" and the flat page leads with them under no heading at all, so a
    /// tree that folded them would make the common case the hidden one.
    /// Everything else starts closed, which is the point of grouping a list
    /// that had grown past a screen.
    /// </remarks>
    public static bool Opens(IReadOnlyList<HelpGroup> tree, UiMode mode)
    {
        ArgumentNullException.ThrowIfNull(tree);

        return mode == UiMode.Normal;
    }

    /// <summary>What a fold is called, which is the only thing a person picks it by.</summary>
    private static string Heading(UiMode mode) => mode switch
    {
        UiMode.Normal => "Always",
        UiMode.Help => "While this page is open",
        _ => $"While {mode.ToString().ToLowerInvariant()} is open",
    };
}
