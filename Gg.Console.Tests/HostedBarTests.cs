namespace Gg.Console.Tests;

/// <summary>
/// gg's own rows while a child owns the screen: what they show, and which
/// keystrokes gg takes to change that.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every key gg takes, the child never sees.</b> That is the whole cost of
/// this feature and it is charged to a program — Claude Code, or vim — that uses
/// nearly all of them. So the surface is one prefix and nothing else: a single
/// key bought once, opening a space of its own, rather than a handful taken from
/// the child forever.
/// </para>
/// <para>
/// <b>A type of its own rather than a <c>UiMode</c>.</b> The console's modes
/// describe the console's screen, and this is a different screen — Terminal.Gui
/// is not running at all while a child is hosted, so `ModalEscapeTests`' walk
/// over "every mode a key can open" would be asserting about a state
/// <c>AppState</c> never holds. What is worth keeping from that discipline is
/// the discipline, so the same three properties are asserted here directly: one
/// escape hatch, keys that say what they do, and letters that agree with the
/// console's where they mean the same thing.
/// </para>
/// </remarks>
public class HostedBarTests
{
    private const byte Prefix = HostedBar.Prefix;

    [Test]
    public async Task Nothing_is_taken_from_the_child_until_the_prefix()
    {
        // THE DEFAULT IS THAT GG IS NOT THERE. A hosted child is a person's
        // editor or their agent, and every byte it does not receive is a
        // keystroke that vanished for a reason they cannot see.
        foreach (var typed in (byte[])[(byte)'e', (byte)'i', 0x1b, 0x03, 0x0d, (byte)'q'])
        {
            await Assert.That(HostedBar.Takes(Shut(), HostedGesture.Typed, [typed])).IsFalse()
                .Because($"0x{typed:x2} is the child's while gg is only showing a status row.");
        }
    }

    [Test]
    public async Task The_prefix_is_the_one_key_gg_charges_the_child()
    {
        await Assert.That(HostedBar.Takes(Shut(), HostedGesture.Typed, [Prefix])).IsTrue();

        await Assert.That(HostedBar.Next(Shut(), HostedGesture.Typed, [Prefix]).Showing)
            .IsEqualTo(HostedView.Envelope)
            .Because("what a person opens this for is the rules in force - so it opens on "
                   + "them rather than on a menu asking which of two things they meant.");
    }

    [Test]
    public async Task Once_it_is_open_gg_has_the_keyboard()
    {
        // A person cannot be typing at the child and reading the envelope at
        // once, and a panel that passed some keys through would be one where
        // `e` sometimes means "show me" and sometimes reaches vim.
        foreach (var typed in (byte[])[(byte)'e', (byte)'i', (byte)'x', 0x1b, 0x0d])
        {
            await Assert.That(HostedBar.Takes(Open(), HostedGesture.Typed, [typed])).IsTrue()
                .Because($"0x{typed:x2} arrived while gg had the keyboard.");
        }
    }

    [Test]
    public async Task Escape_is_the_one_way_out_and_it_is_the_same_key_it_is_everywhere()
    {
        // The console's rule, kept by hand here because this is not a UiMode:
        // exactly one escape hatch, and it is the key it is in every other
        // modal, which is what makes it findable without being learned.
        foreach (var open in (HostedView[])[HostedView.Envelope, HostedView.Intent])
        {
            await Assert.That(
                    HostedBar.Next(new HostedPanel(open, 0), HostedGesture.Typed, [0x1b])
                        .Showing)
                .IsEqualTo(HostedView.Closed);
        }

        var ways = new byte[256].Select((_, b) => (byte)b)
            .Where(b => HostedBar.Next(Open(), HostedGesture.Typed, [b]).Showing
                     == HostedView.Closed)
            .ToList();

        await Assert.That(ways).IsEquivalentTo((byte[])[0x1b, Prefix])
            .Because("one hatch, plus the prefix closing what it opened - which is the same "
                   + "key doing the same thing rather than a second way out. Found: "
                   + string.Join(", ", ways.Select(b => $"0x{b:x2}")));
    }

