using Gg.Contracts;
using Gg.Contracts.Authoring;

namespace Gg.Contracts.Tests;

/// <summary>
/// A work kind can say where its STACK runs, separately from where its loop
/// runs.
/// </summary>
/// <remarks>
/// <para>
/// <b><c>ui-preview</c> has never once produced the fact it exists to
/// produce.</b> GG-309 asked for <c>environment=dev</c>, got a dev worker with
/// no browser and no toolchain, and shipped no <c>preview.url</c>; two
/// rehearsals after it asked for nothing at all and landed on the same machine.
/// The kind could not say what it needed, because nothing in an envelope says
/// it.
/// </para>
/// <para>
/// <b>And <c>environments:</c> is not that thing, which is the correction this
/// test is built on.</b> It carries <c>[Composes(MergeOperators.RootOnly)]</c>
/// and is a BOUND — the set a tenant charts — while the singular
/// <c>environment:</c> is a legacy wire spelling with no operator at all,
/// parsed for compatibility and never rendered. A flight's environment is what
/// <c>gg fly --environment</c> names, or the bound's only member when it holds
/// one. None of that is a work kind saying anything, and none of it changes
/// here.
/// </para>
/// <para>
/// <b>WORK-KIND-ONLY, for <see cref="Envelope.Brief"/>'s reason.</b> Root is not
/// any one job, and a narrowing narrows a job already named. A <c>hosts:</c>
/// that composed would either give every kind the floor's stack or let a
/// narrowing move a kind's out from under it.
/// </para>
/// <para>
/// <b>A LIST, rendered as a scalar when it holds one.</b> The slice writes
/// <c>hosts: ui</c> and a flight is granted exactly one environment, so a
/// scalar is what a person writes and what this must render. It is typed as a
/// list anyway, because <c>BoundOf</c> and <c>Bound</c> already read and write
/// exactly that shape — <i>a scalar when one, a sequence when more</i> — and
/// choosing <c>string?</c> would make naming a second host a wire change rather
/// than a document change. <b>Labelled as a call, not a law:</b> if a kind may
/// only ever name one, this should be <c>string?</c> and the key should be
/// singular.
/// </para>
/// <para>
/// <b>Membership is NOT checked here, and that is deliberate rather than
/// missing.</b> <c>EnvelopeSelectionTests</c> states the split — <i>"a selection
/// is declared once, validated for membership against what the control plane
/// charts, and never merged. The membership half lives control-plane-side; what
/// this package owns is the shape."</i> No member in this package is validated
/// against another today, and <c>hosts:</c> naming something root does not
/// permit is refused at the door by the control plane (S54.2-03), not by this
/// assembly.
/// </para>
/// </remarks>
public class AWorkKindSaysWhereItsStackRunsTests
{
    private static Envelope With(IReadOnlyList<string>? hosts) => new()
    {
        Context = new ContextBinding { Scope = "src/**", Constitution = "1.0.0" },
        Hosts = hosts,
        Accepts = [],
        Produces = [],
        Obligations =
        [
            new Obligation
            {
                Id = "in-scope",
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
                Discharges = ["in-scope"],
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
                Requires = ["in-scope"],
            },
        ],
    };

    [Test]
    public async Task A_stack_location_survives_being_written_and_read_again()
    {
        var rendered = EnvelopeText.Render(With(["ui"]));

        await Assert.That(rendered).Contains("hosts:");

        var read = EnvelopeYaml.Parse(rendered);

        await Assert.That(read.Envelope!.Hosts).IsEquivalentTo(["ui"]);
    }

    [Test]
    public async Task One_host_is_written_the_way_a_person_would_write_it()
    {
        // `hosts: ui`, not a one-item sequence. This is the spelling the slice
        // uses and the spelling `Bound` already produces for every other
        // set-shaped member, so a document a person hand-writes round-trips
        // without being reformatted under them on the next pull.
        var rendered = EnvelopeText.Render(With(["ui"]));

        await Assert.That(rendered).Contains("hosts: ui");
    }

    [Test]
    public async Task A_document_without_one_renders_byte_for_byte_as_it_did()
    {
        // EVERY ENVELOPE THAT EXISTS TODAY HAS NONE. A key emitted empty would
        // show up as a diff in every working copy on the next pull, and the
        // absence has to keep meaning "this kind's stack runs wherever its loop
        // does" rather than "this kind named no host".
        var rendered = EnvelopeText.Render(With(null));

        await Assert.That(rendered).DoesNotContain("hosts:");

        await Assert.That(EnvelopeYaml.Parse(rendered).Envelope!.Hosts).IsNull();
    }

    [Test]
    public async Task It_is_the_work_kinds_to_say_and_nobody_elses()
    {
        var composes = typeof(Envelope).GetProperty(nameof(Envelope.Hosts))!
            .GetCustomAttributes(typeof(ComposesAttribute), inherit: false)
            .Cast<ComposesAttribute>()
            .Single();

        await Assert.That(composes.Operator).IsEqualTo(MergeOperators.WorkKindOnly)
            .Because("root is not any one job and a narrowing narrows a job already named, so a "
                   + "hosts: that composed would give every kind the floor's stack or let a "
                   + "narrowing move a kind's out from under it.");
    }

    [Test]
    public async Task A_selection_that_names_nothing_is_refused_rather_than_carried()
    {
        // The three shapes `Bound` already refuses for every other set-shaped
        // member, asserted here so that hosts: cannot quietly acquire a laxer
        // one. An empty list is the dangerous one: it reads as a decision and
        // would mean "no host", which is a thing this member cannot express.
        await Assert.That(Envelope.Validate(With([]))).IsNotNull()
            .Because("an empty selection reads as a decision, and the decision it would be "
                   + "naming - no host at all - is the absent case, which is spelled by "
                   + "leaving the key out.");

        await Assert.That(Envelope.Validate(With(["   "]))).IsNotNull();

        await Assert.That(Envelope.Validate(With(["ui\nprod"]))).IsNotNull();
    }

    [Test]
    public async Task Absence_is_not_emptiness_on_the_wire()
    {
        // NULLABLE, NEVER ABSORBING. An accessor returning `?? []` would put
        // this member on the wire for ever and make "said nothing" and "said
        // none" the same byte, which is the law NullIsNotOnTheWireTests holds
        // and the reason every set-shaped member here is nullable.
        await Assert.That(With(null).Hosts).IsNull();
    }
}
