using System.Text.RegularExpressions;
using Gg.Console;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// The pane that puts the case in front of somebody.
/// </summary>
/// <remarks>
/// <para>
/// <b>References are named, not fetched.</b> That is a boundary rather than a rendering
/// choice: fetching a reference pulls content across, and the whole disposition model
/// exists so that the approver fetches it themselves, from their own systems,
/// authenticated as themselves. A pane that helpfully retrieved the file would undo the
/// step that decided not to send it.
/// </para>
/// <para>
/// <b>This pane does not fix step 5's finding and must not be read as having.</b> Step 5
/// established that <c>reversibility-plan</c>'s payload cannot settle reversibility, and
/// that is a finding about the obligation. What is rendered here is a payload already
/// known to be insufficient for that obligation; rendering it well changes nothing about
/// that, and the gap must not be re-filed as a rendering problem.
/// </para>
/// </remarks>
public class EvidencePaneTests
{
    [Test]
    public async Task An_inline_item_is_shown()
    {
        var text = PaneText.Evidence(WithPayload());

        await Assert.That(text).Contains("2 file(s)")
            .Because("it fitted, so the content crosses and the person reads it here.");
    }

    [Test]
    public async Task A_digest_is_shown_as_a_summary_and_says_it_is_one()
    {
        // A summary that does not announce itself is indistinguishable from the whole
        // thing, and somebody deciding needs to know they are reading a reduction.
        var text = PaneText.Evidence(WithPayload());

        await Assert.That(text).Contains("400 lines");
        await Assert.That(text).Contains("summary")
            .Because("labelled, because a reduction read as the whole thing is worse than "
                   + "no reduction at all.");
    }

    [Test]
    public async Task A_reference_is_named_and_not_fetched()
    {
        // Named: enough to go and look. Not fetched: the content does not cross.
        var text = PaneText.Evidence(WithPayload());

        await Assert.That(text).Contains("migrations/0003_add_index.sql");
        await Assert.That(text).Contains("4096")
            .Because("the size, so somebody knows what they are opening.");
        await Assert.That(text).Contains("cccccccc"[..7])
            .Because("the commit, so what they fetch is the thing that was decided about.");

        await Assert.That(text).DoesNotContain(Marker)
            .Because("the content itself stays where it is. This pane names it; the person "
                   + "fetches it from their own systems as themselves.");
    }

    [Test]
    public async Task Nothing_in_the_console_fetches_a_reference()
    {
        // THE POISON TWIN'S OTHER HALF. The rendering above shows this pane does not
        // fetch; this shows no path in the console does, so a helpful addition elsewhere
        // cannot quietly undo it.
        var fetching = new Regex(
            @"\.Reference\s*\.\s*(Path|Commit)\s*\)?\s*\)?\s*;?\s*//\s*fetch"
          + @"|ReadAllText\s*\(\s*\w*[Rr]eference"
          + @"|Fetch\w*\(\s*\w*[Rr]eference"
          + @"|GetAsync\s*\(\s*\w*[Rr]eference",
            RegexOptions.Compiled);

        var offenders = ConsoleSources()
            .Where(f => fetching.IsMatch(File.ReadAllText(f)))
            .Select(f => Path.GetFileName(f)!)
            .ToList();

        await Assert.That(offenders).IsEmpty()
            .Because("fetching a reference pulls content across the boundary the "
                   + "disposition model exists to hold. Found: " + string.Join(", ", offenders));

        await Assert.That(fetching.IsMatch("var body = File.ReadAllText(reference.Path);"))
            .IsTrue()
            .Because("the scan can see one, so the emptiness above means something.");
    }

    [Test]
    public async Task The_delta_note_is_shown_even_when_the_delta_is_empty()
    {
        // Absence and silence must not look alike. An empty delta with no note reads as a
        // pane that failed to render its last section.
        var text = PaneText.Evidence(WithPayload() with
        {
            Payload = WithPayload().Payload! with
            {
                Delta = [],
                DeltaNote = "The loop ran again and changed nothing.",
            },
        });

        await Assert.That(text).Contains("changed nothing");
    }

    [Test]
    public async Task A_flight_with_no_payload_says_so_rather_than_rendering_blank()
    {
        var text = PaneText.Evidence(new AppState());

        await Assert.That(text).IsNotEmpty()
            .Because("a blank pane reads as a failure to load rather than as nothing to "
                   + "show.");
    }

    /// <summary>Content that would be in a diff, planted so an absence scan has a target.</summary>
    private const string Marker = "MARKER-MIGRATION-BODY-MUST-NOT-CROSS";

    private static AppState WithPayload() => new()
    {
        Payload = new GateEvidencePayload
        {
            Items =
            [
                new GateEvidenceItem
                {
                    Item = EvidenceItems.ChangeManifest,
                    Disposition = EvidenceDispositions.Inline,
                    Voice = EvidenceVoices.Measured,
                    Inline = "2 file(s), +2/-0 lines\nmigrations/0001.sql\nsrc/greet.py",
                },
                new GateEvidenceItem
                {
                    Item = EvidenceItems.MigrationList,
                    Disposition = EvidenceDispositions.Digest,
                    Voice = EvidenceVoices.Measured,
                    Digest = "400 lines, 9000 bytes. First: a; b; c.",
                },
                new GateEvidenceItem
                {
                    Item = EvidenceItems.AgentAccount,
                    Disposition = EvidenceDispositions.Reference,
                    Voice = EvidenceVoices.Stated,
                    Reference = new EvidenceReference
                    {
                        Commit = new string('c', 40),
                        Path = "migrations/0003_add_index.sql",
                        ContentHash = new string('d', 64),
                        ByteSize = 4096,
                        MediaType = "text/plain",
                    },
                },
            ],
            Delta = ["migrations/0002_down.sql"],
            DeltaNote = "1 path(s) have changed since this was decided.",
        },
    };

    private static IEnumerable<string> ConsoleSources()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Gg.sln")))
        {
            dir = dir.Parent;
        }

        var root = dir ?? throw new InvalidOperationException("Gg.sln not found");

        return Directory.EnumerateFiles(
                Path.Combine(root.FullName, "Gg.Console"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                            StringComparison.Ordinal)
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                            StringComparison.Ordinal));
    }
}
