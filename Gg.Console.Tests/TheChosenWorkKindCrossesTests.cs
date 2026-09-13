using Gg.Console;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// A flight names the work kind the person chose, whichever door opened it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The repository's argument, one flag over.</b> Three doors open a flight —
/// a picked work item, a pasted intent, and one flown by hand — and a kind that
/// crossed on one of them would be a setting that works depending on how you
/// started. That sentence is <c>TheChosenRepositoryCrossesTests</c>' and it is
/// borrowed on purpose: this is the same defect shape, on the flag that had it
/// first.
/// </para>
/// <para>
/// <b>And it has had it twice already.</b> <c>FlightLaunchRequest.WorkKind</c>
/// shipped on the wire with no caller at all, so a tenant that declared a kind
/// could not open a flight for it; then <c>--work-kind</c> reached the CLI and
/// the <c>--hand</c> arm dropped it. A flag is not delivered because somebody
/// added a parameter.
/// </para>
/// <para>
/// <b>Absent stays absent.</b> Row zero is "inherit the floor", which every
/// flight before kinds existed did, and it must travel as null rather than as
/// the word <c>implement</c> — the control plane supplies that reading, and a
/// console that supplied the name would be declaring something nobody chose.
/// </para>
/// </remarks>
public class TheChosenWorkKindCrossesTests
{
    private static EnvelopeTopology Declaring(params string[] kinds) => new()
    {
        Names =
        [
            new TopologyName
            {
                Name = "root",
                Role = Roles.Root,
                DeclaredBy = "kdee",
                DeclaredAt = DateTimeOffset.UnixEpoch,
            },
            .. kinds.Select(kind => new TopologyName
            {
                Name = kind,
                Role = Roles.WorkKind,
                Parent = "root",
                DeclaredBy = "kdee",
                DeclaredAt = DateTimeOffset.UnixEpoch,
            }),
        ],
    };

    /// <summary>A console that has been asked, with the cursor on a kind.</summary>
    private static AppState Answering(ComposingFor door, int row) =>
        Reducer.Browsed(
            new AppState
            {
                BrowseVisible = true,
                Estate = new EstateOnThisMachine
                {
                    Uncommitted = [],
                    Names = Declaring("hal-score", "research"),
                },
                Mode = UiMode.WorkKindChoice,
                AskingKindFor = door,
                KindSelected = row,
            },
            "a-tracker",
            new BrowseOutcome.Listed(new Gg.Local.WorkItemPage(
                [new Gg.Local.WorkItemSummary("18398", "A draft job fails", "New", "", null)],
                null)));

    [Test]
    public async Task A_picked_item_carries_the_kind_that_was_chosen()
    {
        var actions = new ConsoleDoubles.Records();

        _ = ConsoleLoop.FlewPicked(Answering(ComposingFor.WorkItem, row: 1), actions);

        await Assert.That(actions.Kinds).Count().IsEqualTo(1);
        await Assert.That(actions.Kinds[0]).IsEqualTo("hal-score");
    }

    [Test]
    public async Task Row_zero_crosses_as_nothing_at_all()
    {
        // THE ORDINARY STATE, and it must stay reachable. Sending the word
        // `implement' would be the console declaring a kind nobody chose, and
        // sending an empty string would be a name the control plane refuses.
        var actions = new ConsoleDoubles.Records();

        _ = ConsoleLoop.FlewPicked(Answering(ComposingFor.WorkItem, row: 0), actions);

        await Assert.That(actions.Kinds[0]).IsNull();
    }

    [Test]
    public async Task A_pasted_intent_carries_it_too()
    {
        // THE SECOND DOOR. `n' opens a flight from something a person wrote,
        // and a kind chosen before writing it has to survive the writing.
        var actions = new ConsoleDoubles.Records();

        _ = ConsoleLoop.Opened(
            Answering(ComposingFor.NewFlight, row: 2), actions,
            new ConsoleDoubles.Writes("fix the thing"));

        await Assert.That(actions.Intents).Count().IsEqualTo(1);
        await Assert.That(actions.Intents[0].WorkKind).IsEqualTo("research");
    }

    [Test]
    public async Task And_a_flight_flown_by_hand_says_it_on_the_command_line()
    {
        // THE THIRD DOOR, AND THE ONE THAT DROPPED IT. `y' spawns a child
        // `gg fly <intent> --hand`, so the kind crosses as an argument rather
        // than as a parameter - which is exactly where it was lost before, and
        // an assertion about the argv is the only thing that would have caught
        // it.
        // THROUGH Fly, NOT THROUGH THE BUILDER. Asserting on StartInfoFor
        // directly would pass on a builder nothing hands a kind to - which is
        // the same "port nothing calls" shape this whole file is about, and it
        // would have passed while the real door still dropped it.
        var started = new List<System.Diagnostics.ProcessStartInfo>();

        _ = ConsoleHandFlight.Fly(
            Answering(ComposingFor.HandFlight, row: 1),
            plan: () => new Checklist
            {
                EnvelopeVersion = "v1",
                RequiredLabels = [],
                Items = [],
            },
            advertised: [],
            ask: () => "fix the thing",
            self: new Gg.Local.SelfInvocation("gg", []),
            start: info => { started.Add(info); return 0; });

        await Assert.That(started).Count().IsEqualTo(1);
        await Assert.That(started[0].ArgumentList).Contains("--work-kind");
        await Assert.That(started[0].ArgumentList).Contains("hal-score");
    }
}
