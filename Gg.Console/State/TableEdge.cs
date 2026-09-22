namespace Gg.Console;

/// <summary>
/// Whether pressing on past a table's last row leaves the table, or holds the
/// cursor there and says so.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists.</b> A key a table declines bubbles to the tab bar, which
/// answers an arrow by CHANGING TAB - so holding Down at the bottom of a list
/// quietly left it, which nobody asked for. Paging made it worse than a
/// surprise: leaving the tab starts that tab's read, which ABANDONS the page
/// reaching the end had just asked for, so the list stays short and asking
/// again needs the cursor moved off the last row and back.
/// </para>
/// <para>
/// <b>A held key never leaves, however long it is held.</b> There is no key-up
/// event in a terminal, so a hold is recognised by its RATE: the OS repeats far
/// faster than a person can tap. Once repeats are seen the tap count drops to
/// nothing and stays there until the key comes up - which is itself only a gap.
/// </para>
/// <para>
/// <b>Three deliberate taps leave.</b> One tap is how a person finds the edge
/// and the blink is the answer; three is a decision. They must be CONSECUTIVE,
/// because a gap long enough to be a different thought is a different thought.
/// </para>
/// <para>
/// <b>Pure, and no Terminal.Gui</b> - the rule <c>Keymap.Resolve</c> keeps. The
/// decision is a function of the presses and the clock, so it is testable
/// without a terminal; the view holds the running value and the timer.
/// </para>
/// <para>
/// <b>And it is NOT on <c>AppState</c>, deliberately.</b> Which row a person is
/// on is the model's; whether their finger is still down on a key is not, and
/// it must not survive the console handing the terminal to an editor and
/// building a new session from the model. A count that outlived that would be a
/// tap from before somebody wrote a commit message counting towards leaving a
/// table afterwards.
/// </para>
/// </remarks>
public static class TableEdge
{
    /// <summary>
    /// Presses closer together than this are the OS repeating, not a person.
    /// </summary>
    /// <remarks>
    /// Auto-repeat lands every 30-50ms on the platforms this runs on, and the
    /// fastest a person sustains a deliberate tap is nearer 120ms. Seventy sits
    /// between them with room on both sides.
    /// </remarks>
    public static readonly TimeSpan Repeating = TimeSpan.FromMilliseconds(70);

    /// <summary>A gap at least this long means the key came back up.</summary>
    /// <remarks>
    /// <b>Wider than the repeat rate on purpose.</b> Auto-repeat is not evenly
    /// spaced - a loaded scheduler stretches an interval - and treating one
    /// stretched gap as a release would let a hold leave the table, which is the
    /// one thing it may never do.
    /// </remarks>
    public static readonly TimeSpan Released = TimeSpan.FromMilliseconds(250);

    /// <summary>After this long, the next tap is a new thought and counts as one.</summary>
    public static readonly TimeSpan Forgotten = TimeSpan.FromMilliseconds(1500);

    /// <summary>
    /// How long the row goes on flashing after the last press at an edge.
    /// </summary>
    /// <remarks>
    /// <b>Short, because a HELD key re-arms it every repeat.</b> This is
    /// measured from the last press, so a finger down keeps it alive and
    /// flashing for as long as it is held, while a single tap gets a blip of
    /// about two cycles. Set wide enough to read as a flash for one tap, it made
    /// a tap flash six times.
    /// </remarks>
    public static readonly TimeSpan Blinking = TimeSpan.FromMilliseconds(240);

    /// <summary>
    /// Lit for this long, dark for this long.
    /// </summary>
    /// <remarks>
    /// <b>Fast on purpose</b> - about eight cycles a second. The first try ran
    /// at 180ms a half and read as a slow pulse, which looks like something
    /// loading rather than like the table answering a key.
    /// </remarks>
    public static readonly TimeSpan HalfABlink = TimeSpan.FromMilliseconds(60);

    /// <summary>How many deliberate taps leave the table.</summary>
    public const int TapsThatLeave = 3;

    /// <summary>
    /// What one press at an edge means: the presses as they now stand, and
    /// whether this was the one that leaves.
    /// </summary>
    public static (EdgePresses Now, bool Leaves) Pressed(EdgePresses were, DateTimeOffset at)
    {
        if (were.At is not { } last)
        {
            // THE FIRST PRESS AT AN EDGE NEVER LEAVES. It is how a person finds
            // out they are at the end, and the blink is the answer to it.
            return (new EdgePresses(1, at, false), false);
        }

        var gap = at - last;

        // THE OS REPEATING, which is a finger still down. The count goes back to
        // nothing so that no length of hold can add up to leaving.
        if (gap < Repeating)
        {
            return (new EdgePresses(0, at, true), false);
        }

        // STILL THAT SAME HOLD: too soon to be a release, so it is not a tap.
        if (were.Holding && gap < Released)
        {
            return (new EdgePresses(0, at, true), false);
        }

        var taps = gap > Forgotten ? 1 : were.Taps + 1;

        return taps >= TapsThatLeave
            ? (EdgePresses.None, true)
            : (new EdgePresses(taps, at, false), false);
    }

    /// <summary>
    /// Whether the row at the edge is lit this instant.
    /// </summary>
    /// <remarks>
    /// <b>The phase comes off the wall clock rather than off the last press</b>,
    /// so a held key blinks instead of glowing: repeats arrive every 30ms, and a
    /// phase measured from the newest one would restart before it could ever go
    /// dark.
    /// </remarks>
    public static bool Lit(EdgePresses presses, DateTimeOffset now) =>
        presses.At is { } last
        && now - last < Blinking
        && now.ToUnixTimeMilliseconds() / (long)HalfABlink.TotalMilliseconds % 2 == 0;

    /// <summary>Whether anything is still blinking, so a redraw is worth asking for.</summary>
    public static bool Blinks(EdgePresses presses, DateTimeOffset now) =>
        presses.At is { } last && now - last < Blinking;
}

/// <summary>
/// How a person has been pressing against a table's edge.
/// </summary>
/// <param name="Taps">Consecutive deliberate presses, which is what leaves.</param>
/// <param name="At">When the last one arrived, or null if there has not been one.</param>
/// <param name="Holding">
/// Whether what is arriving is auto-repeat - a finger still down.
/// </param>
public readonly record struct EdgePresses(int Taps, DateTimeOffset? At, bool Holding)
{
    /// <summary>Nobody is pressing against an edge.</summary>
    public static EdgePresses None => new(0, null, false);
}
