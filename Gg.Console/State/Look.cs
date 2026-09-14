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

/// <summary>How the tab a person is on is marked out from the rest.</summary>
/// <remarks>
/// <b>Terminal.Gui's <c>Tabs</c> offers nothing for this</b> — it has a line
/// style, a side, a depth and a spacing, and no notion of a selected-tab
/// appearance. What it does have is one <c>View</c> per tab, whose
/// <c>Title</c> it draws and whose scheme it uses, so both of those are
/// reachable: the first four mark the TEXT and the last marks the COLOUR.
/// </remarks>
public enum TabMark
{
    /// <summary>Nothing. The strip's own line is the only indication.</summary>
    None,

    Brackets,
    Arrows,
    Bullet,
    Caps,

    /// <summary>The scheme rather than the text.</summary>
    Accent,
}

/// <summary>How much line work a table is drawn with.</summary>
/// <remarks>
/// <b>Named looks rather than the twelve booleans underneath.</b>
/// <c>TableStyle</c> has a flag per line — headers, their overline and
/// underline, the verticals, the two outer verticals, a bottom line — and
/// offering all of them would be a page of knobs where what a person wants is
/// "less line work". Each of these sets the whole group at once.
/// </remarks>
public enum TableLines
{
    /// <summary>Headers ruled above and below, verticals between every cell.</summary>
    Full,

    /// <summary>The same, closed off with a line underneath the last row.</summary>
    Boxed,

    /// <summary>Headers ruled, and no verticals. Rows read across.</summary>
    Horizontal,

    /// <summary>One line under the headers and nothing else.</summary>
    Minimal,

    /// <summary>No rules at all. Columns are held apart by spacing alone.</summary>
    None,
}

/// <summary>Whether a table says what its columns are.</summary>
public enum TableHeaders
{
    Shown,
    Hidden,
}

/// <summary>What a table marks when the cursor is on a row.</summary>
/// <remarks>
/// <b>The one that changes how a table FEELS rather than how it looks.</b>
/// Marking a cell says "you are editing this"; marking the row says "this is
/// the one you picked" — and every table in this console is picked from rather
/// than edited.
/// </remarks>
public enum RowSelect
{
    Cell,
    FullRow,
}

/// <summary>Whether the header row is coloured apart from the body.</summary>
public enum HeaderTint
{
    Plain,
    Tinted,
}

/// <summary>How something is pushed into the background.</summary>
/// <remarks>
/// <para>
/// <b>Two ways, because one of them is not always honoured.</b>
/// <see cref="Faint"/> is SGR 2 — the terminal's own idea of dim, which keeps
/// the colour and lowers the intensity, and which some terminals ignore
/// entirely. <see cref="Grey"/> changes the foreground instead, so it works
/// everywhere and looks the same in every palette.
/// </para>
/// <para>
/// <b>Applied over whatever the view already had</b>, rather than built from a
/// palette. That is what lets it dim a console running in the terminal's own
/// colours without deciding what those colours are.
/// </para>
/// </remarks>
public enum Dimming
{
    Normal,
    Faint,
    Grey,
}

/// <summary>One row of the Look page: a thing that can be changed.</summary>
public enum LookSetting
{
    Palette,
    AppBorder,
    PaneBorder,
    InnerBorder,
    ModalBorder,
    TabLine,
    TabMark,
    TabSide,
    TabDepth,
    TabSpacing,
    TableLines,
    TableHeaders,
    RowSelect,
    HeaderTint,
    UnselectedTabs,
    StatusText,
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
    // WHAT THIS SHIPS AS WAS CHOSEN ON THIS PAGE. Five of these are not
    // Terminal.Gui's defaults: no line at all round the application, heavy
    // lines round the panes and the dialog, a dotted-round tab strip, and tabs
    // that sit flush instead of overlapping. They were picked by running the
    // spike and copying the result, which is what the copy is for - so the
    // sentence "ships as" now names these rather than the library's.
    //
    // A SECOND ROUND ADDED THREE MORE: the tab strip along the BOTTOM, tables
    // with no rule work at all, and the cursor marking the whole row. The last
    // is the one that says something true - every table here is picked FROM
    // rather than edited.
    //
    // AND A THIRD ROUND ADDED THE STATUS LINE, held back at Faint - the
    // terminal's own dim rather than a grey foreground, which means this
    // machine's terminal honours SGR 2. A console where it does not will draw
    // those two lines at full brightness rather than wrongly, which is the
    // right way for that to fail.
    //
    // The rest are deliberately left alone: no palette (a console nobody has
    // touched must be the terminal's own colours), a plain inner line, no mark
    // on the selected tab, headers shown and untinted, and the unselected tabs
    // at full strength.

    /// <summary>The colours.</summary>
    public Palette Palette { get; init; }

    /// <summary>The line round the whole application.</summary>
    /// <remarks>
    /// <b>The outermost frame, which is the one nothing else was reaching.</b>
    /// It is a Window rather than a FrameView, so the walk that styles panes
    /// steps straight past it — and it is the biggest line on the screen.
    /// </remarks>
    public Edge AppBorder { get; init; } = Edge.None;

