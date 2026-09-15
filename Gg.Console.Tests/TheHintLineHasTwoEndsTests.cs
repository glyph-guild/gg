using Gg.Console;

namespace Gg.Console.Tests;

/// <summary>
/// The keys that are always there sit at the right-hand end of the hint line,
/// and the keys about what is on the screen stay at the left.
/// </summary>
/// <remarks>
/// <para>
/// <b>Quit, refresh and help led the line, and they are the three a person
/// needs to read least often.</b> They are true on every tab and in every
/// state; what changes as somebody moves around is everything after them. So
/// the line opened with the part that never changes and put the part that does
/// behind it, which is the wrong way round for something read left to right.
/// </para>
/// <para>
/// <b>Two ends rather than a reordering.</b> Moving them to the back of one
/// string would leave them floating wherever the context keys happened to end
/// - a different column on every tab, which is the thing an eye cannot learn.
/// Pinned to the right edge they are always in the same place, and the tab you
/// are on decides only what is on the left.
/// </para>
/// <para>
/// <b>And it decides what gets lost when the line is too long.</b> One string
/// truncated at the screen edge drops whatever is last, which was these three;
/// with the right-hand end drawn over the top of the left, what goes is the
/// tail of the context keys and quit is always on the screen.
/// </para>
/// <para>
/// <b><c>Hints</c> still answers "what does this line advertise".</b> Thirty
/// call sites ask it that, and several guards ask it about keys that must not
/// be advertised - so it stays the union of both ends rather than becoming one
/// of them.
/// </para>
/// </remarks>
public class TheHintLineHasTwoEndsTests
{
    private static KeymapContext OnATab() => new(UiMode.Normal, TabId.Queue) { Refresh = "30s" };

    [Test]
    public async Task The_three_that_are_always_true_are_at_the_right()
    {
        var standing = Keymap.HintsStanding(OnATab());

        await Assert.That(standing).Contains("q quit");
        await Assert.That(standing).Contains("? help");
        await Assert.That(standing).Contains("g refresh")
            .Because("the clock is a property of the console rather than of the tab, and it "
                   + "is the one thing on the line that moves on its own.");
    }

    [Test]
    public async Task And_what_can_be_done_here_is_at_the_left()
    {
        var here = Keymap.HintsHere(OnATab());

        await Assert.That(here).Contains("a actions");
        await Assert.That(here).Contains("n new flight");

        await Assert.That(here).DoesNotContain("q quit")
            .Because("a key in both ends would be drawn twice, and the one a person reached "
                   + "for would be whichever they happened to see.");
        await Assert.That(here).DoesNotContain("g refresh");
        await Assert.That(here).DoesNotContain("? help");
    }

    [Test]
    public async Task Every_advertised_key_is_at_exactly_one_end()
    {
        // NOTHING FALLS BETWEEN THEM. Two lists built from one set is the shape
        // that loses a member quietly - so this is the assertion that says the
        // split is a split rather than two filters that happen to agree today.
        foreach (var mode in Enum.GetValues<UiMode>())
        {
            var context = new KeymapContext(mode) { Refresh = "30s" };

            var advertised = Keymap.Hints(context);
            var here = Keymap.HintsHere(context);
            var standing = Keymap.HintsStanding(context);

            var rejoined = (here, standing) switch
            {
                ({ Length: > 0 }, { Length: > 0 }) => here + " · " + standing,
                ({ Length: > 0 }, _) => here,
                _ => standing,
            };

            await Assert.That(rejoined).IsEqualTo(advertised)
                .Because($"{mode}: the two ends put together are the line, or one of them is "
                       + "advertising something the other end also claims - or nothing does.");
        }
    }

    [Test]
    public async Task A_modal_has_nothing_standing_at_all()
    {
        // A MODAL OWNS THE KEYBOARD, so quit and refresh are not offered in
        // one - which means the right-hand end is empty and the view has
        // nothing to draw there. The line is then exactly what it was.
        var inAModal = new KeymapContext(UiMode.GateDecision);

        await Assert.That(Keymap.HintsStanding(inAModal)).IsEmpty();
        await Assert.That(Keymap.HintsHere(inAModal)).Contains("a approve")
            .Because("and what the modal asks stays where a person is already reading.");
    }

    [Test]
    public async Task The_countdown_is_found_in_the_end_it_is_drawn_in()
    {
        // IT MOVED WITH THE KEY IT BELONGS TO. The fade paints over columns,
        // and an offset measured against the whole line would land in the
        // middle of the left-hand end now that refresh is drawn on the right.
        var context = OnATab();
        var standing = Keymap.HintsStanding(context);
        var at = Keymap.Counting(context);

        await Assert.That(at).IsNotNull();
        await Assert.That(standing.Substring(at!.Value.At, at.Value.Length)).IsEqualTo("30s")
            .Because("the seconds are painted at that offset into the right-hand label, so "
                   + "that is the string the offset has to be about. Standing: " + standing);
    }
}
