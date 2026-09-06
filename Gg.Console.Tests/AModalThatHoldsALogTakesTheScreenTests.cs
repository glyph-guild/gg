namespace Gg.Console.Tests;

/// <summary>
/// A modal is as big as what it has to show.
/// </summary>
/// <remarks>
/// <para>
/// <b>The runner's modal opened at fifty-two columns by twelve rows with a log
/// in it.</b> Two of those rows are the border and three are the status, which
/// leaves seven lines of a runner's output - about two seconds of a runner
/// starting up. The modal exists so somebody can watch it come up, and it was
/// sized for a sentence.
/// </para>
/// <para>
/// <b>The decision lived in the view as a literal.</b>
/// <c>Mode is UiMode.Help or UiMode.FlightDetail</c>, inside a class no test can
/// construct - so a modal added later got the small size by default and nothing
/// asked whether that was right. It is a property of what the mode SHOWS, which
/// is a thing the model knows.
/// </para>
/// </remarks>
public class AModalThatHoldsALogTakesTheScreenTests
{
    [Test]
    public async Task Anything_that_shows_a_document_takes_the_screen()
    {
        foreach (var mode in new[] { UiMode.Help, UiMode.FlightDetail, UiMode.Runner })
        {
            await Assert.That(PaneText.ModalIsADocument(mode)).IsTrue()
                .Because($"{mode} renders something a person scrolls rather than reads at a "
                       + "glance.");
        }
    }

    [Test]
    public async Task And_a_question_does_not()
    {
        foreach (var mode in new[]
        {
            UiMode.HandFlight, UiMode.ConfirmFlight, UiMode.SignIn, UiMode.FlightActions,
        })
        {
            await Assert.That(PaneText.ModalIsADocument(mode)).IsFalse()
                .Because($"{mode} is a few lines and two keys, and a box the size of the "
                       + "screen around it reads as something having gone wrong.");
        }
    }

    [Test]
    public async Task The_view_asks_rather_than_deciding()
    {
        // THE RATCHET. The literal is what let a new modal inherit the wrong
        // size in silence, so the view may not carry one.
        var screen = Sources.Read("Gg.Console", "Views", "ConsoleScreen.cs");

        await Assert.That(screen).DoesNotContain("UiMode.Help or UiMode.FlightDetail")
            .Because("a list of modes in the view is a list somebody adding a mode does not "
                   + "know to visit.");
        await Assert.That(screen).Contains("ModalIsADocument");
    }
}
