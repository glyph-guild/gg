using Gg.Client;
using Gg.Console;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// The airspace tab's right-hand pane shows the selected document on disk, as
/// applied, and as it composes.
/// </summary>
/// <remarks>
/// <para>
/// <b>THREE QUESTIONS ABOUT ONE ROW, AND THEY ARE DIFFERENT ANSWERS.</b> What
/// the file says is what you edit; what the airspace holds is what landed;
/// what composes is what actually governs a flight. Somebody spent an evening
/// unable to tell the three apart — the file was there, the document was
/// applied, and the pane that claimed to hold "the rules in force" showed the
/// floor.
/// </para>
/// <para>
/// <b>ON DISK ONLY WHEN IT DIFFERS.</b> A file that matches what is applied
/// makes the first two tabs the same document, and a tab that duplicates its
/// neighbour teaches people to stop reading tabs. It appears when there is
/// something to compare.
/// </para>
/// <para>
/// <b>Ordered the way the work flows:</b> what you wrote, what landed, what
/// governs.
/// </para>
/// </remarks>
public class TheAirspacePaneShowsThreeViewsTests
{
    private const string WorkKind =
        """
        context:
          scope: "**"
          constitution: "1.0.0"
        environments: dev
        accepts:
          - tracker
        produces:
          - loop.outcome
        obligations:
          hal-in-scope:
            check: machine
            rule: no-file-outside-scope
        loops:
          score:
            executor: frontier
            discharges:
              - hal-in-scope
            moves:
              - read
            budget:
              wall-clock: "20m"
            on-exhaustion: handoff-to-human
        """;

    private const string Floor =
        """
        context:
          scope: "**"
          constitution: "1.0.0"
        environments: dev
        instructions:
          - "Keep your summary under 120 words."
        obligations:
          in-scope:
            check: machine
            rule: no-file-outside-scope
        loops:
          implement:
            executor: frontier
            discharges:
              - in-scope
            moves:
              - edit
            budget:
              wall-clock: "20m"
            on-exhaustion: handoff-to-human
        """;

    private static AppState Tree(AirspaceView view, string? onDisk = null)
    {
        var state = new AppState
        {
            ActiveTab = TabId.Envelope,
            AirspaceView = view,
            Estate = new EstateOnThisMachine
            {
                Root = "/home/someone/airspace",
                Uncommitted = [],
                Names = new EnvelopeTopology
                {
                    Names =
                    [
                        new TopologyName
                        {
                            Name = "root", Role = Roles.Root,
                            DeclaredBy = "the floor", DeclaredAt = DateTimeOffset.UnixEpoch,
                        },
                        new TopologyName
                        {
                            Name = "score-hal", Role = Roles.WorkKind, Parent = "root",
                            DeclaredBy = "somebody", DeclaredAt = DateTimeOffset.UnixEpoch,
                        },
                    ],
                },
                Applied =
                [
                    new NamedEnvelopeState
                    {
                        Name = "root", Role = Roles.Root, Version = "v7",
                        UpdatedAt = DateTimeOffset.UnixEpoch, UpdatedBy = "somebody",
                        Envelope = Gg.Contracts.Authoring.EnvelopeYaml.Parse(Floor).Envelope,
                    },
                    new NamedEnvelopeState
                    {
                        Name = "score-hal", Role = Roles.WorkKind, Version = "score-hal@v1",
                        UpdatedAt = DateTimeOffset.UnixEpoch, UpdatedBy = "somebody",
                        Envelope = Gg.Contracts.Authoring.EnvelopeYaml.Parse(WorkKind).Envelope,
                    },
                ],
                Tree = new WorkingCopy
                {
                    Present = true,
                    Documents =
                    [
                        new("work-kind", "score-hal",
                            "airspace/work-kinds/score-hal.yaml", null)
                        {
                            Text = onDisk ?? WorkKind,
                        },
                    ],
                    Unreadable = [],
                },
            },
        };

        // ROW ZERO IS THE SYNTHESISED `airspace/' FOLDER, one is work-kinds/,
        // two is the document.
        var rows = AirspaceRows.Tree(state);

        return state with
        {
            AirspaceSelected = rows.ToList()
                .FindIndex(r => r.Document.Contains("score-hal", StringComparison.Ordinal)),
        };
    }

    private static string Said(AppState state) =>
        string.Join('\n', PaneText.AirspaceDocument(state, 0));

    [Test]
    public async Task Applied_shows_what_the_airspace_holds_with_its_version()
    {
        var said = Said(Tree(AirspaceView.Applied));

        await Assert.That(said).Contains("score-hal@v1", StringComparison.Ordinal)
            .Because("the version names this exact document, and it is what an attribution "
                   + "will say governed a flight.");

        await Assert.That(said).Contains("hal-in-scope", StringComparison.Ordinal);
    }

    [Test]
    public async Task Effective_shows_the_floor_composed_with_it()
    {
        var said = Said(Tree(AirspaceView.Effective));

        await Assert.That(said).Contains("hal-in-scope", StringComparison.Ordinal)
            .Because("the work kind's own obligation still governs.");

        await Assert.That(said).Contains("in-scope", StringComparison.Ordinal)
            .Because("and the floor's, which is what makes this a composition rather than "
                   + $"the document again. Said:\n{said}");
    }

    [Test]
    public async Task On_disk_shows_the_file_verbatim()
    {
        var said = Said(Tree(AirspaceView.OnDisk, onDisk: "# mine\ncontext:\n  scope: \"**\"\n"));

        await Assert.That(said).Contains("# mine", StringComparison.Ordinal)
            .Because("verbatim, comments and all - the point of this view is what is "
                   + "ACTUALLY in the file, not what gg makes of it.");
    }

    [Test]
    public async Task The_on_disk_view_is_offered_only_when_it_differs()
    {
        var same = Tree(AirspaceView.Applied);
        var edited = Tree(AirspaceView.Applied, onDisk: "# edited\ncontext:\n  scope: \"**\"\n");

        await Assert.That(AirspaceViews.Offered(same)).DoesNotContain(AirspaceView.OnDisk)
            .Because("a file matching what is applied makes two tabs one document, and a "
                   + "tab that duplicates its neighbour teaches people to stop reading "
                   + "tabs.");

        await Assert.That(AirspaceViews.Offered(edited)).Contains(AirspaceView.OnDisk)
            .Because("and when there IS something to compare, comparing is the whole "
                   + "reason somebody opened this tab.");
    }

    [Test]
    public async Task The_views_are_ordered_the_way_the_work_flows()
    {
        var edited = Tree(AirspaceView.Applied, onDisk: "# edited\ncontext:\n  scope: \"**\"\n");

        await Assert.That(AirspaceViews.Offered(edited))
            .IsEquivalentTo((AirspaceView[])
                [AirspaceView.OnDisk, AirspaceView.Applied, AirspaceView.Effective])
            .Because("what you wrote, what landed, what governs - which is the order the "
                   + "work happens in and so the order a person reads them.");
    }

    [Test]
    public async Task A_row_with_nothing_applied_says_so_rather_than_drawing_a_blank()
    {
        var state = Tree(AirspaceView.Applied);

        var unapplied = state with
        {
            Estate = state.Estate! with { Applied = [] },
        };

        await Assert.That(Said(unapplied))
            .Contains("not", StringComparison.OrdinalIgnoreCase)
            .Because("a document authored and never applied is the ordinary state of one "
                   + "being written, and an empty pane for it reads as a failure.");
    }
}
