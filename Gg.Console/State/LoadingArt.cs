namespace Gg.Console;

/// <summary>
/// What a tab shows while its read is still in the air.
/// </summary>
/// <remarks>
/// <para>
/// <b>Because "not read yet" and "read, and empty" look identical.</b> The
/// distinction is one this console already keeps in words - it is why
/// <see cref="Tabs.HasRead"/> exists at all, and why the board says "not read
/// yet" until both of its reads answer. This is the same fact, said where
/// somebody sees it without reading.
/// </para>
/// <para>
/// <b>A breath and never a bar.</b> Nothing here knows how far along a read
/// is: the control plane answers or it does not. Anything that filled up, or
/// counted, would be inventing a progress nobody measured - so this returns to
/// where it started, every time.
/// </para>
/// <para>
/// <b>Pure, and drawn nowhere else.</b> The view centres it and owns the timer;
/// what it looks like is decided here, which is what makes it testable without
/// a terminal - <see cref="Keymap.Resolve"/>'s rule one pane over.
/// </para>
/// </remarks>
public static class LoadingArt
{
    /// <summary>The mark, as a mask. `#` takes the breath's character.</summary>
    /// <remarks>
    /// <b>Two glyphs, because the product has two.</b> Block capitals at this
    /// size would need twice the width and a pane cannot spare it; this is the
    /// lower-case pair the binary is named after, descender and all.
    /// </remarks>
    private static readonly string[] Mark =
    [
        "  ######    ######  ",
        " ##    ##  ##    ## ",
        " ##        ##       ",
        " ##  ####  ##  #### ",
        " ##    ##  ##    ## ",
        "  ######    ######  ",
        "      ##        ##  ",
        "  #####     #####   ",
    ];

    /// <summary>
    /// The characters one breath passes through.
    /// </summary>
    /// <remarks>
    /// <b>Out and back, so the loop has no seam.</b> A cycle that jumped from
    /// full to empty would blink rather than breathe. The shades are the four
    /// block-fill characters every terminal font this ships to carries; a
    /// colour change would say the same thing and would be invisible to anybody
    /// whose terminal is not colouring it.
    /// </remarks>
    private static readonly char[] Shades = ['░', '▒', '▓', '█', '▓', '▒'];

    /// <summary>How many ticks one breath takes.</summary>
    public static int Breath => Shades.Length;

    /// <summary>The mark at one tick of the breath.</summary>
    /// <param name="tick">
    /// Any integer. The counter behind this only goes up and is never reset, so
    /// it reaches <see cref="int.MinValue"/> eventually - and a mark that threw
    /// there would take the console down while it waited for a read.
    /// </param>
    public static IReadOnlyList<string> Of(int tick)
    {
        // REMAINDER, THEN LIFTED. C# gives a negative remainder for a negative
        // left side, which would index outside the array - the arithmetic that
        // makes `int.MinValue' the interesting case rather than a silly one.
        var shade = Shades[((tick % Shades.Length) + Shades.Length) % Shades.Length];

        return [.. Mark.Select(line => line.Replace('#', shade))];
    }

    /// <summary>Whether the tab on screen is still waiting for its read.</summary>
    /// <remarks>
    /// <b><see cref="Tabs.HasRead"/> asked the other way, and never a second
    /// answer to it.</b> Two notions of "has this arrived" is how a pane comes
    /// to breathe over a table that is already full.
    /// </remarks>
    public static bool Waiting(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return !Tabs.HasRead(state, state.ActiveTab);
    }
}
