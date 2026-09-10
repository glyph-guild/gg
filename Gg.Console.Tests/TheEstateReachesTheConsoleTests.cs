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

    [Test]
    public async Task The_summary_cannot_hold_a_document()
    {
        // THE BUNDLE ARGUMENT, asserted over the type rather than trusted to a
        // reviewer. AppState is serialized to GG_STATE_DUMP and handed to the
        // diagnostics bundle, so a member able to carry envelope text would put
        // a tenant's governance documents in a file they send us. The topology
        // and the diff both carry names, roles, versions and paths - never
        // bodies - which is why they are what this holds.
        var carriers = typeof(EstateOnThisMachine).GetProperties(
                BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(Envelope)
                     || p.PropertyType == typeof(EnvelopeNarrowing)
                     || p.PropertyType == typeof(EnvironmentStrategy))
            .Select(p => p.Name)
            .ToList();

        await Assert.That(carriers).IsEmpty()
            .Because("a document on this record is a document in the diagnostics bundle. "
                   + "Found: " + string.Join(", ", carriers));
    }
}
