using Gg.Client;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// `gg runner labels`: what each runner advertises, with the word that says
/// what the claim is worth.
/// </summary>
/// <remarks>
/// The same wire document `gg runners` reads, rendered per label rather than
/// per runner - one document, two views, the FlightSummary-facts precedent.
/// The disposition travels beside every name, because a stated claim that
/// lost its qualifier in transit would read as a measurement.
/// </remarks>
public class RunnerLabelsVerbTests
{
    private static RunnerList Fleet() => new()
    {
        Runners =
        [
            new RunnerSummary
            {
                RunnerId = "3f0f8a21-6d53-4c19-8e77-95a2f0c3d6e8",
                Label = "runner-a",
                State = RunnerStates.Idle,
                Labels =
                [
                    new AdvertisedLabel
                    {
                        Name = "environment=aspire-payments",
                        Disposition = LabelDispositions.Stated,
                    },
                    new AdvertisedLabel
                    {
                        Name = "gpu=a100",
                        Disposition = LabelDispositions.Measured,
                    },
                ],
            },
            new RunnerSummary
            {
                RunnerId = "4b0f8a21-6d53-4c19-8e77-95a2f0c3d6e8",
                Label = "runner-b",
                State = RunnerStates.Offline,
                Labels = [],
            },
        ],
    };

    [Test]
    public async Task Every_label_renders_with_its_disposition_beside_it()
    {
        var text = VerbOutput.ToText(new VerbResult.RunnerLabels(Fleet()));

        await Assert.That(text).Contains("environment=aspire-payments");
        await Assert.That(text).Contains(LabelDispositions.Stated);
        await Assert.That(text).Contains(LabelDispositions.Measured);
        await Assert.That(text).Contains("runner-a");
    }

    [Test]
    public async Task A_runner_advertising_nothing_still_appears()
    {
        // "Advertises nothing" is a fact about the fleet somebody is
        // diagnosing, not an absence to hide.
        var text = VerbOutput.ToText(new VerbResult.RunnerLabels(Fleet()));

        await Assert.That(text).Contains("runner-b");
    }

    [Test]
    public async Task A_saved_payload_renders_the_same_as_the_live_one()
    {
        var live = new VerbResult.RunnerLabels(Fleet());
        var reparsed = VerbOutput.Parse(live.Kind, VerbOutput.ToJson(live));

        await Assert.That(VerbOutput.ToText(reparsed)).IsEqualTo(VerbOutput.ToText(live));
    }
}
