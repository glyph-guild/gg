namespace Gg.Console.Tests;

/// <summary>
/// The runner modal offers no buttons, whichever runner it is open on.
/// </summary>
/// <remarks>
/// <para>
/// <b>ASKED FOR, AND IT REVERSES A RECORDED DECISION.</b>
/// <c>ModalButtonTests</c> put this modal on the buttons side of its line, on
/// the argument that restarting a runner meant to be up anyway is "undone by
/// doing it again or by doing nothing". That argument still holds about the
/// ACT; what changed is the room it is offered in.
/// </para>
/// <para>
/// <b>THE MODAL GREW A TAB BAR ALONG THE SAME FOOT.</b> A row of buttons under
/// a bar somebody can already click puts two clickable things in one place —
/// and <c>Next view</c> does exactly what clicking a tab header does, which is
/// two ways to do one thing.
/// </para>
/// <para>
/// <b>AND THE TWO SHAPES OF THIS MODAL NOW AGREE.</b> Somebody else's runner
/// has always been keys-only; ours had buttons. The modal looked different
/// depending on whose machine you opened, which is a difference nobody asked
/// for and one this removes.
/// </para>
/// <para>
/// <b>Nothing becomes undiscoverable.</b> Every key stays bound and stays on
/// the hint line — <c>r restart it · x shut it down · v next view · esc
/// close</c> — which is the rule <c>KeyBinding.OffTheHintLine</c> states: off
/// the line is not out of the program, and here nothing even leaves the line.
/// </para>
/// </remarks>
public class TheRunnerModalIsKeysOnlyTests
{
    [Test]
    public async Task Our_own_runner_offers_no_buttons()
    {
        var buttons = Keymap.Buttons(new KeymapContext
        {
            Mode = UiMode.Runner,
            RunnerIsOurs = true,
            RunnerIsBeating = true,
        });

        await Assert.That(buttons).IsEmpty()
            .Because("the foot of this modal is a tab bar now, and a button row under it is "
                   + "a second clickable thing in the same place.");
    }

    [Test]
    public async Task Somebody_elses_runner_still_offers_none()
    {
        var buttons = Keymap.Buttons(new KeymapContext
        {
            Mode = UiMode.Runner,
            RunnerIsOurs = false,
            RunnerIsBeating = true,
        });

        await Assert.That(buttons).IsEmpty();
    }

    [Test]
    public async Task Every_key_is_still_bound_and_still_advertised()
    {
        // OFF THE BUTTONS IS NOT OUT OF THE PROGRAM, and asserted rather than
        // assumed: taking a button away by deleting the binding would look
        // identical on screen and be a different change entirely.
        var ours = new KeymapContext
        {
            Mode = UiMode.Runner,
            RunnerIsOurs = true,
            RunnerIsBeating = true,
        };

        foreach (var (stroke, command) in new (KeyStroke, Command)[]
        {
            (KeyStroke.Char('r'), Command.RestartRunner),
            (KeyStroke.Char('x'), Command.StopRunner),
            (KeyStroke.Char('w'), Command.WatchRunner),
            (KeyStroke.Char('v'), Command.NextRunnerView),
            (KeyStroke.Esc, Command.CloseModal),
        })
        {
            await Assert.That(Keymap.Resolve(stroke, ours)).IsEqualTo(command)
                .Because($"{stroke} still does what it did; only the button went.");
        }

        var line = Keymap.Hints(ours);

        foreach (var said in (string[])
                 ["r restart it", "x shut it down", "v next view", "esc close"])
        {
            await Assert.That(line).Contains(said, StringComparison.Ordinal)
                .Because("a modal with no buttons has only its line to teach the keys, so "
                       + "every one of them has to be on it. Line: " + line);
        }
    }

    [Test]
    public async Task No_binding_in_this_modal_carries_a_label()
    {
        // THE MECHANISM, ASSERTED. Buttons() is all-or-nothing: it offers them
        // only when EVERY answer carries a label, so one label added back here
        // would not add one button - it would silently do nothing until
        // somebody added the rest. Holding the labels at zero says which state
        // this modal is in rather than leaving it to arithmetic.
        foreach (var ours in (bool[])[true, false])
        {
            var bindings = Keymap.Bindings(new KeymapContext
            {
                Mode = UiMode.Runner,
                RunnerIsOurs = ours,
                RunnerIsBeating = true,
            });

            await Assert.That(bindings.Where(b => b.Label is { Length: > 0 })).IsEmpty();
        }
    }
}
