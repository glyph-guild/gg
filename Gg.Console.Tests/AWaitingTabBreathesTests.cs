using Gg.Console;

namespace Gg.Console.Tests;

/// <summary>
/// A tab with nothing in it yet says so by breathing.
/// </summary>
/// <remarks>
/// <para>
/// <b>Because "not read yet" and "read, and empty" look identical.</b> This
/// console already keeps separate sentences for the two - the rule is written
/// across <c>PaneText</c> and <c>Tabs.HasRead</c>, and it is the same rule that
/// gives the board "not read yet" until BOTH its reads answer. What it has not
/// had is something a person sees from across the desk: a tab still filling
/// looks like a tab that came back empty.
/// </para>
/// <para>
/// <b>Pure, and a pure function of the tick.</b> The art is decided here and
/// drawn nowhere else, so the thing that decides what a waiting tab looks like
/// can be tested without a terminal - <c>Keymap.Resolve</c>'s rule, one pane
/// over.
/// </para>
/// <para>
/// <b>Rectangular on purpose.</b> Centring is the view's job and it can only
/// do it against lines of one width; a frame that changed width as it breathed
/// would wander around the pane.
/// </para>
/// </remarks>
public class AWaitingTabBreathesTests
{
    [Test]
    public async Task It_is_a_large_gg()
    {
        // TWO GLYPHS, which is what the product is called - a single g would be
        // a different mark - and a lower-case g: a bowl, a stem down its right
        // side, and a tail that hooks back left under it.
        await Assert.That(LoadingArt.Mark).IsNotEmpty();
        await Assert.That(LoadingArt.Mark.Count).IsGreaterThanOrEqualTo(10)
            .Because("a descender needs rows under the bowl, so a short mark cannot be "
                   + "a lower-case g at all - it comes out as an o with a nick in it.");
    }

    [Test]
    public async Task The_ink_shimmers_but_the_shape_does_not()
    {
        // THE SILHOUETTE IS THE MARK. Characters may swap for other characters;
        // ink may never become space and space may never become ink, because
        // that is not a shimmer, that is the gg dissolving.
        foreach (var tick in (int[])[0, 1, 7, 19, 33])
        {
            var frame = LoadingArt.Of(tick);

            await Assert.That(frame.Count).IsEqualTo(LoadingArt.Mark.Count);

            for (var row = 0; row < frame.Count; row++)
            {
                await Assert.That(frame[row].Length).IsEqualTo(LoadingArt.Mark[row].Length);

                for (var col = 0; col < frame[row].Length; col++)
                {
                    await Assert.That(frame[row][col] == ' ')
                        .IsEqualTo(LoadingArt.Mark[row][col] == ' ')
                        .Because($"tick {tick} moved the edge at row {row}, column {col}.");
                }
            }
        }
    }

    [Test]
    public async Task It_is_still_a_gg_in_every_frame()
    {
        // A SHIMMER, NOT NOISE. If most of the ink changed every frame nobody
        // would read a letter at all - they would read static in the shape of
        // one, which says "broken" rather than "working".
        foreach (var tick in (int[])[3, 11, 27])
        {
            var frame = LoadingArt.Of(tick);

            var same = frame
                .SelectMany((line, row) => line.Select((c, col) => (c, row, col)))
                .Count(at => at.c == LoadingArt.Mark[at.row][at.col]);

            var all = LoadingArt.Mark.Sum(line => line.Length);

            await Assert.That((double)same / all).IsGreaterThan(0.8)
                .Because($"tick {tick} left only {same} of {all} characters alone.");
        }
    }

    [Test]
    public async Task But_it_does_change()
    {
        var frames = Enumerable.Range(0, LoadingArt.Breath)
            .Select(t => string.Join('\n', LoadingArt.Of(t)))
            .ToList();

        await Assert.That(frames.Distinct().Count()).IsGreaterThan(20)
            .Because("a mark that redrew the same characters every frame is the one that "
                   + "was already there.");
    }

    [Test]
    public async Task And_the_same_tick_draws_the_same_frame()
    {
        // DETERMINISTIC, so a paint that happens twice does not flicker between
        // two versions of one moment - and so this can be tested at all.
        await Assert.That(string.Join('\n', LoadingArt.Of(9)))
            .IsEqualTo(string.Join('\n', LoadingArt.Of(9)));
    }

    [Test]
    public async Task It_settles_as_it_brightens()
    {
        // THE TWO MOVEMENTS ARE ONE. Dim and unsettled, bright and still: the
        // mark reads as resolving into being rather than as a picture with
        // static thrown over it.
        var dimmest = LoadingArt.Of(0);
        var brightest = LoadingArt.Of(LoadingArt.Breath / 2);

        var moved = (IReadOnlyList<string> frame) => frame
            .SelectMany((line, row) => line.Select((c, col) => (c, row, col)))
            .Count(at => at.c != LoadingArt.Mark[at.row][at.col]);

        await Assert.That(moved(brightest)).IsLessThan(moved(dimmest))
            .Because($"brightest moved {moved(brightest)} characters and dimmest "
                   + $"moved {moved(dimmest)}.");
    }

    [Test]
    public async Task Every_line_is_the_same_width()
    {
        // SO THE VIEW CAN CENTRE IT. A ragged frame centres differently line by
        // line, which reads as the mark wobbling rather than breathing.
        var widths = LoadingArt.Mark.Select(line => line.Length).Distinct().ToList();

        await Assert.That(widths.Count).IsEqualTo(1);
    }

