using Gg.Client;
using Gg.Contracts;
using Gg.Contracts.Authoring;

namespace Gg.Client.Tests;

/// <summary>
/// A watch written to the tree reads back as the document it was.
/// </summary>
/// <remarks>
/// <para>
/// <b>S39.1-08, and it exists because adding the role opened a trap.</b>
/// <c>AirspaceNames.Tree</c> now maps <c>watches/</c> to <c>Roles.Watch</c>, so
/// <c>NameFrom</c> answers for a path under it — and <c>AirspaceTree.Parse</c>
/// has arms for a narrowing and a strategy and falls through to <b>envelope</b>
/// for everything else. Before the directory existed such a file was skipped,
/// because an unknown directory answers null and <i>"anything pull did not
/// write is not a document"</i>. After it, the same file was read as an
/// envelope, failed, and landed in <c>Unreadable</c> saying <i>"This does not
/// read as an envelope"</i> — a confusing lie about a perfectly good document.
/// </para>
/// <para>
/// <b>Nobody had such a file, so nothing was broken</b> — which is exactly why
/// this is worth a test rather than a note. The trap was laid for whoever
/// authored the first watch, and would have read as a bug in their document.
/// </para>
/// <para>
/// <b>The round trip is the claim, not the parse.</b> A parser that reads
/// something the renderer never writes, or a renderer whose output the parser
/// refuses, is two halves that only look like a pair — and the tree is the one
/// place both run against the same bytes.
/// </para>
/// </remarks>
public class AWatchReadsBackAsItWasWrittenTests
{
    private static string Scratch()
    {
        var path = Path.Combine(Path.GetTempPath(), $"gg-watch-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static WatchDocument AWatch() => new()
    {
        Shape = WatchShapes.WorkItems,
        Trigger = new WatchTrigger { Every = "1h" },
        Host = "tracker.example",
        Credential = "op://vault/tracker/token",
        Filter = "SELECT [System.Id] FROM WorkItems WHERE [System.Tags] CONTAINS 'needs-review'",
        Skill = ".goodgrief/skills/triage.md",
        Ref = "refs/heads/main",
        Mapping = new WatchMapping { Subject = "id", Version = "rev", IntentKey = "url" },
        PullPoint = PullPoints.ResidentRunner,
        Nominates = new Destination
        {
            Id = "what-a-sweep-opens",
            Kind = DestinationKinds.Flight,
            Requires = [],
            Opens = ["review"],
            OpensAs = DestinationOpening.Gated,
        },
        Bounds = new WatchBounds
        {
            ActiveHours = "09:00-17:00Z",
            CapPerPass = 25,
            Budget = new NominationBudget { Flights = 5, Window = "24h" },
        },
    };

    [Test]
    public async Task The_rendering_parses_back_to_the_same_document()
    {
        var written = AWatch();

        var parsed = EnvelopeYaml.ParseWatch(EnvelopeText.Render(written));

        await Assert.That(parsed.Diagnosis).IsNull()
            .Because("the renderer's own output has to be readable, or the two halves only "
                   + "look like a pair. Refused with: " + (parsed.Diagnosis ?? "nothing"));

        // COMPARED AS THE CANONICAL FORM, not with record equality, and the
        // reason is worth the line: `Destination` carries `Requires` and
        // `Opens` as `IReadOnlyList<string>`, so a synthesised record compares
        // them by REFERENCE - a `string[]` and the parser's own list are never
        // equal however identical their contents. Rendering both sides puts
        // them in one form, which is also the form the tree actually holds.
        await Assert.That(EnvelopeText.Render(parsed.Watch!))
            .IsEqualTo(EnvelopeText.Render(written))
            .Because("every member, including the nested trigger, mapping, bound and bounds. "
                   + "A round trip that drops one is a `gg airspace apply` that quietly "
                   + "un-declares it.");

        // AND THE NESTED MEMBERS BY HAND, because a rendering comparison would
        // also pass if BOTH sides dropped the same member.
        await Assert.That(parsed.Watch!.Trigger.Every).IsEqualTo(written.Trigger.Every);
        await Assert.That(parsed.Watch.Mapping.IntentKey).IsEqualTo(written.Mapping.IntentKey);
        await Assert.That(parsed.Watch.Nominates!.Opens).IsEquivalentTo(written.Nominates!.Opens!);
        await Assert.That(DestinationOpening.Of(parsed.Watch.Nominates))
            .IsEqualTo(DestinationOpening.Gated);
        await Assert.That(parsed.Watch.Bounds!.CapPerPass).IsEqualTo(25);
        await Assert.That(parsed.Watch.Bounds.Budget!.Flights).IsEqualTo(5);
    }

    [Test]
    public async Task A_watch_with_only_its_required_members_round_trips_too()
    {
        // ABSENT STAYS ABSENT, which is this file's whole rule one member over:
        // a rendering that grows a line by itself is a rendering that lies
        // about the stream, and a parser that invents a default is the same
        // defect read from the other end.
        var spare = AWatch() with { Bounds = null };

        var parsed = EnvelopeYaml.ParseWatch(EnvelopeText.Render(spare));

        await Assert.That(parsed.Diagnosis).IsNull();
        await Assert.That(parsed.Watch!.Bounds).IsNull()
            .Because("a watch that declared no bounds must not read back as one that "
                   + "declared empty ones - the first is unbounded and the second is a "
                   + "policy nobody wrote.");
    }

    [Test]
    public async Task The_tree_reads_a_watch_as_a_watch_and_not_as_a_broken_envelope()
    {
        // THE TRAP THIS CLASS EXISTS FOR. `Parse` falls through to the envelope
        // arm, so before `ParseWatch` a hand-authored watch landed in
        // `Unreadable` with a sentence about envelopes.
        var root = Scratch();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, AirspaceTree.Directory, "watches"));
            File.WriteAllText(
                Path.Combine(root, AirspaceTree.Directory, "watches", "nightly-triage.yaml"),
                EnvelopeText.Render(AWatch()));

            var read = AirspaceTree.Read(root);

            await Assert.That(read.Unreadable).IsEmpty()
                .Because("a watch is not a malformed envelope. Found: "
                       + string.Join(", ", read.Unreadable.Select(u => u.Diagnosis)));

            var document = read.Documents.Single();

            await Assert.That(document.Role).IsEqualTo(Roles.Watch);
            await Assert.That(document.Name).IsEqualTo("nightly-triage");
            await Assert.That(EnvelopeText.Render(document.Watch!))
                .IsEqualTo(EnvelopeText.Render(AWatch()))
                .Because("the tree holds the document, not a paraphrase of it.");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task A_watch_the_schema_refuses_is_unreadable_rather_than_silently_dropped()
    {
        // THE OTHER HALF OF THE TRAP. A file the parser refuses must be
        // REPORTED, not skipped - "anything pull did not write is not a
        // document" is the rule for a path nobody recognises, and a path we do
        // recognise carrying a bad document is a different state entirely.
        var root = Scratch();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, AirspaceTree.Directory, "watches"));
            File.WriteAllText(
                Path.Combine(root, AirspaceTree.Directory, "watches", "broken.yaml"),
                "shape: slack-channel\n");

            var read = AirspaceTree.Read(root);

            await Assert.That(read.Documents).IsEmpty();

            var refused = read.Unreadable.Single();

            await Assert.That(refused.Path).Contains("watches/broken.yaml");
            await Assert.That(refused.Diagnosis).DoesNotContain("envelope")
                .Because("the sentence has to be about what this document IS. Telling an "
                       + "author their watch does not read as an envelope sends them looking "
                       + "for the wrong mistake.");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
