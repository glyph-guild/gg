using Gg.Console.Views;
using Terminal.Gui.Configuration;
using Terminal.Gui.Drawing;

namespace Gg.Console.Tests;

/// <summary>
/// The console runs on a dark theme, and a document pane is dimmer text on
/// that same ground rather than a block of its own.
/// </summary>
/// <remarks>
/// <para>
/// <b>The muted panes came out black on grey, which is the inverse of what was
/// meant.</b> The colours were mixed inside <c>ConsoleScreen</c>, a class no
/// test can construct without a terminal, so nothing compared the foreground
/// it chose against the background it chose. A grey block behind black text is
/// exactly what you get if those two swap, and the code that produced it read
/// as if it could not.
/// </para>
/// <para>
/// <b>The background is the half that must not move.</b> Every other pane sits
/// on the theme's own ground; a document pane that names its own background is
/// a rectangle of a different colour in the middle of the console, whichever
/// colour it picks. So the muted scheme takes its ground from Base and dims
/// only the text.
/// </para>
/// <para>
/// <b>And the theme is asked for rather than inherited.</b> "Whatever the
/// terminal already has" was the previous answer and it is not an answer: the
/// library resolves an unset colour against its own Default theme, which is
/// light, so a person on a dark terminal got a light theme's idea of contrast.
/// Naming the theme makes both halves of every attribute known here.
/// </para>
/// </remarks>
[NotInParallel]
public class TheConsoleIsDarkTests
{
    /// <summary>How bright a colour is, 0 to 1, by the usual weighting.</summary>
    private static double Brightness(Color color) =>
        ((0.299 * color.R) + (0.587 * color.G) + (0.114 * color.B)) / 255.0;

    [Test]
    public async Task The_console_asks_for_the_dark_theme()
    {
        ConsoleTheme.Apply();

        await Assert.That(ConsoleTheme.Active).IsEqualTo(ConsoleTheme.Dark)
            .Because("the console picks its theme rather than taking the library's default.");
    }

    [Test]
    public async Task The_console_stands_on_a_ground_it_named_itself()
    {
        // COLOUR.NONE IS NOT A COLOUR, and every background in the Dark theme's
        // Base scheme is None - "leave whatever the terminal is showing". So the
        // foreground was the only half this console chose, chosen against a
        // ground nobody here could see, and a mid grey on a light terminal is
        // dark text on a grey block. This is the assertion that would have
        // caught it: the ground is a colour, and it is dark.
        ConsoleTheme.Apply();

        var ground = ConsoleTheme.Grounded().GetAttributeForRole(VisualRole.Normal);

        await Assert.That(ground.Background).IsNotEqualTo(Color.None)
            .Because("a ground of `whatever is already there' cannot be paired with a "
                   + "foreground, which is the whole defect.");
        await Assert.That(Brightness(ground.Background)).IsLessThan(0.5)
            .Because("dark means the ground is dark, and a theme that failed to load is light.");
        await Assert.That(Brightness(ground.Foreground)).IsGreaterThan(Brightness(ground.Background))
            .Because("and the text on it has to be the brighter of the two.");
    }

    [Test]
    public async Task A_muted_pane_keeps_the_ground_the_rest_of_the_console_is_on()
    {
        ConsoleTheme.Apply();

        var console = ConsoleTheme.Grounded().GetAttributeForRole(VisualRole.Normal);
        var muted = ConsoleTheme.Muted().GetAttributeForRole(VisualRole.Normal);

        await Assert.That(muted.Background).IsEqualTo(console.Background)
            .Because("a document pane that paints its own background is a block of a different "
                   + "colour in the middle of the console, which is what a person saw.");
    }

    [Test]
    public async Task A_muted_pane_lands_between_the_ground_and_the_text_around_it()
    {
        // WHAT MUTED MEANS, arithmetically. Brighter than the surface under it or
        // it is unreadable; dimmer than the console around it or it is not muted.
        // A named grey can promise neither, which is why the previous one was
        // wrong on the terminal it met.
        ConsoleTheme.Apply();

        var console = ConsoleTheme.Grounded().GetAttributeForRole(VisualRole.Normal);
        var muted = ConsoleTheme.Muted().GetAttributeForRole(VisualRole.Normal);

        await Assert.That(Brightness(muted.Foreground)).IsGreaterThan(Brightness(muted.Background))
            .Because("dark-on-grey is the pairing this file exists to catch: the dimmer of the "
                   + "two has to be the ground.");
        await Assert.That(Brightness(muted.Foreground)).IsLessThan(Brightness(console.Foreground))
            .Because("muted means dimmer than the console around it, or it is not muted.");
    }
}
