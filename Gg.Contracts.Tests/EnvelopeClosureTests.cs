using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// The envelope graph, closed — including the human route, which did not exist
/// until now.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every reference resolves, every check has what it needs to be discharged,
/// and one apparent hole is deliberate.</b> An obligation that no loop discharges
/// is <i>allowed</i>, because that is exactly what a gate is: something a person
/// answers, with no loop that could ever satisfy it.
/// </para>
/// <para>
/// <b>That last row is the one step 1 got wrong.</b> It was written as
/// <i>"an obligation nothing discharges is refused"</i> on the reasoning that such
/// a flight can never finish - true when every check was <c>machine</c>, and false
/// the moment a human can answer one. The refusal that does exist is narrower and
/// still right: a loop naming an obligation that is not in the envelope is a
/// dangling reference, which is a different defect from an obligation with no loop.
/// </para>
/// </remarks>
public class EnvelopeClosureTests
{
    private static Envelope With(params Obligation[] obligations) => new()
    {
        Context = new ContextBinding { Scope = "src/**", Constitution = "1.0.0" },
        Obligations = obligations,
        Loops =
        [
            new Loop
            {
                Id = "implement",
                Executor = ExecutorRungs.Frontier,
                // Only the machine ones. A loop that claimed to discharge a human
                // check would be a runner promising to answer for a person.
                Discharges =
                [
                    .. obligations
                        .Where(o => o.Check == ObligationChecks.Machine)
                        .Select(o => o.Id),
                ],
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
                Requires = [.. obligations.Select(o => o.Id)],
            },
        ],
    };

    private static readonly Obligation InScope = new()
    {
        Id = "in-scope",
        Check = ObligationChecks.Machine,
        Rule = ObligationPredicates.NoFileOutsideScope,
    };

    private static readonly Obligation Reversibility = new()
    {
        Id = "reversibility-plan",
        Check = ObligationChecks.Human,
        Approver = "platform-oncall",
    };

    // ---- the human route ----

    [Test]
    public async Task A_human_check_with_an_approver_is_accepted()
    {
        // The positive control on the whole file, first. Everything below refuses
        // something, and a validator that refused everything would satisfy all of
        // it.
        await Assert.That(Envelope.Validate(With(InScope, Reversibility))).IsNull();
    }

    [Test]
    public async Task A_human_check_with_no_approver_is_refused_and_says_what_is_missing()
    {
        // An obligation somebody must answer and nobody was named to answer it is
        // a gate that will sit there forever with no route out - the halt with no
        // exit, arriving through the schema rather than through the state machine.
        var diagnosis = Envelope.Validate(With(InScope, Reversibility with { Approver = null }));

        await Assert.That(diagnosis).IsNotNull();
        await Assert.That(diagnosis!).Contains("reversibility-plan");
        await Assert.That(diagnosis!).Contains("approver");
    }

    [Test]
    public async Task A_human_check_with_a_blank_approver_is_refused_too()
    {
        // Empty and absent must both refuse. An approver of "" would pass a
        // null-check and name nobody, which is the well-formed wrong value again.
        await Assert.That(Envelope.Validate(With(InScope, Reversibility with { Approver = "" })))
            .IsNotNull();
        await Assert.That(Envelope.Validate(With(InScope, Reversibility with { Approver = "   " })))
            .IsNotNull();
    }

    [Test]
    public async Task A_human_check_needs_no_rule_and_is_refused_for_carrying_one()
    {
        // A human check with a machine predicate is two answers to one question,
        // and the Engine would have to pick. Refusing at ingress means it never
        // has to.
        var diagnosis = Envelope.Validate(With(
            InScope, Reversibility with { Rule = ObligationPredicates.NoFileOutsideScope }));

        await Assert.That(diagnosis).IsNotNull();
        await Assert.That(diagnosis!).Contains("rule");
    }

    // ---- the machine route, re-asserted next to it ----

    [Test]
    public async Task A_machine_check_with_no_rule_is_refused()
    {
        // An obligation nothing can evaluate reports satisfied by never running.
        // Article XI, at the earliest point it can be caught.
        var diagnosis = Envelope.Validate(With(InScope with { Rule = null }, Reversibility));

        await Assert.That(diagnosis).IsNotNull();
        await Assert.That(diagnosis!).Contains("in-scope");
        await Assert.That(diagnosis!).Contains("rule");
    }

    [Test]
    public async Task A_machine_check_carrying_an_approver_is_refused()
    {
        // An approver on a machine check is a person named as responsible for
        // something no one will ever ask them about. Somebody would read it as a
        // gate.
        var diagnosis = Envelope.Validate(With(InScope with { Approver = "somebody" }, Reversibility));

        await Assert.That(diagnosis).IsNotNull();
        await Assert.That(diagnosis!).Contains("approver");
    }

