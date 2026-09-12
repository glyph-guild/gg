using Gg.Client;
using Gg.Console;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// `v` over a document row reads that document back as the airspace holds it.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE THING THAT COULD BE APPLIED AND SHOWN TO NOBODY.</b> The envelope
/// modal renders the ROOT document, so a work kind that applied successfully
/// appeared in nothing — and the only evidence it had landed was a diff
/// reporting no changes. Somebody concluded twice that their apply had failed.
/// </para>
/// <para>
/// <b>`v` MEANS TWO THINGS AND THE CURSOR DECIDES.</b> Over a document it
/// reads that document; anywhere else — a folder row, a tree nobody has read —
/// it opens the rules in force. Both answers come from
/// <c>AirspaceRows.Pointed</c>, asked once: the keymap uses it to decide what
/// the key offers and the read uses it to decide what to fetch, so the two
/// cannot disagree about which document is meant.
/// </para>
/// <para>
/// <b>A folder row is not a document</b>, which is what stops `v` promising a
/// read of <c>airspace/</c>.
/// </para>
/// </remarks>
public class AnAppliedDocumentIsReadBackInTheConsoleTests
{
    private static AppState Tree(int cursor) => new()
    {
        ActiveTab = TabId.Envelope,
        AirspaceSelected = cursor,
        Estate = new EstateOnThisMachine
        {
            Root = "/home/someone/airspace",
            Uncommitted = [],
            Tree = new WorkingCopy
            {
                Present = true,
                Documents =
                [
                    new("root", "root", "airspace/root.yaml", "v6"),
                    new("work-kind", "score-hal",
                        "airspace/work-kinds/score-hal.yaml", null),
                ],
                Unreadable = [],
            },
        },
    };

    [Test]
    public async Task Over_a_document_it_offers_to_read_that_document()
    {
        // ROW ZERO IS THE `airspace/' FOLDER, one is root.yaml, two is the
        // work-kinds folder, three is score-hal.yaml.
        var rows = AirspaceRows.Tree(Tree(0));
        var at = rows.ToList().FindIndex(
            r => r.Document.Contains("score-hal", StringComparison.Ordinal));

        var state = Tree(at);

        await Assert.That(AirspaceRows.Pointed(state)?.Name).IsEqualTo("score-hal")
            .Because("which document the cursor is on, resolved once and used by both the "
                   + "keymap and the read.");

        await Assert.That(Keymap.Resolve(KeyStroke.Char('v'), KeymapContext.For(state)))
            .IsEqualTo(Command.ReadDocument)
            .Because("the applied version of the thing under the cursor is what somebody "
                   + "on that row is asking about.");
    }

    [Test]
    public async Task Over_a_folder_row_it_still_offers_the_rules_in_force()
    {
        var rows = AirspaceRows.Tree(Tree(0));
        var folder = rows.ToList().FindIndex(r => r.Document.TrimEnd().EndsWith('/'));

        var state = Tree(folder);

        await Assert.That(AirspaceRows.Pointed(state)).IsNull()
            .Because("a folder holds no document, and `v' over one must not promise to read "
                   + "airspace/ back.");

        await Assert.That(Keymap.Resolve(KeyStroke.Char('v'), KeymapContext.For(state)))
            .IsEqualTo(Command.ReadEnvelope)
            .Because("so the key keeps its other meaning rather than going dead.");
    }

    [Test]
    public async Task With_no_tree_at_all_it_is_the_rules_in_force()
    {
        var empty = new AppState { ActiveTab = TabId.Envelope };

        await Assert.That(Keymap.Resolve(KeyStroke.Char('v'), KeymapContext.For(empty)))
            .IsEqualTo(Command.ReadEnvelope)
            .Because("nothing has been read, so there is no row to point at - which is what "
                   + "a fresh console looks like and why this mode is exempt from the "
                   + "one-keypress reachability walk.");
    }

    [Test]
    public async Task The_read_runs_beside_the_console_rather_than_ending_it()
    {
        await Assert.That(ShellCommands.Reads).Contains(Command.ReadDocument)
            .Because("it asks the control plane and keeps one document. A whole screen "
                   + "taken away and given back to fetch a document is what the Reads "
                   + "category exists to avoid.");

        await Assert.That(ShellCommands.Handled).DoesNotContain(Command.ReadDocument)
            .Because("and the two sets are disjoint - a command in both would end the "
                   + "session AND be patched back into it.");
    }

    [Test]
    public async Task The_modal_renders_the_document_with_its_version()
    {
        var state = Tree(1) with
        {
            Mode = UiMode.ReadingDocument,
            Document = new NamedEnvelopeState
            {
                Name = "score-hal",
                Role = Roles.WorkKind,
                Version = "score-hal@v1",
                UpdatedAt = DateTimeOffset.UnixEpoch,
                UpdatedBy = "Kevin Deenanauth",
                // A WHOLE WORK KIND, VERBATIM from a tree the parser accepted.
                // Two trimmed versions did not parse - it is strict on purpose
                // - and an envelope that came back null rendered "this name
                // holds no body", which is the test measuring its own fixture
                // rather than the pane.
                Envelope = Gg.Contracts.Authoring.EnvelopeYaml.Parse(
                    """
                    context:
                      scope: "**"
                      constitution: "1.0.0"
                    environments: dev
                    repositories:
                      - "JDX/JDNext"
                    accepts:
                      - tracker
                      - repository
                    produces:
                      - loop.outcome
                      - loop.digest
                      - loop.question
                      - destination.landed
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
                          - search
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
                    """).Envelope,
            },
        };

        var said = string.Join('\n', PaneText.DocumentLines(state, 0));

        await Assert.That(said).Contains("score-hal@v1", StringComparison.Ordinal)
            .Because("the version names this exact document permanently, and it is what an "
                   + "attribution will say governed a flight.");

        await Assert.That(said).Contains("hal-in-scope", StringComparison.Ordinal)
            .Because("and the document itself, rendered by the contract's own renderer - "
                   + $"the one a pull writes files with. Said:\n{said}");

        await Assert.That(PaneText.ModalIsADocument(UiMode.ReadingDocument)).IsTrue()
            .Because("it is read down, so it gets the screen rather than a question's box.");
    }

    [Test]
    public async Task A_read_that_has_not_answered_says_so_rather_than_looking_empty()
    {
        var waiting = Tree(1) with { Mode = UiMode.ReadingDocument, ReadInFlight = true };

        await Assert.That(string.Join('\n', PaneText.DocumentLines(waiting, 0)))
            .Contains("Reading it back", StringComparison.OrdinalIgnoreCase)
            .Because("a modal that opened on a blank pane is indistinguishable from a "
                   + "document with nothing in it, and one of those is a thing to fix.");

        var refused = Tree(1) with
        {
            Mode = UiMode.ReadingDocument,
            Diagnosis = "Not signed in. Run gg login first.",
        };

        await Assert.That(string.Join('\n', PaneText.DocumentLines(refused, 0)))
            .Contains("Not signed in", StringComparison.Ordinal)
            .Because("and a refusal is the answer, carried in the door's own words.");
    }
}
