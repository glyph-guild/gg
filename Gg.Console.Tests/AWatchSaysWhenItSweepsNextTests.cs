using Gg.Console;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// The board says how long since each row last moved, and when a watch sweeps
/// next.
/// </summary>
/// <remarks>
/// <para>
/// <b>`since' rather than `when', because it is an age and it measures two
/// different things.</b> On a nomination it is how long since it was made; on a
/// watch it is how long since it last reported. "When" reads as an instant and
/// promises one answer; this column is neither.
/// </para>
/// <para>
/// <b>And the next sweep is the control plane's to say, never this console's.</b>
/// The schedule is timed between two DECISIONS - so a runner's pace never
/// stretches it and its clock never shortens it - and it is held back by a latch
/// while a sweep has not reported and by the watch's active hours. A console
/// holding only <c>LastHeardAt</c> would produce a number that disagrees with
/// the planner exactly when somebody is asking why nothing has run, so it shows
/// what it was told and nothing where it was told nothing.
/// </para>
/// <para>
/// <b>Blank for a nomination, because a nomination has no schedule.</b> A dash
/// would read as "nothing scheduled", which is a claim about a thing that has
/// no next run to have.
/// </para>
/// </remarks>
public class AWatchSaysWhenItSweepsNextTests
{
    private static AppState WithWatch(WatchStanding standing) => new()
    {
        Board = new BoardPage { IncludedEnded = true, Nominations = [] },
        Watches = new WatchStandingList { Standings = [standing] },
    };

    private static WatchStanding Sweeping(
        DateTimeOffset? next = null, string? said = null) => new()
    {
        Name = "nightly-triage",
        Version = "3",
        Window = WatchStanding.DefaultCostWindow,
        Executor = "instructions",
        Outcome = WatchOutcomes.Swept,
        LastHeardAt = DateTimeOffset.UtcNow.AddMinutes(-9),
        NextSweepAt = next,
        NextSweepSaid = said,
    };

    [Test]
    public async Task The_column_is_called_since()
    {
        await Assert.That(Rows.BoardColumns).Contains("since");
        await Assert.That(Rows.BoardColumns).DoesNotContain("when")
            .Because("it is an age, and on this tab it is the age of two different things - "
                   + "a nomination since it was made, a watch since it last reported.");
    }

    [Test]
    public async Task The_board_has_a_next_column_after_since()
    {
        await Assert.That(Rows.BoardColumns).Contains("next");

        await Assert.That(Rows.BoardColumns.ToList().IndexOf("next"))
            .IsEqualTo(Rows.BoardColumns.ToList().IndexOf("since") + 1)
            .Because("last time and next time read together; a column between them is one "
                   + "an eye has to cross twice.");
    }

    [Test]
    public async Task A_watch_shows_how_long_until_its_next_sweep()
    {
        var row = Rows.Board(WithWatch(Sweeping(next: DateTimeOffset.UtcNow.AddMinutes(4))))
            .Single();

        await Assert.That(row.Next).IsEqualTo("4m")
            .Because("the same coarse ages the rest of this tab uses, forwards.");
    }

    [Test]
    public async Task A_sweep_that_is_already_due_says_now()
    {
        // WAITING ON A RUNNER, which is the state a person asks about: the
        // control plane has decided nothing is stopping it, and nothing has
        // pulled it yet. "0m" would read as a countdown that has stalled.
        var row = Rows.Board(WithWatch(Sweeping(next: DateTimeOffset.UtcNow.AddSeconds(-30))))
            .Single();

        await Assert.That(row.Next).IsEqualTo("now");
    }

    [Test]
    public async Task A_watch_that_will_never_sweep_says_why_instead()
    {
        // THE ANSWER SOMEBODY NEEDS, in the column they are already reading. A
        // blank here would read as "soon" for a watch nothing will ever pull.
        var row = Rows.Board(WithWatch(Sweeping(
            next: null,
            said: "this watch is performed by forge-reader, and nothing sweeps from there"))).Single();

        await Assert.That(row.Next).Contains("forge-reader");
    }

    [Test]
    public async Task A_watch_the_control_plane_said_nothing_about_shows_nothing()
    {
        // AN OLDER CONTROL PLANE, which is the ordinary case the day this
        // ships: the member is absent, so the column is empty rather than
        // guessing a schedule out of a last-heard.
        var row = Rows.Board(WithWatch(Sweeping())).Single();

        await Assert.That(row.Next).IsEqualTo("");
    }

    [Test]
    public async Task A_nomination_has_no_next_at_all()
    {
        var state = new AppState
        {
            Watches = new WatchStandingList { Standings = [] },
            Board = new BoardPage
            {
                IncludedEnded = true,
                Nominations =
                [
                    new()
                    {
                        NominationId = new Guid("01a0b2c3-0000-7000-8000-00000000000d"),
                        Nominator = "watch:nightly-triage",
                        Subject = "work-item:https://tracker.example/acme/17",
                        Version = "1",
                        WorkKind = "review",
                        Mode = "gated",
                        State = "standing",
                        MadeAt = DateTimeOffset.UtcNow.AddHours(-2),
                    },
                ],
            },
        };

        await Assert.That(Rows.Board(state).Single().Next).IsEqualTo("")
            .Because("a nomination has no schedule, and a dash would be a claim about a "
                   + "next run it never had.");
    }
}
