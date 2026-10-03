using Gg.Contracts;
using Gg.Contracts.Authoring;

namespace Gg.Contracts.Tests;

/// <summary>
/// Learned context says what it was learned against, or it is refused.
/// </summary>
/// <remarks>
/// <para>
/// <b>Advice is the payload and the header is what lets it age.</b> The owner's
/// decision was that this carries advice rather than measurements — <i>"the point
/// is to give advice"</i> — and the cost of advice is that nothing about it says
/// when it stopped being true. A commit, an image digest, a profile or an envelope
/// version makes staleness computable instead of guessed.
/// </para>
/// <para>
/// <b>Refused where the author can still act</b>, which is the disposition
/// <c>produces:</c> already uses for an unknown fact family, and for the same
/// reason: the failure direction is permissive. Advice with no <c>against:</c> is
/// advice nothing can ever invalidate, so it would outlive every environment it
/// was true of and nothing would report a fault.
/// </para>
/// </remarks>
public class LearnedContextSaysWhatItWasLearnedAgainstTests
{
    /// <summary>One learned-context entry, built from the block each case is about.</summary>
    /// <remarks>
    /// <b>Re-indented in code rather than written as a list at every call site.</b>
    /// <c>learned:</c> became a list when advice was re-keyed by what it was learned
    /// against, and dashing each fixture by hand would have left the negative cases
    /// passing for a SHAPE reason instead of the rule they each name - a test that
    /// still goes red for the wrong cause is worse than one that goes green.
    /// </remarks>
    private static string AsEntry(string block)
    {
        var lines = block.TrimEnd('\n').Split('\n');
        var indented = lines.Where(l => l.Trim().Length > 0).ToList();
        var least = indented.Count == 0 ? 0 : indented.Min(l => l.Length - l.TrimStart().Length);

        return string.Join("\n", lines.Select((line, at) =>
        {
            if (line.Trim().Length == 0)
            {
                return line;
            }

            var relative = (line.Length - line.TrimStart().Length) - least;
            return at == 0
                ? new string(' ', 2) + "- " + line.TrimStart()
                : new string(' ', 4 + relative) + line.TrimStart();
        }));
    }

    private static string Learned(string body) => $"""
        context:
          scope: "**"
          constitution: "1.0.0"
        accepts: [repository]
        produces: [loop.outcome]
        learned:
        {AsEntry(body)}
        obligations:
          in-scope:
            check: machine
            rule: no-file-outside-scope
        loops:
          implement:
            executor: frontier
            discharges: [in-scope]
            moves: [read, edit]
            budget:
              wall-clock: "20m"
            on-exhaustion: handoff-to-human
        destinations:
          forge:
            kind: pull-request
            requires: [in-scope]
        """;

    [Test]
    public async Task Advice_with_no_header_is_refused()
    {
        var read = EnvelopeYaml.Parse(Learned("""
              advice:
                - "Do the thing the way that works."
        """));

        await Assert.That(read.Diagnosis).IsNotNull();
        await Assert.That(read.Diagnosis!).Contains("against");
    }

    /// <summary>
    /// A header naming nothing is the same absence one level in.
    /// </summary>
    /// <remarks>
    /// Every member of the header is nullable, because a pass may know the commit
    /// and not the image. All of them absent is a header that was written to
    /// satisfy a check rather than to say anything, and it is the shape a
    /// requirement invites if nobody refuses it.
    /// </remarks>
    [Test]
    public async Task A_header_that_names_nothing_is_refused()
    {
        var read = EnvelopeYaml.Parse(Learned("""
              against: {}
              advice:
                - "Do the thing the way that works."
        """));

        await Assert.That(read.Diagnosis).IsNotNull();
        await Assert.That(read.Diagnosis!).Contains("against");
    }

    /// <summary>
    /// A key this schema does not know is not a subject.
    /// </summary>
    /// <remarks>
    /// <b>The failure direction is permissive, which is why this refuses.</b> An
    /// unknown key is ignored by the parser rather than stored, so a header naming
    /// only one names nothing - and a document that silently kept it would hold
    /// advice no reader could ever match, which is what this header exists to stop.
    /// </remarks>
    [Test]
    public async Task A_header_naming_only_a_key_this_schema_does_not_know_is_refused()
    {
        var read = EnvelopeYaml.Parse(Learned("""
              against:
                machine: "vmlinux001"
              advice:
                - "Something worth saying."
        """));

        await Assert.That(read.Diagnosis).IsNotNull()
            .Because("an unknown key is ignored rather than stored, so this header names "
                   + "nothing - and advice nothing can match would outlive every environment "
                   + "it was true of.");
    }

