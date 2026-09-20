using Gg.Console;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// Whose a machine is, changed from the modal that says whose it is
/// (slice forty-six, step 2).
/// </summary>
/// <remarks>
/// <para>
/// <b>Two toggles rather than four keys.</b> Claim and unclaim are one idea
/// seen from two sides, and so are reserve and release; each key says what a
/// press will do from where the person is standing.
/// </para>
/// <para>
/// <b>The door decides whether an act applies.</b> A tenant runner refuses a
/// claim in a sentence naming what an admin would have to do, and a console
/// that made that decision itself would be a second copy of the rule that
/// could disagree with it. What the console will not do is offer the key
/// against a control plane with no such door - the pane's "empty is not open",
/// applied to a keymap.
/// </para>
/// </remarks>
public class OwnershipFromTheConsoleTests
{
    private sealed class ScriptedUi(params Func<AppState, UiOutcome>[] script) : IUiSession
    {
        private readonly Queue<Func<AppState, UiOutcome>> _script = new(script);

        public UiOutcome Run(AppState state) => _script.Dequeue()(state);
    }

    private const string Runner = "01a0bca3-b788-72e5-b40a-be3811653226";
    private const string You = "01a06111-2222-7333-8444-555566667777";

    private static AppState Fleet(
        string ownership = RunnerOwnerships.Open,
        string ownerPrincipal = "",
        bool reserved = false) => new()
    {
        ActiveTab = TabId.Runners,
        Mode = UiMode.Runner,
        RunnerSelected = 0,
        PrincipalId = You,
        Runners = new RunnerList
        {
            Runners =
            [
                new RunnerSummary
                {
                    RunnerId = Runner,
                    Label = "vmlinux002",
                    State = RunnerStates.Idle,
                    Ownership = ownership,
                    Owner = ownerPrincipal.Length > 0 ? "Kevin" : "",
                    OwnerPrincipalId = ownerPrincipal,
                    Reserved = reserved,
                },
            ],
        },
    };

    private static IReadOnlyList<KeyBinding> Keys(AppState state) =>
        Keymap.Bindings(KeymapContext.For(state));

    [Test]
    public async Task Both_toggles_are_offered_over_a_machine_whose_ownership_is_known()
    {
        var keys = Keys(Fleet());

        await Assert.That(keys.Any(k => k.Command == Command.ClaimRunner)).IsTrue();
        await Assert.That(keys.Any(k => k.Command == Command.ReserveRunner)).IsTrue();
    }

    [Test]
    public async Task Neither_is_offered_against_a_control_plane_that_says_nothing()
    {
        // Empty is not open. A claim offered against a control plane with no
        // claim door advertises a refusal.
        var keys = Keys(Fleet(ownership: ""));

        await Assert.That(keys.Any(k => k.Command == Command.ClaimRunner)).IsFalse();
        await Assert.That(keys.Any(k => k.Command == Command.ReserveRunner)).IsFalse();
    }

    [Test]
    public async Task Each_key_says_what_a_press_will_do_from_here()
    {
        var open = Keys(Fleet());
        var yours = Keys(Fleet(RunnerOwnerships.Claimed, You));
        var kept = Keys(Fleet(RunnerOwnerships.Claimed, You, reserved: true));

        await Assert.That(open.First(k => k.Command == Command.ClaimRunner).Description)
            .Contains("claim it", StringComparison.Ordinal);
        await Assert.That(yours.First(k => k.Command == Command.ClaimRunner).Description)
            .Contains("give it up", StringComparison.Ordinal);
        await Assert.That(yours.First(k => k.Command == Command.ReserveRunner).Description)
            .Contains("hold it", StringComparison.Ordinal);
        await Assert.That(kept.First(k => k.Command == Command.ReserveRunner).Description)
            .Contains("tenant's work again", StringComparison.Ordinal);
    }