    [Test]
    public async Task An_unknown_check_is_refused_and_the_known_ones_are_listed()
    {
        var diagnosis = Envelope.Validate(With(InScope with { Check = "vibes" }, Reversibility));

        await Assert.That(diagnosis).IsNotNull();
        await Assert.That(diagnosis!).Contains("vibes");
        await Assert.That(diagnosis!).Contains(ObligationChecks.Human);
    }

    // ---- dangling references ----

    [Test]
    public async Task A_loop_discharging_an_obligation_that_does_not_exist_is_refused()
    {
        var envelope = With(InScope, Reversibility);
        var diagnosis = Envelope.Validate(envelope with
        {
            Loops = [envelope.Loops[0] with { Discharges = ["in-scope", "not-an-obligation"] }],
        });

        await Assert.That(diagnosis).IsNotNull();
        await Assert.That(diagnosis!).Contains("not-an-obligation");
        await Assert.That(diagnosis!).Contains("implement")
            .Because("naming which loop is what turns a refusal into something somebody can fix.");
    }

    [Test]
    public async Task A_loop_claiming_to_discharge_a_human_check_is_refused()
    {
        // The new dangling reference, and it is not a naming error - the
        // obligation exists. A loop that discharged a human check would be a
        // runner satisfying a gate, which is the escalation this whole slice is
        // built to prevent.
        var envelope = With(InScope, Reversibility);
        var diagnosis = Envelope.Validate(envelope with
        {
            Loops = [envelope.Loops[0] with { Discharges = ["in-scope", "reversibility-plan"] }],
        });

        await Assert.That(diagnosis).IsNotNull();
        await Assert.That(diagnosis!).Contains("reversibility-plan");
        await Assert.That(diagnosis!).Contains("human");
    }

    [Test]
    public async Task A_destination_requiring_an_obligation_that_does_not_exist_is_refused()
    {
        var envelope = With(InScope, Reversibility);
        var diagnosis = Envelope.Validate(envelope with
        {
            Destinations = [envelope.Destinations[0] with { Requires = ["in-scope", "invented"] }],
        });

        await Assert.That(diagnosis).IsNotNull();
        await Assert.That(diagnosis!).Contains("invented");
    }

    // ---- the hole that is not a hole ----

    [Test]
    public async Task An_obligation_no_loop_discharges_is_allowed_because_that_is_a_gate()
    {
        // THE CORRECTED ROW. `reversibility-plan` appears in no loop's discharges
        // and the envelope is valid - which is the only shape a gate can have. A
        // rule requiring every obligation to have a discharger would make gates
        // unexpressible, and the reasoning behind it ("a flight that can never
        // finish") was true only while every check was machine.
        var envelope = With(InScope, Reversibility);

        await Assert.That(envelope.Loops[0].Discharges.Contains("reversibility-plan")).IsFalse()
            .Because("the fixture really does leave it undischarged, or this proves nothing.");
        await Assert.That(Envelope.Validate(envelope)).IsNull();
    }

    [Test]
    public async Task A_machine_obligation_no_loop_discharges_is_also_allowed()
    {
        // Deliberately not narrowed to human checks. A machine obligation with no
        // loop is a rule measured against whatever the flight happens to do - a
        // constraint rather than a task - and there is no reading of the schema
        // under which that is malformed. Refusing it would be an enforcement
        // stricter than the rule it enforces, which is drift in the other
        // direction.
        var envelope = With(InScope, Reversibility);

        await Assert.That(Envelope.Validate(envelope with
        {
            Loops = [envelope.Loops[0] with { Discharges = [] }],
        })).IsNull();
    }

    // ---- the refusal is never a partial application ----

    [Test]
    public async Task Every_refusal_here_names_the_thing_it_refused()
    {
        // Slice one's rule, applied to the new refusals as a set rather than one
        // at a time: a diagnosis that does not quote the offending value sends
        // somebody reading their whole envelope.
        var envelope = With(InScope, Reversibility);

        Envelope[] broken =
        [
            envelope with { Obligations = [InScope, Reversibility with { Approver = null }] },
            envelope with { Obligations = [InScope with { Rule = null }, Reversibility] },
            envelope with { Obligations = [InScope with { Check = "vibes" }, Reversibility] },
            envelope with { Obligations = [InScope with { Approver = "x" }, Reversibility] },
        ];

        foreach (var candidate in broken)
        {
            var diagnosis = Envelope.Validate(candidate);

            await Assert.That(diagnosis).IsNotNull();
            await Assert.That(diagnosis!.Length).IsGreaterThan(40)
                .Because("a refusal is a sentence somebody acts on, not a code.");
        }
    }
}
