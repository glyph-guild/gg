using Gg.Client;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// The invariant that outlived the pane that carried it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The rule is the contract's own</b>, written on <c>AdvertisedLabel</c>:
/// <i>the disposition travels WITH the name everywhere the name does - the
/// runner listing, the checklist, the refusal text - so a stated claim can
/// never be read as a measurement by losing its qualifier in transit.</i>
/// </para>
/// <para>
/// <b>The checklist was the only place this console showed an advertised
/// label</b>, under the items, because that is where the question was asked.
/// Removing the tab removed the labels with it - the runners pane lists a
/// runner's id, state and current flight and has never rendered what it
/// advertises. So the rule now holds VACUOUSLY, and a vacuous invariant with
/// no test is one that quietly stops being true.
/// </para>
/// <para>
/// <b>Which is what this asserts instead.</b> Not "labels carry dispositions"
/// - there are no labels - but "no pane shows a label at all". The day somebody
/// puts the fleet's advertisements back, this fails, and the failure says to
/// bring the disposition back with them rather than leaving a bare name that
/// reads as a measurement.
/// </para>
/// </remarks>
public class LabelsCarryTheirDispositionsTests
{
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
    public async Task No_pane_shows_a_bare_advertised_label()
    {
        var state = new AppState { Runners = Fleet() };

        foreach (var tab in Tabs.All)
        {
            var pane = PaneText.ForTab(state, tab);

            foreach (var label in Fleet().Runners[0].Labels)
            {
                // A NAME WITHOUT ITS QUALIFIER IS THE FAILURE, so a pane that
                // shows both is fine and only a bare one is not.
                if (!pane.Contains(label.Name, StringComparison.Ordinal))
                {
                    continue;
                }

                await Assert.That(pane).Contains(label.Disposition)
                    .Because($"the {Tabs.Name(tab)} pane shows '{label.Name}' without saying "
                           + "how much its word is worth. AdvertisedLabel's rule is that the "
                           + "two travel together everywhere.");
            }
        }
    }

    [Test]
    public async Task The_runners_pane_still_names_the_fleet()
    {
        // THE ANCHOR. The sweep above passes trivially if no pane renders
        // anything about runners at all, which would be a different bug than
        // the one it is guarding.
        var pane = PaneText.Runners(new AppState { Runners = Fleet() });

        await Assert.That(pane).Contains("the-build-box")
            .Because("the fleet is still listed; it is only what each runner ADVERTISES "
                   + "that left with the checklist.");
    }
}
