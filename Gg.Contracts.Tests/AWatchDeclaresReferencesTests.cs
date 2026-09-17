using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// A watch says what to look at by reference, and never how to look.
/// </summary>
/// <remarks>
/// <para>
/// <b>S39.1-02.</b> ADR-0022 § 5: a watch names <i>a shape, a host and
/// credential reference, a filter, and a mapping from what the shape reports to
/// what a nomination carries</i> — and <i>"the document never contains a
/// procedure; the repository it references may, at a pinned ref, where skills
/// already live."</i> ADR-0015 § 1's rule, narrowed to the document.
/// </para>
/// <para>
/// <b>The credential is a LOCATOR and never a value</b>, which is the boundary
/// this product is built on: the runner resolves the secret into the server's
/// environment, so neither the document nor the executor ever holds it.
/// </para>
/// <para>
/// <b>THE MAPPING IS THREE NAMED FIELDS AND THAT IS A DECISION.</b> A nomination
/// carries more than three members, and the other ones are not the mapping's to
/// fill: <c>work-kind</c> comes from the bound's <c>opens:</c>, <c>mode</c> from
/// its <c>opens-as</c>, and <c>reason</c> and <c>note</c> are what the executor
/// writes. <c>environment</c> and <c>repository</c> are deliberately excluded
/// even though the bound has a <c>may-select</c>, because nothing on this path
/// selects — there is no classifier in front of a watch. A watch that needs to
/// select wants a fourth field with its own argument, not a quiet fourth key.
/// </para>
/// </remarks>
public class AWatchDeclaresReferencesTests
{
    internal static Destination ABound() => new()
    {
        Id = "what-a-sweep-opens",
        Kind = DestinationKinds.Flight,
        Requires = [],
        Opens = ["review"],
    };

    internal static WatchDocument AWatch() => new()
    {
        Shape = WatchShapes.WorkItems,
        Trigger = new WatchTrigger { Every = "1h" },
        Host = "tracker.example",
        Credential = "op://vault/tracker/token",
        Filter = "SELECT [System.Id] FROM WorkItems WHERE [System.Tags] CONTAINS 'needs-review'",
        Repository = "payments",
        Skill = ".goodgrief/skills/triage.md",
        Ref = "refs/heads/main",
        Mapping = new WatchMapping
        {
            Subject = "id",
            Version = "rev",
            IntentKey = "url",
        },
        PullPoint = PullPoints.ResidentRunner,
        Nominates = ABound(),
    };

    [Test]
    public async Task A_whole_watch_is_valid()
    {
        await Assert.That(WatchDocument.Validate(AWatch())).IsNull();
    }

    [Test]
    public async Task The_shape_is_one_this_version_knows()
    {
        var refused = WatchDocument.Validate(AWatch() with { Shape = "slack-channel" });

        await Assert.That(refused).IsNotNull();
        await Assert.That(refused!).Contains("slack-channel")
            .Because("the refusal names what was asked for, because an author reading it has "
                   + "to decide whether the word is wrong or the version is old.");
    }

    [Test]
    public async Task Only_the_tracker_shape_exists_and_the_reason_is_a_missing_server()
    {
        // THE GAP IS NAMED RATHER THAN PAPERED. ADR-0023's consequences say it
        // plainly: `tracker` and `gg` are the servers that exist, and checking
        // pull requests from a runner needs a forge reader server that does
        // not. So a forge shape is not missing by oversight - it is missing
        // because nothing could serve it, and pull-request observation stays
        // the control-plane path thirty-eight built.
        await Assert.That(WatchShapes.All).IsEquivalentTo((string[])[WatchShapes.WorkItems]);
    }

    [Test]
    public async Task The_trigger_is_a_duration_this_reads()
    {
        var refused = WatchDocument.Validate(
            AWatch() with { Trigger = new WatchTrigger { Every = "sometimes" } });

        await Assert.That(refused).IsNotNull();
        await Assert.That(refused!).Contains("sometimes")
            .Because("a period that parses into nothing is a watch that reads as scheduled "
                   + "and sweeps never.");
    }

    [Test]
    public async Task A_watch_with_no_filter_is_refused_rather_than_read_as_everything()
    {
        // EMPTY AND ALL MUST NOT BE THE SAME VALUE. A filter left blank reading
        // as "every work item in the tracker" is the loop this slice's budget
        // exists to bound, arriving by omission rather than by anybody asking.
        var refused = WatchDocument.Validate(AWatch() with { Filter = "  " });

        await Assert.That(refused).IsNotNull();
    }

    [Test]
    public async Task The_mapping_names_the_three_members_a_shape_can_fill()
    {
        var members = typeof(WatchMapping)
            .GetProperties()
            .Select(p => p.Name)
            .ToList();

        await Assert.That(members).IsEquivalentTo((string[])
        [
            nameof(WatchMapping.Subject),
            nameof(WatchMapping.Version),
            nameof(WatchMapping.IntentKey),
        ])
            .Because("the others are not the mapping's to fill - the work kind and the mode "
                   + "come from the bound, the reason and the note from the executor - and "
                   + "`environment` and `repository` are excluded because nothing on this "
                   + "path selects. A fourth key wants its own argument.");
    }

    [Test]
    public async Task A_mapping_missing_a_member_is_refused_at_authoring()
    {
        // A NOMINATION WITHOUT A SUBJECT IS NOT A NOMINATION, and finding that
        // out at sweep time would mean a watch that validated, applied, ran,
        // and produced nothing anybody could act on.
        var refused = WatchDocument.Validate(
            AWatch() with
            {
                Mapping = new WatchMapping { Subject = "", Version = "rev", IntentKey = "url" },
            });

        await Assert.That(refused).IsNotNull();
    }

    [Test]
    public async Task The_document_carries_references_and_never_their_text()
    {
        // ADR-0015 SECTION 1, NARROWED TO THE DOCUMENT, asserted structurally
        // rather than trusted: a member able to carry a procedure is how the
        // rule stops holding, and it stops holding quietly because a document
        // with a script in it is not malformed.
        //
        // `Skill` and `Ref` are a PATH and a REF - a reference resolved by the
        // control plane at apply, which is ADR-0018 section 5's rule one noun
        // over. `Credential` is a locator for the same reason one layer down.
        var watch = AWatch();

        await Assert.That(watch.Skill).DoesNotContain("\n")
            .Because("a path, not a procedure. The repository holds the words, at a ref, "
                   + "where `CODEOWNERS` review them.");

        await Assert.That(watch.Credential).StartsWith("op://")
            .Because("a locator. The runner resolves the secret into the server's "
                   + "environment, so neither this document nor either executor holds it.");

        await Assert.That(typeof(WatchDocument).GetProperties().Select(p => p.Name))
            .DoesNotContain("Script")
            .Because("the script is the repository's, landed by a crystalize flight in "
                   + "slice forty - never a member here.");
    }
}
