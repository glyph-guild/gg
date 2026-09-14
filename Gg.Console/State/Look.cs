namespace Gg.Console;

/// <summary>A named set of colours the console can be drawn in.</summary>
/// <remarks>
/// <b>Ours, not Terminal.Gui's.</b> The library has a <c>ThemeManager</c> with
/// named themes, and reaching it means enabling <c>ConfigurationManager</c> —
/// reflection-driven JSON, which this binary may not have. So the palettes are
/// declared here as data and built into schemes in the view layer, which is
/// both AOT-safe and the shape every other decision in this console already
/// has: a pure model, and one place that knows what a widget calls it.
/// </remarks>
public enum Palette
{
    /// <summary>Whatever the terminal already was. The default, and a way back.</summary>
    Default,

    Midnight,
    Slate,
    Amber,
    Forest,
    Mono,
    Neon,
}

/// <summary>How a line is drawn round a pane.</summary>
/// <remarks>
/// <b>The names are Terminal.Gui's <c>LineStyle</c>, deliberately.</b> A second
/// vocabulary for the same eleven shapes would be a translation to keep honest
/// in two directions; what this enum buys is that <see cref="AppState"/> holds
/// no library type, which is the rule that makes terminal release possible.
/// </remarks>
public enum Edge
{
    None,
    Single,
    Dashed,
    Dotted,
    Double,
    Heavy,
    HeavyDashed,
    HeavyDotted,
    Rounded,
    RoundedDashed,
    RoundedDotted,
}

/// <summary>Which side of a pane its tab strip sits on.</summary>
public enum TabEdge
{
    Top,
    Bottom,
    Left,
    Right,
}

/// <summary>One row of the Look page: a thing that can be changed.</summary>
public enum LookSetting
{
    Palette,
    PaneBorder,
    ModalBorder,
    TabLine,
    TabSide,
    TabDepth,
    TabSpacing,
}

/// <summary>How the console is drawn, as a person has set it.</summary>
/// <remarks>
/// <para>
/// <b>A spike, and the model is the part worth keeping.</b> What is being tried
/// out is whether the look of this console is worth making adjustable at all;
/// what that costs is one record on <see cref="AppState"/>, a pure table, and
/// one file in the view layer that knows what Terminal.Gui calls each of these.
/// </para>
/// <para>
/// <b>Serializable like everything else on the state</b>, so a console that
/// releases the terminal for an editor comes back drawn the way it left — the
/// same property that makes <c>GG_STATE_DUMP</c> able to say what the screen
/// looked like.
/// </para>
/// </remarks>
public sealed record Look
{
    /// <summary>The colours.</summary>
    public Palette Palette { get; init; }

    /// <summary>The line round a pane on a tab.</summary>
    public Edge PaneBorder { get; init; } = Edge.Single;

    /// <summary>The line round a dialog.</summary>
    /// <remarks>
    /// <b>Separate from the panes' on purpose.</b> A modal is a different kind
    /// of thing from a pane and the eye uses the border to say so — the first
    /// thing anybody tries here is a heavier line on the dialog than on what is
    /// behind it.
    /// </remarks>
    public Edge ModalBorder { get; init; } = Edge.Single;

    /// <summary>The line the tab strip is drawn with.</summary>
    public Edge TabLine { get; init; } = Edge.Rounded;

    /// <summary>Which side the tab strip sits on.</summary>
    public TabEdge TabSide { get; init; } = TabEdge.Top;

    /// <summary>How tall a tab is.</summary>
    public int TabDepth { get; init; } = 3;

    /// <summary>How much room is left between tabs.</summary>
    /// <remarks>
    /// <b>Terminal.Gui's default is -1</b>, which overlaps the borders of
    /// neighbouring tabs so they share a line. Widening it separates them.
    /// </remarks>
    public int TabSpacing { get; init; } = -1;

    /// <summary>Which row the cursor is on.</summary>
    public int Selected { get; init; }
}

