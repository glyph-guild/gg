namespace Gg.Contracts.Tests;

/// <summary>
/// One move says the envelope will not bound this agent, and it is the whole
/// answer or it is not there.
/// </summary>
/// <remarks>
/// <para>
/// <b>Asked for plainly: "we need an option to allow the LLM to be
/// dangerous."</b> Every move in this vocabulary names one tool, and a flight
/// that needs the whole of an agent - a migration that must run a database, a
/// spike nobody can enumerate the tools of in advance - had two ways to get
/// there, and both were worse than saying so. It could declare
/// <c>run-tests</c> and reach the rest through <c>Bash</c>, which is a bound
/// defeated rather than declined; or somebody could turn the bound off on the
/// machine, which is invisible to every envelope and every fact.
/// </para>
/// <para>
/// <b>A VALUE and not a field, which is the whole reason it costs a version.</b>
/// The argument <c>write</c> was minted under says it: <i>the only safe response
/// to an unknown value in a closed vocabulary is to halt, so an added value
/// breaks every prior reader by design</i>. A new FIELD saying the same thing -
/// <c>unbounded: true</c> - is the opposite: a reader that predates it ignores
/// it and believes the flight was bounded. The dangerous option has to arrive
/// through the door that halts.
/// </para>
/// <para>
/// <b>Alone, because a list that says both is two answers to one question.</b>
/// <c>[read, anything]</c> cannot be read: either the narrower move was meant
/// to hold, in which case <c>anything</c> is a mistake, or it was not, in which
/// case naming it is theatre. Refused where an author can still act.
/// </para>
/// </remarks>
public class ALoopMayRunUnboundedTests
{
    private static Envelope Declaring(params string[] moves) => new()
    {
        Context = new ContextBinding { Scope = "**", Constitution = "1.0.0" },
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
                Moves = moves,
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
    public async Task The_word_is_in_the_vocabulary()
    {
        await Assert.That(LoopMoves.Anything).IsEqualTo("anything");

        await Assert.That(LoopMoves.All).Contains(LoopMoves.Anything)
            .Because("an envelope that cannot name it would leave the only way to get here "
                   + "being a machine somebody reconfigured, which no fact records.");
    }

    [Test]
    public async Task A_loop_that_names_it_is_unbounded_and_one_that_does_not_is_not()
    {
        await Assert.That(LoopMoves.Unbounded([LoopMoves.Anything])).IsTrue();

        await Assert.That(LoopMoves.Unbounded(
                [LoopMoves.Read, LoopMoves.Edit, LoopMoves.RunTests])).IsFalse()
            .Because("run-tests grants Bash and Bash can do almost anything - but almost is "
                   + "the difference between a bound that leaks and no bound at all, and "
                   + "only one of the two was declared.");

        await Assert.That(LoopMoves.Unbounded([])).IsFalse();
    }

    [Test]
    public async Task On_its_own_it_is_a_valid_envelope()
    {
        await Assert.That(Envelope.Validate(Declaring(LoopMoves.Anything))).IsNull()
            .Because("the point is that this is authorable and visible, not that it is "
                   + "impossible - an option nobody can declare is the machine-level "
                   + "switch this exists to replace.");
    }

    [Test]
    public async Task Beside_another_move_it_is_refused_naming_the_loop()
    {
        var refused = Envelope.Validate(Declaring(LoopMoves.Read, LoopMoves.Anything));

        await Assert.That(refused).IsNotNull()
            .Because("a reader cannot tell whether 'read' was meant to hold, and a bound "
                   + "nobody can read is worse than no bound, because it looks like one.");

        await Assert.That(refused!).Contains("'" + LoopMoves.Anything + "'")
            .Because("the refusal names the value somebody has to remove.");
        await Assert.That(refused!).Contains("implement")
            .Because("and the loop it is on, so the envelope path is exact.");
    }

    [Test]
    public async Task The_order_it_is_refused_in_puts_the_unknown_move_first()
    {
        // UNCHANGED ORDERING, asserted because this adds a second refusal to
        // the same loop. A move nobody declared is refused as unknown rather
        // than as a bad companion: the vocabulary gate stands in front.
        var unknown = Envelope.Validate(Declaring(LoopMoves.Anything, "run-migrations"));

        await Assert.That(unknown).IsNotNull();
        await Assert.That(unknown!).Contains("Unknown move");
    }

    [Test]
    public async Task It_is_record_only_like_everything_else_and_that_is_not_the_bound()
    {
        // THE CLASSIFICATION CANNOT BE THE HONEST ONE, and the table says so in
        // its own words. An outward act whose enforcement no probe can confirm
        // is refused at authoring, so classifying this one would make it
        // undeclarable - and run-tests already carries the same gap for the
        // same reason, one tool narrower.
        await Assert.That(MoveKinds.Of(LoopMoves.Anything)).IsEqualTo(MoveKinds.RecordOnly);

        await Assert.That(MoveKinds.Table.Keys.Order(StringComparer.Ordinal).ToList())
            .IsEquivalentTo(LoopMoves.All.Order(StringComparer.Ordinal).ToList())
            .Because("the totality guard, which is what makes adding a move a decision "
                   + "somebody takes rather than one they discover.");
    }

    [Test]
    public async Task A_sweep_may_not_declare_it()
    {
        // NOT HERE, AND NOT YET. A sweep's moves arrive from a watch the
        // control plane resolved and a schedule performs; what was asked for
        // is a FLIGHT that may run unbounded, which an envelope declares and a
        // person can see on the flight. A value that also worked through the
        // second door would be a bound lost somewhere nobody was looking.
        var sweep = ASweepIsAServedActionTests.AnAction() with
        {
            Moves = [LoopMoves.Anything],
        };

        var refused = WatchAction.Validate(sweep);

        await Assert.That(refused).IsNotNull();
        await Assert.That(refused!).Contains(LoopMoves.Anything);
    }
}
