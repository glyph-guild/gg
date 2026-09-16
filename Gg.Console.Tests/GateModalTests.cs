using System.Text.RegularExpressions;
using Gg.Console;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// The gate's path through the console.
/// </summary>
/// <remarks>
/// <para>
/// <b>The queue holds decisions, not flights.</b> A flight nobody needs anything from
/// should be countable rather than readable, and the health metric of this surface is
/// emptiness - so what puts a row here is somebody being needed, and a gate waiting on a
/// person is the clearest case there is.
/// </para>
/// <para>
/// <b>One flight, one mutation path.</b> The modal and <c>gg decide</c> go through the same
/// code. Two paths to one state transition is how a client's view drifts from the control
/// plane's, and this is the first time two surfaces exist at all.
/// </para>
/// </remarks>
public class GateModalTests
{
    [Test]
    public async Task A_gate_waiting_on_a_person_is_a_queue_row()
    {
        // The queue is a queue of DECISIONS. A gate is the case where somebody is
        // unambiguously needed, so if it does not appear here the pane is a flight list
        // with extra steps.
        await Assert.That(Enum.GetNames<QueueReason>()).Contains(nameof(QueueReason.AwaitingDecision));
    }

    [Test]
    public async Task Deciding_is_a_modal_of_its_own()
    {
        // Not an action on the flight-actions menu: what is being decided has to be stated,
        // the evidence has to be in front of the person, and both answers have to be
        // offered together. A menu item cannot do any of that.
        await Assert.That(Enum.GetNames<UiMode>()).Contains(nameof(UiMode.GateDecision));
    }

    [Test]
    public async Task The_modal_offers_both_answers_and_exactly_one_escape()
    {
        // OWNS THE KEYBOARD, with one way out. The escape is not a third answer - it
        // leaves the gate open and decides nothing - and it exists because a terminal
        // somebody cannot get out of is the worst thing a TUI can do to them.
        var bindings = Keymap.Bindings(
            new KeymapContext(UiMode.GateDecision));

        var commands = bindings.Select(b => b.Command).ToList();

        await Assert.That(commands).Contains(Command.ApproveGate);
        await Assert.That(commands).Contains(Command.RejectGate);
        await Assert.That(commands).Contains(Command.CloseModal);

        await Assert.That(bindings.Count(b => b.Command == Command.CloseModal)).IsEqualTo(1)
            .Because("exactly one escape - two ways out of a modal is two things to explain "
                   + "and one of them will be wrong.");
    }

    [Test]
    public async Task An_agent_login_gate_offers_log_in_first_and_an_ordinary_gate_does_not()
    {
        // THE THIRD THING A GATE CAN OFFER, and only this kind of gate offers
        // it. What clears a runner's agent-login gate is not an answer: it is
        // logging the agent in, after which the runner reports ready and the
        // control plane withdraws the flight. So `s` sits beside the two
        // answers rather than replacing them - somebody who has decided this
        // machine is never coming back still rejects it - and it is FIRST,
        // because RenderModalButtons focuses the first button and the act
        // that fixes the machine is the one to land on.
        var asking = Keymap.Bindings(
            new KeymapContext(UiMode.GateDecision) { GateAsksForAgentLogin = true });
        var ordinary = Keymap.Bindings(new KeymapContext(UiMode.GateDecision));

        await Assert.That(asking.Select(b => b.Command)).Contains(Command.LogAgentIn);
        await Assert.That(asking[0].Command).IsEqualTo(Command.LogAgentIn)
            .Because("the first binding is the focused button, and the act that clears this "
                   + "gate is the one a person should land on.");
        await Assert.That(asking.Select(b => b.Command)).Contains(Command.ApproveGate)
            .Because("a person who has decided the machine is not coming back still answers "
                   + "the gate the ordinary way.");
        await Assert.That(asking.Select(b => b.Command)).Contains(Command.RejectGate);
        await Assert.That(asking.Count(b => b.Command == Command.CloseModal)).IsEqualTo(1);

        await Assert.That(ordinary.Select(b => b.Command)).DoesNotContain(Command.LogAgentIn)
            .Because("a key that does nothing on the gate in front of somebody is the thing "
                   + "AModalDoesNotAdvertiseDeadKeys refuses.");
    }

