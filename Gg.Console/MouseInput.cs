using System.Text;
using XTerm.Input;

namespace Gg.Console;

/// <summary>What gg does with bytes that arrived from the terminal.</summary>
public enum MouseReading
{
    /// <summary>Send them on, as given or as rewritten.</summary>
    Forward,

    /// <summary>A button went down on gg's own rows.</summary>
    Pressed,

    /// <summary>The wheel turned on gg's own rows, away from the person.</summary>
    ScrolledUp,

    /// <summary>The wheel turned on gg's own rows, towards them.</summary>
    ScrolledDown,

    /// <summary>gg's rows were pointed at in a way that means nothing.</summary>
    Nothing,
}

/// <summary>What to do with one read from the terminal.</summary>
/// <param name="Kind">Forward, toggle, or drop.</param>
/// <param name="Bytes">
/// What to forward. The input unchanged unless a row needed moving.
/// </param>
public readonly record struct MouseRead(MouseReading Kind, ReadOnlyMemory<byte> Bytes);

/// <summary>
/// Mouse reporting across the boundary gg puts between a person and a child.
/// </summary>
/// <remarks>
/// <para>
/// <b>WHY ANY OF THIS IS NEEDED.</b> gg runs the child in a pty it owns and
/// re-renders its screen, so everything the child writes lands on the emulator
/// rather than on the terminal. A child that asks for mouse reporting is
/// asking something nobody hears, which is why the wheel did nothing: the
/// terminal was never told to report, so there was nothing to forward.
/// </para>
/// <para>
/// <b>PURE, AND THAT IS DELIBERATE.</b> Everything that can be wrong here is
/// arithmetic on a row number, and a row number is exactly the kind of thing
/// that is wrong by one for a year. This type touches no terminal and no pty;
/// <c>PtyHost</c> does the painting and the writing.
/// </para>
/// </remarks>
public static class MouseInput
{
    /// <summary>
    /// The escape byte, written as an escape.
    /// </summary>
    /// <remarks>
    /// <b>Never the raw character.</b> <c>PtyScreen</c> records what happens
    /// otherwise: a literal 0x1b between quotes was silently stripped to an
    /// empty string, and every sequence built from it did nothing at all.
    /// </remarks>
    private const string Esc = "\u001b";

    private const byte Escape = 0x1b;

    /// <summary>The offset X10 adds to every value so it lands in printable bytes.</summary>
    private const int X10Bias = 32;

    /// <summary>
    /// The flags a button number carries above the button itself.
    /// </summary>
    /// <remarks>
    /// <b>BITS, NOT A RANGE, and reading it as a range is what made the bar
    /// pop open on hover.</b> The low two bits are the button; above them sit
    /// 4 shift, 8 meta, 16 control, 32 motion and 64 wheel. So 35 is "the
    /// pointer moved with nothing held" - a 3 that is not a button at all -
    /// and the first version of this asked only whether the number was below
    /// 64, which every motion report is.
    /// <para>
    /// Testing the whole number is the other easy mistake, and it swallows
    /// shift-click: a modifier held down is still a press.
    /// </para>
    /// </remarks>
    private const int Motion = 32;

    /// <summary>Wheel and the buttons above it, which are not clicks either.</summary>
    private const int WheelUp = 64;

    /// <summary>
    /// What to do with one read from the terminal.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>EVERYTHING UNRECOGNISED COMES OUT AS IT WENT IN.</b> This sits in
    /// the path of every keystroke and every paste, so anything that is not
    /// unmistakably a mouse report is somebody's typing.
    /// </para>
    /// <para>
    /// <b>Rows above the child's first are gg's.</b> A press there opens or
    /// closes the panel; anything else there is dropped, because the child has
    /// no such row and a report naming one would be a lie about where a person
    /// pointed.
    /// </para>
    /// </remarks>
    /// <param name="read">The bytes as the terminal delivered them.</param>
    /// <param name="barRows">
    /// How many rows gg is painting over the child, the hint among them. Every
    /// row gg keeps is at the top, so one number places any click.
    /// </param>
    public static MouseRead Read(ReadOnlyMemory<byte> read, int barRows)
    {
        var bytes = read.Span;

        if (Sgr(bytes) is { } sgr)
        {
            return Decided(read, sgr.Button, sgr.Row, sgr.Pressed, barRows, at =>
            {
                var moved = Encoding.ASCII.GetBytes(
                    $"{Esc}[<{sgr.Button};{sgr.Column};{at}{(sgr.Pressed ? 'M' : 'm')}");

                return new ReadOnlyMemory<byte>(moved);
            });
        }

        if (X10(bytes) is { } x10)
        {
            return Decided(read, x10.Button, x10.Row, x10.Pressed, barRows, at =>
            {
                var moved = read.ToArray();
                moved[5] = (byte)(at + X10Bias);

                return new ReadOnlyMemory<byte>(moved);
            });
        }

        return new MouseRead(MouseReading.Forward, read);
    }

