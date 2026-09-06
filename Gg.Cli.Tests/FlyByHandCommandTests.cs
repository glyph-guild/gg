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
    private static Checklist APlan(params string[] required) => new()
    {
        EnvelopeVersion = "1",
        RequiredLabels = required,
        Items = [],
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

        await Assert.That(order).IsEquivalentTo(new[] { "plan", "open" });
        await Assert.That(exit).IsNotEqualTo(0);
    }
}
