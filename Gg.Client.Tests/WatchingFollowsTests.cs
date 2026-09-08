using Gg.Client;

namespace Gg.Client.Tests;

/// <summary>
/// Watching a flight moves the screen, and does not repeat itself.
/// </summary>
/// <remarks>
/// <para>
/// <b>Following is asking again, because the channel carries a request and a
/// bounded response and nothing else.</b> There is no push shape in
/// <c>RunnerAskKinds</c> and adding one would be a contract change with a
/// fingerprint bump — so a watcher polls, and the whole difficulty moves into
/// deciding what is new.
/// </para>
/// <para>
/// <b>By overlap rather than by counting.</b> A tail is the last N lines, so two
/// reads share a suffix and a prefix. Counting would be wrong the first time a
/// flight said the same thing twice; skipping a fixed number would be wrong
/// whenever the log grew by more than one line between reads.
/// </para>
/// </remarks>
public class WatchingFollowsTests
{
    /// <summary>The overlap finder, reached the way the follower reaches it.</summary>
    private static int Overlap(string[] before, string[] now) =>
        (int)typeof(WatchARunner)
            .GetMethod("OverlapOf", System.Reflection.BindingFlags.NonPublic
                                  | System.Reflection.BindingFlags.Static)!
            .Invoke(null, [before, now])!;

    [Test]
    public async Task A_tail_that_grew_by_one_line_shows_one_new_line()
    {
        var before = new[] { "text: a", "text: b", "text: c" };
        var now = new[] { "text: b", "text: c", "text: d" };

        await Assert.That(Overlap(before, now)).IsEqualTo(2)
            .Because("two of the three are the same lines, so only the third is new.");
    }

    [Test]
    public async Task A_flight_that_said_nothing_new_shows_nothing()
    {
        var same = new[] { "text: a", "text: b" };

        await Assert.That(Overlap(same, same)).IsEqualTo(2)
            .Because("a poll that found no movement must print no movement, or watching a "
                   + "quiet flight fills the screen with what it already said.");
    }

    [Test]
    public async Task A_flight_that_repeats_itself_is_not_mistaken_for_no_movement()
    {
        // THE CASE COUNTING GETS WRONG. An agent that runs the same tool twice
        // says the same line twice, and a follower that compared only the last
        // line would decide nothing had happened.
        var before = new[] { "tool: Read", "tool: → ok" };
        var now = new[] { "tool: → ok", "tool: Read", "tool: → ok" };

        var overlap = Overlap(before, now);

        await Assert.That(now.Skip(overlap)).IsEquivalentTo(new[] { "tool: Read", "tool: → ok" })
            .Because("the repeat is real output and has to appear, or a person watching a loop "
                   + "sees it go round once.");
    }

    [Test]
    public async Task A_gap_bigger_than_the_window_is_noticed_rather_than_hidden()
    {
        // NO OVERLAP AT ALL means the flight said more than a whole tail between
        // two reads, so something was missed. A watcher who is not told that
        // believes they saw everything - which is the one answer this must
        // never give.
        var before = new[] { "text: a", "text: b" };
        var now = new[] { "text: y", "text: z" };

        await Assert.That(Overlap(before, now)).IsEqualTo(0);
    }

    [Test]
    public async Task The_first_read_shows_everything_it_found()
    {
        // The liveness half: an empty "before" must not be treated as an
        // overlap, or the first poll would print nothing at all.
        await Assert.That(Overlap([], ["text: a", "text: b"])).IsEqualTo(0);
    }
}