    /// <summary>Whose row it is, and what that means.</summary>
    private static MouseRead Decided(
        ReadOnlyMemory<byte> read,
        int button,
        int row,
        bool pressed,
        int barRows,
        Func<int, ReadOnlyMemory<byte>> moved)
    {
        if (row > barRows)
        {
            return new MouseRead(MouseReading.Forward, moved(row - barRows));
        }

        // THE WHEEL ON GG'S ROWS IS GG'S TO ACT ON, not a thing to drop. The
        // panel says "… n more" about a body it can now move, and the wheel is
        // how a person will reach for it first.
        if (pressed && (button & WheelUp) != 0)
        {
            return new MouseRead(
                (button & 1) == 0 ? MouseReading.ScrolledUp : MouseReading.ScrolledDown,
                ReadOnlyMemory<byte>.Empty);
        }

        // A PRESS OPENS OR CLOSES; A RELEASE DOES NOTHING. Both arrive for one
        // click, and acting on both would open the panel and shut it again
        // before a finger left the button.
        //
        // NOR IS MOVING OVER IT A PRESS, which is the whole of the hover
        // defect: with any-event tracking on, the pointer crossing gg's rows
        // reports continuously and every one of those ends in `M` like a
        // press does.
        return pressed && (button & Motion) == 0
            ? new MouseRead(MouseReading.Pressed, read)
            : new MouseRead(MouseReading.Nothing, ReadOnlyMemory<byte>.Empty);
    }

    /// <summary>
    /// An SGR report — <c>ESC [ &lt; button ; column ; row M</c> — or null.
    /// </summary>
    /// <remarks>
    /// The encoding every modern client asks for, Claude Code included,
    /// because it is the only one whose coordinates do not run out at 223.
    /// </remarks>
    private static (int Button, int Column, int Row, bool Pressed)? Sgr(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 9 || bytes[0] != Escape || bytes[1] != '[' || bytes[2] != '<')
        {
            return null;
        }

        var last = bytes[^1];
        if (last is not ((byte)'M' or (byte)'m'))
        {
            return null;
        }

        var fields = Encoding.ASCII.GetString(bytes[3..^1]).Split(';');
        if (fields.Length != 3
            || !int.TryParse(fields[0], out var button)
            || !int.TryParse(fields[1], out var column)
            || !int.TryParse(fields[2], out var row))
        {
            return null;
        }

        return (button, column, row, last == (byte)'M');
    }

    /// <summary>
    /// An X10 report — <c>ESC [ M</c> and three biased bytes — or null.
    /// </summary>
    /// <remarks>
    /// <b>Carried for correctness rather than for a caller we have.</b> Claude
    /// asks for SGR; a child that asks for nothing gets this, and a row that
    /// is wrong by the height of the bar is as wrong here as there. Its
    /// release is a button value of 3 rather than a different terminator,
    /// which is why <see cref="Decided"/> is told rather than asked.
    /// </remarks>
    private static (int Button, int Row, bool Pressed)? X10(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != 6 || bytes[0] != Escape || bytes[1] != '[' || bytes[2] != 'M')
        {
            return null;
        }

        var button = bytes[3] - X10Bias;

        // 3 IS THE RELEASE, and it does not say which button let go. A press
        // is anything else.
        return (button, bytes[5] - X10Bias, (button & 3) != 3);
    }

    /// <summary>
    /// What to paint on the real terminal so it reports what the child asked
    /// for.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>TOTAL, NOT INCREMENTAL.</b> Every mode is named either on or off, so
    /// gg never has to remember which ones it set last — the one piece of
    /// state that would go wrong across a resize, a second session, or a child
    /// that changed its mind. Painting this twice is painting it once.
    /// </para>
    /// <para>
    /// <b>Mirrored, never forced.</b> These are copied from what the child
    /// asked the emulator for. Turning mouse reporting on for a child that
    /// never wanted it would take drag-to-select away from a person, and hand
    /// an editor escape sequences it would type into the buffer.
    /// </para>
    /// </remarks>
    public static string Modes(
        MouseTrackingMode tracking, MouseEncoding encoding, bool focus, bool paste)
    {
        var painted = new StringBuilder();

        // OFF FIRST, ALL OF THEM. The tracking modes are not exclusive at the
        // terminal, so setting one without clearing the others leaves whatever
        // a previous state turned on still reporting.
        foreach (var mode in (int[])[(int)MouseTrackingMode.X10, 1000, 1002, 1003])
        {
            painted.Append($"{Esc}[?{mode}l");
        }

        foreach (var mode in (int[])[1005, 1006, 1015])
        {
            painted.Append($"{Esc}[?{mode}l");
        }

        if (tracking != MouseTrackingMode.None)
        {
            // THE ENUM'S VALUES ARE THE DECSET NUMBERS, which is not a
            // coincidence to rely on quietly: X10 is 9, VT200 is 1000,
            // ButtonEvent 1002, AnyEvent 1003. Naming them again here would be
            // a second table to keep in agreement with the emulator's.
            painted.Append($"{Esc}[?{(int)tracking}h");
        }

        if (Encoded(encoding) is { } number)
        {
            painted.Append($"{Esc}[?{number}h");
        }

        painted.Append($"{Esc}[?1004{(focus ? 'h' : 'l')}");
        painted.Append($"{Esc}[?2004{(paste ? 'h' : 'l')}");

        return painted.ToString();
    }

    /// <summary>The DECSET number for an encoding, or none for the default.</summary>
    private static int? Encoded(MouseEncoding encoding) => encoding switch
    {
        MouseEncoding.Utf8 => 1005,
        MouseEncoding.SGR => 1006,
        MouseEncoding.URXVT => 1015,
        _ => null,
    };
}
