namespace Gg.Console;

/// <summary>Which of gg's rows are open, and on what.</summary>
public enum HostedView
{
    /// <summary>One row, saying what ends the session. The child has the keyboard.</summary>
    Closed,

    /// <summary>The rules in force, which is what somebody opens this for.</summary>
    Envelope,

    /// <summary>What was handed back, once anything was.</summary>
    Intent,
}

/// <summary>
/// gg's own rows while a child owns the screen, and the keystrokes that change
/// them.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every key gg takes, the child never sees.</b> That is what this feature
/// costs, and it is charged to a program that uses nearly all of them. So the
/// surface is one prefix: a single key bought once, opening a space of its own,
/// rather than a handful taken from the child forever.
/// </para>
/// <para>
/// <b>Pure, so it is answerable without a terminal.</b> Which key does what, and
/// what ends up on the screen, are questions about values — the same reason
/// <see cref="PtyScreen"/> returns a frame instead of writing one, and the
/// reason four terminal defects stopped needing a person at a keyboard to find.
/// </para>
/// <para>
/// <b>Not a <see cref="UiMode"/>, deliberately.</b> The console's modes describe
/// the console's screen and this is a different one — Terminal.Gui is not
/// running while a child is hosted, so the walk over "every mode a key can open"
/// would be asserting about a state <c>AppState</c> never holds. What carries
/// over is the discipline rather than the machinery: one escape hatch, keys that
/// say what they do, and letters that agree with the console's.
/// </para>
/// </remarks>
/// <summary>What a person did, once gg knows where they did it.</summary>
/// <remarks>
/// <b>THE HOST KNOWS GEOMETRY AND THE BAR KNOWS MEANING.</b> Whether a click
/// landed on gg's rows is a question about how tall the bar is, which only
/// <c>PtyHost</c> can answer; what a click MEANS is a question about whether
/// the panel is open, which only this type can. Passing raw bytes would make
/// each of them answer the other's question.
/// </remarks>
public enum HostedGesture
{
    /// <summary>Bytes from the keyboard, however many.</summary>
    Typed,

    /// <summary>A button went down on a row that is gg's.</summary>
    Pressed,

    /// <summary>The wheel turned on a row that is gg's.</summary>
    ScrolledUp,

    /// <summary>The wheel turned the other way, on a row that is gg's.</summary>
    ScrolledDown,
}

/// <summary>What gg is showing, and how far down it.</summary>
/// <param name="Showing">Which view, if any, is open.</param>
/// <param name="Offset">How many lines of the body have scrolled past the top.</param>
public readonly record struct HostedPanel(HostedView Showing, int Offset);

public static class HostedBar
{
    /// <summary>How far one turn of the wheel or one arrow moves the body.</summary>
    /// <remarks>
    /// Three for the wheel is what every terminal does; one for a key is what
    /// an arrow means. A wheel that moved one line would be a wheel somebody
    /// has to spin, and an arrow that moved three would overshoot the line
    /// they were reading.
    /// </remarks>
    private const int Notch = 3;

    private const int Step = 1;

    /// <summary>How far a page key moves it.</summary>
    private const int Page = 10;

    /// <summary>The one key gg charges the child: <c>ctrl-g</c>.</summary>
    /// <remarks>
    /// <b>Chosen for what it costs rather than for what it stands for.</b> In
    /// readline it aborts and in vim it reports the file name — both things
    /// somebody presses about once a year. <c>ctrl-c</c>, <c>ctrl-d</c> and
    /// <c>ctrl-z</c> were never candidates; <c>ctrl-]</c> looked free until
    /// Claude Code turned out to use it, which is the argument for measuring
    /// this against the actual child rather than reasoning about it.
    /// <para>
    /// That `g` is what gg is named after twice is a mnemonic, not a reason.
    /// </para>
    /// </remarks>
    public const byte Prefix = 0x07;

    /// <summary>Escape: the one way out, as it is in every other modal.</summary>
    private const byte Esc = 0x1b;