    [Test]
    public async Task The_two_views_are_reachable_from_each_other()
    {
        // Without leaving and coming back, because comparing what the agent was
        // told against what it produced is the reason both are here.
        await Assert.That(HostedBar.Next(Open(), HostedGesture.Typed, [(byte)'i']).Showing)
            .IsEqualTo(HostedView.Intent);
        await Assert.That(HostedBar.Next(
                new HostedPanel(HostedView.Intent, 0), HostedGesture.Typed, [(byte)'e'])
            .Showing).IsEqualTo(HostedView.Envelope);
    }

    [Test]
    public async Task Its_letters_are_the_ones_the_console_already_uses()
    {
        // TWO KEY TABLES, AND THIS IS WHAT STOPS THEM DRIFTING. `e` opens the
        // envelope in the console; it opens the envelope here. A person does not
        // hold "which e" in their head, and if somebody moves the console's key
        // this fails rather than quietly disagreeing.
        var console = Keymap.Bindings(KeymapContext.For(new AppState()))
            .Single(b => b.Command == Command.ToggleEnvelope);

        await Assert.That(console.Key).IsEqualTo(KeyStroke.Char('e'));
        await Assert.That(HostedBar.Next(Shut(), HostedGesture.Typed, [Prefix]).Showing)
            .IsEqualTo(HostedView.Envelope);
        await Assert.That(HostedBar.Next(
                new HostedPanel(HostedView.Intent, 0), HostedGesture.Typed, [(byte)'e'])
            .Showing).IsEqualTo(HostedView.Envelope);
    }

    /// <summary>A terminal nobody has ever had fewer columns than.</summary>
    private const int Narrow = 80;

    [Test]
    public async Task A_status_wider_than_the_terminal_takes_the_rows_it_needs()
    {
        // THE DEFECT, AND IT IS SILENT. PtyScreen.Paint writes each row as
        // `text[..columns]` - a hard cut with nothing said - so a bar longer
        // than the terminal is wide simply stops. The drafting session's bar
        // is 160 characters and names three things; at eighty columns a person
        // reads the first two thirds of the first one and has no way to know
        // there was more.
        //
        // This file already knows the argument, for the BODY: "COUNTED,
        // BECAUSE FOUR INSTRUCTIONS OUT OF SIX READ EXACTLY LIKE FOUR OUT OF
        // FOUR." The status row was never given the same treatment, and it is
        // the row that is always there.
        var status = "gg · drafting the airspace — /mcp__gg__start_drafting to start · ask "
                   + "the agent to submit each document it changes · closing leaves the "
                   + "working copy as it stands";

        var rows = HostedBar.Rows(
            Shut(), status, body: "", most: 12, columns: Narrow);

        await Assert.That(rows.Count).IsGreaterThan(1)
            .Because($"the status is {status.Length} characters and the terminal is "
                   + $"{Narrow}. One row can only hold the first {Narrow} of them.");

        foreach (var row in rows)
        {
            await Assert.That(row.Length).IsLessThanOrEqualTo(Narrow)
                .Because("a row wider than the terminal is a row the painter cuts, which "
                       + "is the thing this is fixing. Row: " + row);
        }
    }

    [Test]
    public async Task Nothing_the_bar_says_is_lost_on_the_way()
    {
        // WRAPPED, NOT CUT, and the difference is every word after the first
        // eighty characters.
        var status = "gg · drafting the airspace — /mcp__gg__start_drafting to start · ask "
                   + "the agent to submit each document it changes · closing leaves the "
                   + "working copy as it stands";

        var rows = HostedBar.Rows(
            Shut(), status, body: "", most: 12, columns: Narrow);

        var back = string.Join(" ", rows.Select(row => row.TrimEnd()));

        await Assert.That(back).IsEqualTo(status)
            .Because("every word survives and none is broken across rows: a path or a "
                   + "command split down the middle is one somebody cannot type. Got:\n"
                   + back);
    }

