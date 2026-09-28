using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// A work kind's <c>hosts:</c> reaches the composed envelope, and a tree that
/// names none is unchanged by the member existing.
/// </summary>
/// <remarks>
/// <para>
/// <b>Because the composer is hand-written and drops what it does not name.</b>
/// <c>Compose</c> ends in <c>baseDocument with { … }</c> listing eight members,
/// and everything else survives only by riding the record it was copied from.
/// That is the right behaviour for a work-kind-only member — <c>baseDocument</c>
/// is <c>workKind ?? root</c> — but it is behaviour nothing else asserts, and a
/// member that quietly stopped arriving would fail no other test in this
/// assembly. <c>[Composes]</c> is documentation; two layers is the smallest
/// thing that can actually fail.
/// </para>
/// <para>
/// <b>The second test is the one that would catch a regression.</b> Every
/// envelope in every tenant today names no host, and composition must not start
/// inventing one — an empty list arriving where null was is how "said nothing"
/// becomes "said none" for every document at once.
/// </para>
/// <para>
/// <b>What this deliberately does NOT assert: a refusal on root.</b> No
/// work-kind-only member is refused on the floor today —
/// <see cref="Envelope.Brief"/>, <see cref="Envelope.Description"/> and
/// <see cref="Envelope.Targeting"/> are all simply taken from
/// <c>baseDocument</c>, so a floor value applies when no work kind overrides it.
/// <c>hosts:</c> behaves exactly as those three do. Giving this one member a
/// gate the other three lack would be a difference nobody decided; giving all
/// four a gate is a change worth making on purpose and is not this.
/// </para>
/// </remarks>
public class AHostedStackSurvivesCompositionTests
{
    private static Envelope Document(IReadOnlyList<string>? hosts = null, string duty = "in-scope") => new()
    {
        Context = new ContextBinding { Scope = "src/**", Constitution = "1.0.0" },
        Hosts = hosts,
        Obligations =
        [
            new Obligation
            {
                Id = duty,
                Check = ObligationChecks.Machine,
                Rule = ObligationPredicates.NoFileOutsideScope,
            },
        ],
        Loops =
        [
            new Loop
            {
                Id = "preview",
                Executor = ExecutorRungs.Frontier,
                Discharges = [duty],
                Moves = [LoopMoves.Read],
                Budget = new LoopBudget { WallClock = "30m" },
                OnExhaustion = ExhaustionPolicies.HandoffToHuman,
            },
        ],
        Destinations =
        [
            new Destination
            {
                Id = "pull-request",
                Kind = DestinationKinds.PullRequest,
                Requires = [duty],
            },
        ],
    };

    private static EnvelopeLayer Root() => new()
    {
        Role = Roles.Root,
        Name = "root",
        Parent = null,
        Document = Document(duty: "floor-scope"),
        Version = "v1",
    };

    private static EnvelopeLayer WorkKind(Envelope document) => new()
    {
        Role = Roles.WorkKind,
        Name = "ui-preview",
        Parent = "root",
        Document = document,
        Version = "v1",
    };

    [Test]
    public async Task The_kinds_host_reaches_the_composed_envelope()
    {
        var composition = EnvelopeComposition.Compose([Root(), WorkKind(Document(["ui"]))]);

        await Assert.That(composition.Refused).IsNull();

        await Assert.That(composition.Composed!.Hosts).IsEquivalentTo(["ui"])
            .Because("the composer names eight members explicitly and everything else survives "
                   + "only by riding baseDocument, so a member that stopped arriving would fail "
                   + "nothing else in this assembly.");
    }

    [Test]
    public async Task A_tree_that_names_no_host_composes_to_none_rather_than_to_empty()
    {
        var composition = EnvelopeComposition.Compose([Root(), WorkKind(Document())]);

        await Assert.That(composition.Refused).IsNull();

        await Assert.That(composition.Composed!.Hosts).IsNull()
            .Because("every envelope in every tenant today names no host, and an empty list "
                   + "arriving where null was would turn 'said nothing' into 'said none' for "
                   + "all of them at once.");
    }

    [Test]
    public async Task A_narrowing_does_not_move_it()
    {
        // WORK-KIND-ONLY MEANS THE KIND'S ANSWER STANDS. A narrowing tightens a
        // job already named; it has no say in where that job's stack runs, and
        // the composed value must still be the kind's.
        var composition = EnvelopeComposition.Compose(
        [
            Root(),
            WorkKind(Document(["ui"])),
            new EnvelopeLayer
            {
                Role = Roles.Narrowing,
                Name = "tighter",
                Parent = "ui-preview",
                Narrowing = new EnvelopeNarrowing
                {
                    Obligations =
                    [
                        new Obligation
                        {
                            Id = "reviewed",
                            Check = ObligationChecks.Human,
                            Approver = "lead",
                        },
                    ],
                },
                Version = "v1",
            },
        ]);

        await Assert.That(composition.Refused).IsNull();

        await Assert.That(composition.Composed!.Hosts).IsEquivalentTo(["ui"]);
    }
}
