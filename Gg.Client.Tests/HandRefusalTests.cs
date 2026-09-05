using Gg.Client;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// A machine that cannot run the flight is told so before anything is created.
/// </summary>
/// <remarks>
/// <para>
/// <b>Rule 5, and the reason it is a rule.</b> A flight created and then
/// abandoned because this laptop was wrong is litter with a number on it — it
/// sits in the tenant's queue, it appears in <c>gg flights</c>, and somebody
/// has to decide what became of it. Refusing first costs nothing and leaves
/// nothing behind.
/// </para>
/// <para>
/// <b>The check the fleet does silently, said out loud.</b> A claim that does
/// not match is silence: the flight is simply not offered, and a person waiting
/// at a prompt cannot tell that from an idle fleet. A grant that does not match
/// can name the label — and the person standing in front of it is the one who
/// can bring the environment up.
/// </para>
/// <para>
/// <b>Priced against THIS machine, not the fleet.</b> <c>gg plan</c> answers
/// what a flight opened now would need and whether anybody can serve it; the
/// question here is narrower and different. A label some other runner
/// advertises is satisfied for the fleet and useless to a person at this
/// keyboard.
/// </para>
/// <para>
/// <b>Which is why the requirement is never composed here.</b> The label comes
/// off the checklist, which got it from the one compiler, control-plane-side —
/// and the sentence derives from a <see cref="Reason"/>'s KIND, so this and the
/// fleet's own refusal cannot describe one fleet differently. A client that
/// worked the label out from an environment name would be the second compiler,
/// one process further out.
/// </para>
/// </remarks>
public class HandRefusalTests
{
    private static Checklist Plan(params ChecklistItem[] items) => new()
    {
        EnvelopeVersion = "v1",
        RequiredLabels = [.. items.Select(i => i.Requirement)],
        Items = items,
    };

    private static ChecklistItem Needs(
        string requirement,
        string satisfier = ChecklistSatisfiers.MatchingRunner,
        string disposition = LabelDispositions.Stated) => new()
    {
        Requirement = requirement,
        Verification = "a runner advertises it",
        Satisfier = satisfier,
        Disposition = disposition,
    };

    // ---- S26.3-04 ----

    [Test]
    public async Task A_flight_requiring_nothing_flies_on_a_machine_advertising_nothing()
    {
        // THE ORDINARY CASE STAYS FRICTIONLESS. Most flights require no label at
        // all, and a check that made those people read a sentence would be a
        // tax on the common path to serve the rare one.
        await Assert.That(HandRefusal.For(Plan(), advertised: [])).IsNull();
    }

    [Test]
    public async Task A_machine_advertising_what_the_flight_needs_is_not_refused()
    {
        await Assert.That(HandRefusal.For(
            Plan(Needs("environment=aspire-payments")),
            advertised: ["environment=aspire-payments", "linux"]))
            .IsNull()
            .Because("containment, not equality - a machine with MORE than the flight asks for "
                   + "is eligible, exactly as it is to the fleet's own matcher.");
    }

    // ---- S26.3-02 ----

    [Test]
    public async Task The_refusal_names_the_label_and_is_a_reason_of_the_fleets_own_kind()
    {
        var refusal = HandRefusal.For(
            Plan(Needs("environment=aspire-payments")), advertised: []);

        await Assert.That(refusal).IsNotNull();

        // THE KIND, not a sentence. `Reason.Sentence` derives from it
        // contract-side, one grammar, so this refusal and the fleet's waiting
        // state cannot describe one fleet differently. A locally composed string
        // here is the drift this type exists to stop.
        await Assert.That(refusal!.Reason.Kind).IsEqualTo(ReasonKinds.NoRunnerAdvertises);
        await Assert.That(refusal.Reason.Params).Contains("environment=aspire-payments");

        // AND THE LABEL IS THE CHECKLIST'S OWN. It came off the one compiler,
        // control-plane-side; nothing here turned an environment name into a
        // label, because that mapping is not this side's to know.
        await Assert.That(refusal.Requirement).IsEqualTo("environment=aspire-payments");
    }

    // ---- S26.3-03 ----

    [Test]
    public async Task A_capability_gap_and_a_declined_bound_are_told_apart()
    {
        // THE REMEDIES DIFFER, WHICH IS THE WHOLE REASON TO CARRY IT. Nobody
        // having the environment means bring one up; a strategy declining it
        // means the fleet WOULD serve this and something chose not to - and a
        // person who brings a machine up against the second is doing work that
        // changes nothing.
        var gap = HandRefusal.For(
            Plan(Needs("environment=aspire-payments", ChecklistSatisfiers.Nobody)),
            advertised: []);

        var declined = HandRefusal.For(
            Plan(Needs("environment=aspire-payments", ChecklistSatisfiers.DeclinedByBound)),
            advertised: []);

        await Assert.That(gap!.Satisfier).IsEqualTo(ChecklistSatisfiers.Nobody);
        await Assert.That(declined!.Satisfier).IsEqualTo(ChecklistSatisfiers.DeclinedByBound);

        await Assert.That(gap.Remedy).IsNotEqualTo(declined.Remedy)
            .Because("two refusals that read identically send a person to do the wrong work.");
    }

    // ---- S26.3-05 ----

    [Test]
    public async Task The_disposition_travels_with_the_label()
    {
        // A `stated` REQUIREMENT IS NOT PRESENTED AS MEASURED. Nothing evaluates
        // an environment's registered meaning - `measured` today means somebody
        // registered a sentence, not that anything checked it - so a refusal
        // that implied this machine had been VERIFIED against an environment
        // would be citing a promise nothing keeps.
        var stated = HandRefusal.For(
            Plan(Needs("environment=aspire-payments", disposition: LabelDispositions.Stated)),
            advertised: []);

        await Assert.That(stated!.Disposition).IsEqualTo(LabelDispositions.Stated);

        await Assert.That(stated.Remedy.Contains("verified", StringComparison.OrdinalIgnoreCase))
            .IsFalse()
            .Because("`measured` is aspirational and `stated` is not even that - neither may be "
                   + "rendered as a check this platform performed.");
    }

    [Test]
    public async Task The_first_unmet_requirement_is_the_one_reported()
    {
        // ONE LABEL, NOT A LIST. A person brings up one environment at a time,
        // and a refusal naming three is three pieces of work presented as one
        // wall. The checklist is still there for the whole picture.
        var refusal = HandRefusal.For(
            Plan(
                Needs("environment=aspire-payments"),
                Needs("environment=zephyr")),
            advertised: ["environment=zephyr"]);

        await Assert.That(refusal!.Requirement).IsEqualTo("environment=aspire-payments")
            .Because("the one this machine actually lacks, not the first in the list.");
    }
}