    /// <summary>The line round a pane on a tab.</summary>
    /// <remarks>
    /// <b>The first layer in, and only the first.</b> This console nests frames
    /// two and three deep — a log pane inside a tab inside a modal — and giving
    /// every one of them the same line is what makes a busy screen read as a
    /// grid. See <see cref="InnerBorder"/>.
    /// </remarks>
    public Edge PaneBorder { get; init; } = Edge.Heavy;

    /// <summary>The line round a pane inside another pane.</summary>
    /// <remarks>
    /// <b>Everything deeper than the first layer.</b> Lighter than the pane's
    /// is the usual answer — the outer line groups and the inner one divides —
    /// but the point of a spike is that you can try the other way round.
    /// </remarks>
    public Edge InnerBorder { get; init; } = Edge.Single;

    /// <summary>The line round a dialog.</summary>
    /// <remarks>
    /// <b>Separate from the panes' on purpose.</b> A modal is a different kind
    /// of thing from a pane and the eye uses the border to say so — the first
    /// thing anybody tries here is a heavier line on the dialog than on what is
    /// behind it.
    /// </remarks>
    public Edge ModalBorder { get; init; } = Edge.Heavy;

    /// <summary>The line the tab strip is drawn with.</summary>
    public Edge TabLine { get; init; } = Edge.RoundedDotted;

    /// <summary>How the tab a person is on is marked out.</summary>
    public TabMark TabMark { get; init; } = TabMark.None;

    /// <summary>Which side the tab strip sits on.</summary>
    public TabEdge TabSide { get; init; } = TabEdge.Bottom;

    /// <summary>How tall a tab is.</summary>
    public int TabDepth { get; init; } = 3;

    /// <summary>How much room is left between tabs.</summary>
    /// <remarks>
    /// <b>Terminal.Gui's default is -1</b>, which overlaps the borders of
    /// neighbouring tabs so they share a line. Widening it separates them.
    /// </remarks>
    public int TabSpacing { get; init; } = 0;

    /// <summary>How much line work a table is drawn with.</summary>
    public TableLines TableLines { get; init; } = TableLines.None;

    /// <summary>Whether a table says what its columns are.</summary>
    public TableHeaders TableHeaders { get; init; } = TableHeaders.Shown;

    /// <summary>What a table marks when the cursor is on a row.</summary>
    public RowSelect RowSelect { get; init; } = RowSelect.FullRow;

    /// <summary>Whether the header row is coloured apart from the body.</summary>
    public HeaderTint HeaderTint { get; init; } = HeaderTint.Plain;

    /// <summary>How the tabs a person is NOT on are pushed back.</summary>
    /// <remarks>
    /// <b>The other half of marking the selected one.</b> A strip of eight
    /// equally bright tabs makes the eye work to find the one that is live;
    /// dimming the seven does the same job from the other side, and composes
    /// with whatever <see cref="TabMark"/> is set to.
    /// </remarks>
    public Dimming UnselectedTabs { get; init; } = Dimming.Normal;

    /// <summary>How the activity line and the key hints are pushed back.</summary>
    /// <remarks>
    /// <b>Two lines that are always there and rarely read.</b> They are
    /// reference rather than content — what a person looks at when they want
    /// them and past the rest of the time — and at full brightness they
    /// compete with the pane above them.
    /// </remarks>
    public Dimming StatusText { get; init; } = Dimming.Faint;

    /// <summary>Which row the cursor is on.</summary>
    public int Selected { get; init; }

    /// <summary>
    /// Whether the help modal is being held out of the way so the console
    /// behind it can be seen.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The page changes what is behind the page.</b> Six of these settings
    /// are about panes, borders and tabs that the modal is sitting on top of,
    /// so the one thing this page could not do was let somebody see their own
    /// change. The modal is hidden rather than closed: the mode does not move,
    /// so the keyboard still belongs to the Look page and the same key brings
    /// it back.
    /// </para>
    /// <para>
    /// <b>Not a setting, which is why it is not in
    /// <see cref="Looks.All"/>.</b> It is where a person is standing, like
    /// <see cref="Selected"/> — and it must not appear in what gets copied,
    /// because "the modal was hidden" is not something to make permanent.
    /// </para>
    /// </remarks>
    public bool Peeking { get; init; }
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

        // THE BORDERS OUTSIDE IN, because that is the order somebody looking at
        // the screen would name them: the frame round everything, the panes on
        // a tab, the panes inside those, and the dialog on top.
        LookSetting.AppBorder,
        LookSetting.PaneBorder,
        LookSetting.InnerBorder,
        LookSetting.ModalBorder,

        LookSetting.TabLine,
        LookSetting.TabMark,
        LookSetting.TabSide,
        LookSetting.TabDepth,
        LookSetting.TabSpacing,

        // THE TABLES LAST, because they are the densest part of this console
        // and the group somebody arrives at once the frame around them is
        // settled. Nearly every pane here is a table.
        LookSetting.TableLines,
        LookSetting.TableHeaders,
        LookSetting.RowSelect,
        LookSetting.HeaderTint,