    /// <summary>
    /// An environment is a thing advice can be learned about.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>ASKED FOR BY AN AGENT, GG-800, UNPROMPTED.</b> Three rehearsals produced
    /// good advice about a container daemon and none of it reached a flight. The
    /// third was told that an image key must be digest-pinned and that its own
    /// image is in <c>GG_IMAGE_DIGEST</c>; it was a RESIDENT runner rather than a
    /// pool member, so that variable was unset, and it correctly declined to name
    /// an image - then reached for <c>environment:</c>, because that is what it had
    /// actually learned about. The schema had no slot, the key was ignored, the
    /// header named nothing, and the whole document was refused.
    /// </para>
    /// <para>
    /// <b>THE ENVIRONMENT AND NOT THE INSTANCE, because instance names repeat.</b>
    /// An instance is named per environment per host - <c>ui/gg-env-1</c> on one
    /// machine says nothing about <c>gg-env-1</c> on another - so keying by
    /// instance would hand one machine's advice to a flight on a different one,
    /// which is the confusion this header exists to prevent. A charted environment
    /// is a declared airspace name, unique in the tenant, and it is what the
    /// strategy that furnishes every one of its instances describes.
    /// </para>
    /// <para>
    /// <b>No format rule, unlike <c>commit</c> and <c>image</c>.</b> Those two are
    /// compared against values a machine produces, so a description in either can
    /// never match and is refused here. An environment is a NAME somebody
    /// declared, and the only thing that could validate it is the tenant's own
    /// airspace, which this contract cannot read. The control plane stamps it from
    /// the grant, so the authority sits where the knowledge is.
    /// </para>
    /// </remarks>
    [Test]
    public async Task A_header_naming_only_an_environment_is_taken()
    {
        var read = EnvelopeYaml.Parse(Learned("""
              against:
                environment: "ui"
              advice:
                - "Rootless networking refuses host ports below 1024."
        """));

        await Assert.That(read.Diagnosis).IsNull()
            .Because("an environment is what a rehearsal of a PLACE learns about, and three "
                   + $"rehearsals could not say so: {read.Diagnosis}");

        await Assert.That(read.Envelope!.Learned!.Single().Against.Environment).IsEqualTo("ui")
            .Because("parsed and kept rather than merely tolerated - an ignored key is what "
                   + "made GG-800's header name nothing.");
    }

    [Test]
    public async Task An_environment_survives_the_writer()
    {
        // ROUND TRIP, because a key the writer drops vanishes the first time
        // anything re-renders the document - and the control plane stores a
        // composed envelope and renders it back.
        var first = EnvelopeYaml.Parse(Learned("""
              against:
                environment: "ui"
              advice:
                - "Rootless networking refuses host ports below 1024."
        """));

        await Assert.That(first.Diagnosis).IsNull();

        var again = EnvelopeYaml.Parse(EnvelopeText.Render(first.Envelope!));

        await Assert.That(again.Diagnosis).IsNull();
        await Assert.That(again.Envelope!.Learned!.Single().Against.Environment).IsEqualTo("ui")
            .Because("the writer emits every member of the header, or the next read loses it.");
    }

    [Test]
    public async Task Advice_about_an_environment_replaces_what_was_known_about_it()
    {
        // A SUBJECT, so Fold replaces rather than appends. Envelope.Validate
        // refuses a document carrying two entries for one subject, so an
        // environment the fold did not recognise would accumulate a second entry
        // and produce a proposal the applier could never accept - which is a gate
        // a person opens onto a refusal.
        static LearnedContext About(string environment, string advice) => new()
        {
            Against = new LearnedAgainst { Environment = environment },
            Advice = [advice],
        };

        var folded = Envelope.Fold(
            [About("ui", "The old thing.")],
            [About("ui", "The new thing."), About("dev", "A different place.")]);

        await Assert.That(folded.Count).IsEqualTo(2)
            .Because("one subject was already known and was replaced; one is new.");

        await Assert.That(folded.Single(e => e.Against.Environment == "ui").Advice.Single())
            .IsEqualTo("The new thing.")
            .Because("what a rehearsal hands back REPLACES what was known about that place, "
                   + "which is the rule the kind's own instruction states.");
    }