    [Test]
    public async Task A_status_that_fits_still_takes_one_row()
    {
        // THE OTHER DIRECTION, because every row gg keeps costs the child one.
        // A bar that took three rows to say four words would be worse than the
        // truncation it replaced.
        var rows = HostedBar.Rows(
            Shut(), "gg · composing", body: "", most: 12, columns: Narrow);

        await Assert.That(rows).Count().IsEqualTo(1);
    }

    [Test]
    public async Task The_open_header_wraps_too_and_the_body_gets_what_is_left()
    {
        // THE HEADER GROWS BY WHAT THE STATUS GREW BY, since it is the status
        // plus the way out. If the body were still handed `most - 1` rows it
        // would overrun the budget by exactly the wrapping, and the rows past
        // the end are the ones the painter drops.
        var status = new string('x', Narrow) + " " + new string('y', 20);

        var rows = HostedBar.Rows(
            Open(), status, body: "one\ntwo\nthree", most: 5, columns: Narrow);

        await Assert.That(rows.Count).IsLessThanOrEqualTo(5)
            .Because("the budget is what the caller reserved from the child, and a row "
                   + "past it is painted over the child's own output.");

        foreach (var row in rows)
        {
            await Assert.That(row.Length).IsLessThanOrEqualTo(Narrow)
                .Because("including the header. Row: " + row);
        }
    }

    private static HostedPanel Open(int offset = 0) =>
        new(HostedView.Envelope, offset);

    private static HostedPanel Shut() => new(HostedView.Closed, 0);

    private static byte[] Typed(params byte[] bytes) => bytes;

    /// <summary>Twelve lines, each naming its own number.</summary>
    private static string Twelve() =>
        string.Join("\n", Enumerable.Range(1, 12).Select(n => $"line {n}"));

    [Test]
    public async Task An_open_panel_takes_every_input_there_is()
    {
        // THE RULE THIS TYPE ALREADY STATES AND THE HOST DID NOT KEEP: "All of
        // them or one of them, never some of them." Takes was total over a
        // BYTE, and PtyHost only ever offered it single-byte reads - so an
        // arrow key, which is three bytes, went straight past an open panel
        // and into the child. So did every paste, and so did every mouse
        // event once the mouse started working.
        foreach (var typed in (byte[][])[
            Typed((byte)'x'),
            Typed(0x1b, (byte)'[', (byte)'A'),
            Typed((byte)'h', (byte)'e', (byte)'l', (byte)'l', (byte)'o')])
        {
            await Assert.That(HostedBar.Takes(Open(), HostedGesture.Typed, typed)).IsTrue()
                .Because("a panel that passed some keys through would be one where `e` "
                       + "sometimes shows the envelope and sometimes reaches vim.");
        }

        foreach (var gesture in (HostedGesture[])[
            HostedGesture.Pressed, HostedGesture.ScrolledUp, HostedGesture.ScrolledDown])
        {
            await Assert.That(HostedBar.Takes(Open(), gesture, [])).IsTrue()
                .Because($"{gesture} while the panel is open is the panel's, or the child "
                       + "is being driven from behind something covering it.");
        }
    }

    [Test]
    public async Task A_closed_panel_takes_the_prefix_and_a_press_and_nothing_else()
    {
        await Assert.That(HostedBar.Takes(Shut(), HostedGesture.Typed, [HostedBar.Prefix]))
            .IsTrue();

        await Assert.That(HostedBar.Takes(Shut(), HostedGesture.Pressed, [])).IsTrue()
            .Because("the bottom row says click to open, so a click has to open it.");

        await Assert.That(HostedBar.Takes(Shut(), HostedGesture.Typed, [(byte)'x'])).IsFalse()
            .Because("every byte but the prefix is the child's while the panel is shut, "
                   + "and a keystroke that vanishes is one nobody can account for.");

        await Assert.That(HostedBar.Takes(Shut(), HostedGesture.ScrolledUp, [])).IsFalse()
            .Because("scrolling with the panel shut is the child being scrolled.");
    }

