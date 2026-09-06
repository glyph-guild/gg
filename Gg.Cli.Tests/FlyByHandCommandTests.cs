using Gg.Cli;
using Gg.Client;
using Gg.Contracts;

namespace Gg.Cli.Tests;

/// <summary>
/// `gg fly --hand` reaches the machinery every other test in this slice built.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE THIRD TIME THIS PRODUCT HAS SHIPPED MACHINERY NOTHING CONSTRUCTS.</b>
/// <c>ClaudeCodeExecutor</c> was built and no runner ever held one, so no flight
/// invoked an agent. <c>TakeSession</c> was built and the console never
/// constructed one. And <c>--hand</c> parsed into <c>CliAction.Fly.ByHand</c>
/// while the dispatch arm read <c>fly.Text</c> and never that — so
/// <c>gg fly --hand "…"</c> opened an ordinary fleet flight, printed it, and
/// handed nobody a terminal.
/// </para>
/// <para>
/// <b>Both doors, from one missing read.</b> The console's key spawns
/// <c>gg fly --hand</c>, so step 5's arm was broken by step 4's gap and every
/// row of both was proven — because <c>FlyByHandArgTests</c> asserts the flag
/// PARSES and <c>FlyByHandWiringTests</c> calls <c>FlyByHand.FlyAsync</c>
/// directly. Neither could see the space between them, which is exactly where
/// the feature was missing.
/// </para>
/// <para>
/// <b>So this asserts the space between them.</b> Every seam is injected, and
/// what is checked is that they are reached, in the order the design requires:
/// the plan BEFORE anything is created, and the terminal only after a flight
/// exists to hand over.
/// </para>
/// </remarks>
public class FlyByHandCommandTests
{
    /// <summary>
    /// What a flight opened now would need. The ITEMS are what a refusal reads -
    /// RequiredLabels is the same list flattened for display, and a fixture that
    /// filled only that one refuses nothing.
    /// </summary>
    private static Checklist APlan(params string[] required) => new()
    {
        EnvelopeVersion = "1",
        RequiredLabels = required,
        Items =
        [
            .. required.Select(label => new ChecklistItem
            {
                Requirement = label,
                Verification = "a runner advertises it",
                Satisfier = ChecklistSatisfiers.MatchingRunner,
                Disposition = LabelDispositions.Stated,
            }),
        ],
    };

    private static CliAction.Fly Flying() =>
        new("fix the timeout", null, Json: false, ByHand: true);

    [Test]
    public async Task It_opens_the_flight_and_then_hands_over_the_terminal_for_it()
    {
        var order = new List<string>();

        var exit = await FlyByHandCommand.RunAsync(
            Flying(),
            plan: _ => { order.Add("plan"); return Task.FromResult(APlan()); },
            advertised: [],
            open: _ =>
            {
                order.Add("open");
                return Task.FromResult<VerbResult>(new VerbResult.Launched(
                    new FlightLaunched { FlightId = "flight-9", FlightNumber = "GG-9" }));
            },
            hold: (flight, _) => { order.Add("hold:" + flight); return Task.FromResult(0); },
            say: _ => { });

        await Assert.That(order).IsEquivalentTo(new[] { "plan", "open", "hold:flight-9" })
            .Because("the plan is read FIRST because everything after it creates something, "
                   + "and the terminal is handed over LAST because there has to be a flight "
                   + "to hand over. A hold that ran before the open would be a runner asking "
                   + "for a flight that does not exist yet.");

        await Assert.That(exit).IsEqualTo(0);
    }

    [Test]
    public async Task A_machine_that_cannot_run_the_flight_opens_nothing_and_holds_nothing()
    {
        var order = new List<string>();
        var said = new List<string>();

        var exit = await FlyByHandCommand.RunAsync(
            Flying(),
            plan: _ => { order.Add("plan"); return Task.FromResult(APlan("gpu")); },
            advertised: ["linux"],
            open: _ => { order.Add("open"); return Task.FromResult<VerbResult>(null!); },
            hold: (flight, _) => { order.Add("hold"); return Task.FromResult(0); },
            say: said.Add);

        await Assert.That(order).IsEquivalentTo(new[] { "plan" })
            .Because("reading the plan after the flight is open answers the same question and "
                   + "leaves the flight behind - somebody else's fleet then works what a "
                   + "person meant to fly themselves.");

        await Assert.That(said).IsNotEmpty()
            .Because("a refusal a person cannot see is a hand-flight that silently did "
                   + "nothing.");

        await Assert.That(exit).IsNotEqualTo(0);
    }

