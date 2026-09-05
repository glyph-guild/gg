using Gg.Client;
using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Console.Tests;

/// <summary>
/// What must hold before the selected flight can start.
/// </summary>
/// <remarks>
/// <para>
/// <b>The thing a person stares at while a flight waits</b>, and the console had
/// no way to show it. <c>ConsoleData.PlanAsync</c> has existed with no caller
/// since it was written; <c>gg plan</c> answers it on the command line and the
/// pane a person is already looking at did not.
/// </para>
/// <para>
/// <b>Each item carries its satisfier and its disposition</b>, which is what
/// makes the answer actionable: an unmet requirement whose satisfier is a label
/// nobody advertises is a different job from one waiting on an approver, and a
/// list of requirement names alone cannot tell them apart.
/// </para>
/// </remarks>
public class TheChecklistIsAPaneTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 5, 9, 0, 0, TimeSpan.Zero);

    private static Checklist Plan() => new()
    {
        EnvelopeVersion = "3",
        FlightNumber = FlightRef.Format(1),
        RequiredLabels = ["docker"],
        Items =
        [
            new ChecklistItem
            {
                Requirement = "a runner advertising docker",
                Verification = "machine",
                Satisfier = "the fleet",
                Disposition = "unmet",
            },
            new ChecklistItem
            {
                Requirement = "in-scope",
                Verification = "machine",
                Satisfier = "the loop",
                Disposition = "met",
            },
        ],
    };

    [Test]
    public async Task The_projection_puts_the_checklist_where_the_pane_reads_it()
    {
        var state = ConsoleProjection.Apply(new AppState(), new VerbResult.Plan(Plan()));

        await Assert.That(state.Checklist).IsNotNull();
        await Assert.That(state.Checklist!.Items.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Every_item_carries_its_satisfier_and_its_disposition()
    {
        var pane = PaneText.Checklist(new AppState { Checklist = Plan() });

        await Assert.That(pane).Contains("a runner advertising docker");
        await Assert.That(pane).Contains("unmet")
            .Because("the disposition is the whole answer to 'can this start'.");
        await Assert.That(pane).Contains("the fleet")
            .Because("and the satisfier is what turns it into a job somebody can do - an "
                   + "unmet requirement waiting on the fleet is a different task from one "
                   + "waiting on an approver.");
        await Assert.That(pane).Contains("docker")
            .Because("the required labels are what the fleet has to advertise, and a "
                   + "checklist that names none of them cannot be acted on.");
    }

    [Test]
    public async Task An_unread_checklist_says_so_rather_than_saying_nothing_is_required()
    {
        // Rule 5, and this is the pair that matters most: an empty checklist
        // reads as "nothing is stopping this flight", which is the opposite of
        // "nobody asked".
        // WITH A ROW SELECTED, because `No flight selected.` is a third
        // sentence and a true one - the pane says which of the three nothings
        // it is showing, and this test is about the one that is a gap.
        var pane = PaneText.Checklist(new AppState { Queue = [Row("a", 1)] });

        await Assert.That(pane).Contains("not read")
            .Because("an empty list and an unread one are opposite facts, and one of them "
                   + "says the flight is ready to go.");
    }

    [Test]
    public async Task The_key_is_the_shell_s_work_because_showing_it_is_a_read()
    {
        // THE SAME REASON BROWSE IS. A UI session may not do I/O, and a
        // checklist is a request - so the key ends the session, the loop asks,
        // and the next session renders it. A reducer arm as well would give one
        // key two effects, the local one happening whether or not the remote one
        // did.
        var before = new AppState { Queue = [Row("a", 1)] };

        await Assert.That(ShellCommands.Handled).Contains(Command.ToggleChecklist);
        await Assert.That(Reducer.Reduce(before, Command.ToggleChecklist))
            .IsEqualTo(before)
            .Because("a shell command with a reducer arm is a key that half works.");
    }

    [Test]
    public async Task Opening_it_reads_and_closing_it_does_not()
    {
        // Only on the way IN. Hiding costs nothing, and on this path a read
        // costs a whole session rebuild.
        var reads = 0;
        var ui = new Presses(Command.ToggleChecklist, Command.ToggleChecklist);

        _ = new ConsoleLoop(
            ui,
            new NoEditor(),
            checklist: state =>
            {
                reads++;
                return state with { Checklist = Plan() };
            }).Run(new AppState { Queue = [Row("a", 1)] });

        await Assert.That(reads).IsEqualTo(1)
            .Because("two presses, one open.");
    }

    [Test]
    public async Task A_checklist_read_for_another_row_is_dropped_when_the_cursor_moves()
    {
        var moved = Reducer.Detail(new AppState
        {
            Queue =
            [
                Row("a", 1), Row("b", 2),
            ],
            SelectedRow = 1,
            Checklist = Plan(),
        });

        await Assert.That(moved.Checklist).IsNull()
            .Because("it names the flight it was read for, and that is not this one.");
    }

    private sealed class Presses(params Command[] keys) : IUiSession
    {
        private int _at;

        public UiOutcome Run(AppState state) =>
            _at < keys.Length ? new UiOutcome(keys[_at++], state) : new UiOutcome(Command.Quit, state);
    }

    private sealed class NoEditor : IEditorSession
    {
        public string Edit(string initialText) => "";
    }

    private static QueueRow Row(string id, int number) => new()
    {
        FlightId = id,
        FlightNumber = FlightRef.Format(number),
        Name = "waiting",
        Reason = QueueReason.AwaitingDecision,
        Since = T0,
    };
}
