using Gg.Console.Views;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;

namespace Gg.Console.Tests;

/// <summary>
/// The broadcast's passphrase field looks like somewhere to type, and a refusal says how
/// much of what was typed arrived.
/// </summary>
/// <remarks>
/// <para>
/// <b>Owner, 2026-10-10:</b> <i>"the passphrase field needs a label and background color
/// as well"</i> - after a send failed with "That did not open this key." The field was a
/// bare row of the modal's own ground, so the only sign it existed was the dots appearing
/// once somebody typed into it.
/// </para>
/// <para>
/// <b>The count is the CLI prompt's, and for its reason.</b> <c>ConsoleSecretPrompt</c>
/// prints "(9 characters received)" because how much arrived is the one thing that tells a
/// person their paste or their typing landed. The console's refusal said nothing of the
/// kind, so a wrong passphrase and a field that swallowed keys read the same.
/// </para>
/// </remarks>
[NotInParallel]
public class ThePassphraseFieldIsAFieldTests
{
    private static double Brightness(Color color) =>
        ((0.299 * color.R) + (0.587 * color.G) + (0.114 * color.B)) / 255.0;

    [Test]
    public async Task A_field_stands_out_from_the_ground_and_keeps_its_text_readable()
    {
        ConsoleTheme.Apply();

        var ground = ConsoleTheme.Grounded().GetAttributeForRole(VisualRole.Normal);
        var field = ConsoleTheme.Field();

        foreach (var role in new[] { VisualRole.Normal, VisualRole.Editable, VisualRole.Focus })
        {
            var attribute = field.GetAttributeForRole(role);

            await Assert.That(attribute.Background).IsNotEqualTo(ground.Background)
                .Because($"a field on the modal's own ground ({role}) is invisible until typed into.");
            await Assert.That(Brightness(attribute.Background)).IsGreaterThan(Brightness(ground.Background))
                .Because("lifted off the dark ground, not sunk into it.");
            await Assert.That(Brightness(attribute.Foreground)).IsGreaterThan(Brightness(attribute.Background))
                .Because("the dots typed into it still have to read.");
        }
    }

    [Test]
    public async Task A_held_passphrase_remembers_how_long_it_was_and_nothing_else()
    {
        var held = new HeldSecret();

        held.Hold("nine char");
        _ = held.ReadSecret("passphrase");

        await Assert.That(held.Taken).IsEqualTo(9);
        await Assert.That(held.Holding).IsFalse()
            .Because("taken, not read: the value is still gone once handed over.");
    }

    [Test]
    public async Task Nothing_held_is_taken_as_nothing()
    {
        var held = new HeldSecret();

        _ = held.ReadSecret("passphrase");

        await Assert.That(held.Taken).IsEqualTo(0);
    }

    [Test]
    public async Task A_key_that_did_not_open_says_how_much_arrived()
    {
        var said = HeldSecret.Refusal("That did not open this key.", taken: 9);

        await Assert.That(said).Contains("That did not open this key.")
            .And.Contains("9 characters received");
    }

    [Test]
    public async Task An_empty_field_says_nothing_arrived()
    {
        var said = HeldSecret.Refusal("That did not open this key.", taken: 0);

        await Assert.That(said).Contains("nothing was pasted or typed");
    }
}