    /// <summary>Whether gg takes this rather than passing it to the child.</summary>
    /// <remarks>
    /// <para>
    /// <b>All of them or one of them, never some of them.</b> Closed, gg takes
    /// the prefix and a press on its own rows — everything else is the
    /// child's, and a keystroke that vanishes is one a person cannot account
    /// for. Open, gg takes EVERYTHING: a panel that passed some keys through
    /// would be one where <c>e</c> sometimes shows the envelope and sometimes
    /// reaches vim.
    /// </para>
    /// <para>
    /// <b>This was true of the byte and false of the session.</b> The host
    /// only ever offered single-byte reads, so an arrow key — three bytes —
    /// went past an open panel into the child, and so did every paste. The
    /// rule is stated over whole reads now, which is the only shape in which
    /// it can hold.
    /// </para>
    /// <para>
    /// <b>A REAL KEYPRESS ARRIVES ALONE, and that rule lives here.</b> It is
    /// about whether the prefix OPENS the panel, not about read lengths in a
    /// forwarding loop: text somebody copied must not open a panel and then be
    /// typed into it. Not perfect, and the honest bound on what this does.
    /// </para>
    /// </remarks>
    public static bool Takes(HostedPanel panel, HostedGesture gesture, ReadOnlySpan<byte> typed)
    {
        if (panel.Showing != HostedView.Closed)
        {
            return true;
        }

        return gesture switch
        {
            HostedGesture.Pressed => true,
            HostedGesture.Typed => typed.Length == 1 && typed[0] == Prefix,
            _ => false,
        };
    }

    /// <summary>What gg shows after this.</summary>
    /// <param name="panel">What it is showing now, and how far down.</param>
    /// <param name="gesture">What the person did.</param>
    /// <param name="typed">The bytes, when they typed.</param>
    /// <param name="body">
    /// The text being shown, so scrolling can be stopped at its end. Absent
    /// where the caller has none to hand, which only leaves the bottom
    /// unclamped.
    /// </param>
    /// <param name="most">How many rows the panel may take.</param>
    /// <param name="columns">
    /// How wide a row is, because a long line is several rows and how far a
    /// body scrolls is a question about rows.
    /// </param>
    public static HostedPanel Next(
        HostedPanel panel,
        HostedGesture gesture,
        ReadOnlySpan<byte> typed,
        string? body = null,
        int most = 0,
        int columns = 0)
    {
        if (panel.Showing == HostedView.Closed)
        {
            // OPENS ON THE ENVELOPE rather than on a menu asking which of two
            // things was meant. What a person opens this for is the rules in
            // force — whether the agent actually got the instructions is the
            // question they cannot answer any other way.
            return Takes(panel, gesture, typed)
                ? new HostedPanel(HostedView.Envelope, 0)
                : panel;
        }

        // A PRESS CLOSES WHAT A PRESS OPENED, and so does the prefix. The
        // bottom row says so in both directions, and one gesture doing one
        // thing is why the escape-hatch rule survives having a mouse.
        if (gesture == HostedGesture.Pressed)
        {
            return new HostedPanel(HostedView.Closed, 0);
        }

        if (Scrolled(gesture, typed) is { } by)
        {
            return panel with { Offset = Bounded(panel.Offset + by, body, most, columns) };
        }

        if (gesture != HostedGesture.Typed || typed.Length != 1)
        {
            // ANYTHING ELSE IS SWALLOWED RATHER THAN ACTED ON. It is taken -
            // the panel owns the keyboard - and taken is not the same as
            // meaning something.
            return panel;
        }

        return typed[0] switch
        {
            Esc or Prefix => new HostedPanel(HostedView.Closed, 0),

            // REACHABLE FROM EACH OTHER, without leaving and coming back:
            // comparing what the agent was told against what it produced is
            // the reason both are here. FROM THE TOP, because an offset kept
            // across a switch opens the other view part way down something
            // nobody has read the start of.
            (byte)'e' => new HostedPanel(HostedView.Envelope, 0),
            (byte)'i' => new HostedPanel(HostedView.Intent, 0),
            _ => panel,
        };
    }

