using Gg.Client;
using Gg.Console;

namespace Gg.Console.Tests;

/// <summary>
/// A document with no <c>based-on</c> line has not "never been applied". The
/// row may not say so.
/// </summary>
/// <remarks>
/// <para>
/// <b>REPORTED FROM A REAL SESSION: "whenever i try to apply it says nothing
/// is applied, yet the airspace shows unapplied yaml files."</b> Both halves
/// were true and only one of them was honest. The apply was right - the
/// working copy matched - and the tree row was lying.
/// </para>
/// <para>
/// <b><c>based-on:</c> IS WRITTEN ONLY BY PULL.</b>
/// <c>AirspaceTree.Rendered</c> says it in as many words - <i>"THE
/// PRECONDITION IS PULL'S TO WRITE… pull knows which version it rendered, so
/// pull states it, and apply refuses when the stream has moved past it"</i> -
/// and <c>AirspaceApplyAsync</c> writes nothing to disk at all. So a document
/// somebody authored by hand, or an agent drafted, has no <c>based-on</c>
/// line; applying it does not add one; and only a later pull ever will.
/// </para>
/// <para>
/// <b>Which made the claim permanent.</b> The row inferred "never applied"
/// from the missing line, so a document that had been applied successfully -
/// any number of times - kept being reported as never applied until somebody
/// happened to pull. It is a PRECONDITION, not provenance (ADR-0016), and
/// reading provenance off it is reading a field for something it does not say.
/// </para>
/// <para>
/// <b>The diff already answers the question honestly.</b> A document that
/// differs is in <c>Changes</c> with a direction; one that matches is in
/// neither, and that absence IS the answer. With no diff, nothing about
/// application state can be known and the absence line above the rows says
/// which absence it is. So the fix removes a claim rather than correcting it -
/// there was nothing local to replace it with, which is the whole reason it
/// was wrong.
/// </para>
/// </remarks>
public class ABlankBasedOnIsNotNeverAppliedTests
{
    private static AppState With(
        IReadOnlyList<AirspaceFile> documents,
        EstateDiff? working = null,
        IReadOnlyList<string>? uncommitted = null) => new()
        {
            ActiveTab = TabId.Envelope,
            Estate = new EstateOnThisMachine
            {
                Root = "/home/someone/airspace",
                Uncommitted = uncommitted ?? [],
                Working = working,
                Tree = new WorkingCopy
                {
                    Present = true,
                    Documents = documents,
                    Unreadable = [],
                },
            },
        };

    /// <summary>Exactly the user's tree: one pulled document, one authored.</summary>
    private static IReadOnlyList<AirspaceFile> TheirTree() =>
    [
        new("root", "root", "airspace/root.yaml", "v6"),
        new("work-kind", "score-hal", "airspace/work-kinds/score-hal.yaml", null),
    ];

    private static string StateOf(AppState state, string leaf) =>
        AirspaceRows.Tree(state)
            .Where(r => r.Document.TrimStart().StartsWith(leaf, StringComparison.Ordinal))
            .Select(r => r.State)
            .FirstOrDefault() ?? $"no row for {leaf}";

    [Test]
    public async Task An_applied_document_with_no_precondition_is_not_called_never_applied()
    {
        // THE EXACT STATE THAT WAS REPORTED. The apply landed, so the diff
        // comes back empty - and the file on disk is untouched by apply, so it
        // still has no based-on line.
        var applied = With(TheirTree(), working: new EstateDiff
        {
            Changes = [],
            Retiring = [],
            Unreadable = [],
        });

        await Assert.That(StateOf(applied, "score-hal.yaml"))
            .DoesNotContain("never applied", StringComparison.OrdinalIgnoreCase)
            .Because("the diff was asked and answered that this document matches what is "
                   + "applied. Apply does not write based-on - only pull does - so the "
                   + "missing line says nothing about whether it was ever applied, and "
                   + "saying so contradicted the apply one pane away.");
    }

    [Test]
    public async Task Nor_when_there_is_no_diff_at_all()
    {
        // AND NOT AS A FALLBACK EITHER. With no session there is nothing local
        // that knows application state; the absence line above the rows is
        // where that gets said, and a row inventing an answer is worse than a
        // blank one.
        var unasked = With(TheirTree());

        await Assert.That(StateOf(unasked, "score-hal.yaml"))
            .DoesNotContain("never applied", StringComparison.OrdinalIgnoreCase)
            .Because("nothing on this machine can tell whether a document was applied. "
                   + "based-on is a precondition, not provenance.");
    }

    [Test]
    public async Task A_document_that_really_differs_still_says_which_way_it_moves()
    {
        // THE HALF THAT MUST SURVIVE. The diff is the one place direction is
        // computed, and it is what a person acts on.
        var differing = With(TheirTree(), working: new EstateDiff
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
        });

        await Assert.That(StateOf(differing, "score-hal.yaml"))
            .Contains(Changeset.Tightening, StringComparison.Ordinal)
            .Because("a document the door says differs is the one thing the row is for.");
    }

    [Test]
    public async Task The_based_on_column_still_carries_what_the_file_declares()
    {
        // NOT REMOVED, because it is a true fact about the file and the column
        // is named for it: what precondition this document asserts. Blank
        // there means pull has never written this file, which is worth seeing
        // and is all it means.
        var rows = AirspaceRows.Tree(With(TheirTree()));

        var root = rows.First(r =>
            r.Document.TrimStart().StartsWith("root.yaml", StringComparison.Ordinal));
        var authored = rows.First(r =>
            r.Document.TrimStart().StartsWith("score-hal.yaml", StringComparison.Ordinal));

        await Assert.That(root.Basis).IsEqualTo("v6");
        await Assert.That(authored.Basis).IsEqualTo("")
            .Because("blank is the honest rendering of a file that declares no "
                   + "precondition, and the column is where that belongs.");
    }

    [Test]
    public async Task Uncommitted_is_still_said_because_git_really_knows_it()
    {
        var edited = With(
            TheirTree(),
            uncommitted: ["airspace/work-kinds/score-hal.yaml"]);

        await Assert.That(StateOf(edited, "score-hal.yaml"))
            .Contains("uncommitted", StringComparison.Ordinal)
            .Because("the weaker claim is a real one: git knows the file moved since the "
                   + "pull that wrote it, and that survives having no session.");
    }
}
