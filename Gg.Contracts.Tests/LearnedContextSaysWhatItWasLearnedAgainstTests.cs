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
    private static string Learned(string body) => $"""
        context:
          scope: "**"
          constitution: "1.0.0"
        accepts: [repository]
        produces: [loop.outcome]
        learned:
        {body}
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
