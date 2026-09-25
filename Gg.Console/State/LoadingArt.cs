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
    /// <summary>The mark: two g's, cut the way a pen would.</summary>
    /// <remarks>
    /// <b>Kevin's, character for character, padded to one width.</b> The block
    /// version before it was legible and flat; this has the weight shifting
    /// through the stroke the way a nib does, which is what carries the light
    /// when it breathes. Every line is padded to the widest because centring is
    /// the view's job and it can only do it against a rectangle.
    /// <para>
    /// <b>A descender is still what makes it a g</b>, and here it is the six
    /// rows under the bowl - the first attempt at this had none and came out an
    /// o with a nick in it, which is what the test about rows is for.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<string> Mark { get; } =
    [
        "   ,gggg,gg    ,gggg,gg ",
        "  dP\"  \"Y8I   dP\"  \"Y8I ",
        " i8'    ,8I  i8'    ,8I ",
        ",d8,   ,d8I ,d8,   ,d8I ",
        "P\"Y8888P\"888P\"Y8888P\"888",
        "       ,d8I'       ,d8I'",
        "     ,dP'8I      ,dP'8I ",
        "    ,8\"  8I     ,8\"  8I ",
        "    I8   8I     I8   8I ",
        "    `8, ,8I     `8, ,8I ",
        "     `Y8P\"       `Y8P\"  "
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

    /// <summary>The ink of the mark, which is what the shimmer draws from.</summary>
    /// <remarks>
    /// <b>The mark's own characters and no others.</b> A palette from outside
    /// it would read as something landing on the letter rather than the letter
    /// moving, and these already carry the weights a pen leaves - light commas
    /// and quotes, heavy eights and blocks.
    /// </remarks>
    private static readonly char[] Ink =
        [.. Mark.SelectMany(line => line).Where(c => c != ' ').Distinct().Order()];

    /// <summary>The mark at one tick, with its ink shimmering.</summary>
    /// <remarks>
    /// <para>
    /// <b>Deterministic, from the tick and the cell.</b> Nothing random is
    /// kept: the same tick draws the same frame, so a paint that happens twice
    /// does not flicker between two versions of one moment, and the whole thing
    /// stays a pure function of state.
    /// </para>
    /// <para>
    /// <b>It settles as it brightens.</b> The share of cells that move is tied
    /// to the breath and runs the other way, so the mark is unsettled when dim
    /// and still when full - one movement rather than a picture with static
    /// thrown over it.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<string> Of(int tick)
    {
        var at = ((tick % Breath) + Breath) % Breath;
        var wave = (1 - Math.Cos(2 * Math.PI * at / Breath)) / 2;

        // HOW MANY MOVE, AT MOST ONE IN FIVE. The test that asks for four in
        // five left alone is what holds this: past that a person reads static
        // in the shape of a letter rather than a letter.
        var churn = (uint)(0.18 * (1 - wave) * uint.MaxValue / 1);

        return
        [
            .. Mark.Select((line, row) => string.Create(line.Length, (line, row, at), (span, what) =>
            {
                for (var col = 0; col < span.Length; col++)
                {
                    var here = what.line[col];

                    span[col] = here == ' ' || Scatter(what.at, what.row, col) >= churn
                        ? here
                        : Ink[Scatter(what.at + 7919, what.row, col) % (uint)Ink.Length];
                }
            })),
        ];
    }

    /// <summary>One number per cell per tick, spread evenly and cheaply.</summary>
    /// <remarks>
    /// <b>A mix rather than a Random.</b> An RNG would need somewhere to keep
    /// its state, and state outside the model is the thing this console does
    /// not have - so the cell and the moment ARE the seed. Unchecked because
    /// wrapping is the arithmetic, not an accident.
    /// </remarks>
    private static uint Scatter(int tick, int row, int col)
    {
        unchecked
        {
            var mixed = (uint)((tick * 2654435761u) ^ ((uint)row * 40503u) ^ ((uint)col * 12289u));

            mixed ^= mixed >> 15;
            mixed *= 2246822519u;
            mixed ^= mixed >> 13;
            mixed *= 3266489917u;

            return mixed ^ (mixed >> 16);
        }
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
    /// <b>Two questions, and two tabs where they differ.</b> The browse tab is
    /// the other: <c>BrowseVisible</c> is whether the pane was opened, and
    /// opening it is what starts the read - so it answers HasRead correctly and
    /// this incorrectly. Everywhere else a second notion of "has this arrived"
    /// is how a pane comes to breathe over a table that is already full, so
    /// every other tab defers to the one answer.
    /// </para>
    /// </remarks>
    public static bool Waiting(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        // A PANE WITH SOMETHING TO SAY IS NOT A PANE STILL WAITING. A read that
        // failed leaves its sentence here, and covering that with a mark
        // meaning "still reading" would hide the one thing that explains why
        // nothing is coming - and would breathe over it for ever, since a
        // failed read does not arrive later.
        if (state.Diagnosis is { Length: > 0 })
        {
            return false;
        }

        return state.ActiveTab switch
        {
            // DERIVED, so it has something to show when its inputs land.
            TabId.Queue => state.Flights is null || state.Runners is null,

            // OPEN IS NOT ARRIVED. `BrowseVisible' is whether the pane was
            // opened, which is the right answer to HasRead's question and the
            // wrong one to this: opening it is what STARTS the read, so the
            // whole time it is in the air the pane was showing its empty words.
            TabId.Browse => state.Browse is null,

            _ => !Tabs.HasRead(state, state.ActiveTab),
        };
    }
}