    [Test]
    public async Task A_paste_carrying_the_prefix_does_not_open_it()
    {
        // THE HEURISTIC THAT USED TO LIVE IN THE HOST, moved to where the
        // decision is. Text somebody copied must not open a panel and then be
        // typed into it - and a real keypress arrives on its own, which is
        // the whole of the test and the honest bound on it.
        var pasted = Typed((byte)'a', HostedBar.Prefix, (byte)'b');

        await Assert.That(HostedBar.Takes(Shut(), HostedGesture.Typed, pasted)).IsFalse();
    }

    [Test]
    public async Task The_wheel_scrolls_the_panel_rather_than_the_child()
    {
        var down = HostedBar.Next(Open(), HostedGesture.ScrolledDown, []);

        await Assert.That(down.Offset).IsGreaterThan(0)
            .Because("a panel that says there is more and cannot be moved is a panel "
                   + "telling you about something you cannot read.");

        var back = HostedBar.Next(down, HostedGesture.ScrolledUp, []);

        await Assert.That(back.Offset).IsEqualTo(0);
    }

    [Test]
    public async Task It_does_not_scroll_above_the_first_line()
    {
        var up = HostedBar.Next(Open(), HostedGesture.ScrolledUp, []);

        await Assert.That(up.Offset).IsEqualTo(0)
            .Because("there is nothing above the first line, and an offset below zero "
                   + "would take rows off the top of the body.");
    }

    [Test]
    public async Task The_arrows_and_j_and_k_move_it_too()
    {
        // BY KEY AS WELL AS BY WHEEL, because the wheel only exists while the
        // child has mouse reporting on - gg mirrors it and never forces it, so
        // a session hosting something that never asked has no wheel at all.
        var down = HostedBar.Next(Open(), HostedGesture.Typed, [0x1b, (byte)'[', (byte)'B']);
        await Assert.That(down.Offset).IsGreaterThan(0).Because("the down arrow.");

        var jays = HostedBar.Next(Open(), HostedGesture.Typed, [(byte)'j']);
        await Assert.That(jays.Offset).IsGreaterThan(0)
            .Because("j and k, the letters the console's own lists move by.");

        var back = HostedBar.Next(jays, HostedGesture.Typed, [(byte)'k']);
        await Assert.That(back.Offset).IsEqualTo(0);
    }

    [Test]
    public async Task Switching_views_starts_at_the_top_again()
    {
        var scrolled = HostedBar.Next(Open(offset: 4), HostedGesture.Typed, [(byte)'i']);

        await Assert.That(scrolled.Showing).IsEqualTo(HostedView.Intent);
        await Assert.That(scrolled.Offset).IsEqualTo(0)
            .Because("an offset kept across a switch would open the other view part way "
                   + "down something a person has not read the start of.");
    }

    [Test]
    public async Task Closing_forgets_where_it_was()
    {
        var shut = HostedBar.Next(Open(offset: 4), HostedGesture.Typed, [0x1b]);

        await Assert.That(shut.Showing).IsEqualTo(HostedView.Closed);
        await Assert.That(shut.Offset).IsEqualTo(0);
    }

    [Test]
    public async Task A_press_closes_an_open_panel()
    {
        var shut = HostedBar.Next(Open(), HostedGesture.Pressed, []);

        await Assert.That(shut.Showing).IsEqualTo(HostedView.Closed)
            .Because("the bottom row says click to close while it is open, so a click "
                   + "closes it - one gesture, both directions, like the prefix key.");
    }

    [Test]
    public async Task The_body_shown_is_the_window_the_offset_names()
    {
        var rows = HostedBar.Rows(
            Open(offset: 4), "gg", Twelve(), most: 5, columns: Narrow);

        var shown = string.Join("\n", rows);

        await Assert.That(shown).Contains("line 5", StringComparison.Ordinal)
            .Because("four lines scrolled past means the fifth is at the top. Shown:\n"
                   + shown);

        await Assert.That(shown).DoesNotContain("line 4", StringComparison.Ordinal)
            .Because("and the fourth is above the window. Shown:\n" + shown);
    }

