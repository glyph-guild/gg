using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;

// AMBIGUOUS WITHOUT THIS: Terminal.Gui.Drawing.Attribute and System.Attribute
// are both in scope, and the one meant here is a pair of colours rather than
// something you decorate a class with.
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Gg.Console.Views;

/// <summary>
/// What Terminal.Gui calls each thing the Look page can change.
/// </summary>
/// <remarks>
/// <para>
/// <b><see cref="KeyTranslator"/>'s job, for the other half of the screen.</b>
/// That file is the only place a <c>Key</c> is touched, so <c>Keymap</c> can
/// stay a pure function; this is the only place a <c>LineStyle</c>, a
/// <c>Scheme</c> or a <c>TabSide</c> is touched, so <see cref="Look"/> can stay
/// plain serializable data. Both exist for the same reason and neither is a
/// convenience.
/// </para>
/// <para>
/// <b>The palettes are built here rather than fetched.</b> Terminal.Gui has a
/// <c>ThemeManager</c> with named themes, and reaching it means enabling
/// <c>ConfigurationManager</c> — reflection-driven JSON, which this binary may
/// not have. Measured against 2.4.17: with the manager disabled there is
/// exactly one theme, <c>Default</c>. So a palette is six colours and a
/// constructor, which is AOT-safe and needs nothing on disk.
/// </para>
/// <para>
/// <b><see cref="Palette.Default"/> builds nothing at all.</b> It answers null,
/// and the caller leaves every view's scheme alone — which is what makes it a
/// way back rather than one more palette that happens to resemble the terminal.
/// A console somebody has not touched must be pixel-identical to one built
/// without this file.
/// </para>
/// </remarks>
public static class LookStyles
{
    /// <summary>The line a border is drawn with.</summary>
    public static LineStyle Line(Edge edge) => edge switch
    {
        Edge.None => LineStyle.None,
        Edge.Single => LineStyle.Single,
        Edge.Dashed => LineStyle.Dashed,
        Edge.Dotted => LineStyle.Dotted,
        Edge.Double => LineStyle.Double,
        Edge.Heavy => LineStyle.Heavy,
        Edge.HeavyDashed => LineStyle.HeavyDashed,
        Edge.HeavyDotted => LineStyle.HeavyDotted,
        Edge.Rounded => LineStyle.Rounded,
        Edge.RoundedDashed => LineStyle.RoundedDashed,
        Edge.RoundedDotted => LineStyle.RoundedDotted,

        // REFUSES RATHER THAN ANSWERING, which is the rule this console applies
        // wherever a value decides what is drawn. A default arm here would draw
        // a plausible line for a value nobody defined, and the mismatch would
        // only ever be visible to somebody looking at the screen.
        _ => throw new ArgumentOutOfRangeException(nameof(edge), edge, "unknown edge"),
    };

    /// <summary>Which side of a pane the tab strip sits on.</summary>
    /// <remarks>
    /// <b>The property is <c>Tabs.TabSide</c> and its type is <c>Side</c></b> —
    /// a general one, not the tab strip's own, which is why the obvious spelling
    /// does not compile.
    /// </remarks>
    public static Side Side(TabEdge edge) => edge switch
    {
        TabEdge.Top => Terminal.Gui.ViewBase.Side.Top,
        TabEdge.Bottom => Terminal.Gui.ViewBase.Side.Bottom,
        TabEdge.Left => Terminal.Gui.ViewBase.Side.Left,
        TabEdge.Right => Terminal.Gui.ViewBase.Side.Right,
        _ => throw new ArgumentOutOfRangeException(nameof(edge), edge, "unknown side"),
    };

