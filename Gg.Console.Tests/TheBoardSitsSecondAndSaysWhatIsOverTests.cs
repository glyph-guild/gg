using Gg.Console;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// The board is the second tab, and a row that is over recedes with its ending
/// named.
/// </summary>
/// <remarks>
/// <para>
/// <b>Second because of what the queue is.</b> The queue is what needs
/// somebody, and a standing nomination is already one of its rows — so the
/// board is where a person goes the moment they have answered one: what else
/// stands, what was opened, what was refused, and which watches are finding
/// any of it. Flights is the same relationship one step later, and it keeps
/// third.
/// </para>
/// <para>
/// <b>The flights tab's colour logic, applied to a list with two kinds of row
/// in it.</b> Only the endings are tinted, because most rows are not endings
/// and a colour every row carries distinguishes nothing.
/// </para>
/// <para>
/// <b>What recedes is what nothing came of, which is not the same as what
/// ended.</b> The first draft dimmed every ending, and that put `opened` —
/// the one where a flight is now running — in the background with the refusals.
/// A watch never recedes either: it keeps looking, so a quiet or unreachable
/// one is the opposite of finished.
/// </para>
/// <para>
/// <b>And the two vocabularies share a column.</b> `state` holds a
/// nomination's mode while it stands, its ending once it has one, and a
/// watch's standing — so the mapping is over words from three vocabularies and
/// anything unrecognised stays the ordinary foreground, which is
/// <c>FlightLook</c>'s own rule for a word nobody defined here.
/// </para>
/// </remarks>
public class TheBoardSitsSecondAndSaysWhatIsOverTests
{
    [Test]
    public async Task The_board_is_the_second_tab()
    {
        await Assert.That(Tabs.All[0]).IsEqualTo(TabId.Queue)
            .Because("what needs somebody comes first, and nothing displaces it.");

        await Assert.That(Tabs.All[1]).IsEqualTo(TabId.Board)
            .Because("the board is where somebody who has just answered a queue row goes "
                   + "next, so it sits beside the queue rather than behind the flights.");

        await Assert.That(Tabs.All[2]).IsEqualTo(TabId.Flights);
    }

    [Test]
    public async Task A_standing_nomination_is_the_ordinary_foreground()
    {
        // THE ONE THAT MUST NOT BE COLOURED, for the flights tab's reason: a
        // standing row is the common case and the one somebody can act on.
        foreach (var mode in (string[])["gated", "auto"])
        {
            await Assert.That(BoardLook.Tint(mode)).IsEqualTo(BoardTint.None);
            await Assert.That(BoardLook.IsOver(mode)).IsFalse();
        }
    }

    [Test]
    public async Task An_opened_nomination_keeps_its_foreground()
    {
        // THE ENDING THAT IS NOT AN ENDING. `opened` ends the NOMINATION and
        // starts a flight - the work is running, and on a tab where receding
        // means "this is a record" it was the one row pushed back that somebody
        // can still do something about. The colour was right and the dimming
        // was backwards.
        await Assert.That(BoardLook.Tint(NominationEndings.Opened)).IsEqualTo(BoardTint.Opened);

        await Assert.That(BoardLook.IsOver(NominationEndings.Opened)).IsFalse()
            .Because("what came of it is live, and the flights tab dims a landing because "
                   + "nothing is left to do - the opposite of this.");
    }

    [Test]
    public async Task Every_ending_that_came_to_nothing_recedes()
    {
        await Assert.That(BoardLook.Tint(NominationEndings.Declined)).IsEqualTo(BoardTint.Refused);
        await Assert.That(BoardLook.Tint(NominationEndings.Refused)).IsEqualTo(BoardTint.Refused);
        await Assert.That(BoardLook.Tint(NominationEndings.Withdrawn)).IsEqualTo(BoardTint.Moot);
        await Assert.That(BoardLook.Tint(NominationEndings.Superseded)).IsEqualTo(BoardTint.Moot);
        await Assert.That(BoardLook.Tint(NominationEndings.Lapsed)).IsEqualTo(BoardTint.Moot);

        foreach (var ending in NominationEndings.All.Where(
                     e => !string.Equals(e, NominationEndings.Opened, StringComparison.Ordinal)))
        {
            await Assert.That(BoardLook.IsOver(ending)).IsTrue()
                .Because($"'{ending}' is an ending nothing came of, and this half of the board "
                       + "is a record: what somebody can still act on stays in front.");
        }
    }

    [Test]
    public async Task A_watch_says_how_it_is_doing_and_never_recedes()
    {
        // A WATCH IS NOT AN ENDING. It keeps looking, so a dimmed one would say
        // "finished" about the thing least finished on the tab - and the two
        // that are worth a colour are the two nobody would otherwise notice.
        await Assert.That(BoardLook.Tint(WatchOutcomes.Unreachable)).IsEqualTo(BoardTint.Broken);
        await Assert.That(BoardLook.Tint("quiet")).IsEqualTo(BoardTint.Quiet);

        await Assert.That(BoardLook.Tint(WatchOutcomes.Swept)).IsEqualTo(BoardTint.None)
            .Because("a watch doing its job is the common case.");
        await Assert.That(BoardLook.Tint("never swept")).IsEqualTo(BoardTint.None)
            .Because("a watch nobody has swept yet has not gone wrong; it has not run.");

        foreach (var live in (string[])[WatchOutcomes.Unreachable, WatchOutcomes.Swept, "quiet"])
        {
            await Assert.That(BoardLook.IsOver(live)).IsFalse();
        }
    }

    [Test]
    public async Task A_word_this_console_has_not_been_taught_stays_plain()
    {
        // FlightLook's rule, carried over: a word nobody defined here has no
        // honest colour, and inventing one teaches a person something false.
        await Assert.That(BoardLook.Tint("something-new")).IsEqualTo(BoardTint.None);
        await Assert.That(BoardLook.Tint(null)).IsEqualTo(BoardTint.None);
        await Assert.That(BoardLook.IsOver("something-new")).IsFalse();
    }
}
