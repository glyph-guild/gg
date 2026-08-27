using Gg.Contracts.Authoring;

namespace Gg.Contracts.Tests;

/// <summary>
/// The round trip that carries <c>scope: none</c> would notice it going
/// missing — proven by making it go missing.
/// </summary>
/// <remarks>
/// <para>
/// <b>A round-trip test passes when both sides drop the same field.</b> That
/// is not a hypothetical: <c>evidence:</c> was dropped by the emitter and the
/// parser together for three contract versions while a render-idempotence test
/// stayed green. So the assertion <i>the value survives</i> is worth exactly
/// as much as the proof that the check can fail.
/// </para>
/// <para>
/// <b>The two mutations are the two ways this value dies.</b> Dropped, which
/// is the <c>evidence:</c> failure; and rewritten to <c>"**"</c>, which is
/// worse, because a document that says <i>every path</i> where its author
/// wrote <i>no tree</i> is not broken — it is governing, wrongly, and nothing
/// about it looks wrong.
/// </para>
/// </remarks>
public class ScopeNonePoisonTwinTests
{
    private static Envelope Subjectless() => new()
    {
        Context = new ContextBinding { Scope = EnvelopeScopes.None, Constitution = "1.0.0" },
        Accepts = [],
        Obligations =
        [
            // A PREDICATE THAT READS NO PATH, deliberately. `no-file-outside-scope`
            // over `scope: none` is the pairing step 4 is about, and using it
            // here would make a round-trip test depend on a question this step
            // has not answered yet.
            new Obligation
            {
                Id = "loop-not-exhausted",
                Check = ObligationChecks.Machine,
                Rule = ObligationPredicates.LoopNotExhausted,
            },
        ],
        Loops =
        [
            new Loop
            {
                Id = "read-around",
                Executor = ExecutorRungs.Frontier,
                Discharges = ["loop-not-exhausted"],
                Moves = [LoopMoves.Read],
                Budget = new LoopBudget { WallClock = "30m" },
                OnExhaustion = ExhaustionPolicies.HandoffToHuman,
            },
        ],
        Destinations =
        [
            new Destination
            {
                Id = "forge",
                Kind = DestinationKinds.PullRequest,
                Requires = ["loop-not-exhausted"],
            },
        ],
    };

    /// <summary>The property under test, as one expression both halves can use.</summary>
    private static bool Survives(string text) =>
        EnvelopeYaml.Parse(text) is { Envelope: { } read }
        && string.Equals(read.Context.Scope, EnvelopeScopes.None, StringComparison.Ordinal);

    [Test]
    public async Task The_value_survives_the_round_trip()
    {
        var written = EnvelopeText.Render(Subjectless());

        // The diagnosis is asserted BEFORE the value, so a fixture that stops
        // parsing says why instead of reporting that the scope did not survive.
        await Assert.That(EnvelopeYaml.Parse(written).Diagnosis).IsNull()
            .Because($"the emitter's own output must parse. Wrote:\n{written}");
        await Assert.That(Survives(written)).IsTrue();
    }

    [Test]
    public async Task A_dropped_line_would_be_caught()
    {
        var poisoned = string.Join('\n', EnvelopeText.Render(Subjectless()).Split('\n')
            .Where(l => !l.TrimStart().StartsWith("scope:", StringComparison.Ordinal)));

        await Assert.That(Survives(poisoned)).IsFalse()
            .Because("this is the evidence: failure exactly - both sides dropping one field "
                   + "and a round-trip test staying green for three contract versions.");
    }

    [Test]
    public async Task A_value_rewritten_to_the_universal_glob_would_be_caught()
    {
        var poisoned = EnvelopeText.Render(Subjectless())
            .Replace($"scope: {EnvelopeScopes.None}", "scope: \"**\"", StringComparison.Ordinal);

        await Assert.That(poisoned).DoesNotContain($"scope: {EnvelopeScopes.None}")
            .Because("a twin that did not actually mutate anything proves nothing.");
        await Assert.That(Survives(poisoned)).IsFalse()
            .Because("this is the worse death: a document saying EVERY PATH where its author "
                   + "wrote NO TREE is not broken, it is governing wrongly, and nothing about "
                   + "it looks wrong.");
    }
}
