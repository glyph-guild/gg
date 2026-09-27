using Gg.Contracts;
using Gg.Contracts.Authoring;

namespace Gg.Contracts.Tests;

/// <summary>
/// Learned context reads as a document on its own, without the envelope it amends.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a fifth parse entry point rather than a flag on <c>Parse</c>.</b>
/// <see cref="Envelope"/> has five <c>required</c> members — <c>Text</c>,
/// <c>Context</c>, <c>Obligations</c>, <c>Loops</c>, <c>Destinations</c> — so an
/// amendment cannot BE an envelope; the type will not hold one. That is the
/// language enforcing the same thing this slice is arguing: a partial envelope is
/// not a kind of envelope, and pretending otherwise is what made GG-330 invent
/// <c>obligations</c> it did not mean.
/// </para>
/// <para>
/// <b>The composition happens where the current version lives.</b> This parse runs
/// runner-side, which has no estate and cannot read the document being amended —
/// so it validates SHAPE only, and the control plane folds the result onto the
/// pinned envelope. A runner that could read the estate would be a runner that has
/// to be trusted with it.
/// </para>
/// </remarks>
public class ALearnedAmendmentIsReadOnItsOwnTests
{
    [Test]
    public async Task What_a_flight_learned_reads_without_a_governing_document()
    {
        var read = EnvelopeYaml.ParseLearning("""
            learned:
              against:
                commit: "a1b2c3d"
                repository: "JDX/JDNext"
              advice:
                - "node_modules is absent at checkout; npm install takes about fifty seconds."
                - "jsdom performs no layout, so a margin assertion proves nothing."
            """);

        await Assert.That(read.Diagnosis).IsNull()
            .Because($"nothing is wrong with it: {read.Diagnosis}");

        await Assert.That(read.Learned).IsNotNull();
        await Assert.That(read.Learned!.Advice).HasCount(2);
    }

    [Test]
    public async Task A_key_that_governs_is_refused_by_name()
    {
        // THE DOOR THIS CLOSES. Every one of these is a legitimate envelope key
        // and none of them is a flight's to write, so the refusal names the key
        // rather than describing a category.
        foreach (var governing in (string[])["context", "obligations", "loops", "destinations",
                                             "instructions", "accepts", "produces", "variables"])
        {
            var read = EnvelopeYaml.ParseLearning($"{governing}: {{}}\nlearned:\n  advice: [\"x\"]\n");

            await Assert.That(read.Diagnosis).IsNotNull()
                .Because($"'{governing}' governs, and a flight amends rather than governs.");

            await Assert.That(read.Diagnosis!).Contains(governing)
                .Because("naming the offending key is what a refusal is for.");
        }
    }

    [Test]
    public async Task Advice_says_it_wants_a_list_of_single_values()
    {
        // GG-330 attempts 7 and 8: a list of blocks and a bare scalar, told
        // "should be a single value" and "should be a list" respectively - each
        // true, neither complete, and together a contradiction an author bounces
        // between. `Strings` passed the PARENT path into RequireScalar for every
        // item, which is why both blamed 'learned.advice'.
        var blocks = EnvelopeYaml.ParseLearning(
            "learned:\n  advice:\n    - point: \"x\"\n      basis: \"y\"\n");

        var scalar = EnvelopeYaml.ParseLearning("learned:\n  advice: \"x\"\n");

        await Assert.That(blocks.Diagnosis).IsNotNull();
        await Assert.That(scalar.Diagnosis).IsNotNull();

        await Assert.That(blocks.Diagnosis).IsNotEqualTo(scalar.Diagnosis)
            .Because("two mistakes, two diagnoses.");

        foreach (var diagnosis in (string[])[blocks.Diagnosis!, scalar.Diagnosis!])
        {
            await Assert.That(diagnosis).Contains("list")
                .Because("the container has to be named.");
            await Assert.That(diagnosis).Contains("single value")
                .Because("and so do the items, which is what neither message said.");
        }
    }

    [Test]
    public async Task An_amendment_that_says_nothing_is_refused()
    {
        // AN EMPTY REHEARSAL HANDS NOTHING BACK, which is the kind's own
        // instruction nine - so an amendment carrying no advice is a call that
        // should not have been made, not an amendment that erases advice.
        var read = EnvelopeYaml.ParseLearning("learned:\n  against:\n    commit: \"a1b2c3d\"\n");

        await Assert.That(read.Diagnosis).IsNotNull()
            .Because("handing back an amendment with no advice in it is the 'invented "
                   + "something to have produced something' failure, spelled the other way.");
    }

    [Test]
    public async Task Nothing_but_learned_and_based_on_is_allowed_at_the_root()
    {
        // THE SURFACE, ASSERTED AS A SET rather than key by key, so a key added
        // later is a line in a diff beside this one.
        var read = EnvelopeYaml.ParseLearning("summary: \"x\"\nlearned:\n  advice: [\"y\"]\n");

        await Assert.That(read.Diagnosis).IsNotNull();
        await Assert.That(read.Diagnosis!).Contains("learned")
            .Because("GG-327 handed back 'summary'/'learned_against'/'advice' and GG-330 handed "
                   + "back 'learned_against' again, so the expected key belongs in the sentence.");
    }
}
