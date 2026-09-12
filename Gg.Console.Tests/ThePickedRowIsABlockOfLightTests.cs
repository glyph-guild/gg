using Gg.Console.Views;
using Terminal.Gui.Drawing;

namespace Gg.Console.Tests;

/// <summary>
/// The row under the cursor is a block of light, and the tree it sits in has a
/// border like the pane beside it.
/// </summary>
/// <remarks>
/// <para>
/// <b>REPORTED FROM USE: the left half has no edge and no cursor you can
/// see.</b> The document beside it is drawn inside a tab bar with a border, so
/// the tree read as loose text next to a framed pane - and which row the pane
/// was ABOUT was the one thing the screen did not say.
/// </para>
/// <para>
/// <b>Computed from the ground, never named.</b> This file exists because a
/// colour chosen by eye is right on one theme and wrong on the next, and
/// because <c>Color.None</c> is not a colour - a role left on it takes
/// whatever the terminal happens to be showing, which is how the muted panes
/// once shipped as dark text on a light block. The picked row is the plain
/// attribute turned over: the text takes the ground, the ground takes the
/// text. That is the lightest thing the theme already has, and it cannot come
/// out unreadable because both halves come from one pair.
/// </para>
/// <para>
/// <b>Serialised, like the theme tests beside it.</b> <c>ConsoleTheme.Apply</c>
/// loads the library's configuration sources once per process behind a flag;
/// two tests racing it find the theme half-registered and get
/// <c>KeyNotFoundException: 'Dark'</c> - which reads exactly like a theme that
/// does not exist.
/// </para>
/// </remarks>
[NotInParallel]
public class ThePickedRowIsABlockOfLightTests
{
    [Test]
    public async Task The_picked_row_inverts_the_plain_one()
    {
        ConsoleTheme.Apply();

        var grounded = ConsoleTheme.Grounded();
        var picked = ConsoleTheme.Picked();

        var plain = grounded.GetAttributeForRole(VisualRole.Normal);
        var row = picked.GetAttributeForRole(VisualRole.Focus);

        await Assert.That(row.Background).IsEqualTo(plain.Foreground)
            .Because("the row's ground is the console's own text colour, which is the "
                   + "lightest thing the theme has already chosen.");

        await Assert.That(row.Foreground).IsEqualTo(plain.Background)
            .Because("and its text is the console's ground, so the pair is readable by "
                   + "construction rather than by somebody checking.");
    }

    [Test]
    public async Task The_picked_row_is_not_the_ground_it_sits_on()
    {
        ConsoleTheme.Apply();

        var picked = ConsoleTheme.Picked();
        var row = picked.GetAttributeForRole(VisualRole.Focus);

        await Assert.That(row.Background).IsNotEqualTo(ConsoleTheme.Ground)
            .Because("a cursor the same colour as the surface under it is not a cursor.");

        await Assert.That(row.Background).IsNotEqualTo(Color.None)
            .Because("None is not a colour - it leaves whatever the terminal was showing, "
                   + "which is the defect this whole file was written to end.");
    }

    [Test]
    public async Task The_row_that_lost_the_keyboard_is_still_visible()
    {
        // THE TAB HAS TWO HALVES NOW. Crossing to the document takes the
        // keyboard off the tree, and a cursor that vanishes when focus leaves
        // would lose the one thing that says what the document is ABOUT.
        ConsoleTheme.Apply();

        var picked = ConsoleTheme.Picked();

        // ACTIVE, NOT HotNormal. Read out of TableView rather than guessed:
        // the selected row is drawn `hasFocus ? scheme.Focus : scheme.Active`.
        await Assert.That(picked.GetAttributeForRole(VisualRole.Active).Background)
            .IsEqualTo(picked.GetAttributeForRole(VisualRole.Focus).Background)
            .Because("the table draws its selected row with Active once it no longer holds "
                   + "the keyboard, so both roles have to be the block.");
    }

    [Test]
    public async Task The_tree_is_drawn_inside_a_border()
    {
        var screen = Sources.Read("Gg.Console", "Views", "ConsoleScreen.cs");

        await Assert.That(screen).Contains("_airspaceTreePane", StringComparison.Ordinal)
            .Because("the document beside it is inside a bordered tab bar, and a half with "
                   + "an edge next to a half without one reads as one pane and some loose "
                   + "text.");

        await Assert.That(screen)
            .Contains("_airspaceTreePane.Add(_airspaceTable)", StringComparison.Ordinal)
            .Because("the table goes INSIDE the frame - a frame beside it would be a box "
                   + "around nothing.");
    }

    [Test]
    public async Task The_tree_gets_the_picked_scheme()
    {
        var screen = Sources.Read("Gg.Console", "Views", "ConsoleScreen.cs");

        await Assert.That(screen)
            .Contains("ConsoleTheme.Picked()", StringComparison.Ordinal)
            .Because("a scheme nothing applies is a colour nobody sees.");
    }
}
