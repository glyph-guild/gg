namespace Gg.Console.Tests;

/// <summary>
/// Removing a credential asks first — by making somebody name it.
/// </summary>
/// <remarks>
/// <para>
/// <b>S60.6-02 ASSUMED A DEFECT THAT IS NOT THERE, and this class records what is
/// true instead.</b> The criterion was written expecting `x` to deregister the row
/// under the cursor on one keystroke. It does not: <c>VerbConsoleActions.
/// ForgetCredential</c> releases the terminal, prompts <i>"Which repository's
/// credential should be forgotten?"</i>, re-reads the list, and matches by name.
/// An empty answer forgets nothing and a name that matches nothing forgets nothing.
/// </para>
/// <para>
/// <b>And naming it is a stronger guard than a yes/no confirm.</b> A confirm
/// defends against the wrong keystroke; typing a repository defends against the
/// wrong keystroke AND against the cursor being somewhere a person did not think it
/// was — which is the failure that actually happens in a list that reorders itself
/// as work is done. The port says so: <i>"Removing the wrong credential because the
/// list moved is not a mistake this can be allowed to make."</i>
/// </para>
/// <para>
/// <b>So what this slice adds is not a question but a LABEL.</b> `c` and `x` are
/// bound in Normal with <c>OffTheHintLine = true</c> — advertised nowhere, on a
/// console whose own rule is that a bound key a person cannot find is a key that
/// does not exist. They move behind `a` on the credentials pane, where they get
/// words.
/// </para>
/// </remarks>
public class RemovingACredentialAsksFirstTests
{
    private static AppState OnTheCredentialsTab() =>
        new() { ActiveTab = TabId.Credentials, CredentialsVisible = true };

    [Test]
    public async Task Forgetting_one_reaches_the_terminal_rather_than_acting_in_session()
    {
        // THE GUARD IS THE PROMPT, and the prompt needs the terminal. A forget
        // reduced inside the session would be one that could not ask.
        await Assert.That(ShellCommands.Handled).Contains(Command.ForgetCredential)
            .Because("it asks which repository's credential to forget, and a Terminal.Gui "
                   + "session cannot read a line at a prompt.");
    }

    [Test]
    public async Task The_question_it_asks_names_a_repository_rather_than_offering_yes_or_no()
    {
        // ASSERTED OVER THE SOURCE because the prompt lives behind an ISecretPrompt
        // the loop owns, and what matters is WHICH question is asked: a yes/no
        // cannot tell a mistyped cursor from a deliberate removal, and a name can.
        var source = Source("Gg.Console", "VerbConsoleActions.cs");

        await Assert.That(source).Contains("Which repository's credential should be forgotten?")
            .Because("the guard against removing the wrong credential is that somebody names "
                   + "the right one, not that they press y.");

        await Assert.That(source).Contains("no repository was named")
            .Because("and an empty answer has to forget nothing rather than fall through to a "
                   + "default.");
    }

    [Test]
    public async Task It_re_reads_the_list_rather_than_trusting_the_model()
    {
        // THE MODEL MAY BE A MINUTE OLD. The console refreshes every thirty
        // seconds and a credential list reorders as work is done, so resolving a
        // name against held state is how the wrong one gets removed.
        var source = Source("Gg.Console", "VerbConsoleActions.cs");
        var forget = Between(source, "public string ForgetCredential()", "public string Invite");

        await Assert.That(forget).Contains("ListCredentialsAsync")
            .Because("the name is resolved against a fresh read, which the port states in those "
                   + "words: removing the wrong credential because the list moved is not a "
                   + "mistake this can be allowed to make.");
    }

    [Test]
    public async Task And_it_is_reachable_with_a_word_rather_than_an_unadvertised_letter()
    {
        // THE ACTUAL GAP. `x` and `c` are bound in Normal with OffTheHintLine, so
        // this console's two credential writes are advertised nowhere - and its own
        // rule is that a bound key a person cannot find is a key that does not
        // exist. Behind `a` they get labels.
        var inside = KeymapContext.For(
            OnTheCredentialsTab() with { Mode = UiMode.CredentialActions });

        var offered = Keymap.Bindings(inside);

        foreach (var act in (Command[])
                 [Command.AddCredential, Command.ForgetCredential, Command.MintPersonKey])
        {
            var matching = offered.Where(b => b.Command == act).ToList();

            await Assert.That(matching.Count).IsEqualTo(1)
                .Because($"{act} must be offered here exactly once: none is a dead act, and "
                       + "twice is two keys for one thing.");

            await Assert.That(matching[0].Description).IsNotEmpty()
                .Because("inside a modal every key carries a word, which is the whole reason "
                       + "these three could leave the hint line.");
        }
    }

    private static string Source(string project, string file)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Gg.sln")))
        {
            dir = dir.Parent;
        }

        return File.ReadAllText(Path.Combine(dir!.FullName, project, file));
    }

    private static string Between(string source, string from, string to)
    {
        var start = source.IndexOf(from, StringComparison.Ordinal);
        var end = start < 0 ? -1 : source.IndexOf(to, start, StringComparison.Ordinal);

        return start < 0 || end < 0 ? "" : source[start..end];
    }
}
