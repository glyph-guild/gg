using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

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

    /// <summary>
    /// Draw a table the way the Look page says to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Every flag set every time, never toggled.</b> <c>TableStyle</c> is a
    /// bag of booleans on a shared object, so setting only the ones a preset
    /// cares about leaves the rest holding whatever the last preset put there —
    /// and stepping through the five would accumulate instead of switching.
    /// </para>
    /// <para>
    /// <b>The header's colour comes from the palette</b>, or from a plain
    /// inversion where the palette is the terminal's own. A fixed header colour
    /// is unreadable in half of them.
    /// </para>
    /// </remarks>
    public static void Table(TableView table, Look look)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(look);

        var style = table.Style;

        style.ShowHeaders = look.TableHeaders is TableHeaders.Shown;

        style.ShowHorizontalHeaderOverline =
            look.TableLines is TableLines.Full or TableLines.Boxed;

        style.ShowHorizontalHeaderUnderline = look.TableLines is not TableLines.None;

        style.ShowVerticalCellLines =
            look.TableLines is TableLines.Full or TableLines.Boxed;

        style.ShowVerticalHeaderLines =
            look.TableLines is TableLines.Full or TableLines.Boxed;

        // THE TWO OUTER VERTICALS SEPARATELY, because a table inside a pane
        // already has a line down each side - its own. Drawing another beside
        // it is the doubled rule this console had before the panes were
        // frames.
        style.ShowVerticalCellLineForFirstColumn =
            look.TableLines is TableLines.Full or TableLines.Boxed;

        style.ShowVerticalCellLineForLastColumn =
            look.TableLines is TableLines.Full or TableLines.Boxed;

        style.ShowHorizontalBottomLine = look.TableLines is TableLines.Boxed;

        // WHAT THE CURSOR MARKS. Every table here is picked FROM rather than
        // edited, so a whole-row mark says the true thing; a cell mark says
        // "you are editing this".
        table.FullRowSelect = look.RowSelect is RowSelect.FullRow;

        style.HeaderScheme = look.HeaderTint is HeaderTint.Tinted
            ? Header(look.Palette)
            : null;
    }

    /// <summary>The header row's colours, drawn out of the palette.</summary>
    private static Scheme Header(Palette palette) =>
        Colours(palette) is { } scheme
            ? scheme with { Normal = scheme.Focus, HotNormal = scheme.Focus }
            : new Scheme
            {
                Normal = new Attribute(ColorName16.Black, ColorName16.Gray),
                HotNormal = new Attribute(ColorName16.Black, ColorName16.White),
            };

    /// <summary>
    /// The scheme a view already has, pushed into the background.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Built from a basis the caller supplies, never from the view being
    /// dimmed.</b> Reading the view's own scheme would feed this render's
    /// output into the next one — harmless for these two operations, which are
    /// both idempotent, and a trap the moment a third is not. It also leaves
    /// no way back: a dimmed view asked what it looks like answers "dim".
    /// </para>
    /// <para>
    /// <b>The basis is the palette where there is one, and the parent's
    /// effective scheme where there is not</b> — which is what lets this dim a
    /// console running in the terminal's OWN colours, the default, and the one
    /// case where this code does not know what the foreground is.
    /// </para>
    /// <para>
    /// <b><see cref="Dimming.Faint"/> keeps the colour and lowers the
    /// intensity</b> — SGR 2, which is the terminal's own idea of dim and which
    /// some terminals ignore. <see cref="Dimming.Grey"/> changes the
    /// foreground, so it works everywhere; both are offered because there is no
    /// way to find out from here which one a person's terminal honours.
    /// </para>
    /// <para>
    /// <b>Only Normal and HotNormal.</b> Focus is what a dimmed thing looks
    /// like when it stops being background — a tab somebody has tabbed to, a
    /// hint line with the keyboard in it — and dimming that would hide the one
    /// state the dimming exists to contrast with.
    /// </para>
    /// </remarks>
    public static Scheme? Dimmed(Scheme basis, Dimming how)
    {
        ArgumentNullException.ThrowIfNull(basis);

        if (how is Dimming.Normal)
        {
            return null;
        }

        var scheme = basis;

        return scheme with
        {
            Normal = Back(scheme.Normal, how),
            HotNormal = Back(scheme.HotNormal, how),
        };
    }

    /// <summary>
    /// The countdown's seconds, bright at the top of the wait and dim at the
    /// bottom of it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Here because this is the only file that builds a colour</b>, which is
    /// the rule that keeps every other file answerable without a terminal. What
    /// fraction of the wait is left is <c>AutoRefresh.Left</c>'s, and it is
    /// arithmetic on the model.
    /// </para>
    /// <para>
    /// <b>Grey, so three channels move together.</b> Two of them moving is a
    /// hue, and a countdown that drifted towards green or red would be saying
    /// something about the read that nobody meant - this says only "soon".
    /// </para>
    /// <para>
    /// <b>The background is the line's own.</b> These columns are painted over
    /// the hint line, so anything else would draw a patch a different colour
    /// from the row it sits in.
    /// </para>
    /// <para>
    /// <b>And no <c>Faint</c>.</b> The line under this is dimmed as a whole,
    /// which is why the seconds needed their own colour at all; carrying the
    /// dimming through would put the ramp on top of the thing it exists to
    /// stand out from.
    /// </para>
    /// </remarks>
    public static Scheme Counting(Scheme basis, double left)
    {
        ArgumentNullException.ThrowIfNull(basis);

        // WHITE DOWN TO A GREY THAT IS STILL LEGIBLE. Below about a quarter
        // brightness a terminal with a light background loses it altogether,
        // and a countdown nobody can read at one second is worse than one that
        // does not fade.
        const int Dimmest = 78;

        var level = Dimmest + (int)Math.Round((255 - Dimmest) * Math.Clamp(left, 0.0, 1.0));
        var grey = new Color(level, level, level);

        return basis with
        {
            Normal = new Attribute(grey, basis.Normal.Background),
            HotNormal = new Attribute(grey, basis.Normal.Background),
        };
    }

    /// <summary>
    /// The flights table: what is over recedes, and its ending keeps its
    /// colour.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Its own entry point, because it is about one table.</b>
    /// <see cref="Table"/> above styles every table in this console the same
    /// way; this knows what the rows MEAN, and only the flights are flights.
    /// </para>
    /// <para>
    /// <b>The row recedes and the cell recedes with it</b> - the owner's call.
    /// Faint over a colour keeps the hue, so a finished row still says which
    /// ending it was, one shade quieter. Dimming the row and leaving the cell
    /// bright would have made the colour loudest on exactly the rows nobody
    /// needs to look at.
    /// </para>
    /// <para>
    /// <b>Read off the cell, not off a row object.</b> The getters are handed a
    /// table and an index, so the state is whatever is in that column of that
    /// row - which means this cannot disagree with what is on the screen.
    /// </para>
    /// </remarks>
    public static void FlightStates(TableView table, int stateColumn)
    {
        ArgumentNullException.ThrowIfNull(table);

        table.Style.RowColorGetter = args =>
            FlightLook.IsOver(Cell(args.Table, args.RowIndex, stateColumn))
                ? Receding(table.GetScheme())
                : null;

        table.Style.GetOrCreateColumnStyle(stateColumn).ColorGetter = args =>
            Tinted(args.RowScheme ?? table.GetScheme(), FlightLook.Tint(args.CellValue as string));
    }

    /// <summary>
    /// The board: a finished nomination recedes, and every row says how it is
    /// doing.
    /// </summary>
    /// <remarks>
    /// <b><see cref="FlightStates"/>'s shape over a table with two kinds of row
    /// in it</b>, and the difference is which rows recede. A nomination that
    /// came to nothing is a record, like a flight that landed - but an opened
    /// one started a flight that is running, and a watch never ends at all, so
    /// both keep their foreground however loudly they are tinted.
    /// </remarks>
    public static void BoardStates(TableView table, int stateColumn)
    {
        ArgumentNullException.ThrowIfNull(table);

        table.Style.RowColorGetter = args =>
            BoardLook.IsOver(Cell(args.Table, args.RowIndex, stateColumn))
                ? Receding(table.GetScheme())
                : null;

        table.Style.GetOrCreateColumnStyle(stateColumn).ColorGetter = args =>
            Boarded(args.RowScheme ?? table.GetScheme(), BoardLook.Tint(args.CellValue as string));
    }

    /// <summary>A board row's state cell, in the colour its word earns.</summary>
    /// <remarks>
    /// <b>The flights tab's palette, spent on the same distinctions.</b> Green
    /// for the outcome somebody wanted, yellow for a decision that went the
    /// other way, grey for a question that stopped applying, and the one red is
    /// kept for the watch that cannot do its job - the only thing on this tab
    /// that is broken rather than decided.
    /// </remarks>
    private static Scheme Boarded(Scheme basis, BoardTint tint)
    {
        if (tint is BoardTint.None)
        {
            return basis;
        }

        var colour = new Color(tint switch
        {
            BoardTint.Opened => ColorName16.Green,
            BoardTint.Refused => ColorName16.Yellow,
            BoardTint.Moot => ColorName16.DarkGray,
            BoardTint.Broken => ColorName16.Red,

            // A WATCH THAT HAS GONE SILENT, which is a warning rather than a
            // fault: it has not errored, it has simply stopped finding
            // anything, and that is how a nominator stops nominating without
            // anybody noticing.
            _ => ColorName16.Yellow,
        });

        return basis with
        {
            Normal = new Attribute(colour, basis.Normal.Background, basis.Normal.Style),
            HotNormal = new Attribute(colour, basis.HotNormal.Background, basis.HotNormal.Style),
        };
    }

    /// <summary>
    /// The fleet table: a runner that will take no work recedes, and says why.
    /// </summary>
    /// <remarks>
    /// <see cref="FlightStates"/>'s shape one tab over, and the same two
    /// decisions: the row recedes with its cell, and only the machines that
    /// will not answer are tinted.
    /// </remarks>
    public static void RunnerStates(TableView table, int stateColumn)
    {
        ArgumentNullException.ThrowIfNull(table);

        table.Style.RowColorGetter = args =>
            RunnerLook.IsAside(Cell(args.Table, args.RowIndex, stateColumn))
                ? Receding(table.GetScheme())
                : null;

        table.Style.GetOrCreateColumnStyle(stateColumn).ColorGetter = args =>
            Aside(args.RowScheme ?? table.GetScheme(), RunnerLook.Tint(args.CellValue as string));
    }

    /// <summary>The state cell of a runner that will take no work.</summary>
    /// <remarks>
    /// <b>Grey for offline and for a maintainer, yellow for parked</b> - a
    /// maintainer recedes because it takes no work, not because anything is
    /// wrong - see <c>RunnerLook</c> for
    /// why offline is not red: most offline runners are laptops that are
    /// closed, and a fault colour on every shut machine is the cry of wolf that
    /// teaches people to stop reading colour.
    /// </remarks>
    private static Scheme Aside(Scheme basis, RunnerTint tint)
    {
        if (tint is RunnerTint.None)
        {
            return basis;
        }

        var colour = new Color(tint is RunnerTint.Parked
            ? ColorName16.Yellow
            : ColorName16.DarkGray);

        return basis with
        {
            Normal = new Attribute(colour, basis.Normal.Background, basis.Normal.Style),
            HotNormal = new Attribute(colour, basis.HotNormal.Background, basis.HotNormal.Style),
        };
    }

    /// <summary>One cell, as text, or null where the table has no such place.</summary>
    private static string? Cell(ITableSource source, int row, int column) =>
        source is not null
        && row >= 0 && row < source.Rows
        && column >= 0 && column < source.Columns
            ? source[row, column] as string
            : null;

    /// <summary>A whole row, pushed back because it is over.</summary>
    private static Scheme Receding(Scheme basis) => basis with
    {
        Normal = new Attribute(basis.Normal.Foreground, basis.Normal.Background, TextStyle.Faint),
        HotNormal = new Attribute(basis.HotNormal.Foreground, basis.HotNormal.Background, TextStyle.Faint),
    };

    /// <summary>
    /// The state cell, in the colour its ending earns.
    /// </summary>
    /// <remarks>
    /// <b>The background and the style come from the row.</b> The row is
    /// already faint and already sitting on whatever the selection left behind,
    /// so taking anything but the foreground from it would draw a patch that
    /// does not belong to the line it is in.
    /// </remarks>
    private static Scheme Tinted(Scheme basis, FlightTint tint)
    {
        if (tint is FlightTint.None)
        {
            return basis;
        }

        var colour = new Color(tint switch
        {
            // GREEN IS THE ONE COLOUR NOBODY HAS TO BE TAUGHT.
            FlightTint.Landed => ColorName16.Green,

            // YELLOW, NOT RED. `grounded` is "a person stopped it" - a
            // deliberate act, and red on somebody's own decision is the cry of
            // wolf that teaches people to stop reading colour.
            FlightTint.Grounded => ColorName16.Yellow,

            // THE NON-EVENT. "The question ceased to apply" - the contract's
            // own warning is that this is the most reachable sentence in the
            // vocabulary, so it should look like nothing rather than like an
            // achievement.
            FlightTint.Withdrawn => ColorName16.DarkGray,

            // THE ONE OUTCOME THAT WANTED SOMETHING AND DID NOT GET IT.
            FlightTint.Failed => ColorName16.Red,

            // NOT AN ENDING'S COLOUR AT ALL, deliberately. No ending was
            // recorded and none can be derived - a hole in the record, and it
            // should read as wrong rather than as a result.
            _ => ColorName16.BrightMagenta,
        });

        return basis with
        {
            Normal = new Attribute(colour, basis.Normal.Background, basis.Normal.Style),
            HotNormal = new Attribute(colour, basis.HotNormal.Background, basis.HotNormal.Style),
        };
    }

    /// <summary>One attribute, pushed back.</summary>
    private static Attribute Back(Attribute attribute, Dimming how) => how switch
    {
        Dimming.Faint => new Attribute(
            attribute.Foreground, attribute.Background, TextStyle.Faint),

        Dimming.Grey => new Attribute(
            new Color(ColorName16.DarkGray), attribute.Background, attribute.Style),

        _ => attribute,
    };

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