    [Test]
    public async Task The_actions_modal_offers_the_login_too_and_an_ordinary_flight_does_not()
    {
        // WHERE PEOPLE ACTUALLY LOOK. The gate modal binds `s`, and reaching it
        // means pressing `d` first - so somebody who opens "what can be done"
        // on a held runner's flight is shown Open flight, Decide and Approve,
        // and none of the three is the act that clears the gate in front of
        // them. Reported from the running console, which is the only place
        // this shows up: every test of the act itself passed.
        //
        // THIS MODAL'S WHOLE SUBJECT IS WHAT CAN BE DONE, so an act that can
        // be done and is not here is the omission the modal exists to prevent.
        var asking = Keymap.Bindings(
            new KeymapContext(UiMode.FlightActions, AGateWaits: true)
            { GateAsksForAgentLogin = true });
        var ordinary = Keymap.Bindings(
            new KeymapContext(UiMode.FlightActions, AGateWaits: true));

        await Assert.That(asking.Select(b => b.Command)).Contains(Command.LogAgentIn);
        await Assert.That(asking.Select(b => b.Command)).Contains(Command.ApproveGate)
            .Because("the two answers stay, because somebody who has decided the machine is "
                   + "not coming back still answers the gate the ordinary way.");

        await Assert.That(ordinary.Select(b => b.Command)).DoesNotContain(Command.LogAgentIn)
            .Because("an ordinary gate has no agent to log in, and a key that does nothing "
                   + "on the flight in front of somebody is what this modal's own remark "
                   + "calls a key that should not have been offered.");

        // AND THE READING ONE STAYS FIRST, which this modal's remark calls the
        // safety rather than a preference: enter twice is the commonest thing
        // a person does to it, and the default must not be an act.
        await Assert.That(asking[0].Command).IsEqualTo(Command.ShowFlight)
            .Because("RenderModalButtons focuses the first button, and opening the flight is "
                   + "the one choice here that changes nothing.");
    }

    [Test]
    public async Task The_login_key_is_offered_only_for_the_kind_this_build_knows()
    {
        // GateMaintenanceKinds IS CLOSED so that a kind a newer control plane
        // invents renders as an ordinary gate here rather than offering a key
        // that would start the wrong ceremony.
        var known = KeymapContext.For(WaitingOn(new GateMaintenance
        {
            Kind = GateMaintenanceKinds.AgentLogin,
            Runner = "runner-1",
            Provider = "claude",
        }));
        var unknown = KeymapContext.For(WaitingOn(new GateMaintenance
        {
            Kind = "reboot-the-host",
            Runner = "runner-1",
            Provider = "claude",
        }));
        var none = KeymapContext.For(WaitingOn(null));

        await Assert.That(known.GateAsksForAgentLogin).IsTrue();
        await Assert.That(unknown.GateAsksForAgentLogin).IsFalse()
            .Because("a maintenance kind this build does not know is a gate to read, not an "
                   + "act to offer.");
        await Assert.That(none.GateAsksForAgentLogin).IsFalse();
    }

    /// <summary>A queue sitting on one gate, with the maintenance ask given.</summary>
    private static AppState WaitingOn(GateMaintenance? maintenance) => new()
    {
        Mode = UiMode.GateDecision,
        Gates = new GateList
        {
            Gates =
            [
                new PendingGate
                {
                    FlightNumber = "GG-24-0001",
                    ObligationId = "check-human",
                    Approver = "the-runner-decider",
                    Branch = null,
                    Commit = null,
                    ManifestHash = new string('e', 64),
                    Condition = "a person signs the agent in",
                    Because = "this obligation declares no condition, so it always applies",
                    AwaitingSince = DateTimeOffset.UnixEpoch,
                    Attempt = 1,
                    Maintenance = maintenance,
                },
            ],
        },
        Queue =
        [
            new QueueRow
            {
                Key = "f1",
                Reference = "GG-24-0001",
                FlightId = "f1",
                FlightNumber = "GG-24-0001",
                Name = "maintain gg-pool-ui-3 agent-login",
                Reason = QueueReason.AwaitingDecision,
                Since = DateTimeOffset.UnixEpoch,
            },
        ],
    };

