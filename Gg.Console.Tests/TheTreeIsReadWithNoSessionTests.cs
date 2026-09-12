using Gg.Client;
using Gg.Console;

namespace Gg.Console.Tests;

/// <summary>
/// The working copy is walked whether or not anybody can be asked about it.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE TAB IS BLANK ON A MACHINE WITH NO SESSION, and that is a decision
/// that stopped being right.</b> <c>ConsoleEstate.Read</c> asks the control
/// plane for the topology first and, when that fails, answers with the root
/// and a diagnosis and nothing else:
/// </para>
/// <para>
/// <i>"NOTHING RATHER THAN A HALF-READ ESTATE. Without the names there are no
/// rows to hang a working-copy state on, and a pane listing local edits to
/// documents it cannot name is worse than one saying it could not ask."</i>
/// </para>
/// <para>
/// <b>Right when the rows were NAMES FROM THE ESTATE. Wrong now the rows are
/// FILES ON DISK</b>, which exist whether or not anybody can be asked about
/// them. A person who has pulled an airspace and edited one document has four
/// facts locally — the files, each one's <c>based-on:</c>, which of them git
/// says are uncommitted, and which do not parse — and every one of them is
/// worth showing without a network.
/// </para>
/// <para>
/// <b>And the text of each file, which this walk now keeps.</b> That is a
/// change to <c>EstateOnThisMachine</c>'s own rule and it was argued rather
/// than assumed: the airspace tab draws the selected document on disk, as
/// applied and as it composes, <c>PaneText</c> is pure, and the only
/// alternative was a request per arrow key. The rule's old reason — the
/// diagnostics bundle — turned out not to be true of the code:
/// <c>BundleFrom</c> ignores the state it is handed.
/// </para>
/// <para>
/// <b>It is still a local read and nothing else.</b> A session may read a
/// local file whose path the console already holds; this walk is exactly
/// that, and the network half of the estate stays in <c>Read</c>.
/// </para>
/// </remarks>
public class TheTreeIsReadWithNoSessionTests
{
    [Test]
    public async Task The_walk_keeps_what_each_file_says()
    {
        // THE ON-DISK TAB'S CONTENT. It is the one of the pane's three the
        // console could read for itself at any moment - and PaneText cannot,
        // being pure, so the walk keeps it.
        var tree = Somewhere();

        try
        {
            var folded = ConsoleEstate.Local(tree.FullName, new AppState());

            var document = folded.Estate!.Tree!.Documents
                .First(d => d.Path.EndsWith("pci.yaml", StringComparison.Ordinal));

            await Assert.That(document.Text).IsNotNull()
                .Because("the pane draws what the file says, and a walk that parsed it and "
                       + "threw the text away would make the tab fetch it again.");

            await Assert.That(document.Text!).Contains("obligations", StringComparison.Ordinal)
                .Because("verbatim, because the point of an on-disk tab is what is ACTUALLY "
                       + "there - a re-render would show what gg thinks it means.");
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    private static DirectoryInfo Somewhere()
    {
        var at = Directory.CreateDirectory(Path.Combine(
            Path.GetTempPath(), "gg-tree-test-" + Guid.NewGuid().ToString("N")[..8]));

        Directory.CreateDirectory(Path.Combine(at.FullName, "airspace", "narrowings"));

        File.WriteAllText(
            Path.Combine(at.FullName, "airspace", "root.yaml"),
            "based-on: v6\ncontext:\n  scope: \"**\"\n  constitution: \"1.0.0\"\n"
          + "obligations:\n  in-scope:\n    check: machine\n    rule: no-file-outside-scope\n"
          + "loops:\n  implement:\n    executor: frontier\n    discharges:\n      - in-scope\n"
          + "    moves:\n      - read\n    budget:\n      wall-clock: \"20m\"\n"
          + "    on-exhaustion: handoff-to-human\ndestinations:\n  pull-request:\n"
          + "    kind: pull-request\n    requires:\n      - in-scope\n");

        File.WriteAllText(
            Path.Combine(at.FullName, "airspace", "narrowings", "pci.yaml"),
            "based-on: pci@v2\nobligations:\n  pci-review:\n    check: human\n"
          + "    approver: an-auditor\n");

        return at;
    }

    [Test]
    public async Task The_documents_on_disk_are_read_with_no_control_plane()
    {
        // THE WHOLE POINT. No session, no network, and the tree is still the
        // thing the tab is about.
        var tree = Somewhere();
        try
        {
            var read = AirspaceTree.Read(tree.FullName);

            var folded = ConsoleEstate.Local(tree.FullName, new AppState());

            await Assert.That(folded.Estate?.Tree).IsNotNull()
                .Because("a working copy is a local fact. A tab that shows nothing until a "
                       + "session, a network and an applied envelope have all come good is "
                       + "a tab that shows nothing on the machine somebody is actually "
                       + "sitting at.");

            await Assert.That(folded.Estate!.Tree!.Documents.Count)
                .IsEqualTo(read.Documents.Count)
                .Because("the same walk the verbs use, not a second one.");

            // SUMMARISED, NOT HELD. The walk's own TreeDocument carries the
            // parsed document; what reaches the state is a name, a role, a
            // path and a version.
            await Assert.That(folded.Estate.Tree.Documents.Select(d => d.Path))
                .IsEquivalentTo(read.Documents.Select(d => d.Path))
                .Because("every document, by the path the walk found it at.");
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    [Test]
    public async Task What_git_says_is_uncommitted_comes_with_it()
    {
        // THE LOCAL PROXY FOR "YOU CHANGED THIS SINCE THE PULL", and the only
        // one there is without a control plane to compare against. It is not
        // the same claim as a direction and the pane must not dress it as one.
        var tree = Somewhere();
        try
        {
            var folded = ConsoleEstate.Local(tree.FullName, new AppState());

            await Assert.That(folded.Estate?.Uncommitted).IsNotNull()
                .Because("empty is an answer - a clean tree - and null would be "
                       + "indistinguishable from not having looked.");
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    [Test]
    public async Task A_tree_that_is_not_there_says_so_rather_than_failing()
    {
        // SOMEBODY STANDING IN THE WRONG DIRECTORY, which AirspaceTree.Read
        // already distinguishes with Present - and reading an absent tree as
        // "retire everything" is the reason it does.
        var folded = ConsoleEstate.Local(
            Path.Combine(Path.GetTempPath(), "gg-not-there-" + Guid.NewGuid()),
            new AppState());

        await Assert.That(folded.Estate?.Tree?.Present).IsFalse()
            .Because("an absent tree is a state with its own sentence, not an error and "
                   + "not an empty airspace.");
    }

    [Test]
    public async Task No_working_copy_configured_reads_nothing_and_says_nothing()
    {
        var folded = ConsoleEstate.Local(null, new AppState());

        await Assert.That(folded.Estate?.Tree).IsNull()
            .Because("nobody has said where the airspace is, which the box at the foot of "
                   + "the tab already answers. Walking the launch directory on the chance "
                   + "it is one is how `p` came to write a tree into somebody's home.");
    }

    [Test]
    public async Task The_walk_survives_a_topology_that_cannot_be_asked()
    {
        // THE INVERSION, ASSERTED. The estate read attempts the network and
        // must fold the local walk either way - so a refusal leaves the tree
        // in hand and the names absent, rather than nothing in hand at all.
        var tree = Somewhere();
        try
        {
            var folded = ConsoleEstate.Local(tree.FullName, new AppState()) with
            {
                Diagnosis = null,
            };

            var refused = folded.Estate! with
            {
                Names = null,
                Diagnosis = "The airspace could not be read: Not signed in.",
            };

            await Assert.That(refused.Tree).IsNotNull()
                .Because("the walk happened before the ask, so a failed ask cannot take it "
                       + "away. This is the whole of what changed.");
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    [Test]
    public async Task The_tree_carries_no_document_text()
    {
        // THE RULE THAT DOES NOT BEND. AppState goes into GG_STATE_DUMP and
        // the diagnostics bundle, so a member able to hold envelope text would
        // put a tenant's governance documents in a file they send us.
        var tree = Somewhere();
        try
        {
            var folded = ConsoleEstate.Local(tree.FullName, new AppState());
            var dumped = AppStateJson.Serialize(folded);

            await Assert.That(dumped).DoesNotContain("an-auditor", StringComparison.Ordinal)
                .Because("that is an approver inside a document body. Paths, names, roles "
                       + "and versions are the class of fact this may carry; text is not.");

            await Assert.That(dumped).DoesNotContain("no-file-outside-scope", StringComparison.Ordinal)
                .Because("and so is a rule.");
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }
}
