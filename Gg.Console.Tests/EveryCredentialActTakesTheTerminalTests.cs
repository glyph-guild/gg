namespace Gg.Console.Tests;

/// <summary>
/// Every act on a credential happens between sessions, with the terminal
/// provably free.
/// </summary>
/// <remarks>
/// <para>
/// <b>S60.6-01, and the reason is the echo.</b> Three of the four acts read
/// something a person types that must not appear on screen — a secret, a
/// passphrase typed twice — and a Terminal.Gui session cannot turn the echo off
/// at all. <c>ConsoleSendCredential</c> already says this in those words and takes
/// the editor's slot for it; the other three join that arrangement rather than
/// inventing a second one.
/// </para>
/// <para>
/// <b>They live behind <c>a</c> rather than on four letters.</b> Normal mode has
/// only <c>l m p s z</c> free, so two of the four acts would have got a letter
/// nobody could find. The airspace tab already solved this: its four acts
/// collapsed behind <c>a</c> and kept their letters, <i>"because inside a modal
/// the letters are free, which is the whole reason they could collapse without
/// anybody relearning one."</i>
/// </para>
/// <para>
/// <b>Pushing is already here and is not moved.</b> It is reached from the runner
/// modal, where the machine being given a credential is on the screen. What this
/// adds is the three acts that had nowhere to be reached from at all.
/// </para>
/// </remarks>
public class EveryCredentialActTakesTheTerminalTests
{
    private static AppState OnTheCredentialsTab() =>
        new() { ActiveTab = TabId.Credentials, CredentialsVisible = true };

    [Test]
    public async Task The_tabs_actions_key_opens_the_credential_acts()
    {
        // DECIDED IN THE SHARED ARM, not in a tab spread. Keymap.Resolve answers
        // the FIRST match and `a` is bound above the spreads, so an arm added down
        // there would never fire - which a red test already caught once for
        // `enter` and the comment there warns about by name.
        await Assert.That(Keymap.Resolve(
                KeyStroke.Char('a'), KeymapContext.For(OnTheCredentialsTab())))
            .IsEqualTo(Command.ToggleCredentialActions);
    }

    [Test]
    public async Task Each_act_is_a_key_inside_the_modal()
    {
        var inside = KeymapContext.For(OnTheCredentialsTab() with { Mode = UiMode.CredentialActions });

        await Assert.That(Keymap.Resolve(KeyStroke.Char('n'), inside))
            .IsEqualTo(Command.AddCredential);

        await Assert.That(Keymap.Resolve(KeyStroke.Char('x'), inside))
            .IsEqualTo(Command.AskToRemoveCredential);

        await Assert.That(Keymap.Resolve(KeyStroke.Char('m'), inside))
            .IsEqualTo(Command.MintPersonKey);

        // THE ONE WAY OUT, which every modal in this console has exactly one of.
        await Assert.That(Keymap.Resolve(KeyStroke.Esc, inside))
            .IsEqualTo(Command.CloseModal);
    }

    [Test]
    public async Task Letters_taken_in_normal_are_free_inside_the_modal()
    {
        // `n` IS NEW FLIGHT AND `x` IS SOMETHING ELSE IN NORMAL, which is exactly
        // why the modal exists: the four free letters left in Normal are l m p s z,
        // and "s for add a credential" is a key nobody finds.
        var normal = KeymapContext.For(OnTheCredentialsTab());

        await Assert.That(Keymap.Resolve(KeyStroke.Char('n'), normal))
            .IsNotEqualTo(Command.AddCredential);

        await Assert.That(Keymap.Resolve(KeyStroke.Char('n'), normal)).IsNotNull()
            .Because("it still means what it always meant outside the modal.");
    }

    [Test]
    public async Task The_three_new_acts_take_the_terminal_like_the_send_does()
    {
        // THE SHELL HANDLES THEM, which is what "between sessions" means here:
        // the UI is torn down, the child or the prompt owns the terminal, and the
        // next session is rebuilt from the surviving AppState.
        foreach (var act in (Command[])
                 [Command.AddCredential, Command.RemoveCredential, Command.MintPersonKey])
        {
            await Assert.That(ShellCommands.Handled).Contains(act)
                .Because($"{act} reads something with the echo off, and a Terminal.Gui session "
                       + "cannot turn the echo off at all.");
        }

        // AND THE SEND WAS ALREADY THERE. It is not moved by this step; what this
        // adds is the three that had nowhere to be reached from.
        await Assert.That(ShellCommands.Handled).Contains(Command.SendCredential);
    }

    [Test]
    public async Task Asking_to_remove_does_not_take_the_terminal()
    {
        // THE QUESTION IS NOT THE ACT. Asking is a mode change the session makes
        // for itself - the airspace apply and the ground both work this way - and
        // only the ANSWER needs anything outside it.
        await Assert.That(ShellCommands.Handled).DoesNotContain(Command.AskToRemoveCredential)
            .Because("a question drawn on the screen a person is already looking at must not "
                   + "cost them that screen.");
    }

    [Test]
    public async Task The_modal_offers_a_key_for_every_act_and_no_act_without_a_key()
    {
        // THE DEAD-KEY SHAPE IS ALREADY POLICED, and by something stronger than I
        // was about to write. ShellHandledTests.Every_command_a_key_can_resolve_
        // reaches_somebody checks that EVERY resolvable command reaches either the
        // shell or a reducer arm - written after four keys were found bound,
        // advertised and inert, and tightened again when `x` stopped grounding
        // flights while a confirmation was being added to it.
        //
        // So what is left for this test is the other direction: that the modal
        // offers each act exactly once, which that ratchet cannot see.
        var inside = KeymapContext.For(OnTheCredentialsTab() with { Mode = UiMode.CredentialActions });

        var offered = Keymap.Bindings(inside).Select(b => b.Command).ToList();

        foreach (var act in (Command[])
                 [Command.AddCredential, Command.AskToRemoveCredential, Command.MintPersonKey])
        {
            await Assert.That(offered.Count(c => c == act)).IsEqualTo(1)
                .Because($"{act} must be offered once: none is a dead act, and twice is two keys "
                       + "for one thing with no way to tell which a person pressed.");
        }

        await Assert.That(offered).Contains(Command.CloseModal)
            .Because("exactly one escape hatch, so the terminal can never be locked up.");
    }
}
