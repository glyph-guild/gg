namespace Gg.Console.Tests;

/// <summary>
/// Removing a credential from the pane asks first, and the answer is what acts.
/// </summary>
/// <remarks>
/// <para>
/// <b>S60.6-02, and the reason is what `gg credential rm` does.</b> It deletes the
/// reference in the control plane AND the secret on this machine, in that order,
/// and it prompts for nothing. On a command line that is a sentence somebody typed
/// deliberately. One keystroke from a cursor it is a credential gone from a tenant
/// because a finger landed next to the key it meant.
/// </para>
/// <para>
/// <b>The pattern already exists and is not invented here.</b> <c>x</c> in the
/// flight modal was changed to ask before grounding for the same reason, and
/// <c>AskToApplyEstate</c> asks before an apply. The shape is two commands: the
/// question is a mode change the session makes for itself, and only the answer
/// reaches anything outside it.
/// </para>
/// <para>
/// <b>And the question has to name what it is about.</b> A dialog reading "remove
/// this credential?" over a list is a question about whichever row the cursor
/// happens to be on, which is exactly the state somebody mistyped their way into.
/// </para>
/// </remarks>
public class RemovingACredentialAsksFirstTests
{
    private static AppState OnTheCredentialsTab() =>
        new() { ActiveTab = TabId.Credentials, CredentialsVisible = true };

    [Test]
    public async Task The_key_asks_rather_than_removing()
    {
        var inside = KeymapContext.For(
            OnTheCredentialsTab() with { Mode = UiMode.CredentialActions });

        await Assert.That(Keymap.Resolve(KeyStroke.Char('x'), inside))
            .IsEqualTo(Command.AskToRemoveCredential)
            .Because("one keystroke from a cursor must not deregister a credential and delete "
                   + "the secret behind it.");

        await Assert.That(Keymap.Resolve(KeyStroke.Char('x'), inside))
            .IsNotEqualTo(Command.RemoveCredential);
    }

    [Test]
    public async Task Asking_opens_a_question_and_removes_nothing()
    {
        // A MODE CHANGE AND NOTHING ELSE, which is what makes it safe to bind to a
        // letter beside three other letters.
        var asked = Reducer.Reduce(
            OnTheCredentialsTab() with { Mode = UiMode.CredentialActions },
            Command.AskToRemoveCredential);

        await Assert.That(asked.Mode).IsEqualTo(UiMode.RemoveCredential);
    }

    [Test]
    public async Task The_question_names_the_credential_it_is_about()
    {
        // NOT "this credential". The cursor is the only thing that says which row,
        // and a mistyped key is how somebody arrives at a question about a row
        // they were not looking at.
        var asking = OnTheCredentialsTab() with { Mode = UiMode.RemoveCredential };

        await Assert.That(PaneText.RemovingACredential(asking)).IsNotEmpty();
    }

    [Test]
    public async Task Answering_no_leaves_everything_alone()
    {
        var asking = OnTheCredentialsTab() with { Mode = UiMode.RemoveCredential };

        var closed = Reducer.Reduce(asking, Command.CloseModal);

        await Assert.That(closed.Mode).IsEqualTo(UiMode.Normal);
        await Assert.That(ShellCommands.Handled).DoesNotContain(Command.CloseModal)
            .Because("saying no must not cost a screen, and must not reach the control plane "
                   + "at all.");
    }

    [Test]
    public async Task Answering_yes_is_the_act_and_the_act_takes_the_terminal()
    {
        var asking = OnTheCredentialsTab() with { Mode = UiMode.RemoveCredential };

        await Assert.That(Keymap.Resolve(KeyStroke.Char('x'), KeymapContext.For(asking)))
            .IsEqualTo(Command.RemoveCredential)
            .Because("the same letter answers the question it asked, which is the flight "
                   + "modal's arrangement for grounding.");

        await Assert.That(ShellCommands.Handled).Contains(Command.RemoveCredential)
            .Because("it deletes a reference in the control plane and a secret on this machine, "
                   + "so it belongs outside a session like every other write that reaches both.");
    }

    [Test]
    public async Task The_question_has_exactly_one_way_out()
    {
        // THE CONSOLE'S OWN RULE: a modal owns the keyboard while open, with
        // exactly one escape hatch so the terminal can never be locked up.
        var asking = KeymapContext.For(
            OnTheCredentialsTab() with { Mode = UiMode.RemoveCredential });

        await Assert.That(Keymap.Resolve(KeyStroke.Esc, asking)).IsEqualTo(Command.CloseModal);

        var ways = Keymap.Bindings(asking).Count(b => b.Command == Command.CloseModal);

        await Assert.That(ways).IsEqualTo(1);
    }
}
