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
    /// <summary>
    /// A work kind that parses.
    /// </summary>
    /// <remarks>
    /// <b><c>destinations</c> IS REQUIRED</b> — "an envelope without it governs
    /// nothing", in the validator's own words. Trimming a fixture past it
    /// yields a null <c>Envelope</c>, which every consumer then reports as the
    /// wrong document SHAPE — a true sentence about a symptom, and four
    /// fixtures in one session were written against it before anybody asked
    /// <c>gg envelope validate</c>.
    /// </remarks>
    private const string WorkKind =
        """
        context:
          scope: "**"
          constitution: "1.0.0"
        environments: dev
        accepts:
          - tracker
          # A SUBJECT WITH A TREE, because context.scope is '**' and a path
          # bound over subjects that have none selects nothing - which the
          # validator refuses by name rather than accepting quietly.
          - repository
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
        destinations:
          agentic-backlog:
            kind: work-item-tracker
            may-perform:
              - field
            may-write:
              - "Custom.HAL"
            requires:
              - hal-in-scope
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
        destinations:
          pull-request:
            kind: pull-request
            requires:
              - in-scope
        """;

    private static AppState Tree(
        AirspaceView view, string? onDisk = null, bool uncommitted = false)
    {
        var state = new AppState
        {
            ActiveTab = TabId.Envelope,
            AirspaceView = view,
            Estate = new EstateOnThisMachine
            {
                Root = "/home/someone/airspace",

                // WHOSE ANSWER "DIFFERS" IS. The pane does not compare the raw
                // file against the canonical rendering - that would call every
                // pulled document different, because the renderer normalises
                // what an author wrote. It reads git's answer and the diff's,
                // which is the same rule the rows follow about direction.
                Uncommitted = uncommitted
                    ? ["airspace/work-kinds/score-hal.yaml"]
                    : [],
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
    public async Task Applied_shows_the_document_and_not_a_caption_over_it()
    {
        var said = Said(Tree(AirspaceView.Applied));

        await Assert.That(said).Contains("hal-in-scope", StringComparison.Ordinal);

        // NO HEADER. The tab already says which of the three questions this
        // answers and the row already says which document it is about, so two
        // lines of caption over every pane say what is on screen twice and
        // push the document itself down.
        await Assert.That(said).DoesNotContain("updated", StringComparison.Ordinal)
            .Because("who last applied it is metadata about the document, not the "
                   + "document.");

        await Assert.That(said.Split('\n')[0]).IsNotEmpty()
            .Because("and the pane opens on the document's own first line, with nothing "
                   + "above it and no blank line under a caption that is gone.");
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

        await Assert.That(said).DoesNotContain("what governs a flight of kind",
                StringComparison.Ordinal)
            .Because("the tab below says `effective' and the row says which kind, so the "
                   + "sentence spelling both out is the screen reading itself back.");
    }

    [Test]
    public async Task On_disk_shows_the_file_verbatim()
    {
        var said = Said(Tree(AirspaceView.OnDisk, onDisk: "# mine\ncontext:\n  scope: \"**\"\n"));

        await Assert.That(said).Contains("# mine", StringComparison.Ordinal)
            .Because("verbatim, comments and all - the point of this view is what is "
                   + "ACTUALLY in the file, not what gg makes of it.");

        await Assert.That(said.Split('\n')[0]).IsEqualTo("# mine")
            .Because("VERBATIM FROM THE FIRST LINE. A path and a dash over it was the "
                   + "path already on the row, and it made line one of the file line "
                   + "three of the pane.");
    }

    [Test]
    public async Task The_on_disk_view_is_always_offered()
    {
        // THIS RULE WAS THE OPPOSITE AND IT WAS WRONG IN USE. On disk used to
        // appear only when the file differed from what is applied, on the
        // argument that a tab duplicating its neighbour teaches people to stop
        // reading tabs. What it actually did was take the FILE away from a
        // tree of file names: select root.yaml, committed and applied, and
        // there was no way to see root.yaml.
        //
        // The duplication argument was also weaker than it looked - the two
        // are never quite the same document. One is what somebody wrote,
        // comments and ordering and all; the other is what gg made of it.
        var same = Tree(AirspaceView.Applied);
        var edited = Tree(AirspaceView.Applied, uncommitted: true);

        await Assert.That(AirspaceViews.Offered(same)).Contains(AirspaceView.OnDisk)
            .Because("the row names a file, so the file is always one of the answers.");

        await Assert.That(AirspaceViews.Offered(edited)).Contains(AirspaceView.OnDisk)
            .Because("and when it differs, comparing is the whole reason somebody looked.");
    }

    [Test]
    public async Task The_views_are_ordered_the_way_the_work_flows()
    {
        var edited = Tree(AirspaceView.Applied, uncommitted: true);

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
