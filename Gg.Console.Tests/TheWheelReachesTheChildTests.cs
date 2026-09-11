using System.Text;
using Gg.Console;
using XTerm.Input;

namespace Gg.Console.Tests;

/// <summary>
/// The mouse reaches the child, and a click on gg's own rows opens gg's panel.
/// </summary>
/// <remarks>
/// <para>
/// <b>WHY THE WHEEL WAS DEAD.</b> gg does not hand the terminal to the child —
/// it runs it in a pty it owns and re-renders its screen through an emulator.
/// So every escape sequence the child writes lands on the EMULATOR, and a
/// child asking for mouse reporting is asking something that never reaches the
/// real terminal. Claude Code asks for <c>1000 1002 1003 1004 1006 2004</c> on
/// startup, which are exactly the modes <c>PtyHost</c> turns off before
/// spawning it and never turns back on. There were no mouse bytes to forward
/// because nothing had asked the terminal to send any.
/// </para>
/// <para>
/// <b>MIRRORED, NEVER FORCED.</b> gg turns mouse reporting on only because the
/// child did. Forcing it would take drag-to-select away from a person in a
/// session that never wanted it, and an editor that does not understand mouse
/// reports would receive escape sequences it cannot read and put them in the
/// buffer. The emulator already tracks what the child asked for, so the mirror
/// is a fact gg copies rather than a policy it invents.
/// </para>
/// <para>
/// <b>THE ROW OFFSET IS THE PART THAT WOULD BE SILENTLY WRONG.</b> gg paints
/// its bar on the top rows and the child's screen below. A terminal reports
/// the row a person actually clicked; the child believes its own first row is
/// row one. Forwarded untranslated, every click lands one to three rows off —
/// and now that the bar wraps, by a number that changes with the window's
/// width.
/// </para>
/// <para>
/// <b>A click on gg's rows is the prefix key.</b> Not a second state machine:
/// it goes through <c>took</c> as though the prefix had been pressed, so the
/// click and the keystroke take one path through <c>HostedBar.Next</c> and
/// cannot come to disagree about what open means.
/// </para>
/// </remarks>
public class TheWheelReachesTheChildTests
{
    private const string Esc = "\u001b";

