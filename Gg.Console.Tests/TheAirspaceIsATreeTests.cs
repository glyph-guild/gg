using Gg.Client;
using Gg.Console;

namespace Gg.Console.Tests;

/// <summary>
/// The airspace tab is the working copy, as a tree, with what is known about
/// each document on its row.
/// </summary>
/// <remarks>
/// <para>
/// <b>IT WAS A PARAGRAPH OF TEXT, AND THE ONE LIST-OF-THINGS PANE THAT NEVER
/// GOT A TABLE.</b> <c>PaneText.Estate</c> emitted a hand-counted ten-column
/// role field and a free-width name, with no header, no widths measured from
/// the data, and no cursor — while the Queue, Flights, Browse, Repositories and
/// Runners tabs all went through <c>CollectionViews.Table()</c> and
/// <c>Rows.cs</c>.
/// </para>
/// <para>
/// <b>And it was driven by the topology rather than the disk</b>, so a document
/// the estate does not know about had no row at all, and nothing had a path.
/// The rows are files now.
/// </para>
/// <para>
/// <b>Folders are synthesised, not walked.</b>
/// <c>AirspaceTree.Read</c> orders documents by full path ordinally, which
/// groups them already; the directory rows come from the distinct prefixes of
/// those paths. A <c>TreeView</c> would be new machinery with its own
/// key-eating risk — the reason <c>QuietTable</c> exists — for the same
/// picture a flat list indented by depth draws.
/// </para>
/// <para>
/// <b>Two tiers on one column, and the tab must not dress one as the other.</b>
/// <c>uncommitted</c> is git's answer and needs no session; a direction is the
/// door's answer and needs one. A row that showed the first as though it were
/// the second would be this week's recurring defect in a new hat.
/// </para>
/// </remarks>
public class TheAirspaceIsATreeTests
{
    private static AppState With(
        IReadOnlyList<AirspaceFile>? documents = null,
        IReadOnlyList<UnreadableFile>? unreadable = null,
        IReadOnlyList<string>? uncommitted = null,
        EstateDiff? working = null,
        bool present = true) => new()
        {
            Estate = new EstateOnThisMachine
            {
                Root = "/home/someone/airspace",
                Uncommitted = uncommitted ?? [],
                Working = working,
                Tree = new WorkingCopy
                {
                    Present = present,
                    Documents = documents ??
                    [
                        new("root", "root", "airspace/root.yaml", "v6"),
                        new("narrowing", "pci", "airspace/narrowings/pci.yaml", "pci@v2"),
                        new("strategy", "dev", "airspace/strategies/dev.yaml", "dev@v4"),
                    ],
                    Unreadable = unreadable ?? [],
                },
            },
        };

    [Test]
    public async Task Every_document_on_disk_has_a_row()
    {
        var rows = AirspaceRows.Tree(With());

        foreach (var name in (string[])["root.yaml", "pci.yaml", "dev.yaml"])
        {
            await Assert.That(rows.Select(r => r.Document.Trim())).Contains(name)
                .Because("the rows are the files, so a document the estate has never heard "
                       + "of still has one. Rows: "
                       + string.Join(" | ", rows.Select(r => r.Document.Trim())));
        }
    }

    [Test]
    public async Task The_folders_are_rows_and_the_files_under_them_are_indented()
    {
        var rows = AirspaceRows.Tree(With());
        var shown = rows.Select(r => r.Document).ToList();

        await Assert.That(shown).Contains(d => d.TrimEnd().EndsWith("narrowings/"))
            .Because("a directory structure means the directories are on it. Rows: "
                   + string.Join(" | ", shown));

        var folder = shown.First(d => d.TrimEnd().EndsWith("narrowings/"));
        var file = shown.First(d => d.Contains("pci.yaml", StringComparison.Ordinal));

        await Assert.That(file.Length - file.TrimStart().Length)
            .IsGreaterThan(folder.Length - folder.TrimStart().Length)
            .Because("the file sits under its folder, which is the whole of what a flat "
                   + "list indented by depth has to get right.");
    }

    [Test]
    public async Task A_folder_comes_before_what_is_in_it()
    {
        var shown = AirspaceRows.Tree(With()).Select(r => r.Document).ToList();

        var folder = shown.FindIndex(d => d.TrimEnd().EndsWith("narrowings/"));
        var file = shown.FindIndex(d => d.Contains("pci.yaml", StringComparison.Ordinal));

        await Assert.That(folder).IsLessThan(file)
            .Because("the order a cursor indexes is the order on the screen, so it has to "
                   + "be the order a person reads a tree in.");
    }

    [Test]
    public async Task A_documents_based_on_version_is_its_own_column()
    {
        var rows = AirspaceRows.Tree(With());

        var pci = rows.First(r => r.Document.Contains("pci.yaml", StringComparison.Ordinal));

        await Assert.That(pci.Basis).IsEqualTo("pci@v2")
            .Because("which version of the stream this was rendered from is the fact apply "
                   + "states back as a precondition.");
    }

