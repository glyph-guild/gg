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
public static class HostedBar
{
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

    /// <summary>Whether gg takes this byte rather than passing it to the child.</summary>
    /// <remarks>
    /// <b>All of them or one of them, never some of them.</b> Closed, gg takes
    /// only the prefix — every other byte is the child's, and a keystroke that
    /// vanishes is one a person cannot account for. Open, gg takes everything:
    /// a panel that passed some keys through would be one where <c>e</c>
    /// sometimes shows the envelope and sometimes reaches vim.
    /// </remarks>
    public static bool Takes(HostedView showing, byte typed) =>
        showing != HostedView.Closed || typed == Prefix;

    /// <summary>What gg shows after this byte.</summary>
    public static HostedView Next(HostedView showing, byte typed) => showing switch
    {
        // OPENS ON THE ENVELOPE rather than on a menu asking which of two
        // things was meant. What a person opens this for is the rules in force
        // — whether the agent actually got the instructions is the question they
        // cannot answer any other way.
        HostedView.Closed => typed == Prefix ? HostedView.Envelope : HostedView.Closed,

        // The prefix closes what it opened. That is the same key doing the same
        // thing rather than a second way out, which is why the escape-hatch rule
        // still holds with two bytes in the answer.
        _ when typed is Esc or Prefix => HostedView.Closed,

        _ => typed switch
        {
            // REACHABLE FROM EACH OTHER, without leaving and coming back:
            // comparing what the agent was told against what it produced is the
            // reason both are here.
            (byte)'e' => HostedView.Envelope,
            (byte)'i' => HostedView.Intent,
            _ => showing,
        },
    };

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
        HostedView showing, string status, string body, int most, int columns)
    {
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

        var lines = (body ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries);

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
            rows.Add($"… {lines.Length - fits} more — make the window taller to read them");
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