    [Test]
    public async Task It_will_not_scroll_past_the_last_line()
    {
        // CLAMPED AGAINST THE BODY IT IS SHOWING, or the panel empties itself
        // and reads as a view that failed to load.
        var panel = Open();

        for (var turn = 0; turn < 40; turn++)
        {
            panel = HostedBar.Next(panel, HostedGesture.ScrolledDown, [], Twelve(), most: 5);
        }

        var shown = string.Join("\n", HostedBar.Rows(panel, "gg", Twelve(), 5, Narrow));

        await Assert.That(shown).Contains("line 12", StringComparison.Ordinal)
            .Because("the last line stays on screen however hard it is scrolled. Shown:\n"
                   + shown);
    }

    [Test]
    public async Task A_body_line_wider_than_the_terminal_wraps_like_the_status_does()
    {
        // THE STATUS WAS TAUGHT TO WRAP AND THE BODY WAS NOT. Every body line
        // is handed to the painter whole, and the painter cuts at the
        // terminal's edge - so the rules in force, which is what this panel
        // exists to show, lose the end of every long line. An envelope's
        // `when:` and `instructions:` lines are exactly the long ones.
        var wide = "obligations: " + new string('x', Narrow * 2);

        var rows = HostedBar.Rows(Open(), "gg", wide, most: 12, columns: Narrow);

        foreach (var row in rows)
        {
            await Assert.That(row.Length).IsLessThanOrEqualTo(Narrow)
                .Because("a row wider than the terminal is a row the painter cuts, and "
                       + "this is the panel that must not truncate silently. Row: " + row);
        }

        await Assert.That(string.Join("", rows)).Contains(new string('x', Narrow + 10))
            .Because("wrapped, not cut: the characters past the first screenful are the "
                   + "whole reason to scroll.");
    }

    [Test]
    public async Task Scrolling_stops_with_the_last_line_at_the_bottom()
    {
        // NOT AT THE TOP, which is where it stopped. Next clamped against the
        // line count alone, so a body could be scrolled until ONE line was
        // left in a window with room for eight - the panel emptying itself as
        // you turn the wheel, which is what "wonky" looks like.
        var panel = Open();

        for (var turn = 0; turn < 40; turn++)
        {
            panel = HostedBar.Next(
                panel, HostedGesture.ScrolledDown, [], Twelve(), most: 6, columns: Narrow);
        }

        var rows = HostedBar.Rows(panel, "gg", Twelve(), most: 6, columns: Narrow);
        var shown = string.Join("\n", rows);

        await Assert.That(shown).Contains("line 12", StringComparison.Ordinal)
            .Because("the end of the body is what scrolling to the end should show.");

        await Assert.That(shown).Contains("line 9", StringComparison.Ordinal)
            .Because("and the window stays full: six rows, one of them the header, so the "
                   + "last four lines are on screen rather than the last one. Shown:\n"
                   + shown);
    }

    [Test]
    public async Task It_says_how_much_is_above_as_well_as_below()
    {
        // BOTH ENDS, AND THE ONE THAT WAS MISSING IS THE ONE YOU NEED WHILE
        // SCROLLING. "n above" only appeared once nothing was left below, so
        // in the middle of a long body there was nothing to say where you
        // were at all.
        var rows = HostedBar.Rows(
            Open(offset: 4), "gg", Twelve(), most: 6, columns: Narrow);

        var shown = string.Join("\n", rows);

        await Assert.That(shown).Contains("above", StringComparison.OrdinalIgnoreCase)
            .Because("four lines have gone past the top. Shown:\n" + shown);

        await Assert.That(shown).Contains("more", StringComparison.OrdinalIgnoreCase)
            .Because("and there are more below. Shown:\n" + shown);
    }

