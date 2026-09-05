namespace Gg.Console.Tests;

/// <summary>
/// Refresh is declared as a command the shell handles.
/// </summary>
/// <remarks>
/// <b>The existing ratchet doing its job on a new command.</b>
/// <c>ShellCommands.Handled</c> exists because four bound keys once resolved,
/// reached the reducer and returned the state unchanged - so every command in
/// it needs an arm in <c>ConsoleLoop</c>, and <c>ShellHandledTests</c> holds
/// that. This is the first READ in the set; everything else there is a write,
/// and it is in for the same reason: the effect lives in the loop, because a UI
/// session may not make a request.
/// </remarks>
public class RefreshIsHandledTests
{
    [Test]
    public async Task The_shell_declares_it()
    {
        await Assert.That(ShellCommands.Handled.Contains(Command.Refresh)).IsTrue()
            .Because("a key whose effect is in the loop must say so, or the reducer is asked "
                   + "for an effect it cannot have and answers by changing nothing.");
    }
}
