using Gg.Console;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// A machine saying what it lacks, read in the console (slice forty-six, step 4).
/// </summary>
/// <remarks>
/// <b>The gate with no answer on it.</b> Every other gate is a person's to
/// approve or reject. A bring-up ask is cleared by the machine's next reading:
/// approving it ends the flight with the item still missing and the next
/// reading opens another, and rejecting it ends it without the machine ever
/// having been fixed. Two keys that do not answer teach a person to stop
/// reading the surface they are on.
/// </remarks>
public class TheConsoleAnswersABringUpGateTests
{
    private const string Runner = "01a0bca3-b788-72e5-b40a-be3811653226";

    private static PendingGate Waiting(GateMaintenance? maintenance) => new()
    {
        FlightNumber = "GG-201",
        ObligationId = "maintenance-oncall",
        Approver = "kdeenanauth",
        Branch = null,
        Commit = null,
        ManifestHash = new string('e', 64),
        Condition = "always",
        Because = "this obligation declares no condition",
        AwaitingSince = DateTimeOffset.UnixEpoch,
        Attempt = 1,
        Maintenance = maintenance,
    };

    private static AppState Over(PendingGate gate) => new()
    {
        Mode = UiMode.GateDecision,
        Gates = new GateList { Gates = [gate] },
        Queue =
        [
            new QueueRow
            {
                Key = "f1",
                Reference = gate.FlightNumber,
                FlightId = "f1",
                FlightNumber = gate.FlightNumber,
                Name = "maintain vmlinux002 bring-up: forge ado",
                Reason = QueueReason.AwaitingDecision,
                Since = DateTimeOffset.UnixEpoch,
            },
        ],
    };

    private static AppState Gate(
        string kind, string? item = null, string? subject = null, string? diagnosis = null) =>
        Over(Waiting(new GateMaintenance
        {
            Kind = kind,
            Runner = Runner,
            RunnerLabel = "vmlinux002",
            Provider = kind == GateMaintenanceKinds.AgentLogin ? "claude" : "",
            Item = item,
            Subject = subject,
            Diagnosis = diagnosis,
        }));

    private static IReadOnlyList<Command> Offered(AppState state) =>
        Keymap.Bindings(KeymapContext.For(state)).Select(b => b.Command).ToList();

    [Test]
    public async Task Neither_answer_is_offered_on_a_bring_up_ask()
    {
        var keys = Offered(Gate(GateMaintenanceKinds.BringUp, ReadinessKinds.Forge, "ado"));

        await Assert.That(keys).DoesNotContain(Command.ApproveGate);
        await Assert.That(keys).DoesNotContain(Command.RejectGate);
        await Assert.That(keys).Contains(Command.CloseModal)
            .Because("a modal with no way out is worse than one with keys that do nothing.");
    }

    [Test]
    public async Task Every_other_gate_still_has_both()
    {
        var ordinary = Offered(Over(Waiting(maintenance: null)));

        await Assert.That(ordinary).Contains(Command.ApproveGate);
        await Assert.That(ordinary).Contains(Command.RejectGate);
    }

    [Test]
    public async Task An_agent_login_gate_keeps_its_ceremony_and_its_answers()
    {
        var keys = Offered(Gate(GateMaintenanceKinds.AgentLogin));

        await Assert.That(keys).Contains(Command.LogAgentIn);
        await Assert.That(keys).Contains(Command.ApproveGate)
            .Because("somebody who has decided the machine is not coming back still rejects "
                   + "it, which is why that gate kept both.");
    }

    [Test]
    [Arguments(ReadinessKinds.Agent, "claude", "--agent-binary")]
    [Arguments(ReadinessKinds.Credential, "keyvault://a-vault/npm", "gg credential send")]
    [Arguments(ReadinessKinds.Forge, "ado", "nothing a console does opens one")]
    public async Task It_says_what_is_missing_and_where_it_is_answered(
        string item, string subject, string expected)
    {
        var said = ConsoleBringUp.Said(Gate(GateMaintenanceKinds.BringUp, item, subject));

        await Assert.That(said).Contains("vmlinux002", StringComparison.Ordinal);
        await Assert.That(said).Contains(subject, StringComparison.Ordinal);
        await Assert.That(said).Contains(expected, StringComparison.Ordinal);
    }

    [Test]
    public async Task The_machines_own_words_are_carried_where_it_gave_them()
    {
        var said = ConsoleBringUp.Said(Gate(
            GateMaintenanceKinds.BringUp, ReadinessKinds.Forge, "ado",
            diagnosis: "forge.example.com could not be reached on 443: HostNotFound."));

        await Assert.That(said).Contains("HostNotFound", StringComparison.Ordinal)
            .Because("the machine measured it, and a console that paraphrased would be a "
                   + "second account of one reading.");
    }

    [Test]
    public async Task An_item_this_build_does_not_know_is_named_rather_than_guessed_at()
    {
        var said = ConsoleBringUp.Said(Gate(GateMaintenanceKinds.BringUp, "vault-lease", "a-lease"));

        await Assert.That(said).Contains("vault-lease", StringComparison.Ordinal)
            .Because("an item a newer control plane added is a thing to name, not an act to "
                   + "guess - the closed vocabulary's rule, one level down.");
    }

    [Test]
    public async Task A_gate_that_is_not_a_bring_up_ask_says_none_of_this()
    {
        await Assert.That(ConsoleBringUp.Said(Gate(GateMaintenanceKinds.AgentLogin))).IsEmpty();
    }
}