        // AND THE TWO THAT PUSH THINGS BACK, last because they are about what
        // you should NOT be looking at - which is the thing you decide once the
        // rest of the screen is settled.
        LookSetting.UnselectedTabs,
        LookSetting.StatusText,
    ];

    /// <summary>What the setting is called on the page.</summary>
    public static string Title(LookSetting setting) => setting switch
    {
        LookSetting.Palette => "colours",
        LookSetting.AppBorder => "app border",
        LookSetting.PaneBorder => "pane border",
        LookSetting.InnerBorder => "inner border",
        LookSetting.ModalBorder => "modal border",
        LookSetting.TabLine => "tab line",
        LookSetting.TabMark => "selected tab",
        LookSetting.TabSide => "tab side",
        LookSetting.TabDepth => "tab depth",
        LookSetting.TabSpacing => "tab spacing",
        LookSetting.TableLines => "table lines",
        LookSetting.TableHeaders => "table headers",
        LookSetting.RowSelect => "row select",
        LookSetting.HeaderTint => "header tint",
        LookSetting.UnselectedTabs => "unselected tabs",
        LookSetting.StatusText => "status text",
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
        LookSetting.AppBorder => "The line round the whole application - the "
                               + "outermost frame on the screen.",
        LookSetting.PaneBorder => "The line round each pane on a tab. The first layer in, "
                                + "and only the first.",
        LookSetting.InnerBorder => "The line round a pane INSIDE a pane - a log above its "
                                 + "detail, a table above its prose.",
        LookSetting.ModalBorder => "The line round a dialog, which is what tells "
                                 + "one apart from the panes behind it.",
        LookSetting.TabLine => "The line the tab strip is drawn with.",
        LookSetting.TabMark => "How the tab you are on is marked out. The first four "
                             + "change its text; Accent changes its colour.",
        LookSetting.TabSide => "Which side of the pane the tab strip sits on.",
        LookSetting.TabDepth => "How tall a tab is, in rows.",
        LookSetting.TabSpacing => "Room between tabs. -1 overlaps their borders "
                                + "so neighbours share a line.",
        LookSetting.TableLines => "How much rule work a table carries, from every cell "
                                + "boxed to none at all.",
        LookSetting.TableHeaders => "Whether a table says what its columns are.",
        LookSetting.RowSelect => "What the cursor marks: one cell, or the whole row you "
                               + "picked.",
        LookSetting.HeaderTint => "Whether the header row is coloured apart from the body.",
        LookSetting.UnselectedTabs => "How far back the tabs you are NOT on sit. Faint is "
                                    + "the terminal's own dim and some ignore it; Grey "
                                    + "always works.",
        LookSetting.StatusText => "How far back the activity line and the key hints sit.",
        _ => "",
    };

    /// <summary>What the setting is set to, as a person reads it.</summary>
    public static string Value(Look look, LookSetting setting)
    {
        ArgumentNullException.ThrowIfNull(look);

        return setting switch
        {
            LookSetting.Palette => look.Palette.ToString(),
            LookSetting.AppBorder => look.AppBorder.ToString(),
            LookSetting.PaneBorder => look.PaneBorder.ToString(),
            LookSetting.InnerBorder => look.InnerBorder.ToString(),
            LookSetting.ModalBorder => look.ModalBorder.ToString(),
            LookSetting.TabLine => look.TabLine.ToString(),
            LookSetting.TabMark => look.TabMark.ToString(),
            LookSetting.TabSide => look.TabSide.ToString(),
            LookSetting.TableLines => look.TableLines.ToString(),
            LookSetting.TableHeaders => look.TableHeaders.ToString(),
            LookSetting.RowSelect => look.RowSelect.ToString(),
            LookSetting.HeaderTint => look.HeaderTint.ToString(),
            LookSetting.UnselectedTabs => look.UnselectedTabs.ToString(),
            LookSetting.StatusText => look.StatusText.ToString(),
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
            LookSetting.AppBorder => look with { AppBorder = Step(look.AppBorder, by) },
            LookSetting.PaneBorder => look with { PaneBorder = Step(look.PaneBorder, by) },
            LookSetting.InnerBorder => look with { InnerBorder = Step(look.InnerBorder, by) },
            LookSetting.ModalBorder => look with { ModalBorder = Step(look.ModalBorder, by) },
            LookSetting.TabLine => look with { TabLine = Step(look.TabLine, by) },
            LookSetting.TabMark => look with { TabMark = Step(look.TabMark, by) },
            LookSetting.TabSide => look with { TabSide = Step(look.TabSide, by) },
            LookSetting.TableLines => look with { TableLines = Step(look.TableLines, by) },
            LookSetting.TableHeaders => look with { TableHeaders = Step(look.TableHeaders, by) },
            LookSetting.RowSelect => look with { RowSelect = Step(look.RowSelect, by) },
            LookSetting.HeaderTint => look with { HeaderTint = Step(look.HeaderTint, by) },
            LookSetting.UnselectedTabs => look with
            {
                UnselectedTabs = Step(look.UnselectedTabs, by),
            },
            LookSetting.StatusText => look with { StatusText = Step(look.StatusText, by) },

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
