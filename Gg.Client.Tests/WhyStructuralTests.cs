using Gg.Client;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// <c>gg why</c> answers <i>this rule could never apply to this kind of work</i>
/// at the same words it answers everything else.
/// </summary>
/// <remarks>
/// <para>
/// <b>A decision that leaves no trace is indistinguishable from an
/// omission.</b> ADR-0020 § 2 marks a rule reading a fact family the work kind
/// cannot produce as inapplicable, at authoring, and never evaluates it. If
/// that rule then renders as nothing at all, a person reading their own
/// governance concludes it was never written — which is the failure this
/// vault keeps meeting, one surface over.
/// </para>
/// <para>
/// <b>A MEMBER, not a fourth attachment value, and the distinction is the
/// design.</b> Contracts' own rule is that a member may be added freely and a
/// value may not: the only safe response to an unknown value is to halt, so a
/// fourth <c>Attachments</c> value breaks every prior reader by design, for a
/// distinction ADR-0020 did not ask for. The attachment stays
/// <c>not-attached</c> — which is true, and is what the ADR says — and the
/// STRUCTURAL reason travels beside it as a nullable member no older reader
/// has to understand.
/// </para>
/// <para>
/// <b>It is not prose.</b> The reason is the fact family, in a field, so a
/// caller can tell structural inapplicability from an evaluated-false
/// condition by a null check rather than by reading a sentence.
/// </para>
/// </remarks>
public class WhyStructuralTests
{
    private static FlightAttribution Attributed(ObligationAttribution obligation) => new()
    {
        FlightNumber = "GG-42",
        EnvelopeVersion = "v1",
        Obligations = [obligation],
    };

    private static ObligationAttribution Structural() => new()
    {
        ObligationId = "in-scope",
        Attachment = Attachments.NotAttached,
        Condition = null,
        Inapplicable = FactKinds.ChangeManifest,
        Because = "this work kind accepts no subject, so change.manifest cannot exist",
    };

    [Test]
    public async Task The_member_carries_the_family_rather_than_a_sentence()
    {
        await Assert.That(ObligationAttribution.Validate(Structural())).IsNull();
        await Assert.That(Structural().Inapplicable).IsEqualTo(FactKinds.ChangeManifest)
            .Because("a caller tells structural inapplicability from an evaluated-false "
                   + "condition by a null check, not by reading a sentence.");
    }

    [Test]
    public async Task An_ordinary_attribution_carries_nothing_there()
    {
        // THE POISON TWIN. A member that is always set is a member that says
        // nothing, and every rule in the estate would read as inapplicable.
        //
        // IT CARRIES A CONDITION, and my first draft did not - which Validate
        // caught, correctly. An ordinary not-attached obligation is one whose
        // condition was read and did not hold, so it HAS one; a structural
        // answer is precisely the case that has none, which is why the rule
        // needed the `Inapplicable` arm rather than a relaxation.
        var ordinary = Structural() with
        {
            Inapplicable = null,
            Condition = AttachmentConditions.TouchesPrefix + "db/**",
            Because = "it did not touch db/**",
        };

        await Assert.That(ordinary.Inapplicable).IsNull();
        await Assert.That(ObligationAttribution.Validate(ordinary)).IsNull();
    }

    [Test]
    public async Task A_family_nothing_recognises_is_refused_where_it_is_written()
    {
        var invented = Structural() with { Inapplicable = "no.such.family" };

        var refusal = ObligationAttribution.Validate(invented);

        await Assert.That(refusal).IsNotNull()
            .Because("the member names a fact family, and a value outside the vocabulary would "
                   + "render as a reason nobody can check.");
        await Assert.That(refusal!).Contains("no.such.family");
    }

    [Test]
    public async Task Why_says_it_could_never_apply_rather_than_leaving_the_rule_out()
    {
        var rendered = VerbOutput.ToText(new VerbResult.Why(Attributed(Structural())));

        await Assert.That(rendered).Contains("in-scope")
            .Because("the rule is STATED. A plan that renders fewer obligations than the "
                   + "envelope carries is how a person concludes the rule was never written.");
        await Assert.That(rendered).Contains(FactKinds.ChangeManifest);
        await Assert.That(rendered).Contains("never")
            .Because("and it says the rule could NEVER apply, which is a different sentence "
                   + "from 'it did not apply this time'.");
    }

    [Test]
    public async Task An_evaluated_false_rule_does_not_borrow_that_sentence()
    {
        // The distinction on the surface, asserted from the other side. Both
        // are not-attached; only one of them could never have been otherwise.
        var rendered = VerbOutput.ToText(new VerbResult.Why(Attributed(
            Structural() with
            {
                Inapplicable = null,
                Condition = AttachmentConditions.TouchesPrefix + "db/**",
                Because = "it did not touch db/**",
            })));

        await Assert.That(rendered).Contains("in-scope");
        await Assert.That(rendered).DoesNotContain("never")
            .Because("a rule that was measured and did not fire may fire tomorrow, and telling "
                   + "somebody it never could is a lie about their own envelope.");
    }
}
