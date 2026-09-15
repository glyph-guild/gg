using Gg.Console;
using Gg.Console.Views;
using Terminal.Gui.Drawing;

// Terminal.Gui's Attribute is a colour pair, and System's is the other thing.
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Gg.Console.Tests;

/// <summary>
/// The seconds on the refresh countdown fade as they run down, and nothing
/// else on the line moves with them.
/// </summary>
/// <remarks>
/// <para>
/// <b>A number that changes every second is the brightest thing on a still
/// screen.</b> It sits on the hint line, which this console deliberately dims
/// as a whole - the line is reference rather than content - and the one part
/// of it that ticks was pulling the eye back once a second anyway. Letting it
/// fade turns that into information: bright means the read is a while off, dim
/// means it is about to happen.
/// </para>
/// <para>
/// <b>Only the seconds.</b> The word `refresh' and the key beside it do not
/// change, so brightening them would be animation with nothing behind it - and
/// the rest of the line is other keys, which have nothing to do with the clock.
/// </para>
/// <para>
/// <b>Split into three answers, each testable without a screen.</b> How far
/// through the wait it is, is arithmetic on the model. Where the seconds sit on
/// the line is a question about the hint line's text. What colour that is, is
/// the one place this console builds colours. A console screen cannot be
/// constructed in a test, so a fade computed inside one could only ever be
/// checked by looking at it.
/// </para>
/// </remarks>
public class TheCountdownFadesAsItRunsDownTests
{
    private static AppState Counting(int secondsLeft, int every = 30) => new()
    {
        Refresh = new RefreshState { NextIn = secondsLeft, Every = every },
    };

    // ---- how far through the wait ----

    [Test]
    public async Task At_the_top_of_the_count_all_of_it_is_left()
    {
        await Assert.That(AutoRefresh.Left(Counting(30).Refresh)).IsEqualTo(1.0);
    }

    [Test]
    public async Task At_the_last_second_none_of_it_is()
    {
        // ONE, NOT ZERO. The countdown never shows 0s - Says answers nothing at
        // all once a read is in the air - so the dimmest thing a person ever
        // sees is `1s`, and that is what the bottom of the ramp has to be.
        await Assert.That(AutoRefresh.Left(Counting(1).Refresh)).IsEqualTo(0.0);
    }

    [Test]
    public async Task And_halfway_is_halfway()
    {
        var half = AutoRefresh.Left(Counting(16).Refresh);

        await Assert.That(half).IsGreaterThan(0.45);
        await Assert.That(half).IsLessThan(0.55);
    }

    [Test]
    public async Task A_count_longer_than_the_interval_is_still_the_top_of_the_ramp()
    {
        // The first tick after a keypress can set the seconds before the
        // interval it is measured against; a fraction over one would ask for a
        // colour brighter than white.
        await Assert.That(AutoRefresh.Left(Counting(45).Refresh)).IsEqualTo(1.0);
    }

    [Test]
    public async Task An_interval_of_nothing_asks_for_no_arithmetic()
    {
        // A model that has never been ticked carries zeroes, and the line is
        // drawn once before the first tick.
        await Assert.That(AutoRefresh.Left(new RefreshState())).IsEqualTo(1.0);
    }

    // ---- where the seconds are on the line ----

    [Test]
    public async Task The_seconds_are_found_where_the_line_actually_says_them()
    {
        var state = Counting(30);
        var line = Keymap.Hints(KeymapContext.For(state));
        var at = Keymap.Counting(KeymapContext.For(state));

        await Assert.That(at).IsNotNull()
            .Because("the refresh key is on the hint line in Normal mode, and it is counting.");

        await Assert.That(line.Substring(at!.Value.At, at.Value.Length)).IsEqualTo("30s")
            .Because("the view paints over these columns and nothing else, so the offset has "
                   + "to be the seconds and not the word in front of them. Line: " + line);
    }

    [Test]
    public async Task It_moves_with_the_number_it_is_about()
    {
        // TWO DIGITS AND THEN ONE. `9s` is a column narrower than `10s`, so an
        // offset measured once would paint over the space beside it.
        var state = Counting(9);
        var line = Keymap.Hints(KeymapContext.For(state));
        var at = Keymap.Counting(KeymapContext.For(state))!.Value;

        await Assert.That(line.Substring(at.At, at.Length)).IsEqualTo("9s");
    }