    /// <summary>How far this moves the body, or null when it does not.</summary>
    /// <remarks>
    /// <b>The wheel and the keyboard both, because only one of them is always
    /// there.</b> gg mirrors the child's mouse reporting and never forces it,
    /// so a session hosting something that never asked for a mouse has no
    /// wheel — and the panel would then say "… n more" about something
    /// unreachable.
    /// </remarks>
    private static int? Scrolled(HostedGesture gesture, ReadOnlySpan<byte> typed)
    {
        if (gesture == HostedGesture.ScrolledDown)
        {
            return Notch;
        }

        if (gesture == HostedGesture.ScrolledUp)
        {
            return -Notch;
        }

        if (gesture != HostedGesture.Typed)
        {
            return null;
        }

        if (typed.Length == 1)
        {
            return typed[0] switch
            {
                // THE CONSOLE'S OWN LETTERS. Its lists move by j and k, and a
                // different pair one layer down is a pair to remember.
                (byte)'j' => Step,
                (byte)'k' => -Step,
                _ => null,
            };
        }

        // ARROWS AND PAGES, which arrive as escape sequences and are exactly
        // what used to slip past an open panel into the child.
        if (typed.Length == 3 && typed[0] == Esc && typed[1] == '[')
        {
            return typed[2] switch
            {
                (byte)'B' => Step,
                (byte)'A' => -Step,
                _ => null,
            };
        }

        if (typed.Length == 4 && typed[0] == Esc && typed[1] == '[' && typed[3] == '~')
        {
            return typed[2] switch
            {
                (byte)'6' => Page,
                (byte)'5' => -Page,
                _ => null,
            };
        }

        return null;
    }

    /// <summary>An offset the body can actually be read at.</summary>
    /// <remarks>
    /// <b>Clamped at both ends.</b> Above the first line there is nothing, and
    /// past the last the panel empties itself — which reads as a view that
    /// failed to load rather than as one scrolled too far.
    /// </remarks>
    private static int Bounded(int offset, string? body, int most, int columns)
    {
        if (offset <= 0)
        {
            return 0;
        }

        if (body is not { Length: > 0 })
        {
            return offset;
        }

        // THE LAST LINE COMES TO REST AT THE BOTTOM, not at the top. Clamping
        // to "one line left" empties the panel as the wheel turns, which is
        // what it did: a window with room for eight could be scrolled until
        // one was in it.
        //
        // COUNTED IN DISPLAY ROWS, because a long line is several and the
        // render counts them that way. A clamp counting source lines stops
        // early on exactly the documents worth scrolling.
        var rows = Displayed(body, columns > 0 ? columns : int.MaxValue).Count;

        return Math.Min(offset, Math.Max(rows - Room(most), 0));
    }

    /// <summary>How many rows of body a panel of this height shows.</summary>
    /// <remarks>
    /// One for the header at least, and one more for whichever of "… above"
    /// or "… more" is on screen. Approximate on purpose: the header wraps, so
    /// the exact number is not known until it is rendered — and <c>Rows</c>
    /// clamps again against what it actually has room for.
    /// </remarks>
    private static int Room(int most) => Math.Max(most - 2, 1);

