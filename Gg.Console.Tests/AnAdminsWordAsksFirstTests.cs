using Gg.Console;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// An admin's word about the tenant's machines asks before it lands
/// (slice forty-six, step 3).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this one asks and the other two do not.</b> Claiming and reserving
/// act on one person's own claim and are undone by pressing the same key
/// again. Holding a machine back for the tenant takes it from whoever has
/// claimed it, and opening one hands every person here a machine an admin had
/// held back - neither is a thing to do with one keypress in a modal people
/// arrive at by arrow key.
/// </para>
/// <para>
/// <b>Claimed is never one of the two words.</b> An admin says whether a
/// machine may be claimed; who claims it is that person's own act.
/// </para>
/// </remarks>
public class AnAdminsWordAsksFirstTests
{
    private const string Runner = "01a0bca3-b788-72e5-b40a-be3811653226";

    private sealed class ScriptedUi(params Func<AppState, UiOutcome>[] script) : IUiSession
    {
        private readonly Queue<Func<AppState, UiOutcome>> _script = new(script);

        public UiOutcome Run(AppState state) => _script.Dequeue()(state);
    }

    private static AppState Fleet(string ownership, string owner = "") => new()
    {
        ActiveTab = TabId.Runners,
        Mode = UiMode.Runner,
        RunnerSelected = 0,
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
                    Owner = owner,
                    Reserved = owner.Length > 0,
                },
            ],
        },
    };

    [Test]
    public async Task The_key_opens_a_question_rather_than_saying_it()
    {
        var asked = Reducer.Reduce(Fleet(RunnerOwnerships.Open), Command.AskWhoMayClaim);

        await Assert.That(asked.Mode).IsEqualTo(UiMode.ConfirmOwnership)
            .Because("the other two ownership keys act on one keypress because they act on "
                   + "one person's claim; this one speaks for the tenant.");
    }

    [Test]
    public async Task Answering_no_cannot_act_at_all()
    {
        // NOT "DOES NOT ACT" - CANNOT. Closing is the reducer's, inside the UI
        // session, and the shell that owns every door refuses to handle it:
        // there is no arm a no could reach a control plane through, which is a
        // stronger answer than a test that watched one not being called.
        await Assert.That(ShellCommands.Handled).DoesNotContain(Command.CloseModal);

        var left = Reducer.Reduce(
            Fleet(RunnerOwnerships.Open) with { Mode = UiMode.ConfirmOwnership },
            Command.CloseModal);

        await Assert.That(left.Mode).IsNotEqualTo(UiMode.ConfirmOwnership)
            .Because("a question a person walked away from is not still being asked.");
    }

    [Test]
    public async Task Answering_yes_says_the_other_word()
    {
        var actions = new ConsoleDoubles.Records();
        var ui = new ScriptedUi(
            s => new UiOutcome(Command.SetOwnership, s),
            s => new UiOutcome(Command.Quit, s));

        var final = new ConsoleLoop(ui, new ConsoleDoubles.NoEditor(), actions: actions)
            .Run(Fleet(RunnerOwnerships.Tenant) with { Mode = UiMode.ConfirmOwnership });

        await Assert.That(actions.Ownerships).Count().IsEqualTo(1);
        await Assert.That(actions.Ownerships[0]).IsEqualTo((Runner, RunnerOwnerships.Open));
        await Assert.That(final.Mode).IsEqualTo(UiMode.Runner)
            .Because("a modal that stayed open after its question was answered would invite "
                   + "a second yes.");
    }

    [Test]
    public async Task A_claimed_machine_is_held_back_rather_than_claimed_by_the_tenant()
    {
        var actions = new ConsoleDoubles.Records();
        var ui = new ScriptedUi(
            s => new UiOutcome(Command.SetOwnership, s),
            s => new UiOutcome(Command.Quit, s));

        _ = new ConsoleLoop(ui, new ConsoleDoubles.NoEditor(), actions: actions)
            .Run(Fleet(RunnerOwnerships.Claimed, "Kevin") with { Mode = UiMode.ConfirmOwnership });

        await Assert.That(actions.Ownerships[0].Ownership).IsEqualTo(RunnerOwnerships.Tenant);
        await Assert.That(actions.Ownerships[0].Ownership)
            .IsNotEqualTo(RunnerOwnerships.Claimed)
            .Because("an admin says whether a machine may be claimed, never who claimed it.");
    }

    [Test]
    public async Task The_question_names_the_person_it_takes_the_machine_from()
    {
        var body = PaneText.Modal(
            Fleet(RunnerOwnerships.Claimed, "Kevin") with { Mode = UiMode.ConfirmOwnership });

        await Assert.That(body).Contains("Kevin's", StringComparison.Ordinal);
        await Assert.That(body).Contains("takes it from them", StringComparison.Ordinal)
            .Because("a question about a word, rather than about what happens, is one a "
                   + "person answers without knowing what they answered.");
    }

    [Test]
    public async Task The_key_reads_as_the_word_it_would_say()
    {
        var tenants = Keymap.Bindings(KeymapContext.For(Fleet(RunnerOwnerships.Tenant)));
        var open = Keymap.Bindings(KeymapContext.For(Fleet(RunnerOwnerships.Open)));

        await Assert.That(tenants.First(k => k.Command == Command.AskWhoMayClaim).Description)
            .Contains("open it", StringComparison.Ordinal);
        await Assert.That(open.First(k => k.Command == Command.AskWhoMayClaim).Description)
            .Contains("hold it back", StringComparison.Ordinal);
    }
}
