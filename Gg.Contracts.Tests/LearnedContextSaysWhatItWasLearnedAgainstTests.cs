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
