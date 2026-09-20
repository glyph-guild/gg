using Gg.Console;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// A watch's row on the board says as whom its newest sweep read.
/// </summary>
/// <remarks>
/// <para>
/// <b>The walk's third defect, one surface further in.</b> `gg airspace
/// watches` was taught to say it; this console was not, and the console is
/// where a person actually watches a watch. "swept, 1 nominated" reads the
/// same whoever did the reading, and on a personal watch the credential is the
/// point.
/// </para>
/// <para>
/// <b>It goes in the cost column and not beside the diagnosis.</b> The verb
/// prints the cost and any diagnosis on two lines and can afford to put the
/// account on the first; this column is one string, and a watch with a
/// diagnosis shows the diagnosis INSTEAD of its cost. Appending "as somebody"
/// to "has reported nothing since 04:45" makes a sentence that reads as though
/// the silence were somebody's. So: on the cost, where it belongs, and a watch
/// with a diagnosis keeps the diagnosis whole.
/// </para>
/// <para>
/// <b>Whose the watch IS cannot be said here at all.</b> `WatchStanding`
/// carries the account and no <c>For</c> - that member is on the document, not
/// the standing - so the `for` column stays blank on a watch row. Saying it
/// would take a contract member, which is a slice's decision rather than a
/// renderer's.
/// </para>
/// </remarks>
public class TheBoardSaysAsWhomAWatchReadTests
{
    private static AppState WithWatch(WatchStanding standing) => new()
    {
        Board = new BoardPage { IncludedEnded = true, Nominations = [] },
        Watches = new WatchStandingList { Standings = [standing] },
    };

    private static WatchStanding AWatch(string? account, string? diagnosis = null) => new()
    {
        Name = "nightly-triage",
        Version = "3",
        Window = WatchStanding.DefaultCostWindow,
        Executor = "instructions",
        Outcome = WatchOutcomes.Swept,
        LastHeardAt = DateTimeOffset.UtcNow.AddMinutes(-9),
        Opened = 1,
        Budgeted = 5,
        Account = account,
        Diagnosis = diagnosis,
    };

    private static string WhyOf(WatchStanding standing) =>
        Rows.Board(WithWatch(standing)).Single(r => r.What == BoardRow.Sweep).Why;

    [Test]
    public async Task A_watchs_row_says_as_whom_it_read()
    {
        await Assert.That(WhyOf(AWatch("kdeenanauth"))).IsEqualTo("1 of 5 in 24h, as kdeenanauth")
            .Because("the same sentence the verb prints, because a person reading one and then "
                   + "the other must not have to wonder whether they disagree.");
    }

    [Test]
    public async Task A_watch_whose_report_named_no_account_says_nothing_about_one()
    {
        // A PAIR THAT DECLARED NO ACCOUNT ATTESTS NONE - rule 16 - so this is
        // an absence to carry rather than a name to invent.
        await Assert.That(WhyOf(AWatch(account: null))).IsEqualTo("1 of 5 in 24h");
    }

    [Test]
    public async Task A_watch_with_a_diagnosis_keeps_the_diagnosis_whole()
    {
        // THE COLUMN HOLDS ONE STRING and a diagnosis replaces the cost, so
        // there is nowhere here to put the account without changing what the
        // sentence says. Pinned so that a later hand does not append to it.
        var quiet = AWatch("kdeenanauth", diagnosis: "has reported nothing since 04:45Z");

        await Assert.That(WhyOf(quiet)).IsEqualTo("has reported nothing since 04:45Z")
            .Because("'has reported nothing since 04:45Z, as kdeenanauth' reads as though the "
                   + "silence were that person's.");
    }

    [Test]
    public async Task A_watchs_row_leaves_whose_it_is_blank()
    {
        // NOT AN OVERSIGHT, and written down so it is not fixed by guesswork: a
        // standing carries no `For`. The day one does, this is the test that
        // should change.
        await Assert.That(
            Rows.Board(WithWatch(AWatch("kdeenanauth"))).Single(r => r.What == BoardRow.Sweep).For)
            .IsEqualTo("");
    }
}
