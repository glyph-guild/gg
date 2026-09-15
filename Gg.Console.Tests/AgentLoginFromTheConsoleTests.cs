using Gg.Console;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// The act behind the agent-login gate's <c>s</c>: pure, and it refuses before
/// anybody is asked for a code.
/// </summary>
/// <remarks>
/// <para>
/// <b>The gate says which runner, so the cursor does not.</b> The queue's
/// cursor is on a FLIGHT - the maintenance flight the incident minted - and
/// the machine that needs the login is named in the gate's own maintenance
/// ask. Reading the runner off the cursor would log in whatever runner
/// happened to be selected on a different pane.
/// </para>
/// <para>
/// <b>It refuses what the ceremony cannot do, here rather than at the
/// channel</b> - <c>ConsoleSendCredential</c>'s argument, and it matters for
/// the same reason: the cost of getting it wrong is that somebody visits a
/// browser and types a code for a machine that was never reachable.
/// </para>
/// <para>
/// <b>And it never decides the gate.</b> Logging the agent in is what clears
/// this gate - the runner reports <c>ready</c>, the incident recovers and the
/// flight is withdrawn - so an approval typed here would answer a question
/// nobody asked and leave the runner still unable to fly.
/// </para>
/// </remarks>
public class AgentLoginFromTheConsoleTests
{
    private static readonly DateTimeOffset Asked = new(2026, 9, 15, 9, 0, 0, TimeSpan.Zero);

    private static PendingGate Gate(GateMaintenance? maintenance) => new()
    {
        FlightNumber = "GG-24-0001",
        ObligationId = "check-human",
        Approver = "the-runner-decider",
        Branch = null,
        Commit = null,
        ManifestHash = new string('e', 64),
        Condition = "a person signs the agent in",
        Because = "this obligation declares no condition, so it always applies",
        AwaitingSince = Asked,
        Attempt = 1,
        Maintenance = maintenance,
    };

    private static GateMaintenance Login(string runner = "runner-1") => new()
    {
        Kind = GateMaintenanceKinds.AgentLogin,
        Runner = runner,
        RunnerLabel = "gg-pool-ui-3",
        Provider = "claude",
        Diagnosis = "the agent is not logged in",
    };

    private static AppState Waiting(GateMaintenance? maintenance, string runnerState = RunnerStates.Idle)
    {
        var gate = Gate(maintenance);

        return new AppState
        {
            Gates = new GateList { Gates = [gate] },
            Runners = new RunnerList
            {
                Runners =
                [
                    new RunnerSummary
                    {
                        RunnerId = "runner-1",
                        Label = "gg-pool-ui-3",
                        State = runnerState,
                        LastHeartbeatAt = Asked,
                    },
                ],
            },
            Queue =
            [
                new QueueRow
                {
                    Key = "f1",
                    Reference = gate.FlightNumber,
                    FlightId = "f1",
                    FlightNumber = gate.FlightNumber,
                    Name = "maintain gg-pool-ui-3 agent-login",
                    Reason = QueueReason.AwaitingDecision,
                    Since = Asked,
                },
            ],
            Mode = UiMode.GateDecision,
        };
    }

    [Test]
    public async Task A_gate_that_asks_for_nothing_says_so_and_asks_nobody()
    {
        var asked = 0;

        var after = ConsoleAgentLogin.Begin(
            Waiting(maintenance: null), (_, _) => { asked++; return "logged in"; });

        await Assert.That(asked).IsEqualTo(0)
            .Because("a gate with no maintenance ask is an ordinary gate, and starting a "
                   + "ceremony over one would run a program for a question about something "
                   + "else entirely.");
        await Assert.That(after.LastCredential).IsNotNull();
        await Assert.That(after.LastCredential!).Contains("not asking")
            .Because("said rather than silent: a key that did nothing is the defect this arm "
                   + "exists because of.");
    }

    [Test]
    public async Task A_runner_that_is_not_beating_is_not_asked_for_a_code()
    {
        var asked = 0;

        var after = ConsoleAgentLogin.Begin(
            Waiting(Login(), runnerState: RunnerStates.Offline),
            (_, _) => { asked++; return "logged in"; });

        await Assert.That(asked).IsEqualTo(0);
        await Assert.That(after.LastCredential!).Contains("gg-pool-ui-3")
            .Because("which machine, so a fleet of them is actionable.");
        await Assert.That(after.LastCredential!).Contains("not beating")
            .Because("a person asked to visit a browser for a machine that is switched off "
                   + "has typed a code for nothing.");
    }

    [Test]
    public async Task A_gate_naming_a_runner_this_console_cannot_see_says_so()
    {
        var after = ConsoleAgentLogin.Begin(
            Waiting(Login(runner: "runner-nobody-here")), (_, _) => "logged in");

        await Assert.That(after.LastCredential!).Contains("runner-nobody-here")
            .Because("the id is what somebody takes to `gg runners`, and a gate can outlive "
                   + "the fleet read under it.");
    }

    [Test]
    public async Task A_live_runner_is_handed_to_the_ceremony_by_id_and_provider()
    {
        var handed = new List<(string Runner, string Provider)>();

        var after = ConsoleAgentLogin.Begin(
            Waiting(Login()),
            (runner, provider) => { handed.Add((runner, provider)); return "gg-pool-ui-3 now holds it."; });

        await Assert.That(handed).Count().IsEqualTo(1);
        await Assert.That(handed[0].Runner).IsEqualTo("runner-1")
            .Because("the id the gate named, not the row the cursor is on.");
        await Assert.That(handed[0].Provider).IsEqualTo("claude")
            .Because("the agent the gate named, so a fleet running two would not log in the "
                   + "wrong one.");
        await Assert.That(after.LastCredential).IsEqualTo("gg-pool-ui-3 now holds it.");
        await Assert.That(after.Mode).IsEqualTo(UiMode.Normal)
            .Because("the modal is spent: the ceremony took the terminal, and coming back to "
                   + "a modal over a gate that is about to be withdrawn is a screen to dismiss.");
    }

    [Test]
    public async Task The_console_never_decides_an_agent_login_gate()
    {
        // THE WHOLE POINT OF THE KEY. What clears this gate is the runner
        // reporting ready - the incident recovers and the flight is withdrawn -
        // so an approval typed here answers a question nobody asked and leaves
        // the machine still unable to fly.
        var source = ConsoleSource.Text("Gg.Console", "ConsoleAgentLogin.cs");

        foreach (var deciding in (string[])["Decide", "ApproveGate", "RejectGate", "GateOutcome"])
        {
            await Assert.That(source).DoesNotContain(deciding, StringComparison.Ordinal)
                .Because($"'{deciding}' here would make the console answer the gate it is "
                       + "supposed to be clearing by fixing the machine.");
        }
    }

    [Test]
    public async Task It_reaches_no_network_and_starts_nothing_itself()
    {
        // PURE, like every other reducer half of a shell command: the act
        // happens in the loop with the terminal free, and this decides only
        // whether it happens at all.
        var source = ConsoleSource.Text("Gg.Console", "ConsoleAgentLogin.cs");

        foreach (var reaching in (string[])["HttpClient", "Process.Start", "ControlPlaneClient"])
        {
            await Assert.That(source).DoesNotContain(reaching, StringComparison.Ordinal)
                .Because($"'{reaching}' in a UI-session reducer is the rule LiveStreamingTests "
                       + "holds over this project.");
        }
    }
}