/// <summary>
/// The Look page: what can be changed, what it is currently, and what a person
/// would have to say to make it permanent.
/// </summary>
/// <remarks>
/// <para>
/// <b>Pure, so the page can be asked what it holds without a terminal.</b>
/// <c>PaneText</c>'s rule and <c>HelpPages</c>' shape, one page over.
/// </para>
/// <para>
/// <b>Every value cycles and wraps.</b> A setting a person can walk off the end
/// of is one they have to walk back through, and eleven line styles is a long
/// way back. Wrapping also means one key is enough — there is a way to every
/// value without a second binding, which matters in a modal where letters are
/// scarce.
/// </para>
/// </remarks>
public static class Looks
{
    /// <summary>Every setting, in the order the page lists them.</summary>
    /// <remarks>
    /// <b>Colours first, because it is the one people come for.</b> The borders
    /// after it, then the tab strip's three — grouped by what they are about
    /// rather than by how much they change, so the list reads as a description
    /// of the console rather than as a pile of knobs.
    /// </remarks>
    public static IReadOnlyList<LookSetting> All { get; } =
    [
        LookSetting.Palette,
        LookSetting.PaneBorder,
        LookSetting.ModalBorder,
        LookSetting.TabLine,
        LookSetting.TabSide,
        LookSetting.TabDepth,
        LookSetting.TabSpacing,
    ];

    /// <summary>What the setting is called on the page.</summary>
    public static string Title(LookSetting setting) => setting switch
    {
        LookSetting.Palette => "colours",
        LookSetting.PaneBorder => "pane border",
        LookSetting.ModalBorder => "modal border",
        LookSetting.TabLine => "tab line",
        LookSetting.TabSide => "tab side",
        LookSetting.TabDepth => "tab depth",
        LookSetting.TabSpacing => "tab spacing",
        _ => setting.ToString(),
    };

    /// <summary>One line saying what the setting is for.</summary>
    /// <remarks>
    /// <b>A value with no sentence beside it is a knob rather than a setting.</b>
    /// Eleven line styles named after the glyphs they draw tell somebody
    /// nothing about where the line goes.
    /// </remarks>
    public static string About(LookSetting setting) => setting switch
    {
        LookSetting.Palette => "The colours everything is drawn in.",
        LookSetting.PaneBorder => "The line round each pane on a tab.",
        LookSetting.ModalBorder => "The line round a dialog, which is what tells "
                                 + "one apart from the panes behind it.",
        LookSetting.TabLine => "The line the tab strip is drawn with.",
        LookSetting.TabSide => "Which side of the pane the tab strip sits on.",
        LookSetting.TabDepth => "How tall a tab is, in rows.",
        LookSetting.TabSpacing => "Room between tabs. -1 overlaps their borders "
                                + "so neighbours share a line.",
        _ => "",
    };

    /// <summary>What the setting is set to, as a person reads it.</summary>
    public static string Value(Look look, LookSetting setting)
    {
        ArgumentNullException.ThrowIfNull(look);

        return setting switch
        {
            LookSetting.Palette => look.Palette.ToString(),
            LookSetting.PaneBorder => look.PaneBorder.ToString(),
            LookSetting.ModalBorder => look.ModalBorder.ToString(),
            LookSetting.TabLine => look.TabLine.ToString(),
            LookSetting.TabSide => look.TabSide.ToString(),
            LookSetting.TabDepth => look.TabDepth.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            LookSetting.TabSpacing => look.TabSpacing.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            _ => "",
        };
    }

    /// <summary>The setting the cursor is on.</summary>
    public static LookSetting Under(Look look)
    {
        ArgumentNullException.ThrowIfNull(look);

        // CLAMPED, because the cursor is a number and the list is a list. A
        // selection past the end would render nothing, which reads as a page
        // that broke rather than a cursor that is out of range.
        return All[Math.Clamp(look.Selected, 0, All.Count - 1)];
    }

