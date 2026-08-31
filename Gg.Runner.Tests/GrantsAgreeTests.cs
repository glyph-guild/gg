using Gg.Contracts;
using Gg.Runner.Vcs;

namespace Gg.Runner.Tests;

/// <summary>
/// The two grants describe the same landing, and a runner that finds they do
/// not refuses rather than choosing.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two gates, read independently, and that is deliberate.</b> The push is
/// granted when no machine obligation is violated; the proposal when every
/// requirement is satisfied. <c>RunnerLoop</c> records why they must stay
/// separate: <i>a runner that inferred a push from an admission — or a proposal
/// from a push — would be deciding one of them itself, and this is the one place
/// in this binary where that would pay.</i>
/// </para>
/// <para>
/// <b>So the branch is not read off the admission, and the unread members are
/// not fixed by reading them.</b> That was the obvious repair and it is the
/// wrong one: it collapses two independent grants into one. What the duplicated
/// members are actually good for is a <b>cross-check</b> — the two decisions
/// arrive separately and describe one landing, so a disagreement between them is
/// a real defect, and until now it was invisible.
/// </para>
/// <para>
/// <b>It cannot happen today</b>, because both are built from one flight number
/// in one receptor. It is defence in depth of the same kind as scoping a query
/// that a caller already scoped — and the cost is a comparison, against a runner
/// that would otherwise push one branch and propose another.
/// </para>
/// </remarks>
public class GrantsAgreeTests
{
    private static BranchPush Push(string branch = "gg/GG-42", string slug = "acme/widgets") =>
        new() { Branch = branch, BaseRef = "main", Slug = slug, Reason = "preserved" };

    private static DestinationAdmission Admission(
        string branch = "gg/GG-42", string slug = "acme/widgets", string baseRef = "main") =>
        new()
        {
            DestinationId = "forge",
            Branch = branch,
            BaseRef = baseRef,
            Slug = slug,
            Reason = "every requirement satisfied",
        };

    [Test]
    public async Task Two_grants_describing_one_landing_agree()
    {
        await Assert.That(LandingGrants.Disagreement(Push(), Admission())).IsNull()
            .Because("both are built from one flight number in one receptor, so the ordinary "
                   + "case is agreement and this check costs a comparison.");
    }

    [Test]
    public async Task A_different_branch_in_each_grant_is_named_rather_than_chosen_between()
    {
        var disagreement = LandingGrants.Disagreement(Push(), Admission(branch: "gg/GG-99"));

        await Assert.That(disagreement).IsNotNull();
        await Assert.That(disagreement!).Contains("gg/GG-42");
        await Assert.That(disagreement).Contains("gg/GG-99")
            .Because("naming both is the whole value: a runner that picked one would push one "
                   + "branch and propose another, and the record would show a landing.");
    }

    [Test]
    public async Task A_different_repository_or_base_is_named_too()
    {
        await Assert.That(LandingGrants.Disagreement(Push(), Admission(slug: "acme/other")))
            .IsNotNull();
        await Assert.That(LandingGrants.Disagreement(Push(), Admission(baseRef: "release")))
            .IsNotNull()
            .Because("a proposal opened against a base the push was not made from is a diff "
                   + "nobody asked for.");
    }

    [Test]
    public async Task An_absent_admission_is_not_a_disagreement()
    {
        // A push under a pending gate arrives with no admission at all - the
        // branch is preserved so a person has a commit to decide about, and
        // nothing is proposed. That is the ordinary state, not a conflict.
        await Assert.That(LandingGrants.Disagreement(Push(), admission: null)).IsNull();
    }
}