    /// <summary>
    /// The colours, or null where the answer is "whatever the terminal was".
    /// </summary>
    /// <remarks>
    /// <b>Six attributes, because that is what a person actually sees.</b> A
    /// <c>Scheme</c> declares twenty-three, most of them for syntax colouring
    /// this console does not do; the record's own defaults cover the rest, so
    /// naming the six that matter keeps each palette readable as a palette
    /// rather than as a wall of colour names.
    /// </remarks>
    public static Scheme? Colours(Palette palette) => palette switch
    {
        // NOTHING, AND THAT IS THE POINT. See the remark on this class.
        Palette.Default => null,

        Palette.Midnight => Built(
            ColorName16.Gray, ColorName16.Black,
            ColorName16.BrightCyan, ColorName16.Black,
            ColorName16.White, ColorName16.Blue),

        Palette.Slate => Built(
            ColorName16.White, ColorName16.DarkGray,
            ColorName16.BrightYellow, ColorName16.DarkGray,
            ColorName16.Black, ColorName16.Gray),

        Palette.Amber => Built(
            ColorName16.BrightYellow, ColorName16.Black,
            ColorName16.White, ColorName16.Black,
            ColorName16.Black, ColorName16.BrightYellow),

        Palette.Forest => Built(
            ColorName16.BrightGreen, ColorName16.Black,
            ColorName16.White, ColorName16.Black,
            ColorName16.Black, ColorName16.Green),

        // ONE HUE AND ITS TWO BRIGHTNESSES, for a terminal somebody is
        // screenshotting or reading in bad light. It is the palette a colour
        // scheme should be checked against: anything that only reads because of
        // hue stops reading here.
        Palette.Mono => Built(
            ColorName16.Gray, ColorName16.Black,
            ColorName16.White, ColorName16.Black,
            ColorName16.Black, ColorName16.White),

        Palette.Neon => Built(
            ColorName16.BrightMagenta, ColorName16.Black,
            ColorName16.BrightCyan, ColorName16.Black,
            ColorName16.Black, ColorName16.BrightMagenta),

        _ => throw new ArgumentOutOfRangeException(nameof(palette), palette, "unknown palette"),
    };

    /// <summary>
    /// A tab's title, marked if it is the one showing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Applied to the title the model just wrote, every render.</b> The
    /// screen sets each tab's <c>Title</c> from <c>Tabs.Title(State, tab)</c>
    /// before this runs, so the decoration goes on a clean value and cannot
    /// accumulate — which it would if this were the only thing writing it.
    /// </para>
    /// <para>
    /// <b><see cref="TabMark.Accent"/> answers the title unchanged</b>, because
    /// it marks the colour instead; the caller does that part.
    /// </para>
    /// </remarks>
    public static string Marked(string title, bool showing, TabMark mark)
    {
        if (!showing || mark is TabMark.None or TabMark.Accent)
        {
            return title;
        }

        return mark switch
        {
            TabMark.Brackets => $"[{title}]",
            TabMark.Arrows => $"\u25b8 {title} \u25c2",
            TabMark.Bullet => $"\u25cf {title}",
            TabMark.Caps => title.ToUpperInvariant(),
            _ => title,
        };
    }

    /// <summary>The scheme a tab is drawn in, or null to leave it alone.</summary>
    /// <remarks>
    /// <b>Only <see cref="TabMark.Accent"/> answers anything.</b> Every other
    /// mark is in the title, and a scheme returned here would fight the palette
    /// the rest of the console is being drawn in.
    /// </remarks>
    public static Scheme? TabColours(bool showing, TabMark mark, Palette palette)
    {
        if (mark is not TabMark.Accent || !showing)
        {
            return null;
        }

        // THE PALETTE'S OWN FOCUS COLOURS, INVERTED ONTO THE TAB. A fixed
        // accent would be invisible in half the palettes and unreadable in the
        // rest, which is the thing a spike is for finding out cheaply.
        return Colours(palette) is { } scheme
            ? scheme with { Normal = scheme.Focus, HotNormal = scheme.Focus }
            : new Scheme
            {
                Normal = new Attribute(ColorName16.Black, ColorName16.White),
                HotNormal = new Attribute(ColorName16.Black, ColorName16.BrightYellow),
            };
    }

    /// <summary>A scheme from the six attributes a person actually sees.</summary>
    private static Scheme Built(
        ColorName16 normalFore, ColorName16 normalBack,
        ColorName16 hotFore, ColorName16 hotBack,
        ColorName16 focusFore, ColorName16 focusBack)
    {
        var normal = new Attribute(normalFore, normalBack);
        var hot = new Attribute(hotFore, hotBack);
        var focus = new Attribute(focusFore, focusBack);

        return new Scheme
        {
            Normal = normal,
            HotNormal = hot,

            // FOCUS AND ACTIVE TOGETHER, because a row under the cursor and a
            // row being driven are the same row to the person looking at it.
            // Splitting them is how a table ends up with two highlights.
            Focus = focus,
            HotFocus = focus,
            Active = focus,
            HotActive = focus,
            Highlight = focus,

            // DIMMED RATHER THAN COLOURED. A disabled thing is one there is no
            // point reaching for, and every palette says that the same way.
            Disabled = new Attribute(ColorName16.DarkGray, normalBack),
        };
    }
}
