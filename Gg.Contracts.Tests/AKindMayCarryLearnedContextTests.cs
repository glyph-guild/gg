using Gg.Contracts;
using Gg.Contracts.Authoring;

namespace Gg.Contracts.Tests;

/// <summary>
/// A work kind may carry what earlier flights found out about its environment.
/// </summary>
/// <remarks>
/// <para>
/// <b>ADR-0031.</b> `ui-preview` carried seven instructions; six were goals or
/// prohibitions and one was technique — <i>"Start the server in the background.
/// A server in the foreground ends the loop"</i> — and the technique is the one
/// that killed GG-309. Not by being vague: by naming a mechanism. "Background" is
/// ambiguous between the harness's background-task feature and a detached
/// process, the agent used the former, and the harness kills it when a session
/// ends. An author who has not measured an environment cannot write technique for
/// it, and the next environment differs anyway.
/// </para>
/// <para>
/// So the envelope keeps the goal and this keeps the how. A goal is true of every
/// environment and ages when the kind's intent changes; a technique is a command,
/// and commands rot — which is what the provenance header exists to make
/// computable rather than guessed.
/// </para>
/// <para>
/// <b>Work-kind-only, and the argument is <c>produces:</c>' own.</b> That field is
/// declared work-kind-only on a security ground: <c>moves:</c> composes across
/// layers, so <i>"a narrowing — including one living in a customer's own
/// repository — could delete every scope gate on its own flights"</i>. Advice
/// reaching every future flight of a kind is the same hazard pointing the other
/// way, so a narrowing that could ADD advice would be injecting prompt text from
/// a repository. <see cref="EnvelopeNarrowing"/> gets no member for it at all,
/// exactly as it has none for <c>produces</c>.
/// </para>
/// </remarks>
public class AKindMayCarryLearnedContextTests
{
    private static string WithLearned(string learned) => $"""
        context:
          scope: "**"
          constitution: "1.0.0"
        accepts: [repository]
        produces: [loop.outcome]
        {learned}
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
    public async Task A_kind_may_declare_it_and_it_reads_back()
    {
        var read = EnvelopeYaml.Parse(WithLearned("""
            learned:
              against:
                repository: "JDX/JDNext"
                commit: "a1b2c3d"
              advice:
                - "npm ci under src/JDX.Web takes about four minutes here."
            """));

        await Assert.That(read.Diagnosis).IsNull();
        await Assert.That(read.Envelope!.Learned).IsNotNull();
        await Assert.That(read.Envelope.Learned!.Against.Repository).IsEqualTo("JDX/JDNext");
        await Assert.That(read.Envelope.Learned.Against.Commit).IsEqualTo("a1b2c3d");
        await Assert.That(read.Envelope.Learned.Advice.Single())
            .IsEqualTo("npm ci under src/JDX.Web takes about four minutes here.");
    }

    /// <summary>
    /// A document that said nothing does not gain a section by being written out.
    /// </summary>
    /// <remarks>
    /// <b>The nullable-not-absorbing law, and it has been broken before.</b>
    /// Absorbing is right for a member being repaired and wrong for one being
    /// added: absence is a document that did not say, and an empty section is a
    /// document declaring there is none. A pull that invented one would put words
    /// in an author's mouth and then diff against them.
    /// </remarks>
    [Test]
    public async Task A_kind_that_declares_none_does_not_gain_one_on_a_render()
    {
        var read = EnvelopeYaml.Parse(WithLearned(""));

        await Assert.That(read.Diagnosis).IsNull();
        await Assert.That(read.Envelope!.Learned).IsNull();
        await Assert.That(EnvelopeText.Render(read.Envelope!)).DoesNotContain("learned");
    }

    [Test]
    public async Task It_survives_a_round_trip_through_the_writer()
    {
        var first = EnvelopeYaml.Parse(WithLearned("""
            learned:
              against:
                image: "sha256:abc"
                envelope: "v7"
              advice:
                - "Serve on the port, not a port."
                - "The dev server needs ninety seconds before it answers."
            """));

        await Assert.That(first.Diagnosis).IsNull();

        var again = EnvelopeYaml.Parse(EnvelopeText.Render(first.Envelope!));

        await Assert.That(again.Diagnosis).IsNull();
        // PART BY PART, because a record's own equality compares an
        // IReadOnlyList member by REFERENCE - so `IsEqualTo` on the whole thing
        // can never pass here however faithful the round trip, and would have
        // read as the writer dropping something.
        await Assert.That(again.Envelope!.Learned!.Against)
            .IsEqualTo(first.Envelope!.Learned!.Against);

        await Assert.That(again.Envelope.Learned.Advice)
            .IsEquivalentTo(first.Envelope.Learned.Advice)
            .Because("a member the writer drops is a member a pull silently deletes, which is "
                   + "how an author's document loses a section nobody meant to remove.");
    }

    /// <summary>
    /// A narrowing cannot carry it, and the refusal is the security property.
    /// </summary>
    /// <remarks>
    /// A narrowing may live in a customer's own repository. One that could add
    /// advice would be putting prose from a repository into the prompt of every
    /// later flight of that kind — the injection path the gate exists to close,
    /// reached by a document nobody reviews.
    /// </remarks>
    [Test]
    public async Task A_narrowing_may_not_carry_it()
    {
        var read = EnvelopeYaml.ParseNarrowing("""
            learned:
              against:
                commit: "a1b2c3d"
              advice:
                - "Anything at all."
            """);

        await Assert.That(read.Diagnosis).IsNotNull();
        await Assert.That(read.Diagnosis!).Contains("learned");
    }
}
