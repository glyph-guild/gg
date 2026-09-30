using Gg.Contracts;
using Gg.Contracts.Description;
using Gg.Runner.Environments;

namespace Gg.Runner.Tests;

/// <summary>
/// A kind whose stack script has landed performs it, rather than asking an agent
/// to work the bring-up out again.
/// </summary>
/// <remarks>
/// <para>
/// <b>Slice fifty-six step 6, and ADR-0023's own words for it:</b> <i>sweeps
/// should start off as agent instructions and crystalize into scripts, if they
/// can, that run on the runners.</i> A stack bring-up is the same shape — advice
/// an agent follows until the same advice has been followed enough times to be
/// worth writing down.
/// </para>
/// <para>
/// <b>The runner performs it, on the same terms as the agent it replaces.</b>
/// ADR-0023 § 4: same tool servers, same tier, same grant. Performing a script
/// is not a wider act than asking an agent to run the same commands — and it is
/// narrower in two ways, both in its favour: it reads no text as instructions,
/// so there is nothing to inject into, and it is deterministic.
/// </para>
/// <para>
/// <b>Where the reclaim and the tear-down already are.</b> <c>up</c> after the
/// instance is emptied and before the agent; <c>down</c> after the agent and in
/// place of the polite tear-down's guesswork, because the repository knows how
/// its own stack goes down and this side is guessing.
/// </para>
/// <para>
/// <b>A kind that names none still asks.</b> Every kind in the field names none,
/// and nothing about them changes: the agent works the bring-up out from advice,
/// which is where a kind stays until rule 10 is satisfied.
/// </para>
/// </remarks>
public class APerformedScriptIsNotRederivedTests
{
    [Test]
    public async Task The_lease_carries_the_script_its_kind_names()
    {
        await Assert.That(ProtocolSurface.JsonMembers[typeof(LeaseLoop)]).Contains("stack")
            .Because("the runner performs it and has no other way to learn the path: the "
                   + "envelope that names it is a document this side never receives.");
    }

    [Test]
    public async Task A_kind_that_names_one_is_performed_up_and_down()
    {
        await Assert.That(StackScript.Runs("scripts/stack.ps1")).IsTrue();
        await Assert.That(StackScript.ArgumentFor(StackScript.Up)).IsEqualTo("up");
        await Assert.That(StackScript.ArgumentFor(StackScript.Down)).IsEqualTo("down");
    }

    [Test]
    public async Task A_kind_that_names_none_still_asks_an_agent()
    {
        // EVERY KIND IN THE FIELD. Nothing about them changes - the agent works
        // the bring-up out from `learned:` advice, which is where a kind stays
        // until the advice has been used enough to be worth writing down.
        await Assert.That(StackScript.Runs(null)).IsFalse();
        await Assert.That(StackScript.Runs("   ")).IsFalse();
    }

    [Test]
    public async Task A_script_is_resolved_inside_the_tree_and_nowhere_else()
    {
        // THE DOCUMENT'S REFUSALS ARE NOT ENOUGH ON THEIR OWN. A document is
        // validated when it is applied; a lease arrives from a control plane
        // this binary does not control, so the path is bounded again here. Two
        // checks of one rule, deliberately - the far one is a contract and this
        // one is a boundary.
        var tree = Path.Combine(Path.GetTempPath(), "gg-stack-" + Guid.NewGuid().ToString("n"));

        await Assert.That(StackScript.Within(tree, "scripts/stack.ps1"))
            .IsEqualTo(Path.Combine(tree, "scripts", "stack.ps1"));

        foreach (var outside in (string[])["/etc/passwd", "../../etc/passwd", "scripts/../../x"])
        {
            await Assert.That(StackScript.Within(tree, outside)).IsNull()
                .Because($"'{outside}' resolves outside the checkout, and a runner that ran it "
                       + "would be executing something the repository does not contain.");
        }
    }

    [Test]
    public async Task A_script_that_is_not_there_is_not_a_reason_to_invent_one()
    {
        // A PATH THE DOCUMENT NAMES AND THE TREE DOES NOT HAVE. The document was
        // valid when it was applied and the repository has moved since, which is
        // ordinary - so this resolves to nothing rather than to a guess, and the
        // caller decides. Silently falling back to "ask an agent instead" would
        // make a kind that thinks it is deterministic quietly not be.
        var tree = Directory.CreateDirectory(Path.Combine(
            Path.GetTempPath(), "gg-stack-" + Guid.NewGuid().ToString("n"))).FullName;

        await Assert.That(StackScript.Within(tree, "scripts/stack.ps1")).IsNotNull()
            .Because("resolving a path is not the same as finding a file - the caller checks "
                   + "existence, and this answers only where it would be.");

        Directory.Delete(tree, recursive: true);
    }
}
