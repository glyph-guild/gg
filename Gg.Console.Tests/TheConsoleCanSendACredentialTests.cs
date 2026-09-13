namespace Gg.Console.Tests;

/// <summary>
/// A person looking at a runner that cannot resolve a credential can give it one
/// without leaving the console.
/// </summary>
/// <remarks>
/// <para>
/// <b>The gap <c>VerbParityTests</c> recorded, and the reason it was the one
/// that cost the most.</b> The queue now says a flight is blocked on a
/// credential and names the machine; the runner modal is where that machine is
/// on the screen; and the only way to act on it was to quit, find the runner id
/// again, and type a command. A console that can diagnose something and not fix
/// it is a console a person leaves at exactly the moment it was useful.
/// </para>
/// <para>
/// <b>`c`, and it sits beside `w` for the same reason `w` sits where it
/// does.</b> Both go through the control plane rather than through a pidfile
/// this machine wrote, so both are exactly as available over somebody else's
/// runner as over this one — what gates them is whether the far end registered
/// to this principal, which only the control plane can answer.
/// </para>
/// <para>
/// <b>Withheld while a machine is not beating, which is `w`'s rule and the same
/// mechanism.</b> An introduction is picked up on a heartbeat, so a runner that
/// is not sending them never sees one. Offering the key anyway would spend a
/// person's twenty seconds to tell them what the fleet pane already showed.
/// </para>
/// </remarks>
public class TheConsoleCanSendACredentialTests
{
    private static KeymapContext Looking(bool ours, bool beating) => new()
    {
        Mode = UiMode.Runner,
        RunnerIsOurs = ours,
        RunnerIsBeating = beating,
    };

    [Test]
    public async Task It_is_offered_over_any_runner_that_is_beating()
    {
        // BOTH SHAPES OF THIS MODAL. Ours gets restart and shut-down and
        // somebody else's does not, because those go through a pidfile this
        // machine wrote. This one does not, so a binding in only one list would
        // be the gap `w` was written to close.
        foreach (var ours in (bool[])[true, false])
        {
            await Assert.That(Keymap.Resolve(KeyStroke.Char('c'), Looking(ours, beating: true)))
                .IsEqualTo(Command.SendCredential)
                .Because("reaching a machine goes through the control plane, which is exactly "
                       + "as able to introduce you to somebody else's runner as to this one.");
        }
    }

    [Test]
    public async Task It_is_withheld_while_the_machine_is_not_beating()
    {
        foreach (var ours in (bool[])[true, false])
        {
            await Assert.That(Keymap.Resolve(KeyStroke.Char('c'), Looking(ours, beating: false)))
                .IsNull()
                .Because("an introduction is picked up on a heartbeat, so a machine that is "
                       + "not sending them never sees one - and offering the key would spend "
                       + "somebody's twenty seconds to tell them what the fleet pane said.");
        }
    }

    [Test]
    public async Task It_shadows_nothing_already_live_in_this_modal()
    {
        // ONE LETTER, ONE ACT. Keymap.Resolve answers with the FIRST match, so a
        // second binding on a live key is not a conflict anybody sees - it is a
        // key that silently stops doing what it used to.
        var bound = Keymap.Bindings(Looking(ours: true, beating: true));

        await Assert.That(bound.Count(b => b.Key == KeyStroke.Char('c'))).IsEqualTo(1)
            .Because("two bindings on one key in one mode is how a key comes to mean two "
                   + "things on one screen, and the second one never runs.");
    }

    [Test]
    public async Task The_modal_says_the_key_is_there()
    {
        // A KEY NOBODY CAN FIND IS A KEY NOBODY HAS, and this modal is keys-only
        // - there is no button to notice and no menu to open, so the hint line
        // is the whole of its discoverability.
        var hints = Keymap.Hints(Looking(ours: true, beating: true));

        await Assert.That(hints).Contains("credential", StringComparison.OrdinalIgnoreCase);
    }
}