    /// <summary>
    /// The rows gg keeps: the status, and what is open under it.
    /// </summary>
    /// <param name="showing">Which view, if any, is open.</param>
    /// <param name="status">What the session itself has to say, always row one.</param>
    /// <param name="body">The open view's text, newline separated.</param>
    /// <param name="most">
    /// How many rows gg may take. Each one costs the child a row, so this is the
    /// caller's budget rather than this type's choice.
    /// </param>
    /// <param name="columns">
    /// How wide one row is.
    /// </param>
    /// <remarks>
    /// <b>THE STATUS WRAPS, because the painter cuts and says nothing.</b>
    /// <c>PtyScreen.Paint</c> writes every row as <c>text[..columns]</c>, so a
    /// status longer than the terminal is wide used to stop mid-word — the
    /// drafting session's names three things in 160 characters and an eighty
    /// column terminal showed the first two thirds of the first. The body has
    /// been counted and reported since it was written; the row that is ALWAYS
    /// there was the one nothing looked after.
    /// </remarks>
    public static IReadOnlyList<string> Rows(
        HostedPanel panel, string status, string body, int most, int columns)
    {
        var showing = panel.Showing;

        if (showing == HostedView.Closed || most <= 1)
        {
            return Wrapped(status, columns, most <= 0 ? 1 : most);
        }

        // THE WAY OUT IS ON THE ROW THAT IS ALWAYS THERE. A panel that appeared
        // with no exit named is one somebody quits the whole session to escape.
        //
        // AND IT WRAPS WITH THE STATUS IT IS PART OF, which is why the body's
        // budget below is what is left after the header rather than after one
        // row: a header that grew and a body that did not notice would push
        // rows past the budget, and the painter drops those onto the child.
        var rows = new List<string>(Wrapped(
            $"{status}  ·  {Name(showing)}  ·  e envelope · i intent · esc close",
            columns,
            Math.Max(most - 1, 1)));

        // WRAPPED, LIKE THE STATUS ABOVE IT. Handed over whole, a long line is
        // cut at the terminal's edge by the painter - and the rules in force
        // are what this panel exists to show.
        var all = Displayed(body, columns);

        if (all.Count == 0)
        {
            // SAID RATHER THAN LEFT BLANK. A view with nothing in it and a view
            // that failed to load are the same empty panel, and only one of them
            // is a thing to go and fix.
            rows.Add(Nothing(showing));
            return rows;
        }

        // WHAT IS LEFT AFTER THE HEADER, which wraps and so is not one row.
        // Nothing left means the header filled the budget: a body row added
        // here is a row painted over the child.
        var available = most - rows.Count;

        if (available <= 0)
        {
            return rows;
        }

        // THE MARKER COMES OUT OF THE BUDGET, not on top of it. It is only
        // needed when something is off screen, which is not known until the
        // window is chosen - so the room is reserved when the body cannot
        // fit whole from the top, and given back when it can.
        var whole = Displayed(body, columns).Count <= available && panel.Offset <= 0;
        var room = whole ? available : Math.Max(available - 1, 0);

        // THE WINDOW THE OFFSET NAMES, clamped here rather than trusted: the
        // offset is state a caller holds and the body can have been replaced
        // since - the envelope is re-read between sessions and the intent
        // appears when an agent submits.
        //
        // CLAMPED SO THE LAST ROWS FILL THE WINDOW rather than leaving one
        // line in it. Scrolled to the end, what you want is the end on screen,
        // not the end at the top of an otherwise empty panel.
        var from = Math.Clamp(panel.Offset, 0, Math.Max(all.Count - room, 0));
        var lines = all[from..];
        var fits = Math.Min(lines.Count, room);

        rows.AddRange(lines.Take(fits));

        // BOTH ENDS, ON ONE ROW. Counted, because four instructions out of six
        // read exactly like four out of four - and while somebody is moving,
        // how far they have come is as much of the answer as how much is left.
        // "n above" used to appear only once nothing was left below, so in the
        // middle of a long body nothing on screen said anything had gone past
        // the top. One row rather than two, because every row gg keeps is one
        // the child does not have.
        var below = lines.Count - fits;

        if (below > 0 || from > 0)
        {
            rows.Add(string.Join("  ·  ", (string[])
            [
                .. from > 0 ? (string[])[$"… {from} above"] : [],
                .. below > 0
                    ? (string[])[$"… {below} more — scroll, or make the window taller"]
                    : [],
            ]));
        }

        return rows;
    }

    /// <summary>One line of text, as the rows a terminal that wide can show.</summary>
    /// <remarks>
    /// <para>
    /// <b>On spaces, never mid-word.</b> A path or a command broken across two
    /// rows is one nobody can read off the screen and type — and the things
    /// this bar says are mostly commands.
    /// </para>
    /// <para>
    /// <b>A word longer than the row is cut, because there is nothing else to
    /// do with it</b> — and it is the only case where anything is lost, which
    /// is a far narrower claim than the one this replaces.
    /// </para>
    /// <para>
    /// <b>Bounded by the budget.</b> Rows past what the caller reserved are
    /// painted over the child's own output, so the last row gg keeps says how
    /// much it could not show rather than running on.
    /// </para>
    /// </remarks>
    private static IReadOnlyList<string> Wrapped(string text, int columns, int most)
    {
        var rows = WrapOne(text, columns);

        if (rows.Count <= most)
        {
            return rows;
        }

        // SAID RATHER THAN DROPPED, the way the body already does it. A bar
        // that quietly stopped at the budget would be the defect this method
        // exists to remove, one layer along.
        var kept = rows.Take(Math.Max(most - 1, 0)).ToList();
        kept.Add($"… {rows.Count - kept.Count} more — make the window taller");

        return kept;
    }

