using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// A flight admission opened as a leg of a plan says which leg of which plan, rather than "another
/// flight's classification".
/// </summary>
/// <remarks>
/// <b>Found on ITN-60</b>, a plan its owner proposed: all three legs read "on another flight's
/// classification", which was the only way a flight was opened by admission when the sentence was
/// written, and was false for every leg of a person's plan.
/// </remarks>
public class ALegSaysWhichPlanItIsTests
{
    [Test]
    public async Task A_leg_names_its_subject_and_its_plan()
    {
        var said = FlightStory.Sentence(
            StoryKinds.OpenedByAdmission, ["ui-preview", "'Storybook 10 visual check' of ITN-60"]);

        await Assert.That(said).IsEqualTo(
            "opened by admission as ui-preview, leg 'Storybook 10 visual check' of ITN-60");
    }

    [Test]
    public async Task A_flight_a_classification_opened_still_says_so()
    {
        var said = FlightStory.Sentence(StoryKinds.OpenedByAdmission, ["implement"]);

        await Assert.That(said).Contains("on another flight's classification")
            .Because("a record written before legs carried a label, or by a classifier's nomination, is still that.");
    }
}