    [Test]
    public async Task A_closed_panel_says_how_to_open_it_and_where()
    {
        // THE TOP ROW IS THE SESSION'S AND THE BOTTOM ROW IS GG'S OWN.
        // Everything the bar says while closed is about the session - what to
        // ask the agent, what ends it - and none of it says that there is a
        // panel at all, or that ctrl-g is what opens it. A key nobody is told
        // about is a key nobody has, and the same is true of a click.
        var footer = HostedBar.Footer(HostedView.Closed, columns: Narrow);

        await Assert.That(footer).Contains("click", StringComparison.OrdinalIgnoreCase)
            .Because("the mouse now opens it and there is nothing on the screen saying "
                   + "so. Footer: " + footer);

        await Assert.That(footer).Contains("ctrl-g", StringComparison.OrdinalIgnoreCase)
            .Because("the key has been the only way in since the panel existed and has "
                   + "never been written anywhere a person looks. Footer: " + footer);
    }

    [Test]
    public async Task The_hint_is_the_last_row_of_the_bar_at_the_top()
    {
        // ALL OF GG'S ROWS IN ONE PLACE. The hint was along the bottom of the
        // screen, which put gg on two edges with the child between them - and
        // the thing it is a hint ABOUT is at the top. A person reading the bar
        // had to look somewhere else to find out it could be opened.
        var rows = HostedBar.Rows(Shut(), "gg · drafting", body: "", most: 12,
            columns: Narrow);

        await Assert.That(rows.Count).IsGreaterThan(1)
            .Because("the status and then the hint under it.");

        await Assert.That(rows[^1]).Contains("ctrl-g", StringComparison.OrdinalIgnoreCase)
            .Because("the hint is the LAST row of the bar, against the child, which is "
                   + "where a person's eye leaves gg's rows. Rows:\n"
                   + string.Join("\n", rows));

        await Assert.That(rows[0]).DoesNotContain("ctrl-g", StringComparison.OrdinalIgnoreCase)
            .Because("and not the first, which is the session's to talk about itself.");
    }

    [Test]
    public async Task The_hint_is_inside_the_rows_the_panel_was_offered()
    {
        // IT COSTS A ROW OF THE BUDGET NOW, rather than a row of the screen.
        // A hint that came out of neither would be painted over the child.
        foreach (var most in (int[])[2, 3, 6, 12])
        {
            var rows = HostedBar.Rows(Shut(), "gg", body: "", most: most, columns: Narrow);

            await Assert.That(rows.Count).IsLessThanOrEqualTo(most)
                .Because($"offered {most} and took {rows.Count}.");
        }
    }

    [Test]
    public async Task An_open_panel_says_how_to_close_it_on_its_last_row_too()
    {
        var rows = HostedBar.Rows(Open(), "gg", body: "one\ntwo", most: 8, columns: Narrow);

        await Assert.That(rows[^1]).Contains("close", StringComparison.OrdinalIgnoreCase)
            .Because("the way out belongs where the way in was. Rows:\n"
                   + string.Join("\n", rows));
    }

    [Test]
    public async Task An_open_panel_says_how_to_close_it()
    {
        // THE ROW STAYS, AND ONLY ITS WORDS CHANGE. A footer that vanished
        // when the panel opened would move every row of the child by one at
        // the moment somebody was reading them - and the way out belongs on
        // the row that is always there as much as the way in does.
        var footer = HostedBar.Footer(HostedView.Envelope, columns: Narrow);

        await Assert.That(footer).Contains("close", StringComparison.OrdinalIgnoreCase)
            .Because("Footer: " + footer);
    }

