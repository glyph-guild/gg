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
    public static HostedPanel Next(
        HostedPanel panel,
        HostedGesture gesture,
        ReadOnlySpan<byte> typed,
        string? body = null,
        int most = 0)
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
            return panel with { Offset = Bounded(panel.Offset + by, body, most) };
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
    private static int Bounded(int offset, string? body, int most)
    {
        if (offset <= 0)
        {
            return 0;
        }

        if (body is not { Length: > 0 } text)
        {
            return offset;
        }

        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;

        // ONE ROW FOR THE HEADER, and one line of body always left on screen.
        //
        // WITHOUT A BUDGET, the floor is the last line rather than a screenful
        // of it: a caller that knows the body but not how tall the window is
        // can still stop somebody scrolling into an empty panel, which is the
        // failure worth preventing. Rows clamps again against what it is
        // actually showing.
        var floor = most > 1 ? lines - (most - 1) : lines - 1;

        return Math.Min(offset, Math.Max(floor, 0));
    }

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

        var all = (body ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // THE WINDOW THE OFFSET NAMES. Clamped again here rather than trusted,
        // because the offset is state a caller holds and the body it was
        // scrolled against can have been replaced since - the envelope is
        // re-read between sessions and the intent appears when an agent
        // submits.
        var from = Math.Clamp(panel.Offset, 0, Math.Max(all.Length - 1, 0));
        var lines = all[from..];

        if (lines.Length == 0)
        {
            // SAID RATHER THAN LEFT BLANK. A view with nothing in it and a view
            // that failed to load are the same empty panel, and only one of them
            // is a thing to go and fix.
            rows.Add(Nothing(showing));
            return rows;
        }

        // The header is however many rows it took, and one more may be needed
        // to say what was cut, so the body gets what is left after both.
        var room = most - rows.Count;
        var fits = lines.Length <= room ? lines.Length : Math.Max(room - 1, 0);

        rows.AddRange(lines.Take(fits));

        if (fits < lines.Length)
        {
            // COUNTED, BECAUSE FOUR INSTRUCTIONS OUT OF SIX READ EXACTLY LIKE
            // FOUR OUT OF FOUR. Silently truncating the rules in force is the
            // one thing this panel must not do.
            //
            // AND IT SAYS HOW TO SEE THEM, which until the body could scroll
            // was a taller window or nothing.
            rows.Add($"… {lines.Length - fits} more — scroll, or make the window taller");
        }
        else if (from > 0)
        {
            // THE OTHER END. Scrolled to the bottom, nothing else says that
            // what is on screen is not the whole of it.
            rows.Add($"… {from} above");
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

            // A WORD WIDER THAN THE ROW, which no wrap can help: it goes on
            // its own row and the painter trims what will not fit. The only
            // loss left in this function.
            if (word.Length > width)
            {
                rows.Add(word);
                continue;
            }

            row.Append(word);
        }

        if (row.Length > 0)
        {
            rows.Add(row.ToString());
        }

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
