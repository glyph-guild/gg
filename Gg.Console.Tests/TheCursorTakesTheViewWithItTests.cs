using Gg.Console;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// Moving the airspace cursor clamps which view the pane is showing to one the
/// new row actually has.
/// </summary>
/// <remarks>
/// <para>
/// <b>WHICH VIEWS A ROW HAS IS THE ROW'S OWN ANSWER.</b> A file matching what
/// is applied has no on-disk view - there would be nothing to compare it
/// against. A strategy has no effective one - composition is per work kind,
/// because that is what a flight has. So an arrow key from a work kind with
/// local edits to a strategy takes two of the three views away.
/// </para>
/// <para>
/// <b>The pane would then be pointing at a tab that is not on the bar</b> -
/// the same disagreement between a model and a widget that crashed the window's
/// tab bar, one pane smaller and reachable with an arrow key rather than once
/// in the life of a console.
/// </para>
/// <para>
/// <b>Clamped where the cursor moves, which is one place.</b> Both the arrow
/// key and the mouse arrive at <c>Reducer.Pointed</c>, so the keyboard and the
/// click cannot answer this differently.
/// </para>
/// </remarks>
public class TheCursorTakesTheViewWithItTests
{
    private const string Floor = """
        context:
          scope: "**"
        destinations:
          - kind: tracker
            accepts: [tracker]
        obligations:
          - id: in-scope
            says: the floor holds
        """;

    private static AppState Estate() => new()
    {
        ActiveTab = TabId.Envelope,
        Estate = new EstateOnThisMachine
        {
            Root = "/home/someone/airspace",

            // THE WORK KIND IS EDITED AND THE STRATEGY IS NOT, which is what
            // makes the two rows offer different views: three and one.
            Uncommitted = ["airspace/work-kinds/score-hal.yaml"],
            Names = new EnvelopeTopology
            {
                Names =
                [
                    new TopologyName
                    {
                        Name = "score-hal", Role = Roles.WorkKind, Parent = "root",
                        DeclaredBy = "somebody", DeclaredAt = DateTimeOffset.UnixEpoch,
                    },
                    new TopologyName
                    {
                        Name = "dev", Role = Roles.Strategy,
                        DeclaredBy = "somebody", DeclaredAt = DateTimeOffset.UnixEpoch,
                    },
                ],
            },
            Applied =
            [
                new NamedEnvelopeState
                {
                    Name = "score-hal", Role = Roles.WorkKind, Version = "score-hal@v1",
                    UpdatedAt = DateTimeOffset.UnixEpoch, UpdatedBy = "somebody",
                    Envelope = Gg.Contracts.Authoring.EnvelopeYaml.Parse(Floor).Envelope,
                },
                new NamedEnvelopeState
                {
                    Name = "dev", Role = Roles.Strategy, Version = "dev@v1",
                    UpdatedAt = DateTimeOffset.UnixEpoch, UpdatedBy = "somebody",
                    Envelope = Gg.Contracts.Authoring.EnvelopeYaml.Parse(Floor).Envelope,
                },
            ],
            Tree = new WorkingCopy
            {
                Present = true,
                Documents =
                [
                    new("strategy", "dev", "airspace/strategies/dev.yaml", null)
                    {
                        Text = Floor,
                    },
                    new("work-kind", "score-hal",
                        "airspace/work-kinds/score-hal.yaml", null)
                    {
                        Text = Floor,
                    },
                ],
                Unreadable = [],
            },
        },
    };

    private static int Row(AppState state, string named) =>
        AirspaceRows.Tree(state).ToList()
            .FindIndex(r => r.Document.Contains(named, StringComparison.Ordinal));

    [Test]
    public async Task A_row_without_the_showing_view_moves_the_pane_to_one_it_has()
    {
        var state = Estate();

        var onTheWorkKind = Reducer.Pointed(state, Row(state, "score-hal"))
            with
        { AirspaceView = AirspaceView.Effective };

        await Assert.That(AirspaceViews.Offered(onTheWorkKind))
            .Contains(AirspaceView.Effective)
            .Because("an edited work kind has all three, which is the premise.");

        var onTheStrategy = Reducer.Pointed(onTheWorkKind, Row(state, "dev"));

        await Assert.That(AirspaceViews.Offered(onTheStrategy))
            .DoesNotContain(AirspaceView.Effective)
            .Because("what governs a flight is composed per work kind, and a strategy is "
                   + "not one.");

        await Assert.That(onTheStrategy.AirspaceView).IsEqualTo(AirspaceView.Applied)
            .Because("the pane may not be left pointing at a tab this row does not have - "
                   + "that disagreement between a model and a bar is what crashed the "
                   + "window's tabs.");
    }

    [Test]
    public async Task A_view_the_new_row_still_has_is_left_alone()
    {
        var state = Estate();

        var onTheWorkKind = Reducer.Pointed(state, Row(state, "score-hal"))
            with
        { AirspaceView = AirspaceView.Applied };

        var onTheStrategy = Reducer.Pointed(onTheWorkKind, Row(state, "dev"));

        await Assert.That(onTheStrategy.AirspaceView).IsEqualTo(AirspaceView.Applied)
            .Because("clamping is a repair, not a reset - somebody reading what is applied "
                   + "down a list of documents is asking one question, and snapping back "
                   + "to the first tab on every arrow key would make them re-ask it.");
    }

    [Test]
    public async Task A_folder_row_leaves_the_view_where_it_was()
    {
        // NO DOCUMENT, SO NO ANSWER. Offered is empty on a folder row, and an
        // empty list is not evidence that the view somebody chose is wrong -
        // the next row down will have it.
        var state = Estate();

        var onTheWorkKind = Reducer.Pointed(state, Row(state, "score-hal"))
            with
        { AirspaceView = AirspaceView.OnDisk };

        var onTheFolder = Reducer.Pointed(onTheWorkKind, 0);

        await Assert.That(AirspaceViews.Offered(onTheFolder)).IsEmpty();
        await Assert.That(onTheFolder.AirspaceView).IsEqualTo(AirspaceView.OnDisk);
    }
}
