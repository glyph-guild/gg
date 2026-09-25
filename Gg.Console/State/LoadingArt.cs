namespace Gg.Console;

/// <summary>
/// What a tab shows while its read is still in the air.
/// </summary>
/// <remarks>
/// <para>
/// <b>Because "not read yet" and "read, and empty" look identical.</b> The
/// distinction is one this console already keeps in words - it is why
/// <see cref="Tabs.HasRead"/> exists at all, and why the board says "not read
/// yet" until both of its reads answer. This is the same fact, said where
/// somebody sees it without reading.
/// </para>
/// <para>
/// <b>The shape holds still and the light on it moves.</b> A mark whose
/// CHARACTERS changed would shimmer - the eye catches the substitution rather
/// than the movement. Holding the glyph and sliding a colour across it is what
/// makes this read as breathing, and it is the only way to get more than a
/// handful of steps out of a terminal.
/// </para>
/// <para>
/// <b>A breath and never a bar.</b> Nothing here knows how far along a read
/// is: the control plane answers or it does not. Anything that filled up, or
/// counted, would be inventing a progress nobody measured - so this returns to
/// where it started, every time.
/// </para>
/// <para>
/// <b>Pure, and drawn nowhere else.</b> The view centres it, owns the timer
/// and turns <see cref="Glow"/> into a colour; what it looks like is decided
/// here, which is what makes it testable without a terminal -
/// <see cref="Keymap.Resolve"/>'s rule one pane over.
/// </para>
/// </remarks>
public static class LoadingArt
{
    /// <summary>The mark: two lower-case g's, bowl, stem and tail.</summary>
    /// <remarks>
    /// <b>A descender is what makes it a g.</b> The bowl is the top six rows,
    /// the stem runs down the right of it, and the tail hooks back left
    /// underneath - without those last four rows this is an o with a nick in
    /// it, which is the first attempt at this and why the test asks for rows.
    /// </remarks>
    public static IReadOnlyList<string> Mark { get; } =
    [
        "   ██████      ██████   ",
        "  ██    ██    ██    ██  ",
        " ██      ██  ██      ██ ",
        " ██      ██  ██      ██ ",
        " ██      ██  ██      ██ ",
        " ██      ██  ██      ██ ",
        "  ██    ██    ██    ██  ",
        "   ███████     ███████  ",
        "         ██          ██ ",
        "         ██          ██ ",
        " ██      ██  ██      ██ ",
        "  ███████     ███████   ",
    ];

    /// <summary>How many ticks one breath takes.</summary>
    /// <remarks>
    /// <b>Forty, at the view's fiftieth of a second, is two seconds.</b> Slow
    /// enough to read as breathing rather than pulsing at somebody, and enough
    /// steps that no two frames differ by much - which is what the test about
    /// jumps holds.
    /// </remarks>
    public static int Breath => 40;

    /// <summary>
    /// How brightly the mark is lit at one tick, from 0 to 1.
    /// </summary>
    /// <param name="tick">
    /// Any integer. The counter behind this only goes up and is never reset, so
    /// it reaches <see cref="int.MinValue"/> eventually - and a mark that threw
    /// there would take the console down while it waited for a read.
    /// </param>
    /// <remarks>
    /// <b>A cosine, because a triangle has corners.</b> Linear up and linear
    /// down turns at the top and the bottom, and the eye sees each turn as a
    /// tick; easing through them is the difference between a breath and a
    /// metronome.
    /// <para>
    /// <b>And it never reaches nought.</b> A mark at zero brightness is a pane
    /// that looks empty again, which is the one thing this exists to stop.
    /// </para>
    /// </remarks>
    public static double Glow(int tick)
    {
        // REMAINDER, THEN LIFTED. C# gives a negative remainder for a negative
        // left side, which would put the phase behind the start of the breath -
        // the arithmetic that makes `int.MinValue' the interesting case rather
        // than a silly one.
        var at = ((tick % Breath) + Breath) % Breath;

        var wave = (1 - Math.Cos(2 * Math.PI * at / Breath)) / 2;

        // THE FLOOR IS THE POINT ABOVE, and the span is what is left over it.
        const double Dimmest = 0.35;

        return Dimmest + ((1 - Dimmest) * wave);
    }

    /// <summary>Whether the tab on screen has anything to show yet.</summary>
    /// <remarks>
    /// <para>
    /// <b><see cref="Tabs.HasRead"/> for every tab but the queue, and the
    /// difference is the point.</b> That method answers whether arriving at a
    /// tab must ASK for something, and for the queue the answer is no - the
    /// boot builds it, so landing there fetches nothing. `Going back to a tab
    /// the boot filled asks for nothing' holds that, and it is right.
    /// </para>
    /// <para>
    /// <b>This asks whether there is anything to LOOK at, which for the queue
    /// is a different question.</b> Its rows are derived - a flight that needs
    /// somebody, a runner stranded holding one - so until the flight list and
    /// the fleet have landed the pane is not an empty queue, it is a queue
    /// nobody has read yet. It spent every boot saying nothing needed anybody.
    /// </para>
    /// <para>
    /// <b>Two questions, and only here.</b> Anywhere else a second notion of
    /// "has this arrived" is how a pane comes to breathe over a table that is
    /// already full, so every other tab defers to the one answer.
    /// </para>
    /// </remarks>
    public static bool Waiting(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return state.ActiveTab == TabId.Queue
            ? state.Flights is null || state.Runners is null
            : !Tabs.HasRead(state, state.ActiveTab);
    }
}
