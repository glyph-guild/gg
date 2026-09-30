using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// A work kind may name the script that brings its stack up and down, as a path
/// in the repository the flight checks out.
/// </summary>
/// <remarks>
/// <para>
/// <b>Slice fifty-six step 6, and ADR-0023's shape at a second subject.</b> A
/// watch names a skill and, later or never, a script — both as paths in the
/// customer's repository, read at a pinned ref, with the control plane pinning
/// the commit and the runner reading the file. This is that, for the thing only
/// a repository knows: how its own stack comes up.
/// </para>
/// <para>
/// <b>One path, with the verb as an argument.</b> Bring-up and tear-down are two
/// halves of one procedure and the repository knows both; two members would be
/// two things to keep in sync, and a kind that named one without the other would
/// be half a procedure nobody could complete.
/// </para>
/// <para>
/// <b>A path inside the checkout, and nothing else.</b> An absolute path is a
/// file on the pool host rather than in the repository, and a path that climbs
/// out reaches the same place by another route — both would be a document
/// choosing what the runner executes on a machine it does not own.
/// </para>
/// <para>
/// <b>And it is refused without <c>hosts:</c></b>, because a script for a stack
/// that never runs is a file nobody calls — the declared-and-unread shape this
/// slice family has now produced three times, caught at the document this time
/// rather than in the field.
/// </para>
/// </remarks>
public class AWorkKindNamesItsStackScriptTests
{
    private static Envelope AKind(string? stack, IReadOnlyList<string>? hosts) => new()
    {
        Context = new ContextBinding { Scope = "src/**", Constitution = "1.0.0" },
        Obligations =
        [
            new Obligation
            {
                Id = "scope-respected",
                Check = ObligationChecks.Machine,
                Rule = ObligationPredicates.NoFileOutsideScope,
            },
        ],
        Loops =
        [
            new Loop
            {
                Id = "implement",
                Executor = ExecutorRungs.Frontier,
                Discharges = ["scope-respected"],
                Moves = [LoopMoves.Read, LoopMoves.Edit],
                Budget = new LoopBudget { WallClock = "30m" },
                OnExhaustion = ExhaustionPolicies.HandoffToHuman,
            },
        ],
        Destinations =
        [
            new Destination
            {
                Id = "forge",
                Kind = DestinationKinds.PullRequest,
                Requires = ["scope-respected"],
            },
        ],
        Hosts = hosts,
        Stack = stack,
    };

    [Test]
    public async Task A_hosted_kind_may_name_one()
    {
        await Assert.That(Envelope.Validate(AKind("scripts/stack.ps1", ["ui"]))).IsNull();
    }

    [Test]
    public async Task A_kind_that_names_none_is_unchanged()
    {
        // EVERY KIND IN THE FIELD. The member is optional and stays optional:
        // absence means the agent works the bring-up out from advice, which is
        // what happens today.
        await Assert.That(Envelope.Validate(AKind(stack: null, hosts: null))).IsNull();
        await Assert.That(AKind(null, null).Stack).IsNull();
    }

    [Test]
    public async Task A_script_for_a_stack_that_never_runs_is_refused()
    {
        // THE DECLARED-AND-UNREAD SHAPE, caught at the document. A kind with no
        // `hosts:` is never granted an instance, so nothing would ever perform
        // this script - and a file nobody calls reads exactly like one that
        // works.
        await Assert.That(Envelope.Validate(AKind("scripts/stack.ps1", hosts: null)))
            .IsNotNull();
    }

    [Test]
    public async Task An_absolute_path_is_refused()
    {
        // A file on the POOL HOST rather than in the repository. The whole point
        // is that the repository knows how its stack comes up; a path outside it
        // is a document choosing what the runner executes on a machine it does
        // not own.
        foreach (var outside in (string[])["/usr/local/bin/stack", "C:\\stack.ps1"])
        {
            await Assert.That(Envelope.Validate(AKind(outside, ["ui"]))).IsNotNull()
                .Because($"'{outside}' is not in the checkout.");
        }
    }

    [Test]
    public async Task A_path_that_climbs_out_is_refused()
    {
        await Assert.That(Envelope.Validate(AKind("../../etc/profile", ["ui"]))).IsNotNull()
            .Because("it reaches the host by another route, and a check that refused only "
                   + "absolute paths would be one somebody walks around in an afternoon.");
    }

    [Test]
    public async Task It_travels_on_the_wire()
    {
        await Assert.That(ProtocolSurface.JsonMembers[typeof(Envelope)]).Contains("stack");
    }

    [Test]
    public async Task It_is_the_work_kinds_and_not_the_roots()
    {
        // ROOT IS NOT ANY KIND, so a stack script there would be a procedure for
        // every flight the tenant flies - which is Brief's reason, one member up.
        var composes = typeof(Envelope).GetProperty(nameof(Envelope.Stack))!
            .GetCustomAttributes(typeof(ComposesAttribute), inherit: false)
            .Cast<ComposesAttribute>()
            .Single();

        await Assert.That(composes.Operator).IsEqualTo(MergeOperators.WorkKindOnly);
    }
}
