using Gg.Client;
using Gg.Console;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// A document whose name nobody has declared is marked on its row, before
/// anybody presses apply.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE STATE THAT MADE APPLY LOOK BROKEN.</b> A real tree held
/// <c>airspace/work-kinds/score-hal.yaml</c> and the topology held no
/// <c>score-hal</c>. An envelope applied to an undeclared name is refused -
/// <c>ProtocolSurface</c>: <i>"an envelope applied to an undeclared name is
/// refused pointing HERE, so the door ships in the same contract as the
/// refusal"</i> - and because apply runs tightenings first, that document was
/// the FIRST one tried. The whole apply stopped on it and nothing else was
/// attempted.
/// </para>
/// <para>
/// <b>Nothing on the tab said so.</b> The row showed a document like any
/// other, so the only way to discover it was to press apply and read a
/// refusal that did not fit the screen. The fact was already in hand: the
/// estate read fetches the topology on the same key that fills the tree.
/// </para>
/// <para>
/// <b>IT IS THE STRONGER TIER, and it is marked as such.</b> Whether a name
/// exists is the control plane's answer and needs a session - so it is said
/// only when the topology has actually been read, never inferred from its
/// absence. That is the discipline this pane already carries for direction
/// versus <c>uncommitted</c>, and the one a <c>based-on</c> line was read
/// against it once already.
/// </para>
/// </remarks>
public class AnUndeclaredNameIsMarkedOnTheRowTests
{
    private static TopologyName Declared(string name, string role) => new()
    {
        Name = name,
        Role = role,
        Parent = role == "root" ? null : "root",
        DeclaredBy = "Kevin Deenanauth",
        DeclaredAt = DateTimeOffset.UnixEpoch,
    };

    /// <summary>Exactly the tenant's topology when this was found.</summary>
    private static EnvelopeTopology Topology() => new()
    {
        Names =
        [
            Declared("root", "root"),
            Declared("implement", "work-kind"),
            Declared("dev", "strategy"),
        ],
    };

    private static AppState With(EnvelopeTopology? names) => new()
    {
        ActiveTab = TabId.Envelope,
        Estate = new EstateOnThisMachine
        {
            Root = "/home/someone/airspace",
            Uncommitted = [],
            Names = names,
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

    private static string StateOf(AppState state, string leaf) =>
        AirspaceRows.Tree(state)
            .Where(r => r.Document.TrimStart().StartsWith(leaf, StringComparison.Ordinal))
            .Select(r => r.State)
            .FirstOrDefault() ?? $"no row for {leaf}";

    [Test]
    public async Task The_document_whose_name_is_not_declared_says_so()
    {
        var said = StateOf(With(Topology()), "score-hal.yaml");

        await Assert.That(said).Contains("declar", StringComparison.OrdinalIgnoreCase)
            .Because("this is the one row that refuses the whole apply, and the only way "
                   + "to find it was to press apply and read a refusal off the edge of the "
                   + "screen. Said: " + said);
    }

    [Test]
    public async Task And_it_says_what_to_do_about_it()
    {
        var said = StateOf(With(Topology()), "score-hal.yaml");

        await Assert.That(said).Contains("score-hal", StringComparison.Ordinal)
            .Because("the name is what the command needs, and a person reading a state "
                   + "column should not have to work out which name a path implies. Said: "
                   + said);

        await Assert.That(said).Contains("work-kind", StringComparison.Ordinal)
            .Because("and the role, because declaring one takes both - the document's "
                   + "directory already decided it, so the row can say it.");
    }

    [Test]
    public async Task A_declared_name_says_nothing_of_the_kind()
    {
        var said = StateOf(With(Topology()), "root.yaml");

        await Assert.That(said).DoesNotContain("declar", StringComparison.OrdinalIgnoreCase)
            .Because("only the rows somebody has to act on, or the mark stops meaning "
                   + "anything. Said: " + said);
    }

    [Test]
    public async Task With_no_topology_read_it_claims_nothing()
    {
        // THE TIER RULE, AND THE LESSON FROM THE based-on CLAIM. Whether a name
        // exists is the door's answer. With no session the absence of a
        // topology is not evidence that a name is undeclared - it is evidence
        // that nobody asked - and the pane's own absence line is where that
        // gets said.
        var said = StateOf(With(null), "score-hal.yaml");

        await Assert.That(said).DoesNotContain("declar", StringComparison.OrdinalIgnoreCase)
            .Because("marking every document undeclared on a machine with no session would "
                   + "be showing the weaker answer as though it were the stronger one, "
                   + "which is exactly what reading `never applied` off a based-on line "
                   + "did. Said: " + said);
    }

    [Test]
    public async Task It_is_said_before_the_direction_because_it_refuses_the_apply()
    {
        // ORDERED BY WHAT A PERSON DOES NEXT. A direction tells you what will
        // happen when this applies; an undeclared name tells you it will not
        // apply at all, so it reads first.
        var state = With(Topology());

        var withDiff = state with
        {
            Estate = state.Estate! with
            {
                Working = new EstateDiff
                {
                    Changes =
                    [
                        new DocumentChange
                        {
                            Name = "score-hal",
                            Path = "airspace/work-kinds/score-hal.yaml",
                            Direction = Changeset.Tightening,
                        },
                    ],
                    Retiring = [],
                    Unreadable = [],
                },
            },
        };

        var said = StateOf(withDiff, "score-hal.yaml");

        var declare = said.IndexOf("declar", StringComparison.OrdinalIgnoreCase);
        var direction = said.IndexOf(Changeset.Tightening, StringComparison.Ordinal);

        await Assert.That(declare).IsGreaterThanOrEqualTo(0);
        await Assert.That(direction).IsGreaterThanOrEqualTo(0)
            .Because("both are true at once and both are said: the diff computed a "
                   + "direction for a document that cannot be applied yet.");

        await Assert.That(declare).IsLessThan(direction)
            .Because("the blocker comes before the forecast, because it is the thing to go "
                   + "and do. Said: " + said);
    }
}