    [Test]
    public async Task The_footer_is_centred_and_fills_the_row()
    {
        // CENTRED, because it is the one thing on the screen that is gg's
        // rather than the session's or the child's, and a hint hanging off the
        // left edge reads as part of whatever is above it.
        var footer = HostedBar.Footer(HostedView.Closed, columns: Narrow);

        await Assert.That(footer.Length).IsEqualTo(Narrow)
            .Because("a row that does not fill the width lets the child show through "
                   + "beside it, which is the defect the panel rows are padded against.");

        var left = footer.Length - footer.TrimStart().Length;
        var right = footer.Length - footer.TrimEnd().Length;

        await Assert.That(Math.Abs(left - right)).IsLessThanOrEqualTo(1)
            .Because($"centred within a character. Left {left}, right {right}.");
    }

    [Test]
    public async Task A_terminal_too_narrow_for_the_hint_still_gets_a_row()
    {
        // NARROWER THAN THE WORDS, which is a window somebody has dragged
        // small rather than a state to refuse. The row is still the width of
        // the terminal, because the padding is what stops the child showing
        // through.
        var footer = HostedBar.Footer(HostedView.Closed, columns: 12);

        await Assert.That(footer.Length).IsEqualTo(12);
    }

    [Test]
    public async Task Closed_is_one_row_and_it_is_the_status()
    {
        var rows = HostedBar.Rows(
            Shut(), "gg · composing", body: "", most: 12, columns: Narrow);

        await Assert.That(rows).Count().IsEqualTo(1);
        await Assert.That(rows[0]).StartsWith("gg · composing", StringComparison.Ordinal);
    }

    [Test]
    public async Task Open_says_what_it_is_showing_and_how_to_leave()
    {
        // A panel that appeared with no way out named is one somebody quits the
        // whole session to escape.
        var rows = HostedBar.Rows(
            Open(), "gg · composing", body: "keep the diff small",
            most: 12, columns: Narrow);

        await Assert.That(rows.Count).IsGreaterThan(1);
        await Assert.That(rows[0]).Contains("esc", StringComparison.OrdinalIgnoreCase)
            .Because("the one way out is named on the row that is always there.");
        await Assert.That(string.Join("\n", rows))
            .Contains("keep the diff small", StringComparison.Ordinal);
    }

    [Test]
    public async Task It_never_takes_more_rows_than_it_was_given()
    {
        // The panel costs the child a row each. Taking more than gg was offered
        // would leave a child with a negative screen, which is not a thing a pty
        // can be told about.
        var many = string.Join("\n", Enumerable.Range(0, 200).Select(i => $"line {i}"));

        foreach (var most in (int[])[1, 2, 5, 12])
        {
            var rows = HostedBar.Rows(Open(), "gg", many, most, Narrow);

            await Assert.That(rows.Count).IsLessThanOrEqualTo(most)
                .Because($"it was offered {most} rows and took {rows.Count}.");
            await Assert.That(rows.Count).IsGreaterThan(0);
        }
    }

    [Test]
    public async Task A_body_it_has_no_room_for_says_so_rather_than_stopping_mid_sentence()
    {
        // Silently truncating the rules in force is the one thing this panel
        // must not do: a person reading four of six instructions has no way to
        // know there were six.
        var six = string.Join("\n", Enumerable.Range(1, 6).Select(i => $"instruction {i}"));
        var rows = HostedBar.Rows(Open(), "gg", six, most: 4, columns: Narrow);

        await Assert.That(string.Join(" ", rows)).Contains("more", StringComparison.OrdinalIgnoreCase)
            .Because("what is cut has to be counted, or a truncated envelope reads as a "
                   + "complete one. Rows: " + string.Join(" | ", rows));
    }

    [Test]
    public async Task Nothing_to_show_is_said_rather_than_left_blank()
    {
        // An envelope that has not been read and one with no instructions look
        // identical as a blank panel, and the first is a thing to go and fix.
        var rows = HostedBar.Rows(new HostedPanel(HostedView.Intent, 0), "gg", body: "", most: 8,
            columns: Narrow);

        await Assert.That(rows.Count).IsGreaterThan(1);
        await Assert.That(string.Join(" ", rows).Trim()).IsNotEmpty();
    }
}
