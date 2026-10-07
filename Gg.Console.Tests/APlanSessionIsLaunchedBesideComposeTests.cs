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
    public async Task On_a_tenant_with_kinds_n_then_l_plans_too()
    {
        // FOUND BY THE OWNER, pressing it: with work kinds declared, `n` opens the work-kind
        // picker first and the compose choice only after a kind is chosen - so `l` there did
        // nothing. The first test above pressed it in a mode the console never opens on a
        // tenant that has declared kinds, which is every tenant in the field.
        var tenant = new AppState
        {
            Estate = new EstateOnThisMachine
            {
                Uncommitted = [],
                Names = new Gg.Contracts.EnvelopeTopology
                {
                    Names =
                    [
                        new Gg.Contracts.TopologyName
                        {
                            Name = "root", Role = Gg.Contracts.Roles.Root,
                            DeclaredBy = "kdee", DeclaredAt = DateTimeOffset.UnixEpoch,
                        },
                        new Gg.Contracts.TopologyName
                        {
                            Name = "implement", Role = Gg.Contracts.Roles.WorkKind, Parent = "root",
                            DeclaredBy = "kdee", DeclaredAt = DateTimeOffset.UnixEpoch,
                        },
                    ],
                },
            },
        };

        var asked = Press(tenant, KeyStroke.Char('n'));
        await Assert.That(asked.Mode).IsEqualTo(UiMode.WorkKindChoice)
            .Because("this is the path the owner was on: kinds are declared, so n asks for one.");

        await Assert.That(Keymap.Resolve(KeyStroke.Char('l'), KeymapContext.For(asked)))
            .IsEqualTo(Command.PlanWithAgent);
    }

    private static AppState Press(AppState state, KeyStroke key) =>
        Keymap.Resolve(key, KeymapContext.For(state)) is { } command
            ? Reducer.Reduce(state, command)
            : state;

    [Test]
    public async Task It_is_the_shells_to_run_and_the_reducers_to_leave()
    {
        await Assert.That(ShellCommands.Handled).Contains(Command.PlanWithAgent)
            .Because("it hands the terminal to a child, which a UI session may never do.");

        var before = new AppState { Mode = UiMode.ComposeChoice };
        await Assert.That(Reducer.Reduce(before, Command.PlanWithAgent)).IsEqualTo(before);
    }
}
