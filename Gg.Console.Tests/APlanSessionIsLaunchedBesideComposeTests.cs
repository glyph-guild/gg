namespace Gg.Console.Tests;

/// <summary>
/// <b>S66.2-01</b> - <c>n</c> then <c>l</c> in the compose choice starts the plan session; it is a
/// shell command the reducer leaves alone.
/// </summary>
/// <remarks>
/// <b>Beside compose</b> (slice sixty-six rule 1): <c>l</c> (for legs) is free in Normal mode, so pressing it one keypress early does nothing else, and a plan is
/// new work as an intent is. Help naming the key is HelpNamesEveryKeyTests' to hold.
/// </remarks>
public class APlanSessionIsLaunchedBesideComposeTests
{
    [Test]
    public async Task L_in_the_compose_choice_plans_with_an_agent()
    {
        var resolved = Keymap.Resolve(
            KeyStroke.Char('l'), new KeymapContext(UiMode.ComposeChoice, TabId.Flights));

        await Assert.That(resolved).IsEqualTo(Command.PlanWithAgent);
    }

    [Test]
    public async Task It_is_the_shells_to_run_and_the_reducers_to_leave()
    {
        await Assert.That(ShellCommands.Handled).Contains(Command.PlanWithAgent)
            .Because("it hands the terminal to a child, which a UI session may never do.");

        var before = new AppState { Mode = UiMode.ComposeChoice };
        await Assert.That(Reducer.Reduce(before, Command.PlanWithAgent)).IsEqualTo(before);
    }
}