    /// <summary>The same look with the setting under the cursor moved.</summary>
    /// <param name="by">+1 for the next value, -1 for the one before it.</param>
    public static Look Turned(Look look, int by)
    {
        ArgumentNullException.ThrowIfNull(look);

        return Under(look) switch
        {
            LookSetting.Palette => look with { Palette = Step(look.Palette, by) },
            LookSetting.PaneBorder => look with { PaneBorder = Step(look.PaneBorder, by) },
            LookSetting.ModalBorder => look with { ModalBorder = Step(look.ModalBorder, by) },
            LookSetting.TabLine => look with { TabLine = Step(look.TabLine, by) },
            LookSetting.TabSide => look with { TabSide = Step(look.TabSide, by) },

            // BOUNDED RATHER THAN WRAPPED, because these two are numbers and a
            // number that jumps from its largest to its smallest reads as a
            // bug. One row is the shallowest tab that can hold a title, and
            // beyond five nothing is gained but height.
            LookSetting.TabDepth => look with
            {
                TabDepth = Math.Clamp(look.TabDepth + by, 1, 5),
            },

            // -1 IS THE MEANINGFUL FLOOR, not zero: it is the overlap that
            // makes neighbouring tabs share a border, which is the default and
            // the tightest the strip goes.
            LookSetting.TabSpacing => look with
            {
                TabSpacing = Math.Clamp(look.TabSpacing + by, -1, 4),
            },

            _ => look,
        };
    }

    /// <summary>The next value of an enum, wrapping at either end.</summary>
    private static T Step<T>(T value, int by) where T : struct, Enum
    {
        var all = Enum.GetValues<T>();
        var at = Array.IndexOf(all, value);

        // A MODULO THAT ANSWERS FOR NEGATIVES. C# gives -1 % 11 as -1, which
        // indexes nothing; adding the length first is what makes stepping back
        // off the front land on the end.
        return all[((at + by) % all.Length + all.Length) % all.Length];
    }

    /// <summary>The rows the page draws.</summary>
    public static IReadOnlyList<LookRow> Rows(Look look)
    {
        ArgumentNullException.ThrowIfNull(look);

        return [.. All.Select(setting => new LookRow
        {
            Setting = Title(setting),
            Value = Value(look, setting),
            Changed = Differs(look, setting) ? "changed" : "",
        })];
    }

    /// <summary>The columns the page's table declares.</summary>
    public static IReadOnlyList<string> Columns { get; } = ["setting", "value", ""];

    /// <summary>Whether this setting has been moved off what it ships as.</summary>
    /// <remarks>
    /// <b>What makes the copy worth having.</b> A person who changed two things
    /// out of seven wants to be handed those two, not a listing of the console's
    /// defaults with their two buried in it.
    /// </remarks>
    public static bool Differs(Look look, LookSetting setting)
    {
        ArgumentNullException.ThrowIfNull(look);

        return !string.Equals(
            Value(look, setting), Value(new Look(), setting), StringComparison.Ordinal);
    }

    /// <summary>Everything a person has moved, or nothing.</summary>
    public static IReadOnlyList<LookSetting> Changed(Look look) =>
        [.. All.Where(setting => Differs(look, setting))];

    /// <summary>
    /// What goes on the clipboard: the changes, said as an instruction.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A spike's output is an argument, and this is the form it takes.</b>
    /// The page is for trying looks on; what a person wants afterwards is to
    /// hand somebody the two that worked. So the copy is not a dump of the
    /// model — it is a sentence naming what to change and what to change it to,
    /// with the defaults left out because they are not the point.
    /// </para>
    /// <para>
    /// <b>It says so when nothing was changed</b>, rather than copying an empty
    /// block. <c>ConsoleClipboard</c> already refuses to copy whitespace and
    /// reports that it did; a header over nothing would get past that check and
    /// paste as a heading with no content under it.
    /// </para>
    /// </remarks>
    public static string Copyable(Look look)
    {
        ArgumentNullException.ThrowIfNull(look);

        var changed = Changed(look);

        if (changed.Count == 0)
        {
            return "The console is drawn as it ships. Nothing has been changed on the "
                 + "Look page, so there is nothing to make permanent.";
        }

        return string.Join('\n', (string[])
        [
            "Make these console look settings permanent:",
            "",
            .. changed.Select(setting =>
                $"  {Title(setting)}: {Value(look, setting)}"
              + $"   (ships as {Value(new Look(), setting)})"),
            "",
            "They were chosen on the Look page of gg's help modal, which is a spike -"
          + " the values live on AppState.Look and are applied in Views/LookStyles.cs.",
        ]);
    }
}

/// <summary>One row of the Look page's table.</summary>
public sealed record LookRow
{
    /// <summary>What can be changed.</summary>
    public string Setting { get; init; } = "";

    /// <summary>What it is set to.</summary>
    public string Value { get; init; } = "";

    /// <summary>Whether it has been moved off what it ships as.</summary>
    public string Changed { get; init; } = "";
}
