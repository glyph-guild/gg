using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// The canonical form is a function of what an envelope says.
/// </summary>
/// <remarks>
/// <para>
/// <b>The criterion this replaces was passing for the wrong reason.</b> Slice two
/// asserted that <c>gg envelope show</c> twice gives identical bytes. At one
/// obligation that holds whatever order the emitter uses, because there is only
/// one order - so it could not distinguish an emitter with an ordering rule from
/// an emitter with none. The emitter had none: it iterated the collection.
/// </para>
/// <para>
/// <b>By id, ordinal.</b> Sorted rather than authored, so two envelopes declaring
/// the same rules produce the same bytes and therefore the same version. A version
/// derived from these bytes has to mean the rules changed, not that two lines were
/// swapped in a text editor.
/// </para>
/// </remarks>
public class EnvelopeCanonicalOrderTests
{
    private static Obligation Named(string id, string rule) =>
        new() { Id = id, Check = ObligationChecks.Machine, Rule = rule };

    private static Envelope With(params Obligation[] obligations) => new()
    {
        Context = new ContextBinding { Scope = "src/**", Constitution = "1.0.0" },
        Obligations = obligations,
        Loops =
        [
            new Loop
            {
                Id = "implement",
                Executor = ExecutorRungs.Frontier,
                Discharges = [.. obligations.Select(o => o.Id)],
                Moves = [LoopMoves.Read, LoopMoves.Edit],
                Budget = new LoopBudget { WallClock = "30m" },
                OnExhaustion = ExhaustionPolicies.HandoffToHuman,
            },
        ],
        Destinations =
        [
            new Destination
            {
                Id = "pull-request",
                Kind = DestinationKinds.PullRequest,
                Requires = [.. obligations.Select(o => o.Id)],
            },
        ],
    };

    private static readonly Obligation InScope =
        Named("in-scope", ObligationPredicates.NoFileOutsideScope);

    private static readonly Obligation NotExhausted =
        Named("not-exhausted", ObligationPredicates.LoopNotExhausted);

    [Test]
    public async Task Two_envelopes_differing_only_in_authored_order_emit_identical_bytes()
    {
        // THE CRITERION THAT REPLACES THE ONE THAT COULD NOT FAIL. Same rules,
        // typed in the other order, and the canonical text is the same text -
        // which is what canonical has to mean when a version comes from it.
        var authored = EnvelopeText.Render(With(InScope, NotExhausted));
        var reversed = EnvelopeText.Render(With(NotExhausted, InScope));

        await Assert.That(reversed).IsEqualTo(authored);
    }

    [Test]
    public async Task The_declared_order_is_by_id_and_the_bytes_show_it()
    {
        // Stated, not implied. A reader of the canonical text can predict the
        // order without reading the emitter, which is the difference between a
        // rule and an accident.
        var text = EnvelopeText.Render(
            With(Named("zzz-last", ObligationPredicates.LoopNotExhausted),
                 Named("aaa-first", ObligationPredicates.NoFileOutsideScope)));

        await Assert.That(text.IndexOf("aaa-first", StringComparison.Ordinal))
            .IsLessThan(text.IndexOf("zzz-last", StringComparison.Ordinal));
    }

    [Test]
    public async Task The_same_envelope_twice_still_gives_identical_bytes()
    {
        // Slice two's assertion, kept. It is necessary and it was never
        // sufficient, and keeping it says which.
        var envelope = With(InScope, NotExhausted);

        await Assert.That(EnvelopeText.Render(envelope))
            .IsEqualTo(EnvelopeText.Render(envelope));
    }

    [Test]
    public async Task The_ordering_is_observable_at_two_and_was_not_at_one()
    {
        // Why the old criterion could not fail, asserted so nobody re-derives a
        // one-obligation version of it. At one obligation, reversing the
        // collection is the same collection.
        var one = With(InScope);

        await Assert.That(EnvelopeText.Render(one))
            .IsEqualTo(EnvelopeText.Render(With([.. one.Obligations.Reverse()])))
            .Because("a single obligation emits identically under every ordering rule and under "
                   + "none, which is exactly why the old criterion proved nothing.");

        // And at two it is observable: an emitter with no rule would produce
        // different bytes for the two authorings above.
        // Joined, because equivalence over a collection ignores order and this
        // assertion is entirely about order.
        await Assert.That(string.Join(",", With(InScope, NotExhausted).Obligations.Select(o => o.Id)))
            .IsNotEqualTo(
                string.Join(",", With(NotExhausted, InScope).Obligations.Select(o => o.Id)))
            .Because("the two inputs really are in different orders, or the test above compares "
                   + "one thing with itself.");
    }

    [Test]
    public async Task Two_envelopes_that_differ_in_a_rule_do_not_emit_identical_bytes()
    {
        // The positive control on the whole idea. An emitter that returned a
        // constant would satisfy every assertion above.
        var one = EnvelopeText.Render(With(InScope, NotExhausted));
        var other = EnvelopeText.Render(
            With(InScope, Named("not-exhausted", ObligationPredicates.NoFileOutsideScope)));

        await Assert.That(other).IsNotEqualTo(one);
    }
}
