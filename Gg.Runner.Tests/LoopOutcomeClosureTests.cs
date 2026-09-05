using Gg.Contracts;

namespace Gg.Runner.Tests;

/// <summary>
/// A runner too old to know an outcome halts on it, rather than treating it as
/// nothing to do.
/// </summary>
/// <remarks>
/// <para>
/// <b>The closure working, and the trade written down where the trade is
/// made.</b> `LoopOutcomes` is a closed fact vocabulary, so a fourth value is a
/// version move with a designed blast radius: a reader built before it halts.
/// `ExhaustionPolicies.HandoffToAgent` records exactly this for exactly this
/// reason - <i>"a value in a closed enumeration, so the only safe response to
/// it in a prior reader is to halt."</i>
/// </para>
/// <para>
/// <b>The direction of failure is what makes it worth a test.</b> An unknown
/// outcome read as <c>completed</c> would report a flight nobody worked as
/// finished; read as <c>failed</c> it would send an impasse to whoever handles
/// crashes. Refusing is the only answer that is not a lie, and this is the same
/// shape as the claim-state closure that slice twenty-four's walk found
/// missing after a parked runner was killed by its absence.
/// </para>
/// </remarks>
public class LoopOutcomeClosureTests
{
    [Test]
    public async Task Every_declared_outcome_is_one_a_digest_accepts()
    {
        // The sweep, over the vocabulary rather than over a list beside it, so
        // a fifth value inherits this the day it lands.
        foreach (var outcome in LoopOutcomes.All)
        {
            await Assert.That(LoopDigest.Validate(Digest(outcome))).IsNull()
                .Because($"'{outcome}' is declared, so a digest carrying it is well formed. "
                       + "A value in All that the validator refuses is a vocabulary and a "
                       + "reader that disagree, which is the drift this sweep exists for.");
        }
    }

    [Test]
    public async Task An_outcome_nobody_declared_is_refused_naming_what_is_expected()
    {
        var refusal = LoopDigest.Validate(Digest("gave-up"));

        await Assert.That(refusal).IsNotNull()
            .Because("an unknown outcome read as completed reports a flight nobody worked as "
                   + "finished, and read as failed it sends an impasse to whoever handles "
                   + "crashes. Refusing is the only answer that is not a lie.");

        foreach (var declared in LoopOutcomes.All)
        {
            await Assert.That(refusal!).Contains(declared)
                .Because("the refusal names what it expected, so a person meeting a newer "
                       + "control plane learns which build they are behind rather than "
                       + "reading a bare rejection.");
        }
    }

    [Test]
    public async Task Blocked_joined_the_vocabulary_rather_than_replacing_anything()
    {
        // A closed vocabulary GROWS; it does not get rewritten. An outcome
        // removed here would make every prior fact carrying it unreadable, and
        // the three that were there are still there.
        await Assert.That(LoopOutcomes.All).Contains(LoopOutcomes.Completed);
        await Assert.That(LoopOutcomes.All).Contains(LoopOutcomes.Failed);
        await Assert.That(LoopOutcomes.All).Contains(LoopOutcomes.Exhausted);
        await Assert.That(LoopOutcomes.All).Contains(LoopOutcomes.Blocked);
        await Assert.That(LoopOutcomes.All.Count).IsEqualTo(4)
            .Because("four, and a fifth is a version move rather than an edit. If this "
                   + "number changed, a fact vocabulary moved and the ledger says so.");
    }

    private static LoopDigest Digest(string outcome) => new()
    {
        LoopId = "implement",
        StopReason = outcome,
        FilesEdited = [],
        FilesReadNotEdited = [],
        Searches = [],
        Errors = [],
        RefusedMoves = [],
        Attempts = 1,
    };
}
