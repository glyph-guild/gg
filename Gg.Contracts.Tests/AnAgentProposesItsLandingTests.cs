using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// An agent states what its proposal should be called, and the envelope says
/// how.
/// </summary>
/// <remarks>
/// <para>
/// <b>A title cut out of prose is not one the agent chose.</b> The landing
/// composes one from the loop's account - first sentence, marker off, word
/// boundary - and that is a fallback with the failure mode of every fallback:
/// it is whatever the agent happened to write first. Pull request 8629 was
/// called <i>"GG-118: Destination 'pull-request' requires 'in-scope', and it
/// holds."</i>, which is the same class of accident one rung lower.
/// </para>
/// <para>
/// <b>A move, a tool, and a fact, which is this project's one idiom for a
/// deliberate statement.</b> <c>propose</c> grants the nomination tool and
/// <c>propose-work-item</c> grants the proposal tool; an agent calls it, gg's
/// own server answers, and the digest extracts what was ANSWERED - literally,
/// with no model in the path. A convention inside a summary would be none of
/// that: <c>CONSIDERED:</c> has been written into every flight's prose for
/// weeks, is cut by the reason's own first-paragraph rule, and is read by
/// nothing.
/// </para>
/// <para>
/// <b>The instruction is the destination's, because the title is about what it
/// opens.</b> <c>branch:</c> is already there for the same reason, and a team
/// whose review convention wants an imperative under seventy characters has
/// nowhere else to say so that reaches the agent at all.
/// </para>
/// <para>
/// <b>RECORD-ONLY, like the proposal beside it.</b> Calling it changes nothing
/// and grants nothing: the runner uses the title if it lands, admission can
/// refuse the flight entirely, and a person reading <c>gg facts</c> can see
/// what the agent asked for beside what was published.
/// </para>
/// </remarks>
public class AnAgentProposesItsLandingTests
{
    [Test]
    public async Task The_move_exists_and_an_envelope_may_name_it()
    {
        // A CLOSED VOCABULARY, so this costs a version: the only safe response
        // to an unknown value is to halt, and an added value breaks every prior
        // reader by design. Envelopes in force are unchanged in meaning - they
        // cannot propose a landing, which they could not before either.
        await Assert.That(LoopMoves.All).Contains(LoopMoves.ProposeLanding);
        await Assert.That(LoopMoves.ProposeLanding).IsEqualTo("propose-landing");
    }

    [Test]
    public async Task It_is_record_only_because_calling_it_moves_nothing()
    {
        // The classification is held in a dictionary rather than a switch
        // precisely so a new move cannot arrive unclassified - and the
        // unclassified default would be record-only, which is the answer that
        // lets an unrecallable act be granted by accident.
        await Assert.That(MoveKinds.Of(LoopMoves.ProposeLanding))
            .IsEqualTo(MoveKinds.RecordOnly);
    }

    [Test]
    public async Task A_proposed_landing_is_a_fact_kind()
    {
        await Assert.That(FactKinds.All).Contains(FactKinds.LandingProposal);
        await Assert.That(FactKinds.LandingProposal).IsEqualTo("landing.proposal");
    }

    [Test]
    public async Task The_envelope_carries_it_and_nothing_else_does()
    {
        // ONE SLOT PER KIND, which is what makes a fact whose kind and payload
        // disagree refusable rather than merely wrong.
        var envelope = new FactEnvelope
        {
            IdempotencyKey = "k",
            Kind = FactKinds.LandingProposal,
            Digest = new string('a', 64),
            ObservedAt = DateTimeOffset.UnixEpoch,
            Landing = new LandingProposal { Title = "Remove the residual explicit local" },
        };

        await Assert.That(FactEnvelope.Validate(envelope)).IsNull();

        await Assert.That(FactEnvelope.Validate(envelope with { Landing = null }))
            .IsNotNull()
            .Because("a fact of this kind carrying no proposal is one that says nothing, and "
                   + "the runner would have to invent what the agent meant.");
    }

    [Test]
    public async Task A_title_is_required_and_a_description_is_not()
    {
        // THE TITLE IS THE POINT. A description is a courtesy - the one a
        // destination writes already carries the branch and the work item, so
        // an agent with nothing to add leaves it out rather than padding it.
        await Assert.That(
                LandingProposal.Validate(new LandingProposal { Title = "Name the change" }))
            .IsNull();

        await Assert.That(LandingProposal.Validate(new LandingProposal { Title = "  " }))
            .IsNotNull();
    }

