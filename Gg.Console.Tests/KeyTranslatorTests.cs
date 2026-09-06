using Gg.Console.Views;
using Terminal.Gui.Input;

namespace Gg.Console.Tests;

/// <summary>
/// Every key the keymap answers can actually be produced from a terminal.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE SEAM THAT HAD NO TEST, AND IT COST A FEATURE.</b> Enter was added to
/// the keymap, bound to a command, and rendered in the help page - and pressing
/// it did nothing, because <c>KeyTranslator</c> never learned it. Its rune is
/// KeyCode 13, which the fall-through rejects as a control character, so it
/// became a <c>KeyStroke</c> with nothing set at all and matched no binding.
/// </para>
/// <para>
/// <b>A producer row and a consumer row do not add up to a row for the wire
/// between them.</b> The keymap is tested purely and the translator needs no
/// terminal either - a <c>Key</c> is an object - so the only reason this gap
/// existed is that nobody had written the test that spans them.
/// </para>
/// </remarks>
public class KeyTranslatorTests
{
    [Test]
    public async Task Every_key_the_keymap_answers_can_be_typed()
    {
        // WALKED OVER THE CATALOGUE, so the next named key fails here rather
        // than in somebody's terminal. A char binding is producible by
        // construction; a NAMED key needs an arm in the translator, and this is
        // what says so.
        foreach (var entry in Keymap.Catalogue())
        {
            var stroke = entry.Binding.Key;

            var typed = stroke switch
            {
                { Escape: true } => KeyTranslator.Translate(Key.Esc),
                { Tab: true } => KeyTranslator.Translate(Key.Tab),
                { Enter: true } => KeyTranslator.Translate(Key.Enter),
                { Ctrl: true, Input: { } c } => KeyTranslator.Translate(new Key(c).WithCtrl),
                { Input: { } c } => KeyTranslator.Translate(new Key(c)),
                _ => default,
            };

            await Assert.That(typed).IsEqualTo(stroke)
                .Because($"the keymap answers {stroke.Name} ({entry.Binding.Description}), so a "
                       + $"terminal has to be able to send it. Translated to: {typed.Name}");
        }
    }

    [Test]
    public async Task The_interrupt_is_typable_too()
    {
        // Not in the catalogue, because it is not a binding: it is handled
        // ahead of the table in every mode. It still has to arrive.
        await Assert.That(KeyTranslator.Translate(Key.C.WithCtrl)).IsEqualTo(Keymap.Interrupt);
    }

    [Test]
    public async Task A_key_the_console_does_not_answer_translates_to_something_harmless()
    {
        // THE ANCHOR. A translator that returned `enter` for everything would
        // satisfy the walk above.
        var unbound = KeyTranslator.Translate(Key.F5);

        await Assert.That(Keymap.Resolve(unbound, new KeymapContext(UiMode.Normal))).IsNull()
            .Because("F5 means nothing here, and a keystroke that resolved anyway would be a "
                   + "key doing something nobody asked for.");
    }
}