    [Test]
    public async Task A_header_with_no_advice_is_refused()
    {
        var read = EnvelopeYaml.Parse(Learned("""
              against:
                commit: "a1b2c3d"
              advice: []
        """));

        await Assert.That(read.Diagnosis).IsNotNull();
        await Assert.That(read.Diagnosis!).Contains("advice");
    }

    [Test]
    public async Task One_named_member_is_enough()
    {
        var read = EnvelopeYaml.Parse(Learned("""
              against:
                commit: "a1b2c3d"
              advice:
                - "Wait for the install before starting the server."
        """));

        await Assert.That(read.Diagnosis).IsNull()
            .Because("a pass may know the commit and not the image, and demanding all four "
                   + "would make the header a form to fill in.");
    }

    /// <summary>
    /// A commit is a commit id and nothing else.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Measured on GG-333</b>, the first rehearsal to hand its advice back
    /// cleanly. Its entry read:
    /// </para>
    /// <code>
    /// commit: 3a6de3c (HEAD detached at FETCH_HEAD)
    /// </code>
    /// <para>
    /// git's own chatter, pasted into the ONE field whose whole purpose is to be
    /// compared later. <see cref="LearnedAgainst"/> exists so advice can be told to
    /// have gone stale — <i>"advice that cannot be told to have gone stale will
    /// outlive every environment it was true of"</i> — and a value like that can
    /// never match a commit id, so the staleness check silently never fires. The
    /// document was valid, the round trip was clean, and the field was useless.
    /// </para>
    /// <para>
    /// <b>Refused rather than trimmed to the leading sha.</b> Trimming would be this
    /// schema guessing which part of a string the author meant, and it would take
    /// `3a6de3c (HEAD detached...)` and `3a6de3c-dirty` to the same place. A refusal
    /// reaches the agent at the tool, where it can look again and answer.
    /// </para>
    /// </remarks>
    [Test]
    public async Task A_commit_carrying_anything_but_a_commit_is_refused()
    {
        foreach (var written in (string[])
            ["3a6de3c (HEAD detached at FETCH_HEAD)", "3a6de3c-dirty", "HEAD",
             "refs/heads/develop", "3a6de3c ", "not a sha"])
        {
            var read = EnvelopeYaml.Parse(Learned($"""
                against:
                  commit: "{written}"
                advice:
                  - "Something worth saying."
                """));

            await Assert.That(read.Diagnosis).IsNotNull()
                .Because($"'{written}' cannot be compared with a commit id, so advice "
                       + "written against it can never be told to have gone stale.");

            await Assert.That(read.Diagnosis!).Contains("commit")
                .Because("and the refusal names the field, so an agent can fix it and "
                       + "call again rather than guessing which value was wrong.");
        }
    }