    [Test]
    public async Task A_document_with_no_based_on_line_has_never_been_applied()
    {
        var rows = AirspaceRows.Tree(With(documents:
            [new("narrowing", "new-one", "airspace/narrowings/new-one.yaml", null)]));

        var row = rows.First(r => r.Document.Contains("new-one", StringComparison.Ordinal));

        await Assert.That(row.State).Contains("never applied", StringComparison.OrdinalIgnoreCase)
            .Because("null based-on is genesis, not unchanged - and the two are opposite "
                   + "facts about whether the stream has ever seen it.");
    }

    [Test]
    public async Task What_git_calls_uncommitted_is_marked_without_a_session()
    {
        var rows = AirspaceRows.Tree(With(
            uncommitted: ["airspace/narrowings/pci.yaml"]));

        var pci = rows.First(r => r.Document.Contains("pci.yaml", StringComparison.Ordinal));
        var root = rows.First(r => r.Document.Contains("root.yaml", StringComparison.Ordinal));

        await Assert.That(pci.State).Contains("uncommitted", StringComparison.OrdinalIgnoreCase)
            .Because("this is the local proxy for 'you changed it since the pull', and the "
                   + "only answer there is with no control plane to compare against.");

        await Assert.That(root.State)
            .DoesNotContain("uncommitted", StringComparison.OrdinalIgnoreCase)
            .Because("and only the file git named.");
    }

    [Test]
    public async Task A_direction_is_shown_only_once_the_door_has_answered()
    {
        var diffed = AirspaceRows.Tree(With(working: new EstateDiff
        {
            Changes =
            [
                new DocumentChange
                {
                    Name = "pci",
                    Path = "airspace/narrowings/pci.yaml",
                    Direction = Changeset.Widening,
                    Field = "obligations",
                },
            ],
            Retiring = [],
            Unreadable = [],
        }));

        var pci = diffed.First(r => r.Document.Contains("pci.yaml", StringComparison.Ordinal));

        await Assert.That(pci.State).Contains("widening", StringComparison.OrdinalIgnoreCase)
            .Because("direction comes from the comparator the door itself runs, and the "
                   + "console decides none of it.");

        // AND NOT INVENTED WHEN NOBODY HAS ANSWERED. A tab that showed a
        // direction it had not been told is the second opinion ADR-0016 § 6
        // refused a permission model for.
        var local = AirspaceRows.Tree(With(uncommitted: ["airspace/narrowings/pci.yaml"]));
        var alone = local.First(r => r.Document.Contains("pci.yaml", StringComparison.Ordinal));

        await Assert.That(alone.State)
            .DoesNotContain("widening", StringComparison.OrdinalIgnoreCase)
            .Because("git says the file moved and nothing local says which way. Said: "
                   + alone.State);

        await Assert.That(alone.State)
            .DoesNotContain("tightening", StringComparison.OrdinalIgnoreCase)
            .Because("nor the other way, which is the tempting half of the same mistake.");
    }

    [Test]
    public async Task A_file_that_does_not_parse_says_it_stops_every_apply()
    {
        var rows = AirspaceRows.Tree(With(unreadable:
            [new("airspace/narrowings/broken.yaml", "obligations: is not a mapping")]));

        var broken = rows.First(r => r.Document.Contains("broken", StringComparison.Ordinal));

        await Assert.That(broken.State).Contains("unreadable", StringComparison.OrdinalIgnoreCase)
            .Because("it sits where a document goes and does not read as one.");

        await Assert.That(string.Join(" ", rows.Select(r => r.State)))
            .Contains("stops every apply", StringComparison.OrdinalIgnoreCase)
            .Because("one of these refuses the whole changeset rather than being skipped, "
                   + "which is the difference between a row somebody fixes now and one "
                   + "they fix later.");
    }

    [Test]
    public async Task Nothing_pulled_and_nowhere_to_pull_are_different_and_neither_is_a_row()
    {
        await Assert.That(AirspaceRows.Tree(With(documents: [], present: false))).IsEmpty()
            .Because("an absent tree is somebody standing in the wrong directory, and the "
                   + "pane says so in a sentence rather than with a header over nothing - "
                   + "which is Rows.cs's own rule.");

        await Assert.That(AirspaceRows.Tree(new AppState())).IsEmpty()
            .Because("and nothing read at all is a third thing again.");
    }

    [Test]
    public async Task The_columns_are_what_the_rows_carry()
    {
        await Assert.That(AirspaceRows.AirspaceColumns.Count).IsEqualTo(3)
            .Because("the document, what it is based on, and what is known about it. "
                   + "TheTablesAreTablesTests holds the headings against this.");
    }
}
