using Gg.Contracts;

namespace Gg.Runner.Vcs;

/// <summary>
/// Whether the two grants a landing needs describe the same landing.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two gates, read independently, and this does not join them.</b> The push
/// is granted when no machine obligation is violated; the proposal when every
/// requirement is satisfied. <c>RunnerLoop</c> records why they stay separate —
/// <i>a runner that inferred a push from an admission, or a proposal from a
/// push, would be deciding one of them itself</i> — and reading the branch off
/// whichever arrived is exactly that inference.
/// </para>
/// <para>
/// <b>So this compares rather than chooses.</b> The two decisions arrive
/// separately and describe one landing; a disagreement is a defect in whatever
/// produced them, and the honest response is to name both values and land
/// nothing. Picking one would push a branch and propose a different one, and the
/// record would show a landing.
/// </para>
/// <para>
/// <b>It cannot fire today</b>, because both grants are built from one flight
/// number in one receptor. That is the same standing as scoping a query a caller
/// already scoped: the cost is a comparison, and the alternative is trusting
/// two decisions to agree because they always have.
/// </para>
/// </remarks>
public static class LandingGrants
{
    /// <summary>
    /// What the two grants disagree about, or null when they describe one landing.
    /// </summary>
    /// <remarks>
    /// A null admission is not a disagreement: a push under a pending gate
    /// preserves the branch so a person has a commit to decide about, and
    /// proposes nothing.
    /// </remarks>
    public static string? Disagreement(BranchPush push, DestinationAdmission? admission)
    {
        ArgumentNullException.ThrowIfNull(push);

        if (admission is null)
        {
            return null;
        }

        var differences = new List<string>();

        Compare(differences, "branch", push.Branch, admission.Branch);
        Compare(differences, "repository", push.Slug, admission.Slug);
        Compare(differences, "base", push.BaseRef, admission.BaseRef);

        return differences.Count == 0
            ? null
            : "The push grant and the destination admission describe different landings: "
            + string.Join("; ", differences)
            + ". Both are decisions of the control plane about one flight, so this runner "
            + "lands nothing rather than choosing between them - pushing one branch and "
            + "proposing another would be recorded as a landing.";
    }

    private static void Compare(List<string> differences, string what, string pushed, string admitted)
    {
        if (!string.Equals(pushed, admitted, StringComparison.Ordinal))
        {
            // BOTH VALUES, because "the grants disagree" sends somebody to read
            // two documents and a named pair sends them to one line.
            differences.Add($"the push names {what} '{pushed}' and the admission names '{admitted}'");
        }
    }
}