    /// <summary>
    /// An image is an image reference and nothing else.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Measured on GG-785</b>, the first rehearsal whose advice ever reached the
    /// envelope by itself. Its entry read:
    /// </para>
    /// <code>
    /// image: rootless Docker 29.7.2 / containerd 2.3.4 / Compose v5.5.0, env instance gg-env-1 (DOCKER_HOST=unix:///srv/env/gg-env-1/run/docker.sock)
    /// </code>
    /// <para>
    /// <b>THIS IS THE COMMIT DEFECT ABOVE, IN THE FIELD NEXT DOOR.</b> A description
    /// of the place, written into the one field whose purpose is to be compared with
    /// something. The document was valid, the round trip was clean, the advice was
    /// good — and the key can never equal an image reference, so the entry is
    /// undeliverable for ever and nothing says so. The <c>commit</c> field was
    /// hardened against exactly this on GG-333; <c>image</c> was left open, and the
    /// next rehearsal walked into it.
    /// </para>
    /// <para>
    /// <b>The digest form, because that is the only thing it is ever compared
    /// with.</b> <see cref="EnvironmentStrategy"/> refuses an image that is not
    /// pinned (<i>"what reset resets TO must be a fixed point, or the reset converges
    /// on whatever the tag means today"</i>), so every image this platform runs a
    /// member on is <c>name@sha256:…</c>. An entry keyed by a tag is not wrong in
    /// spirit and is still undeliverable in fact, which is the failure this refuses
    /// rather than the shape it dislikes.
    /// </para>
    /// <para>
    /// <b>Refused rather than parsed out of the prose</b>, on GG-333's own argument:
    /// trimming would be this schema guessing which part of a string the author
    /// meant. A refusal reaches the agent at the tool, where it has another attempt
    /// and can look again.
    /// </para>
    /// </remarks>
    [Test]
    public async Task An_image_carrying_anything_but_an_image_reference_is_refused()
    {
        foreach (var written in (string[])
            ["rootless Docker 29.7.2 / containerd 2.3.4 / Compose v5.5.0, env instance gg-env-1",
             "ghcr.io/acme/ci:12", "gg-member:latest", "127.0.0.1:5000/gg-member",
             "sha256:1c82ce2828c4b885b9bf7d3e1182b0b93975e0d1580e158b19319d851cccba5a",
             "the ui member image"])
        {
            var read = EnvelopeYaml.Parse(Learned($"""
                against:
                  image: "{written}"
                advice:
                  - "Something worth saying."
                """));

            await Assert.That(read.Diagnosis).IsNotNull()
                .Because($"'{written}' can never equal the digest-pinned reference a member "
                       + "actually runs, so advice keyed to it reaches no flight and nothing "
                       + "reports that it did not.");

            await Assert.That(read.Diagnosis!).Contains("image")
                .Because("and the refusal names the field, so an agent can fix it and call "
                       + "again rather than guessing which value was wrong.");
        }
    }

    [Test]
    public async Task An_image_pinned_by_digest_is_taken()
    {
        // THE REAL ONES, off this tenant's own strategies. A rule that refused
        // what the platform actually pins would refuse every entry there is,
        // and both of these are what `gg airspace show` serves today.
        foreach (var written in (string[])
            ["127.0.0.1:5000/gg-member@sha256:1c82ce2828c4b885b9bf7d3e1182b0b93975e0d1580e158b19319d851cccba5a",
             "127.0.0.1:5000/gg-member-browser@sha256:2cdc8f27925b6637085c82075d280da8782c8b9e1b3361b1b89d08154c9facf5",
             "ghcr.io/acme/ci@sha256:1c82ce2828c4b885b9bf7d3e1182b0b93975e0d1580e158b19319d851cccba5a"])
        {
            var read = EnvelopeYaml.Parse(Learned($"""
                against:
                  image: "{written}"
                advice:
                  - "Something worth saying."
                """));

            await Assert.That(read.Diagnosis).IsNull()
                .Because($"'{written}' is pinned by digest: {read.Diagnosis}");
        }
    }

    [Test]
    public async Task A_header_naming_a_repository_is_not_held_to_the_image_rule()
    {
        // THE GUARD. Every member of the header is nullable because a pass may
        // know one and not another, and a rule that fired on an ABSENT image
        // would refuse every repository-keyed entry there is - which is all six
        // lines this tenant delivers today.
        var read = EnvelopeYaml.Parse(Learned("""
            against:
              repository: "JDX/JDNext"
            advice:
              - "Something worth saying."
            """));

        await Assert.That(read.Diagnosis).IsNull()
            .Because("an absent image is not a malformed one, and conflating them would "
                   + $"refuse the advice that already works: {read.Diagnosis}");
    }

    [Test]
    public async Task A_commit_id_short_or_long_is_taken()
    {
        // BOTH ENDS, because refusing a short sha would refuse what `git rev-parse
        // --short` hands an agent, and refusing a long one would refuse what a
        // machine has. Seven is git's own short default.
        foreach (var written in (string[])
            ["3a6de3c", "3a6de3c5cbe45e6bd23e6579d54c30db053a03f3",
             "3A6DE3C5CBE45E6BD23E6579D54C30DB053A03F3"])
        {
            var read = EnvelopeYaml.Parse(Learned($"""
                against:
                  commit: "{written}"
                advice:
                  - "Something worth saying."
                """));

            await Assert.That(read.Diagnosis).IsNull()
                .Because($"'{written}' is a commit id: {read.Diagnosis}");
        }
    }
}
