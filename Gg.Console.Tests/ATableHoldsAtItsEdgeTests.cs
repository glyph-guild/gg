namespace Gg.Console.Tests;

/// <summary>
/// Pressing on past the last row holds the cursor there; three deliberate taps
/// leave, and no length of hold ever does.
/// </summary>
/// <remarks>
/// <para>
/// <b>Asked for 2026-09-22, after the paging walk found what the old behaviour
/// costs.</b> A key a table declines bubbles to the tab bar, which answers an
/// arrow by changing tab - so holding Down at the bottom of the flights list
/// left it. That was always a surprise; with paging it also abandons the page
/// that reaching the end had just asked for, because the new tab's read
/// replaces it.
/// </para>
/// <para>
/// <b>There is no key-up in a terminal</b>, so every test here is about the
/// clock: a hold and a tap differ only in how fast they arrive.
/// </para>
/// </remarks>
public class ATableHoldsAtItsEdgeTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 22, 6, 0, 0, TimeSpan.Zero);

    /// <summary>Presses at the given offsets in milliseconds, in order.</summary>
    private static (EdgePresses Presses, int Left) Pressing(params int[] offsets)
    {
        var presses = EdgePresses.None;
        var left = 0;

        foreach (var offset in offsets)
        {
            var (now, leaves) = TableEdge.Pressed(presses, T0.AddMilliseconds(offset));
            presses = now;

            if (leaves)
            {
                left++;
            }
        }

        return (presses, left);
    }

    [Test]
    public async Task The_first_press_at_an_edge_stays()
    {
        var (presses, left) = Pressing(0);

        await Assert.That(left).IsEqualTo(0)
            .Because("one press is how a person finds out they are at the end, and the blink "
                   + "is the answer to it - leaving on the first is the behaviour this "
                   + "replaces.");
        await Assert.That(presses.Taps).IsEqualTo(1);
    }

    [Test]
    public async Task Two_taps_still_stay()
    {
        var (presses, left) = Pressing(0, 200);

        await Assert.That(left).IsEqualTo(0);
        await Assert.That(presses.Taps).IsEqualTo(2);
    }

    [Test]
    public async Task Three_deliberate_taps_leave()
    {
        var (presses, left) = Pressing(0, 200, 400);

        await Assert.That(left).IsEqualTo(1)
            .Because("three consecutive taps is a decision rather than a discovery.");
        await Assert.That(presses).IsEqualTo(EdgePresses.None)
            .Because("and the count starts again, so coming back to the edge is a fresh "
                   + "discovery rather than one tap from leaving.");
    }

    [Test]
    public async Task Tapping_as_fast_as_a_person_can_still_takes_three()
    {
        var (_, left) = Pressing(0, 120, 240);

        await Assert.That(left).IsEqualTo(1)
            .Because("120ms apart is a fast tap and not the OS repeating, so it counts - "
                   + "the ask was three taps however quickly they come.");
    }

    [Test]
    public async Task A_held_key_never_leaves()
    {
        // A REAL HOLD: one press, the OS's initial delay, then repeats every
        // 30ms for two solid seconds. Sixty-odd presses.
        var offsets = new List<int> { 0, 400 };
        for (var at = 430; at <= 2400; at += 30)
        {
            offsets.Add(at);
        }

        var (presses, left) = Pressing([.. offsets]);

        await Assert.That(left).IsEqualTo(0)
            .Because("a finger held down must pause at the last row for as long as it is "
                   + "held, which is the whole ask - and sixty presses reaching three taps "
                   + "would be the old behaviour with extra steps.");
        await Assert.That(presses.Holding).IsTrue();
        await Assert.That(presses.Taps).IsEqualTo(0)
            .Because("the count is dropped the moment repeats are recognised, so releasing "
                   + "and tapping once does not land on the third.");
    }

    [Test]
    public async Task A_stretched_repeat_is_still_the_same_hold()
    {
        // One interval stretched to 200ms - a loaded machine, not a release.
        var (presses, left) = Pressing(0, 400, 430, 460, 660, 690, 720);

        await Assert.That(left).IsEqualTo(0)
            .Because("auto-repeat is not evenly spaced, and reading one stretched gap as a "
                   + "release is how a hold would come to leave the table.");
        await Assert.That(presses.Holding).IsTrue();
    }

    [Test]
    public async Task A_hold_then_a_release_then_three_taps_leaves()
    {
        var offsets = new List<int> { 0, 400, 430, 460, 490 };

        // Released - a gap far wider than any repeat - then three taps.
        offsets.AddRange([1200, 1400, 1600]);

        var (_, left) = Pressing([.. offsets]);

        await Assert.That(left).IsEqualTo(1)
            .Because("letting go and then deciding is exactly the gesture that should "
                   + "leave, and the hold before it must not have used the count up.");
    }

    [Test]
    public async Task Taps_too_far_apart_are_not_consecutive()
    {
        var (presses, left) = Pressing(0, 200, 3000);

        await Assert.That(left).IsEqualTo(0)
            .Because("a gap long enough to be a different thought is a different thought.");
        await Assert.That(presses.Taps).IsEqualTo(1)
            .Because("and the late one counts as the first of a new run rather than as "
                   + "nothing at all.");
    }

    // ---- the blink ----

    [Test]
    public async Task The_row_blinks_rather_than_glowing_while_a_key_is_held()
    {
        // A FINGER DOWN: the press keeps being re-armed, as auto-repeat does,
        // so the only thing that can make it go dark is the phase.
        var lit = new List<bool>();
        var step = TableEdge.HalfABlink / 4;

        for (var tick = 0; tick < 24; tick++)
        {
            var at = T0 + (step * tick);
            lit.Add(TableEdge.Lit(new EdgePresses(0, at, true), at));
        }

        var flips = lit.Zip(lit.Skip(1)).Count(pair => pair.First != pair.Second);

        await Assert.That(flips).IsGreaterThanOrEqualTo(2)
            .Because("the phase comes off the wall clock rather than off the last press: "
                   + "measured from the newest repeat it would restart every 30ms and the "
                   + "row would glow steadily instead of flashing. Sampled four times a "
                   + "half-cycle so the step cannot alias with the period.");
    }

    [Test]
    public async Task A_single_tap_is_a_blip_rather_than_a_pulse()
    {
        var cycles = TableEdge.Blinking / (TableEdge.HalfABlink * 2);

        await Assert.That(cycles).IsLessThanOrEqualTo(3)
            .Because("one tap flashing six times reads as something loading rather than as "
                   + "the table answering a key - and a held key keeps re-arming the window, "
                   + "so it does not need to be long to flash for as long as somebody holds.");
        await Assert.That(cycles).IsGreaterThanOrEqualTo(1)
            .Because("and it has to complete at least one, or a tap could land entirely "
                   + "inside the lit half and show nothing at all.");
    }

    [Test]
    public async Task The_blink_stops_on_its_own()
    {
        var presses = new EdgePresses(1, T0, false);

        await Assert.That(TableEdge.Blinks(presses, T0.AddMilliseconds(100))).IsTrue();
        await Assert.That(TableEdge.Blinks(presses, T0 + TableEdge.Blinking)).IsFalse()
            .Because("a row that never stopped blinking would be a table that looks broken "
                   + "rather than one answering a press.");
    }

    [Test]
    public async Task Nothing_blinks_when_nobody_is_at_an_edge()
    {
        await Assert.That(TableEdge.Blinks(EdgePresses.None, T0)).IsFalse();
        await Assert.That(TableEdge.Lit(EdgePresses.None, T0)).IsFalse();
    }
}