    /// <summary>One line of text, as the rows a terminal that wide can show.</summary>
    /// <remarks>
    /// <b>Uncapped, because the body needs every row to count them.</b> How
    /// far a body can be scrolled is a question about DISPLAY rows rather than
    /// source lines — a single long line is several rows, and clamping
    /// against the source count stops the scroll early on exactly the
    /// documents worth scrolling.
    /// </remarks>
    private static List<string> WrapOne(string text, int columns)
    {
        var width = Math.Max(columns, 1);

        if ((text ?? "").Length <= width)
        {
            return [text ?? ""];
        }

        var rows = new List<string>();
        var row = new System.Text.StringBuilder();

        foreach (var word in text!.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var next = row.Length == 0 ? word : $"{row} {word}";

            if (next.Length <= width)
            {
                row.Clear().Append(next);
                continue;
            }

            if (row.Length > 0)
            {
                rows.Add(row.ToString());
                row.Clear();
            }

            // A WORD WIDER THAN THE ROW IS BROKEN ACROSS ROWS. It used to
            // go on one row and be trimmed by the painter, which was a
            // tolerable escape hatch for a status line and is not one for a
            // body: an envelope carries globs, paths and conditions that are
            // one long token, and losing the end of them is losing the part
            // that says which files.
            //
            // Nothing is lost in this function now. Breaking mid-word is
            // ugly and readable; cutting mid-word is neither.
            if (word.Length > width)
            {
                for (var at = 0; at < word.Length; at += width)
                {
                    rows.Add(word[at..Math.Min(at + width, word.Length)]);
                }

                continue;
            }

            row.Append(word);
        }

        if (row.Length > 0)
        {
            rows.Add(row.ToString());
        }

        return rows;
    }

    /// <summary>The body as the rows a terminal that wide will show it in.</summary>
    /// <remarks>
    /// <b>One place, because the scroll and the render have to agree.</b> If
    /// the clamp counted source lines and the render counted display rows, the
    /// end of a wrapped body would be unreachable — which is the shape the
    /// offset was wrong in.
    /// </remarks>
    private static List<string> Displayed(string? body, int columns) =>
        [.. (body ?? "")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .SelectMany(line => WrapOne(line, columns))];

    /// <summary>
    /// The row along the bottom, which is the only thing on screen that is
    /// gg's own.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>NOTHING SAID THE PANEL EXISTED.</b> The top row is the SESSION's —
    /// what to ask the agent, what ends it, where the working copy is — and
    /// none of that says there is anything to open or that <c>ctrl-g</c> is
    /// what opens it. The key has been the only way in since the panel was
    /// written and has never appeared anywhere a person looks; the click that
    /// now opens it is just as invisible.
    /// </para>
    /// <para>
    /// <b>Centred, and the full width.</b> Centred because this row is gg
    /// speaking rather than the session or the child, and a hint against the
    /// left edge reads as part of whatever is above it. Full width because an
    /// unpadded row lets the child show through beside it, which is the same
    /// reason every panel row is padded.
    /// </para>
    /// <para>
    /// <b>It stays when the panel opens and only its words change.</b> A row
    /// that came and went would move every row of the child by one at the
    /// moment somebody was reading them — and the way out belongs on the row
    /// that is always there as much as the way in does.
    /// </para>
    /// </remarks>
    public static string Footer(HostedView showing, int columns)
    {
        var text = showing == HostedView.Closed
            ? "click to open or press ctrl-g"
            : "click or press esc to close";

        var width = Math.Max(columns, 0);

        if (text.Length >= width)
        {
            // A WINDOW DRAGGED SMALLER THAN THE WORDS is somebody resizing,
            // not a state to refuse. The row is still the width of the
            // terminal, because the padding is what keeps the child out of it.
            return text.Length > width ? text[..width] : text;
        }

        var left = (width - text.Length) / 2;

        return text.PadLeft(left + text.Length).PadRight(width);
    }

    private static string Name(HostedView showing) => showing switch
    {
        HostedView.Envelope => "the rules in force",
        HostedView.Intent => "what was handed back",
        _ => "",
    };

    private static string Nothing(HostedView showing) => showing switch
    {
        HostedView.Envelope =>
            "No envelope has been read, so nothing here says what governs this flight.",
        HostedView.Intent =>
            "Nothing has been handed back yet. An agent submits once you and it are happy.",
        _ => "",
    };
}