    [Test]
    public async Task A_flight_that_did_not_launch_hands_over_nothing()
    {
        // THE THIRD OUTCOME, and it is the one a happy path forgets. The open
        // can answer something that is not a launch - a refusal from the
        // control plane, a gate diverting it - and there is then no flight id
        // to claim. A runner started anyway would ask for a flight that does
        // not exist and wait out its long poll while the person watches.
        var order = new List<string>();

        var exit = await FlyByHandCommand.RunAsync(
            Flying(),
            plan: _ => { order.Add("plan"); return Task.FromResult(APlan()); },
            open: _ =>
            {
                order.Add("open");
                return Task.FromResult<VerbResult>(new VerbResult.Flights(
                    new FlightList { Flights = [] }));
            },
            advertised: [],
            hold: (flight, _) => { order.Add("hold"); return Task.FromResult(0); },
            say: _ => { });

        // NOTHING HELD is the claim; the exit code is whatever the result
        // itself maps to. A non-launch is not necessarily a failure - a flight
        // diverted to a gate is a real answer - so asserting non-zero here would
        // be asserting something this command does not decide.
        await Assert.That(order).IsEquivalentTo(new[] { "plan", "open" });
    }

    // ---- S26.8-01 and S26.8-02 ----

    private static PendingGate AGate() => new()
    {
        FlightNumber = "GG-9",
        ObligationId = "somebody-looks",
        Approver = "a-lead",
        Because = "the change touches migrations, and somebody has to look at that",
        AwaitingSince = new DateTimeOffset(2026, 9, 6, 6, 0, 0, TimeSpan.Zero),
        ManifestHash = new string('a', 64),
        Attempt = 1,
    };

    [Test]
    public async Task A_gate_the_flight_opened_is_offered_at_the_terminal()
    {
        // THE SAME FIELDS `gg gates` SHOWS, BY CONSTRUCTION RATHER THAN BY
        // COPYING. What is asserted is that the offer renders through the one
        // renderer - so a field added to the gate list appears here without
        // anybody remembering, and a field that stopped appearing there stops
        // appearing here. A second layout would drift on the first change and
        // the drift would be invisible: both would still look like a gate.
        //
        // `because` is the column that makes this worth doing at all. It is the
        // gate list's most important column, and when the condition is "loop
        // asked for a decision" the Engine composes the sentence from the fact,
        // so the agent's own question is the tail of it.
        var said = new List<string>();

        await FlyByHandCommand.RunAsync(
            Flying(),
            plan: _ => Task.FromResult(APlan()),
            advertised: [],
            open: _ => Task.FromResult<VerbResult>(new VerbResult.Launched(
                new FlightLaunched { FlightId = "flight-9", FlightNumber = "GG-9" })),
            hold: (_, _) => Task.FromResult(0),
            say: said.Add,
            gates: _ => Task.FromResult<IReadOnlyList<PendingGate>>([AGate()]),
            answer: _ => DecisionOutcomes.Approved,
            decide: (_, _, _) => Task.FromResult(true));

        var offered = string.Join("\n", said);
        var asGgGatesWouldShowIt = VerbOutput.ToText(
            new VerbResult.Gates(new GateList { Gates = [AGate()] }));

        await Assert.That(offered).Contains(asGgGatesWouldShowIt)
            .Because("one renderer, so the fields cannot drift apart. A second layout would "
                   + "still look like a gate on the day it stopped showing `because`.");
    }

    [Test]
    public async Task The_decision_is_posted_for_the_gate_the_person_answered()
    {
        var posted = new List<string>();

        await FlyByHandCommand.RunAsync(
            Flying(),
            plan: _ => Task.FromResult(APlan()),
            advertised: [],
            open: _ => Task.FromResult<VerbResult>(new VerbResult.Launched(
                new FlightLaunched { FlightId = "flight-9", FlightNumber = "GG-9" })),
            hold: (_, _) => Task.FromResult(0),
            say: _ => { },
            gates: _ => Task.FromResult<IReadOnlyList<PendingGate>>([AGate()]),
            answer: _ => DecisionOutcomes.Rejected,
            decide: (flight, obligation, outcome) =>
            {
                posted.Add($"{flight}:{obligation}:{outcome}");
                return Task.FromResult(true);
            });

        await Assert.That(posted).IsEquivalentTo(new[]
        {
            "GG-9:somebody-looks:" + DecisionOutcomes.Rejected,
        });
    }

    [Test]
    public async Task A_flight_that_opened_no_gate_asks_nobody_anything()
    {
        // THE COMMON CASE GAINS NO PROMPT. A hand-flight whose envelope opened
        // nothing is the ordinary one, and a prompt that appeared anyway - even
        // an empty one - would make every flight cost a keystroke.
        var asked = 0;

        await FlyByHandCommand.RunAsync(
            Flying(),
            plan: _ => Task.FromResult(APlan()),
            advertised: [],
            open: _ => Task.FromResult<VerbResult>(new VerbResult.Launched(
                new FlightLaunched { FlightId = "flight-9", FlightNumber = "GG-9" })),
            hold: (_, _) => Task.FromResult(0),
            say: _ => { },
            gates: _ => Task.FromResult<IReadOnlyList<PendingGate>>([]),
            answer: _ => { asked++; return DecisionOutcomes.Approved; },
            decide: (_, _, _) => Task.FromResult(true));

        await Assert.That(asked).IsEqualTo(0);
    }
}
