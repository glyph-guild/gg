using System.Linq;
using Gg.Console;

namespace Gg.Console.Tests;

/// <summary>
/// A key a modal binds has to reach the keymap before the dialog eats it.
/// </summary>
/// <remarks>
/// <para>
/// <b>FOUND IN A PTY, AND INVISIBLE EVERYWHERE ELSE.</b> The filter modal
/// binds enter to "pick this"; <c>Keymap.Resolve</c> answers it, the reducer
/// acts on it, and both are covered. In a real terminal nothing happened: the
/// dialog has the keyboard, Terminal.Gui gives enter its own meaning on a
/// <c>Dialog</c>, and the screen's handler - which is on the screen, above the
/// dialog - is never reached.
/// </para>
/// <para>
/// <b>Two modals bind enter and both were affected</b>, which is why this is a
/// ratchet rather than a line in one of them: the work kind question is
/// confirmed with enter as well, and it has been unanswerable that way since it
/// shipped.
/// </para>
/// <para>
/// <b>Read off the source, because the routing is Terminal.Gui's.</b> What is
/// checkable without a terminal is that the dialog is subscribed at all -
/// whether the key then resolves is the keymap's business, and that is tested
/// directly everywhere else.
/// </para>
/// </remarks>
public class AModalHearsItsOwnKeysTests
{
    [Test]
    public async Task The_dialog_is_subscribed_to_its_own_keys()
    {
        var screen = ConsoleSource.Text("Gg.Console", Path.Combine("Views", "ConsoleScreen.cs"));

        await Assert.That(screen).Contains("_modal.KeyDown +=")
            .Because("the screen's handler is above the dialog, so a key the dialog gives its "
                   + "own meaning to never gets there - which is what made enter do nothing "
                   + "in every modal that binds it.");

        await Assert.That(screen).Contains("_modal.KeyDown -=")
            .Because("the screen is disposed and rebuilt on every terminal release, and a "
                   + "handler that is added and never removed is one per session.");
    }

    [Test]
    public async Task Every_modal_key_the_keymap_answers_is_one_a_person_can_press()
    {
        // THE LIST THIS EXISTS FOR. Both of these modes are dialogs with a
        // cursor in them, and both bind enter to the decision the modal is
        // about.
        foreach (var mode in (UiMode[])[UiMode.BrowseFilter, UiMode.WorkKindChoice])
        {
            await Assert.That(
                Keymap.Bindings(new KeymapContext(mode)).Any(b => b.Key == KeyStroke.EnterKey))
                .IsTrue()
                .Because($"{mode} is one of the modes this ratchet is about.");
        }
    }
}
