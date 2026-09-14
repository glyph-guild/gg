using Gg.Contracts;
using Gg.Contracts.Authoring;

namespace Gg.Contracts.Tests;

/// <summary>
/// A work kind can say, in one line, what a flight of that kind is TO DO.
/// </summary>
/// <remarks>
/// <para>
/// <b>The runner told every agent to write code, whatever the kind.</b>
/// <c>ClaudeCodeExecutor.Prompt</c> opened with <i>"Make the code changes it
/// asks for"</i> on every flight, and the work kind's instructions were
/// appended after it as standing policy. So <c>score-hal</c> — which writes a
/// number to a tracker field and changes no code at all — sent its agent to
/// find the files a bug report named, and the agent reported back that it had
/// been <i>"asked to make the code changes"</i>. It was.
/// </para>
/// <para>
/// <b>The document could not say otherwise, which is why its author tried to
/// say it in the wrong place.</b> Half of <c>score-hal</c>'s instructions are
/// task statements wearing policy's clothes — <i>"score that item and no
/// other"</i>, <i>"the score is written to Custom.HAL and to no other
/// field"</i> — and they lose, because the hardcoded imperative comes first and
/// names the job. An envelope could say what a flight ACCEPTS, PRODUCES and may
/// LAND, and never what it is to do.
/// </para>
/// <para>
/// <b>Not <see cref="Envelope.Description"/>, which rules itself out.</b> That
/// member's own remark says <i>"it governs nothing … an agent is never shown
/// it - so a wrong one misleads a person and cannot misgovern a flight"</i>.
/// Driving the agent's whole job from it would make a wrong one misgovern every
/// flight of that kind, which is the property it was given to not have.
/// </para>
/// <para>
/// <b>WORK-KIND-ONLY, for <see cref="Envelope.Description"/>'s reason.</b> Root
/// is not any one job and a narrowing narrows a job already named, so a brief
/// that composed would give every kind the floor's task or let a narrowing
/// rewrite the kind's.
/// </para>
/// <para>
/// <b>Called a BRIEF rather than a task.</b> A property named <c>Task</c> in a
/// codebase whose every port returns <c>Task&lt;T&gt;</c> is a name that
/// resolves to two things inside the type that declares it; a brief is what the
/// statement is called anyway.
/// </para>
/// </remarks>
public class AWorkKindSaysWhatTheFlightIsToDoTests
{
    private static Envelope With(string? brief) => new()
    {
        Context = new ContextBinding { Scope = "src/**", Constitution = "1.0.0" },
        Brief = brief,
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
                Id = "score",
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
    public async Task A_brief_survives_being_written_and_read_again()
    {
        var rendered = EnvelopeText.Render(
            With("Score this work item against the HAL rubric and write the score to the tracker."));

        await Assert.That(rendered).Contains("brief:");

        var read = EnvelopeYaml.Parse(rendered);

        await Assert.That(read.Envelope!.Brief).IsEqualTo(
            "Score this work item against the HAL rubric and write the score to the tracker.");
    }

    [Test]
    public async Task A_document_without_one_renders_byte_for_byte_as_it_did()
    {
        // EVERY ENVELOPE THAT EXISTS TODAY HAS NONE, and none of them may be
        // rewritten by this member arriving. A key emitted empty would show up
        // as a diff in every working copy on the next pull - and every kind
        // that says nothing must keep the wording it has always been given.
        var rendered = EnvelopeText.Render(With(null));

        await Assert.That(rendered).DoesNotContain("brief:");

        await Assert.That(EnvelopeYaml.Parse(rendered).Envelope!.Brief).IsNull();
    }

    [Test]
    public async Task It_is_the_work_kinds_to_say_and_nobody_elses()
    {
        var composes = typeof(Envelope).GetProperty(nameof(Envelope.Brief))!
            .GetCustomAttributes(typeof(ComposesAttribute), inherit: false)
            .Cast<ComposesAttribute>()
            .Single();

        await Assert.That(composes.Operator).IsEqualTo(MergeOperators.WorkKindOnly)
            .Because("root is not any one job and a narrowing narrows a job already named, so "
                   + "a brief that composed would give every kind the floor's task or let a "
                   + "narrowing rewrite the kind's.");
    }

    [Test]
    public async Task A_brief_that_says_nothing_is_refused_rather_than_carried()
    {
        // AN EMPTY BRIEF IS WORSE THAN NO BRIEF, because no brief keeps the
        // wording every flight has always had and an empty one would replace
        // the only sentence that tells an agent what it is doing with nothing.
        await Assert.That(Envelope.Validate(With("   "))).IsNotNull();
    }

    [Test]
    public async Task A_brief_is_one_line_like_an_instruction_block()
    {
        // ONE LINE, for the reason an instruction block is one: it is read in a
        // prompt and diffed in review, and a paragraph that grows forever is
        // where the round trip through a hand-rolled emitter usually dies.
        await Assert.That(Envelope.Validate(With("Score it.\nThen write it."))).IsNotNull();
    }

    [Test]
    public async Task A_brief_within_the_rules_is_accepted()
    {
        // THE POSITIVE CONTROL. A refusal that fired on everything would make
        // the two above pass while the member was unusable.
        await Assert.That(Envelope.Validate(With("Score this item and write the score.")))
            .IsNull();
    }
}