    [Test]
    public async Task It_breathes()
    {
        // THE POINT. Something that does not change is a picture, and a picture
        // of a logo in an empty pane says nothing about whether anything is
        // happening. The SHAPE holds still and the light on it moves - a mark
        // whose characters changed would shimmer rather than breathe.
        var over = Enumerable.Range(0, LoadingArt.Breath).Select(LoadingArt.Glow).ToList();

        await Assert.That(over.Distinct().Count()).IsGreaterThan(8)
            .Because("a handful of steps is a flicker; smooth means many.");
        await Assert.That(over.Max()).IsGreaterThan(0.9);
        await Assert.That(over.Min()).IsLessThan(0.4);
    }

    [Test]
    public async Task And_no_step_of_it_is_a_jump()
    {
        // WHAT `SMOOTH` MEANS, ASSERTED. Adjacent frames must be close, or the
        // eye reads the change rather than the movement - which is the whole
        // difference between breathing and blinking.
        var steps = Enumerable.Range(0, LoadingArt.Breath + 1)
            .Select(LoadingArt.Glow)
            .Zip(Enumerable.Range(1, LoadingArt.Breath + 1).Select(LoadingArt.Glow),
                 (a, b) => Math.Abs(b - a))
            .ToList();

        await Assert.That(steps.Max()).IsLessThan(0.1)
            .Because($"the largest step between frames was {steps.Max():F3}, which is a "
                   + "visible jolt rather than a breath.");
    }

    [Test]
    public async Task And_never_goes_dark_or_over_full()
    {
        // A MARK AT ZERO IS A PANE THAT LOOKS EMPTY AGAIN, and over one is a
        // colour the terminal will clamp somewhere nobody chose.
        foreach (var tick in (int[])[0, 3, 7, 19, 40, -5, int.MaxValue, int.MinValue])
        {
            var glow = LoadingArt.Glow(tick);

            await Assert.That(glow).IsGreaterThanOrEqualTo(0.0);
            await Assert.That(glow).IsLessThanOrEqualTo(1.0);
        }
    }

    [Test]
    public async Task And_comes_back_around()
    {
        // A BREATH, NOT A PROGRESS BAR. It cannot say how far along a read is -
        // nothing here knows - so it must not look like it is counting up to
        // something.
        await Assert.That(LoadingArt.Glow(LoadingArt.Breath)).IsEqualTo(LoadingArt.Glow(0));
    }

    [Test]
    public async Task A_tick_from_anywhere_is_safe()
    {
        // THE COUNTER LIVES IN AppState AND ONLY EVER GOES UP, so this is
        // handed every int there is eventually - and a mark that threw on one
        // of them would take the console down while it waited for a read.
        foreach (var tick in (int[])[-1, -7, int.MinValue, int.MaxValue, 99999])
        {
            await Assert.That(LoadingArt.Glow(tick)).IsGreaterThanOrEqualTo(0.0)
                .Because($"tick {tick} has to light the mark somehow.");
        }
    }

    [Test]
    public async Task A_tab_that_has_read_is_not_waiting()
    {
        // IT IS Tabs.HasRead, ASKED THE OTHER WAY. Two answers to "has this
        // arrived" is how a pane comes to show a spinner over a full table.
        var loaded = new AppState
        {
            ActiveTab = TabId.Flights,
            Flights = new Gg.Contracts.FlightList { Flights = [] },
        };

        await Assert.That(LoadingArt.Waiting(loaded)).IsFalse()
            .Because("an empty list is an answer, and the pane already has a sentence "
                   + "for it. Breathing over that would say the read is still coming.");
    }

    [Test]
    public async Task The_queue_waits_for_what_it_is_derived_from()
    {
        // AND IT IS NOT Tabs.HasRead HERE. That answers whether arriving must
        // ASK for something, and for the queue it never does - the boot builds
        // it. This asks whether there is anything to LOOK at, and a queue whose
        // flights and fleet have not landed is not an empty queue.
        var booting = new AppState { ActiveTab = TabId.Queue };

        await Assert.That(LoadingArt.Waiting(booting)).IsTrue()
            .Because("it spent every boot saying nothing needed anybody.");

        var landed = booting with
        {
            Flights = new Gg.Contracts.FlightList { Flights = [] },
            Runners = new Gg.Contracts.RunnerList { Runners = [] },
        };

        await Assert.That(LoadingArt.Waiting(landed)).IsFalse()
            .Because("both answered, and an empty queue is then a fact rather than a gap.");
    }

    [Test]
    public async Task The_browse_tab_waits_for_its_listing_not_for_being_open()
    {
        // OPENING IT IS WHAT STARTS THE READ, so `BrowseVisible' is true for the
        // whole time the listing is in the air - which is the right answer to
        // HasRead's question and the wrong one to this.
        var opened = new AppState { ActiveTab = TabId.Browse, BrowseVisible = true };

        await Assert.That(LoadingArt.Waiting(opened)).IsTrue();
    }

    [Test]
    public async Task A_pane_with_something_to_say_is_not_waiting()
    {
        // A READ THAT FAILED DOES NOT ARRIVE LATER. Covering its sentence with a
        // mark meaning "still reading" would hide the one thing that explains
        // why nothing is coming, and would breathe over it for ever.
        var failed = new AppState
        {
            ActiveTab = TabId.Browse,
            BrowseVisible = true,
            Diagnosis = "the tracker refused: no credential for dev.azure.com",
        };

        await Assert.That(LoadingArt.Waiting(failed)).IsFalse();
    }

    [Test]
    public async Task A_tab_still_waiting_is()
    {
        var unread = new AppState { ActiveTab = TabId.Flights };

        await Assert.That(LoadingArt.Waiting(unread)).IsTrue();
    }
}
