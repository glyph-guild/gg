using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// A sweep is handed its skill's <b>ref</b>, and reports the commit it read.
/// </summary>
/// <remarks>
/// <para>
/// <b>Rule 16 amended, decided 2026-09-17 by the owner.</b> The rule said the
/// control plane pins the commit and the runner reads the skill there. It now
/// says the runner does both: it resolves the watch's ref and reports what it
/// landed on. The owner's words were <i>"the runners should be doing the work
/// not the control plane"</i>, asked and confirmed against the alternative.
/// </para>
/// <para>
/// <b>What this gives up, so nobody rediscovers it as a bug.</b> The old
/// contract refused a moving ref in this slot, in these words: <i>"a branch or
/// a tag moves, and a pin that moves pins nothing"</i>. That is still true —
/// two sweeps of one decided action can now read different skills if the
/// branch moved in between. What replaces the guarantee is a RECORD: the
/// runner reports the commit it resolved beside the blob digest, so <i>what
/// ran</i> is answerable afterwards even though it is not fixed beforehand.
/// </para>
/// <para>
/// <b>What it buys.</b> The runner already holds the credential and the reach
/// — it clones the skill's repository either way, and <c>CloneOutcome</c>
/// already carries the commit it landed on, so nothing new computes it. The
/// control plane stops needing forge reach per tenant for this path, which is
/// what blocked the walk on a repository whose forge this system has no
/// binding shape for - and the refusal a person read named a different forge's
/// idea of an installation, which is worse than saying nothing.
/// </para>
/// <para>
/// <b>Rule 19 is untouched.</b> The control plane still resolves the watch's
/// repository NAME to a provider and a slug, because the registry lives there
/// and the machine that fetches holds none.
/// </para>
/// </remarks>
public class TheRunnerResolvesTheSkillsRefTests
{
    private static WatchAction AnAction(string? pinnedRef = null) =>
        ASweepIsAServedActionTests.AnAction() with
        {
            Skill = ASweepIsAServedActionTests.ASkill(pinnedRef ?? "refs/heads/main"),
        };

    [Test]
    public async Task A_sweep_is_handed_a_ref_rather_than_a_commit()
    {
        await Assert.That(WatchAction.Validate(AnAction())).IsNull()
            .Because("the runner resolves it, so a branch is what it is handed - the contract "
                   + "refused exactly this a version ago, and the refusal's argument is now "
                   + "carried by the reported commit instead.");
    }

    [Test]
    public async Task A_commit_is_still_a_ref_this_contract_accepts()
    {
        // NOT A WIDENING THAT BREAKS THE OLD SHAPE. A control plane that has
        // something better than a branch may still hand a commit over, and a
        // runner asked for one resolves it to itself.
        await Assert.That(WatchAction.Validate(AnAction(new string('a', 40)))).IsNull();
    }

    [Test]
    public async Task A_sweep_with_a_blank_ref_is_still_refused()
    {
        // THE REFUSAL NARROWS RATHER THAN GOING. A fetch keyed by nothing is
        // still a fetch of nothing, which is what the provider and slug arm
        // beside this one already says.
        var refused = WatchAction.Validate(AnAction("   "));

        await Assert.That(refused).IsNotNull();
        await Assert.That(refused!.Contains("ref", StringComparison.OrdinalIgnoreCase)).IsTrue();
    }

    [Test]
    public async Task A_good_pass_says_which_commit_it_read()
    {
        var unsaid = WatchAttestation.Validate(
            ASweepIsAServedActionTests.AnAttestation() with { SkillCommit = null });

        await Assert.That(unsaid).IsNotNull();
        await Assert.That(unsaid!.Contains("commit", StringComparison.OrdinalIgnoreCase)).IsTrue()
            .Because("the commit is the whole of what replaces the pin. A pass that read a "
                   + "skill and will not say from where leaves the review with a digest and "
                   + "nothing to resolve it against.");
    }

    [Test]
    public async Task The_commit_it_reports_has_to_be_one()
    {
        await Assert.That(WatchAttestation.Validate(
                ASweepIsAServedActionTests.AnAttestation() with { SkillCommit = "main" }))
            .IsNotNull()
            .Because("'main' is what it was asked to resolve, not what it resolved - reporting "
                   + "the question back is the one answer that looks like an answer and is "
                   + "not.");
    }

    [Test]
    public async Task An_unreachable_sweep_need_not_name_a_commit()
    {
        // IT MAY NEVER HAVE GOT THERE. The skill digest is optional on this
        // outcome for the same reason, one field over.
        var unreachable = ASweepIsAServedActionTests.AnAttestation() with
        {
            Outcome = WatchOutcomes.Unreachable,
            Nominated = [],
            SkillSha = null,
            SkillCommit = null,
            Diagnosis = "the tracker refused all 3 of this sweep's reads",
        };

        await Assert.That(WatchAttestation.Validate(unreachable)).IsNull();
    }

    [Test]
    public async Task The_reported_commit_is_on_the_wire()
    {
        await Assert.That(ProtocolSurface.JsonMembers[typeof(WatchAttestation)])
            .Contains("skillCommit")
            .Because("a member the runner fills and the wire drops is a record that exists on "
                   + "one machine, which is the same as no record at all.");
    }
}
