using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// What the control plane hands back when a tracker destination is admitted,
/// and what its absence means.
/// </summary>
/// <remarks>
/// <para>
/// <b>S35.5-01, the contract half.</b> Absent means no. A facts response with no
/// tracker admission writes nothing, and the runner never derives one from a
/// verdict it can see — the rule <see cref="LandingDecision.Admission"/> and
/// <see cref="LandingDecision.Push"/> already hold for the repository path, where
/// their own remark says "two permissions, two fields, each refused by its own
/// absence".
/// </para>
/// <para>
/// <b>A sibling rather than a widening, and that is the whole design decision.</b>
/// <see cref="DestinationAdmission"/> requires a branch, a base ref and a slug.
/// A tracker has none of the three, so carrying a tracker on it would mean
/// making three required members optional — which would silently weaken the
/// repository path, where their being required is what stops a runner pushing
/// somewhere nobody named.
/// </para>
/// <para>
/// <b>Each proposal, not the batch.</b> A triage ships one fact per change so a
/// person can take the re-field and refuse the link. An admission that said
/// only <i>yes</i> would hand the runner a decision nobody made, so what comes
/// back is the set of proposals admitted — named by the idempotency key their
/// facts already carry, because inventing a second identity for something that
/// has one is how two identifiers drift.
/// </para>
/// </remarks>
public class ATrackerWriteIsAdmittedTests
{
    [Test]
    public async Task Absent_means_nothing_is_written()
    {
        var settled = new LandingDecision { Settled = true };

        await Assert.That(settled.Tracker).IsNull()
            .Because("a response that says nothing about a tracker is a response that "
                   + "admits nothing to one, for every reason at once: no destination "
                   + "declared, obligations unmet, or a control plane too old to answer.");
    }

    [Test]
    public async Task It_names_each_proposal_rather_than_saying_yes()
    {
        var admitted = new TrackerAdmission
        {
            DestinationId = "backlog",
            Reason = "two of the three were admitted; the link was not",
            Proposals = ["01a0776a-cacb-76dc", "01a0776a-cacb-76dd"],
        };

        await Assert.That(TrackerAdmission.Validate(admitted)).IsNull();
        await Assert.That(admitted.Proposals.Count).IsEqualTo(2)
            .Because("an admission that said only yes would hand the runner a decision "
                   + "nobody made - the third proposal was refused, and the runner has to "
                   + "be able to tell which.");
    }

    [Test]
    public async Task An_admission_that_names_no_proposal_is_refused()
    {
        // NOT AN EMPTY LIST MEANING NOTHING. A destination that admitted none of
        // them is an ABSENT tracker admission, which the first test pins; an
        // admission object naming nothing is a control plane that decided and
        // then failed to say what, and a runner cannot tell that from "all of
        // them" without guessing.
        var empty = new TrackerAdmission
        {
            DestinationId = "backlog",
            Reason = "admitted",
            Proposals = [],
        };

        await Assert.That(TrackerAdmission.Validate(empty)).IsNotNull()
            .Because("absence is how nothing is said; an empty list is a sentence with the "
                   + "subject missing.");
    }

    [Test]
    public async Task It_is_registered_the_way_every_wire_type_is()
    {
        // The four-way rule, which this inherits rather than restates: a type on
        // the wire carries a pinned id and appears in the vocabulary, and
        // ContractSurfaceTests fingerprints its shape. Named here only so a
        // reader of this file knows the registration is not optional.
        await Assert.That(Vocabulary.Types).Contains(typeof(TrackerAdmission));
    }
}
