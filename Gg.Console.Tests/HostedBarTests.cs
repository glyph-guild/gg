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
            await Assert.That(HostedBar.Takes(HostedView.Closed, typed)).IsFalse()
                .Because($"0x{typed:x2} is the child's while gg is only showing a status row.");
        }
    }

    [Test]
    public async Task The_prefix_is_the_one_key_gg_charges_the_child()
    {
        await Assert.That(HostedBar.Takes(HostedView.Closed, Prefix)).IsTrue();

        await Assert.That(HostedBar.Next(HostedView.Closed, Prefix))
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
            await Assert.That(HostedBar.Takes(HostedView.Envelope, typed)).IsTrue()
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
            await Assert.That(HostedBar.Next(open, 0x1b)).IsEqualTo(HostedView.Closed);
        }

        var ways = new byte[256].Select((_, b) => (byte)b)
            .Where(b => HostedBar.Next(HostedView.Envelope, b) == HostedView.Closed)
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
        await Assert.That(HostedBar.Next(HostedView.Envelope, (byte)'i'))
            .IsEqualTo(HostedView.Intent);
        await Assert.That(HostedBar.Next(HostedView.Intent, (byte)'e'))
            .IsEqualTo(HostedView.Envelope);
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
        await Assert.That(HostedBar.Next(HostedView.Closed, Prefix)).IsEqualTo(HostedView.Envelope);
        await Assert.That(HostedBar.Next(HostedView.Intent, (byte)'e')).IsEqualTo(HostedView.Envelope);
    }

    [Test]
    public async Task Closed_is_one_row_and_it_is_the_status()
    {
        var rows = HostedBar.Rows(HostedView.Closed, "gg · composing", body: "", most: 12);

        await Assert.That(rows).Count().IsEqualTo(1);
        await Assert.That(rows[0]).StartsWith("gg · composing", StringComparison.Ordinal);
    }

    [Test]
    public async Task Open_says_what_it_is_showing_and_how_to_leave()
    {
        // A panel that appeared with no way out named is one somebody quits the
        // whole session to escape.
        var rows = HostedBar.Rows(
            HostedView.Envelope, "gg · composing", body: "keep the diff small", most: 12);

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
            var rows = HostedBar.Rows(HostedView.Envelope, "gg", many, most);

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
        var rows = HostedBar.Rows(HostedView.Envelope, "gg", six, most: 4);

        await Assert.That(string.Join(" ", rows)).Contains("more", StringComparison.OrdinalIgnoreCase)
            .Because("what is cut has to be counted, or a truncated envelope reads as a "
                   + "complete one. Rows: " + string.Join(" | ", rows));
    }

    [Test]
    public async Task Nothing_to_show_is_said_rather_than_left_blank()
    {
        // An envelope that has not been read and one with no instructions look
        // identical as a blank panel, and the first is a thing to go and fix.
        var rows = HostedBar.Rows(HostedView.Intent, "gg", body: "", most: 8);

        await Assert.That(rows.Count).IsGreaterThan(1);
        await Assert.That(string.Join(" ", rows).Trim()).IsNotEmpty();
    }
}
