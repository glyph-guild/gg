using Gg.Contracts;
using Gg.Contracts.Authoring;
using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// A watch says which repository its skill is in, and changing where it reads
/// is reviewed.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two gaps, found while designing what reads the skill.</b> Slice
/// thirty-nine's step 2 has the control plane fetch a watch's skill at its ref,
/// ADR-0018 § 5's rule one noun over. That needs to know <i>which
/// repository</i> — and <see cref="WatchDocument.Skill"/> said "a path in the
/// repository" with no member naming one. ADR-0022 § 5 says <i>"the repository
/// it references"</i>; the document referenced none.
/// </para>
/// <para>
/// <b>And <c>WatchDirection</c> compared the bound, the filter, the period and
/// the bounds — and nothing a watch READS or REACHES.</b> <see
/// cref="WatchDocument.Ref"/>'s own summary says it exists <i>"so what ran is
/// what somebody reviewed"</i>, and pointing it at a branch nobody reviewed was
/// an ungated apply. So was a different skill, a different host, a different
/// credential, a different performer, and a mapping that turns the same sweep
/// into more nominations.
/// </para>
/// <para>
/// <b>Every one of them is the filter's argument.</b> None has an order this
/// side can compute — a different credential is not "more" or "less", it is
/// an identity whose reach is not visible from here. So equality is the only
/// comparison available, and the conservative answer is the only sound one.
/// What is left ungated is what can be proven to do less: a longer period,
/// lower bounds and a narrower bound.
/// </para>
/// </remarks>
public class AWatchNamesWhatItReadsTests
{
    private static WatchDocument A() => AWatchDeclaresReferencesTests.AWatch();

    [Test]
    public async Task A_watch_names_the_repository_its_skill_is_in()
    {
        var refused = WatchDocument.Validate(A() with { Repository = " " });

        await Assert.That(refused).IsNotNull();
        await Assert.That(refused!).Contains("repository")
            .Because("a skill path with no repository is a path in nothing - and the control "
                   + "plane, which fetches it, would have to guess whose tree to read.");

        await Assert.That(WatchDocument.Validate(A())).IsNull();
    }

    [Test]
    public async Task The_repository_is_on_the_wire()
    {
        await Assert.That(ProtocolSurface.JsonMembers[typeof(WatchDocument)])
            .Contains("repository");
    }

    [Test]
    public async Task The_repository_is_written_to_the_tree_and_read_back()
    {
        var text = EnvelopeText.Render(A());

        await Assert.That(text).Contains($"repository: {A().Repository}\n");

        var parsed = EnvelopeYaml.ParseWatch(text);

        await Assert.That(parsed.Diagnosis).IsNull();
        await Assert.That(parsed.Watch!.Repository).IsEqualTo(A().Repository);
    }

    [Test]
    public async Task A_watch_file_with_no_repository_is_refused_naming_it()
    {
        var text = EnvelopeText.Render(A()).Replace(
            $"repository: {A().Repository}\n", string.Empty, StringComparison.Ordinal);

        var parsed = EnvelopeYaml.ParseWatch(text);

        await Assert.That(parsed.Watch).IsNull();
        await Assert.That(parsed.Diagnosis!).Contains("'repository'");
    }

    [Test]
    [Arguments("repository")]
    [Arguments("skill")]
    [Arguments("ref")]
    [Arguments("host")]
    [Arguments("credential")]
    [Arguments("shape")]
    [Arguments("pull-point")]
    [Arguments("mapping.subject")]
    [Arguments("mapping.version")]
    [Arguments("mapping.intent-key")]
    public async Task Any_change_to_what_a_watch_reads_or_reaches_is_a_widening(string field)
    {
        var changed = Changing(field);

        var widening = WatchDirection.Widening(A(), changed);

        await Assert.That(widening).IsNotNull()
            .Because($"'{field}' has no order this side can compute, so a change to it can "
                   + "only be reviewed or trusted - and trusting it is the ungated direction.");

        await Assert.That(widening!.Field).IsEqualTo(field);

        await Assert.That(widening.Because).IsNotEmpty();
    }

    [Test]
    public async Task A_ref_moved_says_that_nobody_reviewed_the_words_there()
    {
        // THE ONE THAT DEFEATS ITS OWN MEMBER. `Ref` exists so that what ran is
        // what somebody reviewed; a branch is anybody's, so moving the ref
        // without a gate is the review skipped by the member meant to hold it.
        var moved = WatchDirection.Widening(A(), A() with { Ref = "refs/heads/my-branch" });

        await Assert.That(moved!.Because).Contains("review");
    }

    [Test]
    public async Task Each_reference_member_says_why_in_its_own_words()
    {
        // WRITTEN BY HAND, and this holds it: one sentence copied across ten
        // arms would be a table wearing arms' clothes, and the author told why
        // would be told the same thing whatever they changed.
        string[] fields =
        [
            "repository", "skill", "ref", "host", "credential", "shape", "pull-point",
            "mapping.subject", "mapping.version", "mapping.intent-key",
        ];

        var sentences = fields
            .Select(f => WatchDirection.Widening(A(), Changing(f))!.Because)
            .ToList();

        await Assert.That(sentences.Distinct(StringComparer.Ordinal).Count())
            .IsEqualTo(fields.Length);
    }

    [Test]
    public async Task What_can_be_proven_to_do_less_still_needs_no_gate()
    {
        // THE LINE HAS TO STAY WHERE IT WAS on the side that was right. A watch
        // whose every edit took a gate would teach its authors to stop editing
        // it; slowing a watch down is somebody deciding to do less.
        await Assert.That(WatchDirection.Widening(
            A(), A() with { Trigger = new WatchTrigger { Every = "24h" } })).IsNull();

        await Assert.That(WatchDirection.Widening(A(), A())).IsNull();
    }

    private static WatchDocument Changing(string field) => field switch
    {
        "repository" => A() with { Repository = "a-different-service" },
        "skill" => A() with { Skill = ".goodgrief/skills/something-else.md" },
        "ref" => A() with { Ref = "refs/heads/my-branch" },
        "host" => A() with { Host = "another-tracker.example" },
        "credential" => A() with { Credential = "op://vault/admin/token" },
        "shape" => A() with { Shape = "pull-requests" },
        "pull-point" => A() with { PullPoint = PullPoints.ControlPlane },
        "mapping.subject" => A() with { Mapping = A().Mapping with { Subject = "title" } },
        "mapping.version" => A() with { Mapping = A().Mapping with { Version = "changed-at" } },
        "mapping.intent-key" => A() with { Mapping = A().Mapping with { IntentKey = "id" } },
        _ => throw new ArgumentOutOfRangeException(nameof(field), field, null),
    };
}
