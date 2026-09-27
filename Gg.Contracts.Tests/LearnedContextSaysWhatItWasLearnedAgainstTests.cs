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
}
