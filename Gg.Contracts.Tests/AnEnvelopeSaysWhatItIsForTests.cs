using Gg.Contracts;
using Gg.Contracts.Authoring;

namespace Gg.Contracts.Tests;

/// <summary>
/// A work kind can say, in one line, what it is for.
/// </summary>
/// <remarks>
/// <para>
/// <b>The names are a tenant's own words and nothing explains them.</b>
/// <c>score-hal</c>, <c>triage</c>, <c>implement</c> — a person being asked
/// which of those a flight is for is being asked to pick between strings they
/// may never have seen. Everything the envelope carries is for the AGENT; the
/// one thing it never carried is a sentence for the person choosing.
/// </para>
/// <para>
/// <b>In the document, because that is where the kind is defined.</b> A
/// description kept anywhere else drifts from the obligations and loops it
/// describes; here it is changed by whoever changes them, in the same file, in
/// the same review.
/// </para>
/// <para>
/// <b>WORK-KIND-ONLY, and that is the whole of the composition rule.</b> Root
/// describes nothing in particular and a narrowing narrows something already
/// described, so a description that composed would give every kind the floor's
/// sentence or let a narrowing overwrite the kind's. The one layer that names a
/// purpose is the one that may say it.
/// </para>
/// </remarks>
public class AnEnvelopeSaysWhatItIsForTests
{
    private static Envelope With(string? description) => new()
    {
        Context = new ContextBinding { Scope = "src/**", Constitution = "1.0.0" },
        Description = description,
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
                Id = "implement",
                Executor = ExecutorRungs.Frontier,
                Discharges = ["in-scope"],
                Moves = [LoopMoves.Read, LoopMoves.Edit],
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
    public async Task A_description_survives_being_written_and_read_again()
    {
        // THE ROUND TRIP IS THE PRODUCT. Pull renders the estate into the
        // working copy and apply parses it back; a line that does not survive
        // both halves is a line nobody can keep.
        var rendered = EnvelopeText.Render(With("Scores backlog items against the HAL rubric."));

        await Assert.That(rendered).Contains("description:");

        var read = EnvelopeYaml.Parse(rendered);

        await Assert.That(read.Envelope!.Description)
            .IsEqualTo("Scores backlog items against the HAL rubric.");
    }

    [Test]
    public async Task A_document_without_one_renders_byte_for_byte_as_it_did()
    {
        // EVERY ENVELOPE THAT EXISTS TODAY HAS NONE, and none of them may be
        // rewritten by this member arriving. A key emitted empty would show up
        // as a diff in every working copy on the next pull.
        var rendered = EnvelopeText.Render(With(null));

        await Assert.That(rendered).DoesNotContain("description:");

        await Assert.That(EnvelopeYaml.Parse(rendered).Envelope!.Description).IsNull();
    }

    [Test]
    public async Task It_is_the_work_kinds_to_say_and_nobody_elses()
    {
        var composes = typeof(Envelope).GetProperty(nameof(Envelope.Description))!
            .GetCustomAttributes(typeof(ComposesAttribute), inherit: false)
            .Cast<ComposesAttribute>()
            .Single();

        await Assert.That(composes.Operator).IsEqualTo(MergeOperators.WorkKindOnly)
            .Because("root describes nothing in particular and a narrowing narrows something "
                   + "already described - one would give every kind the floor's sentence and "
                   + "the other would let a narrowing overwrite the kind's.");
    }

    [Test]
    public async Task Changing_it_is_never_a_widening_in_either_direction()
    {
        // WHAT THAT LETS A TENANT DO, SAID OUT LOUD: reword what a kind says it
        // is for without the widening gate. Nothing governs on it - no
        // obligation reads it, no loop is bounded by it, and it never reaches a
        // prompt - so an envelope that adds, edits or removes it is at-or-below
        // the one before it on every governed quantity, which is what the
        // comparator asks. The alternative is a human approval for a typo.
        var none = With(null);
        var said = With("Scores backlog items against the HAL rubric.");
        var other = With("Scores items on the Agentic backlog.");

        await Assert.That(EnvelopeDirection.Widening(none, said)).IsNull()
            .Because("adding a sentence grants nothing.");

        await Assert.That(EnvelopeDirection.Widening(said, none)).IsNull()
            .Because("and removing one takes nothing away that was ever enforced.");

        await Assert.That(EnvelopeDirection.Widening(said, other)).IsNull()
            .Because("nor does rewording it, which is the case a gate would make daily.");
    }

    [Test]
    public async Task The_topology_carries_it_so_a_picker_never_reads_an_envelope()
    {
        // WHERE THE CONSOLE READS IT. A modal listing a tenant's kinds has the
        // topology and nothing else; fetching an envelope per row to find one
        // sentence would be a read per name every time somebody opens a
        // question.
        var name = new TopologyName
        {
            Name = "score-hal",
            Role = Roles.WorkKind,
            Parent = "root",
            Description = "Scores backlog items against the HAL rubric.",
            DeclaredBy = "Kevin",
            DeclaredAt = DateTimeOffset.UnixEpoch,
        };

        await Assert.That(name.Description)
            .IsEqualTo("Scores backlog items against the HAL rubric.");

        await Assert.That(new TopologyName
        {
            Name = "implement", Role = Roles.WorkKind, Parent = "root",
            DeclaredBy = "Kevin", DeclaredAt = DateTimeOffset.UnixEpoch,
        }.Description).IsNull()
            .Because("nullable, because the two repositories are not upgraded in step - a "
                   + "control plane that predates this member sends none, and a console that "
                   + "demanded one would refuse every tenant in the field.");
    }
}
