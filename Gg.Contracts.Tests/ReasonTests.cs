using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// A refusal or a wait crosses the wire as a KIND with params, and the
/// sentence a person reads is derived from it - one grammar, contract-side,
/// so no surface can reword what another surface asserted.
/// </summary>
/// <remarks>
/// <para>
/// <b>Slice eight's prose retires.</b> The waiting sentence was a string
/// built control-plane-side and pattern-matched by clients and scripts -
/// wording as protocol, undeclared. A Reason is the declaration: the kind is
/// closed, the params are the facts, and the sentence is derived where both
/// sides can see the grammar.
/// </para>
/// <para>
/// <b>An unknown kind poisons, never blanks.</b> A renderer that shrugs at a
/// kind it does not know turns a governed refusal into silence - Article XI's
/// exact shape. Sentence and FamilyOf throw, so the gap fails somebody's
/// build instead of somebody's audit.
/// </para>
/// </remarks>
public class ReasonTests
{
    [Test]
    public async Task The_three_families_are_declared_and_every_kind_names_its_family()
    {
        await Assert.That(ReasonFamilies.All)
            .IsEquivalentTo((string[])["declined", "failed", "refused"]);

        await Assert.That(ReasonKinds.FamilyOf(ReasonKinds.NoRunnerAdvertises))
            .IsEqualTo(ReasonFamilies.Failed)
            .Because("nobody said no: the fleet simply cannot satisfy it right now.");
        foreach (var refusal in (string[])
            [ReasonKinds.CannotBeShownToTighten, ReasonKinds.WideningRequiresAGate,
             ReasonKinds.Uncharted, ReasonKinds.RegistrationIsAWidening])
        {
            await Assert.That(ReasonKinds.FamilyOf(refusal)).IsEqualTo(ReasonFamilies.Refused);
        }
    }

    [Test]
    public async Task The_waiting_sentence_is_derived_and_byte_compatible()
    {
        var sentence = Reason.Sentence(
            ReasonKinds.NoRunnerAdvertises, ["environment=aspire-payments"]);

        await Assert.That(sentence)
            .IsEqualTo("waiting: no runner advertises environment=aspire-payments")
            .Because("the wording every surface and script already asserts - the kind "
                   + "arrives under the sentence, not instead of it.");

        await Assert.That(Reason.Sentence(ReasonKinds.NoRunnerAdvertises, ["a=1", "b=2"]))
            .IsEqualTo("waiting: no runner advertises a=1, b=2");
    }

    [Test]
    public async Task Every_refusal_kind_derives_a_sentence_naming_its_params_and_the_fix()
    {
        var ungated = Reason.Sentence(ReasonKinds.WideningRequiresAGate, ["context.scope"]);
        await Assert.That(ungated).Contains("context.scope");
        await Assert.That(ungated).Contains(AttachmentConditions.Widens)
            .Because("the refusal names the obligation form that is missing.");
        await Assert.That(ungated.ToLowerInvariant()).Contains("tighten")
            .Because("declaring the gate is itself a tightening on the owner's say-so.");

        var floor = Reason.Sentence(ReasonKinds.RegistrationIsAWidening, ["airspace.repositories"]);
        await Assert.That(floor).Contains("airspace.repositories");
        await Assert.That(floor).Contains("/v1/envelope")
            .Because("the refusal points at the door that fixes it: the floor first.");

        var uncharted = Reason.Sentence(ReasonKinds.Uncharted, ["never-charted"]);
        await Assert.That(uncharted).Contains("never-charted");
        await Assert.That(uncharted).Contains("charted")
            .Because("the fix is named: the chart is where names become selectable.");

        var incomparable = Reason.Sentence(ReasonKinds.CannotBeShownToTighten, ["obligations.widen-root.approver"]);
        await Assert.That(incomparable).Contains("obligations.widen-root.approver");
        await Assert.That(incomparable.ToLowerInvariant()).Contains("widening")
            .Because("what cannot be shown to tighten is treated as widening - Article XI.");
    }

    [Test]
    public async Task An_unknown_kind_poisons_rather_than_blanking()
    {
        await Assert.That(() => Reason.Sentence("kind-nobody-declared", []))
            .Throws<InvalidOperationException>()
            .Because("a renderer that shrugs turns a governed refusal into silence.");
        await Assert.That(() => ReasonKinds.FamilyOf("kind-nobody-declared"))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task A_reason_whose_family_disagrees_with_its_kind_is_refused()
    {
        var lying = new Reason
        {
            Family = ReasonFamilies.Refused,
            Kind = ReasonKinds.NoRunnerAdvertises,
            Params = ["environment=x"],
        };

        await Assert.That(Reason.Validate(lying)).IsNotNull()
            .Because("the family is derivable from the kind; a stored disagreement is a "
                   + "reader-facing lie one of them must be telling.");
        await Assert.That(Reason.Validate(Reason.For(ReasonKinds.NoRunnerAdvertises, ["environment=x"])))
            .IsNull()
            .Because("For derives the family, so a reason built the intended way validates.");
    }

    [Test]
    public async Task The_reason_is_pinned_in_the_vocabulary_and_rides_the_two_waiting_members()
    {
        await Assert.That(Vocabulary.Types).Contains(typeof(Reason));
        await Assert.That(ProtocolSurface.JsonMembers[typeof(Reason)])
            .IsEquivalentTo((string[])["family", "kind", "params"]);

        await Assert.That(typeof(FlightSummary).GetProperty("Waiting")!.PropertyType)
            .IsEqualTo(typeof(Reason))
            .Because("the wire NAME survives and the type changes - a loud break, chosen "
                   + "over the silent-health flip a rename would cause in old readers.");
        await Assert.That(typeof(ChecklistItem).GetProperty("WhenUnmet")!.PropertyType)
            .IsEqualTo(typeof(Reason));
    }
}
