using System.Reflection;
using Gg.Client;
using Gg.Console;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// The Envelope pane shows what governs; now it also shows what that was
/// composed from.
/// </summary>
/// <remarks>
/// <para>
/// <b>A person who wants to change a rule could see the rule and nothing
/// else.</b> The pane renders the composed envelope — the answer — with no way
/// to reach the documents it came from, which name, which role, or whether the
/// working copy on this machine has been edited. So the first question anybody
/// asks after reading it, <i>where do I change that</i>, had no answer on the
/// screen.
/// </para>
/// <para>
/// <b>The console computes none of it.</b> The names come from the topology
/// read and the working-copy states come from the diff verb, both rendered as
/// they arrive. A pane that worked out its own direction would be the second
/// source of truth about what tightens that ADR-0016 § 6 refused a permission
/// model for — and this one would be the copy nobody was looking at.
/// </para>
/// <para>
/// <b>It is a summary, never the documents.</b> <c>AppState</c> is written to
/// <c>GG_STATE_DUMP</c> and handed to the diagnostics bundle, so a field
/// holding envelope text would put a tenant's governance documents in a file
/// they send us — the argument <c>OfferedOnThisMachine</c> already makes one
/// feature over, for the same reason.
/// </para>
/// </remarks>
public class TheEstateReachesTheConsoleTests
{
    private static EstateOnThisMachine Estate(
        IReadOnlyList<DocumentChange>? changes = null,
        IReadOnlyList<string>? unreadable = null,
        IReadOnlyList<string>? retiring = null,
        string? root = "/home/someone/estate") => new()
        {
            Uncommitted = [],
            Root = root,
            IsRepository = true,
            Names = new EnvelopeTopology
            {
                Names =
                [
                    Named("root", Roles.Root),
                    Named("implement", Roles.WorkKind),
                    Named("pci", Roles.Narrowing),
                ],
            },
            Working = new EstateDiff
            {
                Changes = changes ?? [],
                Retiring = retiring ?? [],
                Unreadable = unreadable ?? [],
            },
        };

    private static TopologyName Named(string name, string role) => new()
    {
        Name = name,
        Role = role,
        Parent = role == Roles.Root ? null : "root",
        DeclaredBy = "an-architect",
        DeclaredAt = DateTimeOffset.UnixEpoch,
    };

    [Test]
    public async Task Every_name_the_tenant_has_is_on_the_pane()
    {
        var text = PaneText.Estate(new AppState { Estate = Estate() });

        await Assert.That(text).Contains("root", StringComparison.Ordinal);
        await Assert.That(text).Contains("implement", StringComparison.Ordinal);
        await Assert.That(text).Contains("pci", StringComparison.Ordinal)
            .Because("the whole point is answering 'where do I change that', and a document "
                   + "absent from the list is one a person cannot know to open.");
    }

    [Test]
    public async Task A_document_the_working_copy_changed_says_which_way_it_moves()
    {
        var text = PaneText.Estate(new AppState
        {
            Estate = Estate(
            [
                new DocumentChange
                {
                    Name = "pci",
                    Path = "airspace/narrowings/pci.yaml",
                    Direction = Changeset.Widening,
                    Field = "obligations",
                },
            ]),
        });

        await Assert.That(text).Contains("widening", StringComparison.Ordinal)
            .Because("direction is what decides whether an apply lands or waits at a gate, "
                   + "so it is the one fact worth a column.");
        await Assert.That(text).Contains("obligations", StringComparison.Ordinal)
            .Because("the widened field is already computed and carried, and 'something "
                   + "widened' sends somebody reading the whole document to find out what.");
    }

    [Test]
    public async Task A_document_nothing_touched_reads_as_unchanged()
    {
        // THE POSITIVE CONTROL. A pane that marked every row would satisfy the
        // test above and tell a person their whole estate was edited.
        var text = PaneText.Estate(new AppState { Estate = Estate() });

        await Assert.That(text).DoesNotContain("widening", StringComparison.Ordinal);
        await Assert.That(text).DoesNotContain("tightening", StringComparison.Ordinal)
            .Because("an unchanged estate has no direction to report, and reporting one "
                   + "would make the column meaningless the first time it mattered.");
    }

    [Test]
    public async Task A_file_that_does_not_read_as_a_document_is_named_on_the_pane()
    {
        var text = PaneText.Estate(new AppState
        {
            Estate = Estate(unreadable: ["airspace/narrowings/pci.yaml"]),
        });

        await Assert.That(text).Contains("pci.yaml", StringComparison.Ordinal)
            .Because("one unreadable file stops the whole apply - applying the rest would "
                   + "land part of a changeset somebody meant as a whole - so a pane that "
                   + "did not name it would leave a person guessing why nothing applies.");
    }

