using Gg.Console;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// The pane says the airspace could not be read, rather than that nothing is
/// applied to the document.
/// </summary>
/// <remarks>
/// <para>
/// <b>SEEN, ON A REAL TREE, WITH THE CONTROL PLANE DOWN.</b> The estate read
/// failed with <i>Connection refused (localhost:5199)</i>, the local walk
/// succeeded, and the pane said "Nothing has been applied to 'root' yet. The
/// file is here and the airspace holds no document for it. Apply it with s."
/// Every clause of that is false, and the last one is an instruction to act on
/// it.
/// </para>
/// <para>
/// <b>This is the week's recurring defect in a new hat.</b> An empty
/// collection standing in for "nobody could ask" has cost a refusal rendered
/// as success, an apply question that claimed the working copy matched, and
/// now this. The discriminator was already in hand: <c>Estate.Names</c> is the
/// topology, and the estate read is documented as answering NOTHING rather
/// than a half-read estate - so a null topology means the remote half never
/// answered, and <c>Diagnosis</c> says why.
/// </para>
/// <para>
/// <b>Three absences and they are three sentences</b>, which this tab has said
/// before at its other end: never asked, asked and nothing there, asked and
/// failed. A person acts differently on each.
/// </para>
/// </remarks>
public class AnUnreachableAirspaceIsNotAnEmptyOneTests
{
    private const string WorkKind = """
        context:
          scope: "src/**"
        destinations:
          - kind: tracker
            accepts: [tracker]
        obligations:
          - id: hal-in-scope
            says: stay inside the scoring code
        """;

    /// <summary>A tree on disk, with the remote half of the estate given.</summary>
    /// <param name="failed">
    /// What the read threw, if it ran and threw. <b>Composed the way
    /// ConsoleEstate composes it</b> - "The airspace could not be read: " and
    /// then the message - because Diagnosis is a whole sentence and a fixture
    /// that hands over a fragment tests a shape the console never produces.
    /// </param>
    private static AppState Tree(EnvelopeTopology? names, string? failed)
    {
        var diagnosis = failed is { Length: > 0 }
            ? "The airspace could not be read: " + failed
            : "";

        var state = new AppState
        {
            ActiveTab = TabId.Envelope,
            Estate = new EstateOnThisMachine
            {
                Root = "/home/someone/airspace",
                Diagnosis = diagnosis,
                Names = names,
                Uncommitted = [],
                Applied = [],
                Tree = new WorkingCopy
                {
                    Present = true,
                    Documents =
                    [
                        new("work-kind", "score-hal",
                            "airspace/work-kinds/score-hal.yaml", null)
                        {
                            Text = WorkKind,
                        },
                    ],
                    Unreadable = [],
                },
            },
        };

        var rows = AirspaceRows.Tree(state);

        return state with
        {
            AirspaceSelected = rows.ToList().FindIndex(
                r => r.Document.Contains("score-hal", StringComparison.Ordinal)),
        };
    }

    /// <summary>The topology answered, and it holds the name.</summary>
    private static EnvelopeTopology Answered() => new()
    {
        Names =
        [
            new TopologyName
            {
                Name = "score-hal", Role = Roles.WorkKind, Parent = "root",
                DeclaredBy = "somebody", DeclaredAt = DateTimeOffset.UnixEpoch,
            },
        ],
    };

    private static string Said(AppState state, AirspaceView view) =>
        string.Join('\n', PaneText.AirspaceDocument(state with { AirspaceView = view }, 0));

    [Test]
    public async Task A_read_that_failed_says_so_rather_than_that_nothing_is_applied()
    {
        var said = Said(
            Tree(names: null, failed: "Connection refused (localhost:5199)"),
            AirspaceView.Applied);

        await Assert.That(said).Contains("could not be read", StringComparison.Ordinal)
            .Because("nobody could ask, and a pane that reports the answer to a question "
                   + "nobody asked is the defect this week keeps producing.");

        await Assert.That(said).Contains("Connection refused", StringComparison.Ordinal)
            .Because("and the reason is the only part of this a person can act on - it is "
                   + "already in hand, so passing it through costs nothing.");

        await Assert.That(said)
            .DoesNotContain("Nothing has been applied", StringComparison.Ordinal)
            .Because("that is a claim about the airspace, made by a console that could not "
                   + "reach it.");

        await Assert.That(said).DoesNotContain("Apply it with s", StringComparison.Ordinal)
            .Because("worse than the false claim: an instruction to act on it.");
    }

    [Test]
    public async Task The_reason_is_passed_through_rather_than_introduced()
    {
        // SEEN ON SCREEN: "The airspace could not be read: The airspace could
        // not be read: Connection refused (localhost:5199)". Diagnosis is the
        // estate's own SENTENCE, not a fragment, so a pane that introduces it
        // says the same thing twice - and a reader who has to skip a stutter
        // to reach the reason is being told the console is confused.
        var said = Said(
            Tree(names: null, failed: "Connection refused (localhost:5199)"),
            AirspaceView.Applied);

        var times = said.Split("could not be read").Length - 1;

        await Assert.That(times).IsEqualTo(1)
            .Because($"once, not twice. Said:\n{said}");
    }

    [Test]
    public async Task The_effective_view_does_not_compose_out_of_what_it_never_read()
    {
        var said = Said(
            Tree(names: null, failed: "Connection refused (localhost:5199)"),
            AirspaceView.Effective);

        await Assert.That(said).Contains("could not be read", StringComparison.Ordinal)
            .Because("composing the floor with nothing yields the floor, and calling that "
                   + "what governs a flight is how an applied work kind came to look like "
                   + "it had never landed.");

        await Assert.That(said).DoesNotContain("beyond the floor", StringComparison.Ordinal);
    }

    [Test]
    public async Task An_airspace_that_answered_and_holds_nothing_still_says_so()
    {
        // THE SECOND OF THE THREE, and it must survive the fix. A document
        // authored and not yet applied is the ordinary state of one being
        // written, and it is the case the apply key exists for.
        var said = Said(Tree(Answered(), failed: null), AirspaceView.Applied);

        await Assert.That(said)
            .Contains("Nothing has been applied", StringComparison.Ordinal)
            .Because("the topology answered and holds no document for this name, which is "
                   + "a fact about the airspace rather than about the connection.");

        await Assert.That(said).Contains("Apply it with s", StringComparison.Ordinal);
    }

    [Test]
    public async Task An_estate_nobody_has_read_yet_is_the_third_sentence()
    {
        // NOT THE SAME AS A FAILURE. Nothing has been attempted, so there is
        // no reason to give - and the thing to do is the read, not a fix.
        var state = Tree(names: null, failed: null);

        var said = Said(state, AirspaceView.Applied);

        await Assert.That(said).Contains("not been read", StringComparison.Ordinal)
            .Because("never-asked and asked-and-failed are different, and the console "
                   + "already keeps them apart where it asks to apply.");

        await Assert.That(said)
            .DoesNotContain("Nothing has been applied", StringComparison.Ordinal);
    }
}