    [Test]
    public async Task A_read_in_the_air_is_counting_nothing()
    {
        // Busy shows a mark rather than a number, and fading a mark that does
        // not change would be a fade about nothing.
        var busy = new AppState { Refresh = new RefreshState { Busy = true, Every = 30 } };

        await Assert.That(Keymap.Counting(KeymapContext.For(busy))).IsNull();
    }

    [Test]
    public async Task And_a_line_without_the_key_on_it_has_nowhere_to_paint()
    {
        // Inside a modal the refresh key is not offered, so the line it would
        // have been on is somebody else's.
        var inAModal = Counting(30) with { Mode = UiMode.GateDecision };

        await Assert.That(Keymap.Counting(KeymapContext.For(inAModal))).IsNull();
    }

    // ---- what colour that is ----

    [Test]
    public async Task The_top_of_the_ramp_is_brighter_than_the_bottom()
    {
        var basis = new Scheme { Normal = new Attribute(new Color(200, 200, 200), new Color(0, 0, 0)) };

        var bright = LookStyles.Counting(basis, left: 1.0).Normal.Foreground;
        var dim = LookStyles.Counting(basis, left: 0.0).Normal.Foreground;

        await Assert.That((int)bright.R).IsGreaterThan((int)dim.R)
            .Because("that is the whole feature: 30s reads as bright and 1s as dim.");
    }

    [Test]
    public async Task The_top_of_the_ramp_is_white()
    {
        var basis = new Scheme { Normal = new Attribute(new Color(200, 200, 200), new Color(0, 0, 0)) };

        var bright = LookStyles.Counting(basis, left: 1.0).Normal.Foreground;

        await Assert.That((int)bright.R).IsEqualTo(255);
        await Assert.That((int)bright.G).IsEqualTo(255);
        await Assert.That((int)bright.B).IsEqualTo(255);
    }

    [Test]
    public async Task It_stays_grey_all_the_way_down()
    {
        // A RAMP AND NOT A HUE. Three channels moving together is grey; two of
        // them moving is a colour change, and a countdown that went green would
        // be saying something this console does not mean.
        var basis = new Scheme { Normal = new Attribute(new Color(200, 200, 200), new Color(0, 0, 0)) };

        foreach (var left in (double[])[1.0, 0.75, 0.5, 0.25, 0.0])
        {
            var fore = LookStyles.Counting(basis, left).Normal.Foreground;

            await Assert.That((int)fore.G).IsEqualTo((int)fore.R);
            await Assert.That((int)fore.B).IsEqualTo((int)fore.R);
        }
    }

    [Test]
    public async Task The_background_is_the_line_it_sits_on()
    {
        // It is painted over the hint line, so anything else would draw a patch
        // a different colour from the row it is in.
        var basis = new Scheme { Normal = new Attribute(new Color(200, 200, 200), new Color(20, 30, 40)) };

        var over = LookStyles.Counting(basis, left: 0.5).Normal.Background;

        await Assert.That((int)over.R).IsEqualTo(20);
        await Assert.That((int)over.G).IsEqualTo(30);
        await Assert.That((int)over.B).IsEqualTo(40);
    }

    [Test]
    public async Task Every_second_of_a_thirty_second_wait_is_a_step_down()
    {
        // NO PLATEAUS. A ramp that repeated a value for three seconds would
        // read as a clock that had stopped, which is the defect the countdown
        // itself was added to avoid.
        var basis = new Scheme { Normal = new Attribute(new Color(200, 200, 200), new Color(0, 0, 0)) };

        var steps = Enumerable.Range(1, 30)
            .Select(second => (int)LookStyles
                .Counting(basis, AutoRefresh.Left(Counting(second).Refresh))
                .Normal.Foreground.R)
            .ToList();

        await Assert.That(steps.Distinct().Count()).IsEqualTo(30)
            .Because("thirty seconds should be thirty shades. Found: "
                   + string.Join(", ", steps));
    }
}
