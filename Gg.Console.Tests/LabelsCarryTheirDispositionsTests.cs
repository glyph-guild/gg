using Gg.Client;
using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Console.Tests;

/// <summary>
/// Every label the console shows carries its disposition.
/// </summary>
/// <remarks>
/// <para>
/// <b>The invariant is the contract's own</b>, written on
/// <c>AdvertisedLabel</c>: <i>the disposition travels WITH the name everywhere
/// the name does - the runner listing, the checklist, the refusal text - so a
/// stated claim can never be read as a measurement by losing its qualifier in
/// transit.</i>
/// </para>
/// <para>
/// <b>And the console showed neither.</b> <c>AppState.Runners</c> is fetched at
/// boot, assigned through <c>Apply</c>, used to derive the queue - and read by
/// no pane at all. It is the mirror of the notices defect: that field was
/// rendered and never assigned, this one is assigned and never rendered, and
/// <see cref="StateAssignmentTests"/> only looks in one of those directions.
/// </para>
/// <para>
/// <b>It goes under the checklist because that is where the question is asked.</b>
/// A checklist item reading <c>unmet   environment=docker</c> is answered by
/// what the fleet advertises, and a person who has to change panes to find out
/// is a person comparing two screens from memory.
/// </para>
/// </remarks>
public class LabelsCarryTheirDispositionsTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 5, 9, 0, 0, TimeSpan.Zero);

    private static RunnerList Fleet() => new()
    {
        Runners =
        [
            new RunnerSummary
            {
                RunnerId = "r-1",
                Label = "the-build-box",
                State = "idle",
                Labels =
                [
                    new AdvertisedLabel { Name = "environment=docker", Disposition = "asserted" },
                    new AdvertisedLabel { Name = "arch=arm64", Disposition = "observed" },
                ],
            },
        ],
    };

    [Test]
    public async Task No_label_is_shown_without_the_disposition_beside_it()
    {
        var pane = PaneText.Checklist(new AppState { Queue = [Row()], Runners = Fleet() });

        foreach (var label in Fleet().Runners[0].Labels)
        {
            var at = pane.IndexOf(label.Name, StringComparison.Ordinal);

            await Assert.That(at).IsGreaterThanOrEqualTo(0)
                .Because($"{label.Name} is advertised and the console does not say so.");

            var line = pane[at..].Split('\n')[0];

            await Assert.That(line).Contains(label.Disposition)
                .Because("a stated claim read as a measurement is the thing the disposition "
                       + "exists to prevent, and it is lost by rendering the name alone.");
        }
    }

    [Test]
    public async Task A_runner_advertising_nothing_says_so_rather_than_vanishing()
    {
        // A fact somebody diagnosing a waiting flight needs, not an absence to
        // hide - the same sentence `gg runner labels` prints for it.
        var bare = new RunnerList
        {
            Runners =
            [
                new RunnerSummary
                {
                    RunnerId = "r-2", Label = "the-quiet-one", State = "idle", Labels = [],
                },
            ],
        };

        var pane = PaneText.Checklist(new AppState { Queue = [Row()], Runners = bare });

        await Assert.That(pane).Contains("the-quiet-one");
        await Assert.That(pane).Contains("advertises nothing");
    }

    [Test]
    public async Task An_empty_fleet_says_what_to_do_about_it()
    {
        var pane = PaneText.Checklist(
            new AppState { Queue = [Row()], Runners = new RunnerList { Runners = [] } });

        await Assert.That(pane).Contains("gg runner up")
            .Because("a checklist that cannot be met by any runner and an estate with no "
                   + "runners at all want different actions, and only one of them is "
                   + "somebody's to take right now.");
    }

    [Test]
    public async Task A_fleet_that_was_not_loaded_is_not_an_empty_fleet()
    {
        // Rule 5 again. `no runners are registered` is a claim about the estate;
        // saying it because a read failed is a lie with a remedy attached.
        var pane = PaneText.Checklist(new AppState { Queue = [Row()] });

        await Assert.That(pane).DoesNotContain("gg runner up")
            .Because("nothing was read, so nothing is known about the fleet.");
    }

    private static QueueRow Row() => new()
    {
        FlightId = "a",
        FlightNumber = FlightRef.Format(1),
        Name = "waiting",
        Reason = QueueReason.AwaitingDecision,
        Since = T0,
    };
}
