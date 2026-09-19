using Gg.Contracts;
using Gg.Contracts.Authoring;

namespace Gg.Client.Tests;

/// <summary>
/// A watch says whose work its flights are: the tenant's, or one person's.
/// </summary>
/// <remarks>
/// <para>
/// <b>S42.1-02.</b> ADR-0024: a watch is the tenant's or one person's, and there
/// is nothing in between. Before this member every watch was the tenant's and
/// nobody was behind it, so a watch's flights carried no person at all.
/// </para>
/// <para>
/// <b>Absent is the tenant</b>, because that is what every watch written before
/// this member meant, and nothing already in force changes meaning.
/// </para>
/// <para>
/// <b>The round trip is the claim, as <c>AWatchReadsBackAsItWasWrittenTests</c>
/// says of the whole document</b>: a parser that reads a key the renderer never
/// writes is two halves that only look like a pair.
/// </para>
/// </remarks>
public class AWatchSaysWhoseItIsTests
{
    private const string APerson =
        "a-directory:72f988bf-86f1-41af-91ab-2d7cd011db47/0b1c2d3e-4f50-6172-8394-a5b6c7d8e9f0";

    private static WatchDocument AWatch() => new()
    {
        Shape = WatchShapes.WorkItems,
        Trigger = new WatchTrigger { Every = "1h" },
        Host = "tracker.example",
        Credential = "op://vault/tracker/token",
        Filter = "SELECT [System.Id] FROM WorkItems WHERE [System.AssignedTo] = @Me",
        Repository = "payments",
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
        },
    };

    [Test]
    public async Task A_watch_that_says_nothing_is_the_tenants()
    {
        var parsed = EnvelopeYaml.ParseWatch(EnvelopeText.Render(AWatch()));

        await Assert.That(parsed.Diagnosis).IsNull();
        await Assert.That(parsed.Watch!.For).IsNull()
            .Because("absent means the tenant, which is what every watch before this meant.");
        await Assert.That(WatchDocument.Validate(parsed.Watch!)).IsNull();
    }

    [Test]
    [Arguments(LinesOfWork.Tenant)]
    [Arguments(APerson)]
    public async Task Whose_it_is_renders_and_reads_back(string whose)
    {
        var written = AWatch() with { For = whose };

        var text = EnvelopeText.Render(written);
        var parsed = EnvelopeYaml.ParseWatch(text);

        await Assert.That(text).Contains($"for: {whose}")
            .Because("a member the renderer drops is one the tree silently forgets.");
        await Assert.That(parsed.Diagnosis).IsNull()
            .Because("refused with: " + (parsed.Diagnosis ?? "nothing"));
        await Assert.That(parsed.Watch!.For).IsEqualTo(whose);
        await Assert.That(WatchDocument.Validate(parsed.Watch!)).IsNull();
    }

    [Test]
    public async Task A_value_that_is_neither_the_tenant_nor_a_person_is_refused()
    {
        // NOTHING IN BETWEEN. `team` is exactly the third value ADR-0024 says a
        // watch cannot have, and a value nobody recognises must not read as the
        // tenant by default.
        var refused = WatchDocument.Validate(AWatch() with { For = "team" });

        await Assert.That(refused).IsNotNull();
        await Assert.That(refused!).Contains("team");
    }

    [Test]
    public async Task A_malformed_person_is_refused_in_the_parsers_words()
    {
        var refused = WatchDocument.Validate(AWatch() with { For = "a-directory:" });

        await Assert.That(refused).IsNotNull();
        await Assert.That(refused!).IsEqualTo(PersonSpelling.Diagnose("a-directory:"))
            .Because("one parser, one sentence - a watch and an obligation must not describe "
                   + "the same mistake two ways.");
    }

    [Test]
    public async Task A_personal_watch_cannot_be_swept_by_the_control_plane()
    {
        // "THAT PERSON'S RUNNERS" CANNOT INCLUDE THE CONTROL PLANE, and the
        // control plane holds no person's credential. Refused where the
        // document is authored, not discovered when no sweep ever runs.
        var refused = WatchDocument.Validate(
            AWatch() with { For = APerson, PullPoint = PullPoints.ControlPlane });

        await Assert.That(refused).IsNotNull();
        await Assert.That(refused!).Contains(PullPoints.ResidentRunner)
            .Because("the author has to learn what a personal watch may be performed by.");
    }

    [Test]
    [Arguments(null, APerson)]
    [Arguments(LinesOfWork.Tenant, APerson)]
    [Arguments(APerson, LinesOfWork.Tenant)]
    public async Task Changing_whose_it_is_is_reported_and_never_as_a_tightening(
        string? was, string now)
    {
        // THE DOOR REFUSES THIS OUTRIGHT: a watch never changes hands. The diff
        // still has to say so, in its own sentence, rather than showing nothing
        // and letting the author find out at apply.
        var widening = WatchDirection.Widening(
            AWatch() with { For = was }, AWatch() with { For = now });

        await Assert.That(widening).IsNotNull();
        await Assert.That(widening!.Field).IsEqualTo("for");
        await Assert.That(widening.Because).Contains("retire")
            .Because("the only way a watch changes whose it is, and the sentence should say it.");
    }

    [Test]
    public async Task Saying_tenant_where_nothing_was_said_is_not_a_change()
    {
        // ABSENT AND `tenant` ARE ONE ANSWER, so writing the default down must
        // not read as moving the watch.
        var widening = WatchDirection.Widening(
            AWatch() with { For = null }, AWatch() with { For = LinesOfWork.Tenant });

        await Assert.That(widening).IsNull();
    }
}