    [Test]
    public async Task The_box_says_which_tree_it_is_talking_about()
    {
        // ON THE BOX RATHER THAN IN THE LIST, since the path moved to the field
        // a person edits. The question is the same one, and it is real: an
        // unset airspace is refused now rather than guessed at, so the answer
        // has to be somewhere a person can read it without asking.
        await Assert.That(PaneText.AirspacePath(new AppState { Estate = Estate() }))
            .IsEqualTo("/home/someone/estate");
    }

    [Test]
    public async Task No_working_copy_configured_says_so_rather_than_rendering_blank()
    {
        var text = PaneText.Estate(new AppState
        {
            Estate = Estate(root: null) with { Working = null, IsRepository = false },
        });

        await Assert.That(text).IsNotEmpty()
            .Because("a tenant with names and no working copy is a real and common state - "
                   + "nothing has been pulled yet - and an empty pane says nothing about "
                   + "which of the two it is.");
        await Assert.That(text).Contains("pull", StringComparison.OrdinalIgnoreCase)
            .Because("the next thing to do is the one thing worth saying, and it is one "
                   + "verb.");
    }

    [Test]
    public async Task Nothing_read_at_all_is_a_different_answer_from_nothing_there()
    {
        var text = PaneText.Estate(new AppState());

        await Assert.That(text).Contains("e", StringComparison.OrdinalIgnoreCase);
        await Assert.That(text).DoesNotContain("/home/someone", StringComparison.Ordinal)
            .Because("an unread pane and an empty estate must not read alike - the first is "
                   + "a key to press and the second is a state of the world.");
    }

    /// <summary>
    /// One member carries documents, and it is the one the pane draws.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>THIS ASSERTED THE OPPOSITE, AND THE ARGUMENT UNDER IT WAS NOT
    /// TRUE.</b> It read: <i>"AppState is serialized to GG_STATE_DUMP and
    /// handed to the diagnostics bundle, so a member able to carry envelope
    /// text would put a tenant's governance documents in a file they send
    /// us."</i> Measured before changing it: <c>ConsoleData.BundleFrom</c>
    /// takes the state and ignores it, and <c>GG_STATE_DUMP</c> is an opt-in
    /// variable <c>Program.cs</c> calls a "Demo/verification hook".
    /// </para>
    /// <para>
    /// <b>AND IT WOULD HAVE MISSED WHAT REPLACED IT.</b> The old check looked
    /// for a property whose type IS a document — it would have passed an
    /// <c>IReadOnlyList&lt;NamedEnvelopeState&gt;</c> without a word, which is
    /// exactly the member now added. A guard that cannot see the thing it
    /// guards against is worse than none, because it reads as cover.
    /// </para>
    /// <para>
    /// <b>So the rule narrowed rather than vanished:</b> documents may ride
    /// here through <c>Applied</c>, which is what the airspace tab draws, and
    /// through nothing else. A second member wanting one has to come past this
    /// and say why.
    /// </para>
    /// </remarks>
    [Test]
    public async Task Only_the_pane_s_own_member_carries_documents()
    {
        var documents = (Type[])
            [typeof(Envelope), typeof(EnvelopeNarrowing), typeof(EnvironmentStrategy)];

        // REACHES A DOCUMENT, rather than IS one. A list, a nullable or a
        // record that holds one all count - the old check saw only the last
        // step and would have missed every other shape.
        static bool Reaches(Type type, Type[] documents) =>
            documents.Contains(type)
            || (type.IsGenericType
                && type.GetGenericArguments().Any(a => documents.Contains(a)
                    || a.GetProperties().Any(p => documents.Contains(p.PropertyType))))
            || (type.Namespace?.StartsWith("Gg.", StringComparison.Ordinal) is true
                && type.GetProperties().Any(p => documents.Contains(p.PropertyType)));

        var carriers = typeof(EstateOnThisMachine).GetProperties(
                BindingFlags.Public | BindingFlags.Instance)
            .Where(p => Reaches(p.PropertyType, documents))
            .Select(p => p.Name)
            .ToList();

        await Assert.That(carriers).IsEquivalentTo((string?[])["Applied"])
            .Because("the pane draws the selected document three ways and PaneText is pure, "
                   + "so the bodies ride here - and ONLY there. A request per arrow key was "
                   + "the alternative, which this console refuses by name. Found: "
                   + string.Join(", ", carriers));
    }
}