    [Test]
    public async Task Somebody_elses_claim_is_not_read_as_yours()
    {
        // WHO, NOT WHAT THEY ARE CALLED. Two surfaces may render one person
        // differently, and a key decided by a display name would offer "give it
        // up" over a machine belonging to somebody with the same name.
        var theirs = Keys(Fleet(RunnerOwnerships.Claimed, "01a0aaaa-0000-7000-8000-000000000000"));

        await Assert.That(theirs.First(k => k.Command == Command.ClaimRunner).Description)
            .Contains("claim it", StringComparison.Ordinal);
    }

    [Test]
    public async Task Pressing_it_asks_the_door_and_says_what_came_back()
    {
        var actions = new ConsoleDoubles.Records();
        var ui = new ScriptedUi(
            s => new UiOutcome(Command.ClaimRunner, s),
            s => new UiOutcome(Command.Quit, s));

        var final = new ConsoleLoop(ui, new ConsoleDoubles.NoEditor(), actions: actions)
            .Run(Fleet());

        await Assert.That(actions.Claimed).Count().IsEqualTo(1);
        await Assert.That(actions.Claimed[0]).IsEqualTo((Runner, true));
        await Assert.That(final.LastDecision).Contains(Runner, StringComparison.Ordinal);
    }

    [Test]
    public async Task A_machine_you_own_is_given_up_rather_than_claimed_again()
    {
        var actions = new ConsoleDoubles.Records();
        var ui = new ScriptedUi(
            s => new UiOutcome(Command.ClaimRunner, s),
            s => new UiOutcome(Command.Quit, s));

        _ = new ConsoleLoop(ui, new ConsoleDoubles.NoEditor(), actions: actions)
            .Run(Fleet(RunnerOwnerships.Claimed, You));

        await Assert.That(actions.Claimed[0]).IsEqualTo((Runner, false))
            .Because("the key said 'give it up', and a key whose label and act disagree is "
                   + "worse than no key.");
    }

    [Test]
    public async Task Reserving_toggles_the_same_way()
    {
        var actions = new ConsoleDoubles.Records();
        var ui = new ScriptedUi(
            s => new UiOutcome(Command.ReserveRunner, s),
            s => new UiOutcome(Command.Quit, s));

        _ = new ConsoleLoop(ui, new ConsoleDoubles.NoEditor(), actions: actions)
            .Run(Fleet(RunnerOwnerships.Claimed, You, reserved: true));

        await Assert.That(actions.Reserved[0]).IsEqualTo((Runner, false));
    }

    [Test]
    public async Task A_refusal_is_the_control_planes_own_sentence()
    {
        var actions = new ConsoleDoubles.Records(refusing: true);
        var ui = new ScriptedUi(
            s => new UiOutcome(Command.ClaimRunner, s),
            s => new UiOutcome(Command.Quit, s));

        var final = new ConsoleLoop(ui, new ConsoleDoubles.NoEditor(), actions: actions)
            .Run(Fleet(RunnerOwnerships.Tenant));

        await Assert.That(final.LastDecision).Contains("the tenant's", StringComparison.Ordinal)
            .Because("the console does not compose its own refusal: the door's words name what "
                   + "an admin would have to do, and a second vocabulary could disagree.");
    }

    [Test]
    public async Task No_new_binding_is_a_letter_in_normal_mode()
    {
        // Normal mode has no free letters, and a tab-scoped binding for a
        // letter already global never fires - Keymap.Resolve returns the first
        // match and the Normal arm is declared above the tab spreads.
        var normal = Keymap.Bindings(new KeymapContext(UiMode.Normal));

        foreach (var key in (char[])['m', 'h'])
        {
            await Assert.That(normal.Any(b => b.Key == KeyStroke.Char(key))).IsFalse()
                .Because($"'{key}' is the runner modal's, and a letter live in Normal as well "
                       + "would be two sets a person holds at one moment.");
        }
    }
}
