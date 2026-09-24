namespace Gg.Cli.Tests;

using Gg.Client;
using Gg.Contracts;

/// <summary>
/// <c>gg facts</c>: what a flight recorded, beside <c>gg log</c> and <c>gg why</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>A flight records nine facts and a log shows one.</b> Two of the thirteen
/// kinds reach a story, and the loop outcome's reason is cut where it is
/// produced. So the manifests, the provenance of each tree, the digest
/// extracted from the transcript and the proposal a scoring flight exists to
/// make were all recorded, hashed, read by admission, and unreadable by a
/// person.
/// </para>
/// <para>
/// <b>A command rather than only a tab</b>, because every other question about
/// a flight has one - <c>log</c>, <c>why</c>, <c>plan</c>, <c>show</c> - and
/// evidence is the one a script has most reason to want.
/// </para>
/// </remarks>
public class GgFactsReadsWhatAFlightRecordedTests
{
    [Test]
    public async Task The_verb_takes_a_flight_the_way_log_and_why_do()
    {
        await Assert.That(((CliAction.Facts)CliArgs.Parse(["facts", "GG-42"])).Reference)
            .IsEqualTo("GG-42");

        await Assert.That(((CliAction.Facts)CliArgs.Parse(["facts", "GG-42", "--json"])).Json)
            .IsTrue()
            .Because("the machine-readable form is the point of this one: a script asking "
                   + "what a flight recorded is the caller with the most reason to.");
    }

    [Test]
    public async Task Naming_no_flight_says_what_to_type_rather_than_what_is_wrong()
    {
        // Article XI, and the shape `gg log` already uses: a refusal that names
        // the thing to type saves a reader a trip to --help.
        var refused = CliArgs.Parse(["facts"]);

        await Assert.That(refused).IsTypeOf<CliAction.Unknown>();
        await Assert.That(((CliAction.Unknown)refused).Message).Contains("GG-")
            .Because($"it has to show the shape. Said: {((CliAction.Unknown)refused).Message}");
    }

    [Test]
    public async Task A_preview_row_says_where_to_look()
    {
        // THE ONE FACT WHOSE CONTENT IS AN INSTRUCTION. `gg facts` is what
        // somebody runs when they have been asked to review a preview, and the
        // gate that asked them cannot carry the address - its payload is
        // assembled, used for a null check and discarded. Says' default arm
        // answers "" and cannot fail, so a kind nobody wrote a case for prints
        // a row with the column a person came for missing.
        var text = VerbOutput.ToText(new VerbResult.Facts(new FlightFacts
        {
            FlightNumber = "GG-268",
            Facts =
            [
                new RecordedFact
                {
                    Disposition = EvidenceDispositions.Inline,
                    RecordedAt = new DateTimeOffset(2026, 9, 24, 17, 9, 53, TimeSpan.Zero),
                    Fact = new FactEnvelope
                    {
                        IdempotencyKey = "preview-1",
                        Kind = FactKinds.PreviewUrl,
                        Digest = new string('b', 64),
                        ObservedAt = new DateTimeOffset(2026, 9, 24, 17, 9, 52, TimeSpan.Zero),
                        Preview = new PreviewUrl
                        {
                            Url = "https://jdapp-01.goodgrief.dev",
                            Exposure = "jdapp",
                            Slot = "01",
                        },
                    },
                },
            ],
        }));

        await Assert.That(text).Contains("https://jdapp-01.goodgrief.dev")
            .Because("this verb is what a person runs to find out where the preview is, and a "
                   + "row that names the kind and withholds the address has told them one "
                   + "exists without telling them where.");
        await Assert.That(text).Contains("jdapp")
            .Because("the exposure and slot come with it, so the address can be reconciled "
                   + "against an inventory when a preview stops answering.");
    }

    [Test]
    public async Task Each_row_says_its_kind_and_the_budget_that_held_it()
    {
        // THE DISPOSITION IS ON THE ROW, because it is the answer to why one of
        // them has no content. A transcript rendered as empty without saying
        // `reference` reads as a defect rather than as a boundary.
        var text = VerbOutput.ToText(new VerbResult.Facts(new FlightFacts
        {
            FlightNumber = "GG-42",
            Facts =
            [
                new RecordedFact
                {
                    Disposition = "reference",
                    RecordedAt = new DateTimeOffset(2026, 9, 14, 17, 9, 53, TimeSpan.Zero),
                    Fact = new FactEnvelope
                    {
                        IdempotencyKey = "t-1",
                        Kind = FactKinds.LoopTranscript,
                        Digest = new string('a', 64),
                        ObservedAt = new DateTimeOffset(2026, 9, 14, 17, 9, 52, TimeSpan.Zero),
                        Transcript = new ArtifactReference
                        {
                            Locator = "file:///home/gg/.cache/one.jsonl",
                            Sha256 = new string('b', 64),
                            Bytes = 392901,
                            MediaType = "application/x-ndjson",
                            // RUNNER-LOCAL, which is the whole point of the row:
                            // the bytes never left the machine that made them.
                            Scope = ArtifactScopes.RunnerLocal,
                        },
                    },
                },
            ],
        }));

        await Assert.That(text).Contains(FactKinds.LoopTranscript, StringComparison.Ordinal);
        await Assert.That(text).Contains("reference", StringComparison.Ordinal)
            .Because("which budget held it is why the bytes are not here.");
        await Assert.That(text).Contains("one.jsonl", StringComparison.Ordinal)
            .Because("the locator is the whole value of a reference row - it is what the "
                   + "truncated reason has been pointing at with no route to it.");
    }

    [Test]
    public async Task A_flight_that_recorded_nothing_says_so_rather_than_printing_a_header()
    {
        // An empty list is a real answer - a flight nobody has flown yet, or one
        // whose runner shipped nothing - and it is not the same as a failed read.
        var text = VerbOutput.ToText(new VerbResult.Facts(new FlightFacts
        {
            FlightNumber = "GG-1",
            Facts = [],
        }));

        await Assert.That(text.Length).IsGreaterThan(20)
            .Because($"a bare header reads as a broken read. Said: {text}");
    }
}
