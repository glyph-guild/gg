using Gg.Client;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// `gg runners` shows what each runner advertises, on the row.
/// </summary>
/// <remarks>
/// The fleet listing is where somebody looks when a flight is waiting, and
/// the label is the thing they are looking for - a listing that made them run
/// a second verb to see it would be the answer hidden behind the question.
/// The dispositions stay with `gg runner labels`; the row carries the names.
/// </remarks>
public class RunnersColumnTests
{
    [Test]
    public async Task The_fleet_listing_shows_advertised_labels()
    {
        var text = VerbOutput.ToText(new VerbResult.Runners(new RunnerList
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
                    ],
                },
            ],
        }));

        await Assert.That(text).Contains("environment=aspire-payments");
    }
}
