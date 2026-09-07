using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// A runner somebody withheld does not read on the screen as one with nothing
/// to do.
/// </summary>
/// <remarks>
/// <para>
/// <b>The contract carries the distinction; this is what stops the screen
/// throwing it away.</b> <c>ParkedAt</c> and <c>ParkedBecause</c> sit BESIDE
/// <c>State</c> rather than inside it, because parking withholds claiming and
/// never touches a lease - so a runner can be parked and busy at once, which is
/// the reason to park anything. That asymmetry is right, and it has a failure
/// mode of its own: a table printing <c>State</c> and nothing else shows a
/// withheld machine as <c>idle</c>, and the two silences are collapsed on the
/// screen however carefully the wire keeps them apart.
/// </para>
/// <para>
/// <b>So the composition is the deliverable, not the member.</b>
/// <c>LeaseClaimStates.Parked</c> calls collapsing these two "the defect
/// <c>Waiting</c> was added to fix"; shipping a member nobody renders would be
/// that defect with a changelog entry.
/// </para>
/// </remarks>
public class AWithheldRunnerDoesNotReadAsIdleTests
{
    private const string Esc = "\u001b";

    private static readonly DateTimeOffset Parked = new(2026, 9, 7, 18, 0, 0, TimeSpan.Zero);

    private static AppState Fleet(string state, DateTimeOffset? parked, string because = "") =>
        new()
        {
            Machine = "Kevins-MBP",
            Runners = new RunnerList
            {
                Runners =
                [
                    new RunnerSummary
                    {
                        RunnerId = "01a06572-a784-72ae-b951-f147553cd48e",
                        Label = "vmlinux001",
                        State = state,
                        ParkedAt = parked,
                        ParkedBecause = because,
                    },
                ],
            },
        };

    [Test]
    public async Task A_withheld_runner_says_so_where_its_state_is_shown()
    {
        var row = Rows.Runners(Fleet(RunnerStates.Idle, Parked))[0];

        await Assert.That(row.State).IsNotEqualTo(RunnerStates.Idle)
            .Because("idle and withheld are the two silences the claim path refuses to "
                   + "collapse, and a column showing only the first collapses them here.");

        await Assert.That(row.State).Contains("parked");
    }

    [Test]
    public async Task A_runner_nobody_withheld_reads_exactly_as_before()
    {
        // The anchor. A change that made every row say something new would pass
        // the test above and be worse than the defect.
        var row = Rows.Runners(Fleet(RunnerStates.Idle, parked: null))[0];

        await Assert.That(row.State).IsEqualTo(RunnerStates.Idle);
    }

    [Test]
    public async Task A_draining_runner_shows_both_facts()
    {
        // THE CASE THE ASYMMETRY EXISTS FOR. Parked and busy at once is the
        // reason to park anything: finish what you have, take nothing more. A
        // row showing one of the two would be the fourth-state design arriving
        // through the renderer.
        var row = Rows.Runners(Fleet(RunnerStates.Busy, Parked))[0];

        await Assert.That(row.State).Contains(RunnerStates.Busy);
        await Assert.That(row.State).Contains("parked");
    }

    [Test]
    public async Task A_parked_runner_that_stopped_beating_still_reads_offline_first()
    {
        // Offline is decided before anything else and parking is not a way to
        // take a machine away, so the word a person needs first is still there.
        var row = Rows.Runners(Fleet(RunnerStates.Offline, Parked))[0];

        await Assert.That(row.State).StartsWith(RunnerStates.Offline);
    }

    [Test]
    public async Task Why_it_was_withheld_reaches_the_modal()
    {
        // The grid has room for a word; the reason is a sentence and belongs
        // where somebody opened one row deliberately. Without it "parked" sends
        // a person to ask a human, which is what the reason was recorded to
        // prevent.
        var state = Fleet(RunnerStates.Idle, Parked, "draining before the kernel upgrade") with
        {
            Mode = UiMode.Runner,
            ActiveTab = TabId.Runners,
            RunnerSelected = 0,
        };

        await Assert.That(PaneText.Modal(state)).Contains("draining before the kernel upgrade");
    }

    [Test]
    public async Task A_crafted_reason_is_stripped_before_it_is_stored()
    {
        // A person's own words about their own fleet, and still text somebody
        // else chose arriving over a wire into a terminal gg owns. The console's
        // rule is at ingress rather than at render.
        var row = Rows.Runners(Fleet(RunnerStates.Idle, Parked, Esc + "[2Jdraining"))[0];

        await Assert.That(row.ParkedBecause).DoesNotContain(Esc);
        await Assert.That(row.ParkedBecause).Contains("draining");
    }
}