    [Test]
    public async Task A_title_that_is_an_essay_is_refused_rather_than_trimmed()
    {
        // REFUSED, NOT TRUNCATED, on the nomination's terms: a silent trim is a
        // title the agent did not write appearing under its name, and the agent
        // can read a refusal and shorten it.
        var essay = new LandingProposal { Title = new string('a', LandingProposal.MaxTitle + 1) };

        await Assert.That(LandingProposal.Validate(essay)).IsNotNull();

        await Assert.That(LandingProposal.Validate(
                new LandingProposal { Title = new string('a', LandingProposal.MaxTitle) }))
            .IsNull();
    }

    [Test]
    public async Task A_title_is_one_line()
    {
        // A pull request title is a single line on every forge that has one.
        // An agent that sent a paragraph would have it rendered as a title
        // somewhere and as a title-with-newlines somewhere else.
        await Assert.That(LandingProposal.Validate(
                new LandingProposal { Title = "Two\nlines" }))
            .IsNotNull();
    }

    [Test]
    public async Task A_description_past_its_bound_is_refused_too()
    {
        await Assert.That(LandingProposal.Validate(new LandingProposal
        {
            Title = "Name the change",
            Description = new string('b', LandingProposal.MaxDescription + 1),
        })).IsNotNull();
    }

    [Test]
    public async Task A_destination_says_how_to_name_what_it_opens()
    {
        // BESIDE branch:, and for its reason. The instruction is about what
        // this destination opens, and a team's review convention has nowhere
        // else to say so that reaches an agent at all.
        var destination = new Destination
        {
            Id = "pull-request",
            Kind = DestinationKinds.PullRequest,
            Requires = ["in-scope"],
            Title = "Imperative mood, under seventy characters, name the change.",
            Description = "Say what changed and why. Do not repeat the diff.",
        };

        await Assert.That(destination.Title).IsNotEmpty();
        await Assert.That(destination.Description).IsNotEmpty();
    }

    [Test]
    public async Task A_kind_that_opens_nothing_may_not_say_how_to_name_it()
    {
        // ON BRANCH'S TERMS, one member over: a tracker destination lands by
        // writing a field and opens no proposal, so instructions for naming one
        // are a setting somebody makes and believes they made.
        var tracker = new Envelope
        {
            Context = new ContextBinding { Scope = "**", Constitution = "1.0.0" },
            Obligations =
            [
                new Obligation
                {
                    Id = "looked",
                    Check = ObligationChecks.Machine,
                    Rule = ObligationPredicates.LoopNotExhausted,
                },
            ],
            Loops =
            [
                new Loop
                {
                    Id = "triage",
                    Executor = ExecutorRungs.Frontier,
                    Discharges = ["looked"],
                    Moves = [LoopMoves.Read, LoopMoves.ProposeWorkItem],
                    Budget = new LoopBudget { WallClock = "30m" },
                    OnExhaustion = ExhaustionPolicies.HandoffToHuman,
                },
            ],
            Destinations =
            [
                new Destination
                {
                    Id = "backlog",
                    Kind = DestinationKinds.WorkItemTracker,
                    Requires = ["looked"],
                    MayPerform = [WorkItemOperations.Field],
                    MayWrite = ["Custom.Score"],
                    Title = "Imperative mood.",
                },
            ],
        };

        await Assert.That(Envelope.Validate(tracker)).IsNotNull();
    }

    [Test]
    public async Task A_lease_carries_the_instruction_to_the_agent()
    {
        // WHERE THE AGENT CAN SEE IT. A document nothing renders into a lease
        // is a policy nobody is told about - which is the omission that left
        // `instructions:` green end to end with nothing reaching an agent, and
        // then `brief:` after it, in the same method.
        var loop = new LeaseLoop
        {
            LoopId = "implement",
            Executor = ExecutorRungs.Frontier,
            Moves = [LoopMoves.Read, LoopMoves.Edit, LoopMoves.ProposeLanding],
            WallClockSeconds = 600,
            OnExhaustion = ExhaustionPolicies.HandoffToHuman,
            Landing = new LeaseLanding
            {
                Title = "Imperative mood, under seventy characters.",
                Description = "Say what changed and why.",
            },
        };

        await Assert.That(loop.Landing!.Title).IsNotEmpty();
        await Assert.That(loop.Landing!.Description).IsNotEmpty();
    }
}
