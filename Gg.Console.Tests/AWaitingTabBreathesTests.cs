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
    public async Task A_tab_still_waiting_is()
    {
        var unread = new AppState { ActiveTab = TabId.Flights };

        await Assert.That(LoadingArt.Waiting(unread)).IsTrue();
    }
}
