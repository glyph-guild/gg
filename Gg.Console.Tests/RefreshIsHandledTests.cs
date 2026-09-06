namespace Gg.Console.Tests;

/// <summary>
/// Refresh is the tick's, and the reducer only says one is wanted.
/// </summary>
/// <remarks>
/// <para>
/// <b>It was the shell's, and the reason it stopped being is that the shell
/// tears the terminal down.</b> Every command in <c>ShellCommands.Handled</c>
/// ends the UI session, which is right for handing the screen to an editor and
/// is the console vanishing every thirty seconds on a timer. This was the only
/// READ in that set; the rest are writes and stay.
/// </para>
/// <para>
/// <b>The ratchet it was an instance of still applies, from the other side.</b>
/// A command that is NOT in the set must have an arm in the reducer, or the key
/// resolves and nothing happens - which is the defect the set was invented for,
/// arriving by the opposite route. So the assertion is inverted rather than
/// deleted: not the shell's, and the reducer answers it.
/// </para>
/// </remarks>
public class RefreshIsHandledTests
{
    [Test]
    public async Task The_shell_does_not_declare_it_any_more()
    {
        await Assert.That(ShellCommands.Handled).DoesNotContain(Command.Refresh)
            .Because("everything in that set ends the session, and a refresh that reads one "
                   + "tab has nothing to hand the terminal to.");
    }

    [Test]
    public async Task And_the_reducer_answers_it_rather_than_shrugging()
    {
        // THE HALF THE SET EXISTS FOR. A command in neither place is a key that
        // resolves and does nothing, which is what four bound keys did before
        // ShellCommands.Handled was written down.
        var asked = Reducer.Reduce(new AppState(), Command.Refresh);

        await Assert.That(asked.Refresh.Wanted).IsTrue()
            .Because("the reducer is pure, so all it can say is that one is wanted - and "
                   + "AutoRefresh is what does it, on the tick, without the session going "
                   + "away.");
    }
}
