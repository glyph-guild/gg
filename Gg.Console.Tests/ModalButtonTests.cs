namespace Gg.Console.Tests;

/// <summary>
/// The buttons a modal shows, and the labels on them.
/// </summary>
/// <remarks>
/// <para>
/// <b>Derived, so a button cannot say something the key does not do.</b> Which
/// answers a modal has is <see cref="Keymap"/>'s already; a hand-kept list
/// beside it would be a second place the same thing is written, and the one that
/// drifts is the one nobody is looking at. A button is a second way to REACH a
/// decision, never a second decision.
/// </para>
/// <para>
/// <b>Pure, so it is answerable without a screen.</b> What the view does with
/// these is render them; that they are the right ones, labelled sensibly, is a
/// question about values — the same reason <c>PtyScreen</c> returns a frame
/// rather than writing one.
/// </para>
/// </remarks>
public class ModalButtonTests
{
    private static KeymapContext In(UiMode mode) => new(mode);

    [Test]
    public async Task A_modal_that_asks_something_offers_its_answers_as_buttons()
    {
        // THE ONE THIS WAS BUILT FOR. Two answers, and until now the only place
        // they appeared was the hint line at the foot of the screen - the
        // furthest point from where somebody who just pressed `n` is looking.
        var buttons = Keymap.Buttons(In(UiMode.ComposeChoice));

        await Assert.That(buttons.Select(b => b.Command))
            .IsEquivalentTo((Command[])[Command.ComposeInEditor, Command.ComposeWithAgent]);
    }

    [Test]
    public async Task The_escape_hatch_never_becomes_a_button()
    {
        // It is on every modal, and the frame around the box already means
        // "there is a way out of this". A button for it would be the one
        // affordance a person did not need help finding.
        foreach (var mode in Enum.GetValues<UiMode>())
        {
            await Assert.That(Keymap.Buttons(In(mode)).Any(b => b.Key == KeyStroke.Esc))
                .IsFalse()
                .Because($"{mode} offers escape as a button.");
        }
    }

    [Test]
    public async Task Normal_mode_has_none_because_it_is_not_asking_anything()
    {
        // A row of buttons under the whole console would be furniture. These
        // belong to a question, and Normal mode is not one.
        await Assert.That(Keymap.Buttons(In(UiMode.Normal))).IsEmpty();
    }

    [Test]
    public async Task A_mode_labels_all_of_its_answers_or_none_of_them()
    {
        // ALL OR NOTHING, and that is the rule that makes rolling this out
        // safe. A modal showing a button for `approve` and nothing for `reject`
        // is worse than one showing neither: it reads as though approving is
        // the only thing on offer. So a mode joins in when somebody has written
        // every label, and until then it looks exactly as it does today.
        foreach (var mode in Enum.GetValues<UiMode>().Where(m => m != UiMode.Normal))
        {
            var answers = Keymap.Bindings(In(mode))
                .Where(b => b.Key != KeyStroke.Esc)
                .ToList();

            var buttons = Keymap.Buttons(In(mode));

            if (buttons.Count == 0)
            {
                continue;
            }

            await Assert.That(buttons.Count).IsEqualTo(answers.Count)
                .Because($"{mode} shows {buttons.Count} buttons for {answers.Count} answers, "
                       + "so at least one answer is reachable by key and not by mouse - which "
                       + "is a menu that hides one of its options.");
        }
    }

    [Test]
    public async Task A_label_is_a_word_or_two_rather_than_the_hint_line_sentence()
    {
        // The description is written for a line that explains; a button is a
        // thing you point at. "write it in your editor" is right in one place
        // and too long for the other, and using one for both is what made the
        // first attempt render two half-labels.
        foreach (var mode in Enum.GetValues<UiMode>())
        {
            foreach (var b in Keymap.Buttons(In(mode)))
            {
                await Assert.That(b.Label).IsNotNull();
                await Assert.That(b.Label!.Length).IsLessThanOrEqualTo(14)
                    .Because($"'{b.Label}' is a sentence on a button. Two of these sit side "
                           + "by side in a box a person's eye takes in at once.");
            }
        }
    }

    [Test]
    public async Task Every_button_is_a_key_that_really_resolves()
    {
        // THE HAZARD THIS WHOLE SHAPE EXISTS TO AVOID: a button that sends a
        // command the keymap would not, which is a second decision wearing the
        // first one's clothes.
        foreach (var mode in Enum.GetValues<UiMode>())
        {
            foreach (var b in Keymap.Buttons(In(mode)))
            {
                await Assert.That(Keymap.Resolve(b.Key, In(mode))).IsEqualTo(b.Command)
                    .Because($"the button labelled '{b.Label}' claims to be {b.Key}.");
            }
        }
    }

    [Test]
    public async Task The_compose_modal_says_what_the_two_answers_mean()
    {
        // THE DEFECT A SPIKE FOUND ON MAIN. PaneText.Modal had no arm for this
        // mode, so the box was drawn with a title and NOTHING IN IT. The test
        // that was supposed to cover it asserted the HINTS name both ways, which
        // they did - the hints are a different surface, at the other end of the
        // screen.
        var body = PaneText.Modal(new AppState { Mode = UiMode.ComposeChoice });

        await Assert.That(body).IsNotEmpty()
            .Because("a titled empty box is what this rendered as.");

        foreach (var word in (string[])["editor", "agent"])
        {
            await Assert.That(body).Contains(word, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Test]
    public async Task No_modal_that_asks_something_is_drawn_empty()
    {
        // THE RATCHET FOR IT, over every mode rather than the one that was
        // wrong. A mode added later gets a title from ModalTitle and a body from
        // Modal, and forgetting the second is silent - it looks like a box that
        // has not loaded yet.
        var empty = Enum.GetValues<UiMode>()
            .Where(m => m != UiMode.Normal)
            .Where(m => PaneText.Modal(new AppState { Mode = m }).Trim().Length == 0)
            .ToList();

        await Assert.That(empty).IsEmpty()
            .Because("these draw a title over nothing: " + string.Join(", ", empty));
    }
}