    [Test]
    public async Task Nothing_else_answers_a_gate_while_the_modal_is_open()
    {
        // It owns the keyboard: a key that means something in Normal mode must not reach
        // through the modal and act on the flight behind it.
        var inModal = Keymap.Resolve(
            KeyStroke.Char('?'),
            new KeymapContext(UiMode.GateDecision));

        var inNormal = Keymap.Resolve(
            KeyStroke.Char('?'),
            new KeymapContext(UiMode.Normal));

        await Assert.That(inNormal).IsNotNull()
            .Because("ASK WHY IT PASSES: if this key meant nothing anywhere, the assertion "
                   + "below would hold for the wrong reason.");
        await Assert.That(inModal).IsNull()
            .Because("the modal has the keyboard while it is open.");
    }

    [Test]
    public async Task Exactly_one_thing_writes_a_decision()
    {
        // INVARIANT 4, STRUCTURALLY. The modal and `gg decide` reach one implementation.
        // Two paths to one state transition is how a console's view and the control
        // plane's record drift apart, and nothing would say which was right.
        var writers = ClientSources()
            .Where(f => Regex.IsMatch(File.ReadAllText(f), @"\bDecideAsync\s*\("))
            .Select(f => Path.GetFileName(f)!)
            .Order(StringComparer.Ordinal)
            .ToList();

        await Assert.That(writers).IsEquivalentTo(new[]
        {
            // Declared and implemented: the one place a decision is posted.
            "FlightCommands.cs",

            // The verb, calling it.
            "ControlPlaneClient.cs",
        })
            .Because("a third file posting a decision is a second path to one transition. "
                   + "Found: " + string.Join(", ", writers));
    }

    [Test]
    public async Task The_console_reaches_that_one_writer_rather_than_its_own()
    {
        // The other half: the modal must actually go through it. A structural test that
        // only counted writers would pass if the console wrote nothing at all.
        var console = ConsoleSources()
            .Where(f => File.ReadAllText(f).Contains("DecideAsync", StringComparison.Ordinal))
            .Select(f => Path.GetFileName(f)!)
            .ToList();

        await Assert.That(console).IsNotEmpty()
            .Because("the modal reaches the verb's implementation, or it is not on the same "
                   + "path at all.");
    }

    [Test]
    public async Task The_modal_marks_nothing_satisfied_by_itself()
    {
        // ARTICLE IX, WITH A MODAL IN PLAY - and a modal is exactly where local state
        // feels natural, because the person just pressed a key and something should
        // happen. What happens is a post; what is rendered is what came back.
        var reading = new Regex(
            @"Outcome\s*=\s*ObligationOutcomes|Satisfied\s*=\s*true|MarkSatisfied|"
          + @"Admitted\s*=\s*true", RegexOptions.Compiled);

        var offenders = ConsoleSources()
            .Where(f => reading.IsMatch(File.ReadAllText(f)))
            .Select(f => Path.GetFileName(f)!)
            .ToList();

        await Assert.That(offenders).IsEmpty()
            .Because("the console renders what the control plane decided. Found: "
                   + string.Join(", ", offenders));

        await Assert.That(reading.IsMatch("state = state with { Satisfied = true };")).IsTrue()
            .Because("the scan can see one, so the emptiness above means something.");
    }

    private static IEnumerable<string> ClientSources() => Under("Gg.Client");

    private static IEnumerable<string> ConsoleSources() => Under("Gg.Console");

    private static IEnumerable<string> Under(string project)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Gg.sln")))
        {
            dir = dir.Parent;
        }

        var root = dir ?? throw new InvalidOperationException("Gg.sln not found");

        return Directory.EnumerateFiles(
                Path.Combine(root.FullName, project), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                            StringComparison.Ordinal)
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                            StringComparison.Ordinal));
    }
}