    private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);

    private static string Text(ReadOnlyMemory<byte> bytes) =>
        Encoding.UTF8.GetString(bytes.Span);

    /// <summary>An SGR report: button, column, row, and pressed or released.</summary>
    private static byte[] Sgr(int button, int column, int row, bool pressed = true) =>
        Bytes($"{Esc}[<{button};{column};{row}{(pressed ? 'M' : 'm')}");

    [Test]
    public async Task A_wheel_below_the_bar_reaches_the_child_on_the_row_it_belongs_to()
    {
        // 64 is wheel-up in SGR. The person is pointing at real row 9 and the
        // bar is three rows deep, so the child's own row is six.
        var read = MouseInput.Read(Sgr(64, 40, 9), barRows: 3);

        await Assert.That(read.Kind).IsEqualTo(MouseReading.Forward);
        await Assert.That(Text(read.Bytes)).IsEqualTo($"{Esc}[<64;40;6M")
            .Because("the child believes its first row is row one, and gg's bar is above "
                   + "it. Forwarded untranslated, everything lands three rows out.");
    }

    [Test]
    public async Task A_click_below_the_bar_is_translated_the_same_way()
    {
        var read = MouseInput.Read(Sgr(0, 12, 4), barRows: 1);

        await Assert.That(read.Kind).IsEqualTo(MouseReading.Forward);
        await Assert.That(Text(read.Bytes)).IsEqualTo($"{Esc}[<0;12;3M");
    }

    [Test]
    public async Task A_release_is_translated_too_and_keeps_its_letter()
    {
        // A release is the same report with a lower-case m. Dropping it leaves
        // the child believing the button is still down.
        var read = MouseInput.Read(Sgr(0, 12, 4, pressed: false), barRows: 1);

        await Assert.That(read.Kind).IsEqualTo(MouseReading.Forward);
        await Assert.That(Text(read.Bytes)).IsEqualTo($"{Esc}[<0;12;3m");
    }

    [Test]
    public async Task A_click_on_the_bar_is_the_prefix_key()
    {
        foreach (var row in (int[])[1, 2, 3])
        {
            var read = MouseInput.Read(Sgr(0, 5, row), barRows: 3);

            await Assert.That(read.Kind).IsEqualTo(MouseReading.Toggle)
                .Because($"row {row} is gg's, and the panel it opens is what a person is "
                       + "reaching for when they click a bar that says there is more.");
        }
    }

    [Test]
    public async Task Moving_over_the_bar_is_not_clicking_it()
    {
        // THE BAR POPPED OPEN ON HOVER, and this is why. Claude asks for 1003,
        // any-event tracking, so the terminal reports every MOVEMENT of the
        // pointer and not only its buttons - and a motion report ends in `M`
        // exactly as a press does. Read as a press, every pixel of travel
        // across gg's rows toggled the panel.
        //
        // The bit is 32. SGR packs the button in the low two bits and then
        // flags above them: 4 shift, 8 meta, 16 control, 32 MOTION, 64 wheel.
        // So 35 is "moved with nothing held" - a 3 that is not a button at all
        // - and 32 is "moved with the left button down", which is a drag and
        // still not a click.
        foreach (var button in (int[])[32, 33, 34, 35])
        {
            var read = MouseInput.Read(Sgr(button, 5, 2), barRows: 3);

            await Assert.That(read.Kind).IsEqualTo(MouseReading.Nothing)
                .Because($"button {button} has the motion bit set, so the pointer went "
                       + "over the bar rather than being pressed on it.");
        }
    }

    [Test]
    public async Task A_modifier_held_down_is_still_a_click()
    {
        // AND THE OTHER DIRECTION, because the fix is a bit test and the easy
        // wrong version tests the whole number. 4, 8 and 16 are shift, meta
        // and control - somebody holding one of those and clicking has still
        // clicked.
        foreach (var button in (int[])[4, 8, 16])
        {
            var read = MouseInput.Read(Sgr(button, 5, 2), barRows: 3);

            await Assert.That(read.Kind).IsEqualTo(MouseReading.Toggle)
                .Because($"button {button} is the left button with a modifier held, which "
                       + "is a press.");
        }
    }

    [Test]
    public async Task Moving_below_the_bar_still_reaches_the_child()
    {
        // MOTION IS THE CHILD'S BUSINESS WHEREVER THE CHILD IS. It asked for
        // any-event tracking, so dropping motion outside gg's rows would take
        // away the hover highlighting it turned the mode on for.
        var read = MouseInput.Read(Sgr(35, 5, 9), barRows: 3);

        await Assert.That(read.Kind).IsEqualTo(MouseReading.Forward);
        await Assert.That(Text(read.Bytes)).IsEqualTo($"{Esc}[<35;5;6M");
    }

    [Test]
    public async Task Releasing_on_the_bar_does_nothing_so_one_click_is_one_toggle()
    {
        var read = MouseInput.Read(Sgr(0, 5, 2, pressed: false), barRows: 3);

        await Assert.That(read.Kind).IsEqualTo(MouseReading.Nothing)
            .Because("a press and a release are two reports. Acting on both would open the "
                   + "panel and close it again before a finger left the button.");
    }

    [Test]
    public async Task The_wheel_over_the_bar_is_swallowed_rather_than_sent_anywhere()
    {
        var read = MouseInput.Read(Sgr(64, 5, 1), barRows: 3);

        await Assert.That(read.Kind).IsEqualTo(MouseReading.Nothing)
            .Because("it is not a click, so it does not toggle - and forwarding it would "
                   + "hand the child a row above its own first one.");
    }

    [Test]
    public async Task Anything_that_is_not_a_mouse_report_is_passed_through_untouched()
    {
        var arrow = Bytes($"{Esc}[A");
        var read = MouseInput.Read(arrow, barRows: 3);

        await Assert.That(read.Kind).IsEqualTo(MouseReading.Forward);
        await Assert.That(Text(read.Bytes)).IsEqualTo($"{Esc}[A")
            .Because("this sits in the path every keystroke takes, so anything it does not "
                   + "recognise has to come out exactly as it went in.");
    }

    [Test]
    public async Task The_old_encoding_is_translated_too()
    {
        // X10: ESC [ M then three bytes, each the value plus 32. Claude asks
        // for SGR, but a child that does not gets the same correctness.
        var report = new byte[] { 0x1b, (byte)'[', (byte)'M', 32, 32 + 10, 32 + 9 };
        var read = MouseInput.Read(report, barRows: 3);

        await Assert.That(read.Kind).IsEqualTo(MouseReading.Forward);
        await Assert.That(read.Bytes.Span[5]).IsEqualTo((byte)(32 + 6))
            .Because("the row byte carries the same offset the SGR row does.");
    }

    [Test]
    public async Task What_the_child_asked_for_is_what_gg_turns_on()
    {
        var painted = MouseInput.Modes(
            MouseTrackingMode.AnyEvent, MouseEncoding.SGR, focus: true, paste: true);

        foreach (var on in (string[])["?1003h", "?1006h", "?1004h", "?2004h"])
        {
            await Assert.That(painted).Contains(on, StringComparison.Ordinal)
                .Because($"the child asked for it, so the real terminal has to hear it too. "
                       + $"Painted: {painted.Replace(Esc, "ESC")}");
        }
    }

    [Test]
    public async Task Turning_it_off_says_so_rather_than_saying_nothing()
    {
        // TOTAL, NOT INCREMENTAL. The string names every mode either on or off,
        // so gg never has to remember which ones it set last - the one piece
        // of state that would go wrong on a resize or a second session.
        var painted = MouseInput.Modes(
            MouseTrackingMode.None, MouseEncoding.Default, focus: false, paste: false);

        foreach (var off in (string[])["?1000l", "?1002l", "?1003l", "?1006l", "?1004l", "?2004l"])
        {
            await Assert.That(painted).Contains(off, StringComparison.Ordinal)
                .Because("a mode left on after the child stopped wanting it sends the next "
                       + "occupant sequences it never asked for - which is the reason "
                       + "PtyHost clears them on the way in. Painted: "
                       + painted.Replace(Esc, "ESC"));
        }
    }
}
