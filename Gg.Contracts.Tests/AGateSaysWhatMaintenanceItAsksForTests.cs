using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// A gate raised by the platform about a machine says so, so a console can
/// offer the act that clears it rather than approve or reject.
/// </summary>
/// <remarks>
/// <para>
/// <b>A gate cannot say what it is for through <c>Because</c>.</b> For an
/// unconditional obligation the engine writes <i>"this obligation declares no
/// condition, so it always applies…"</i>, and a flight's name is <i>"what
/// somebody CALLED the flight"</i>. So the runner-login gate a maintenance
/// flight opens carries its own member, on <see cref="GateNomination"/>'s
/// argument: <i>"its own type because it is one fact with one standing."</i>
/// </para>
/// <para>
/// <b>Null rather than an empty ask</b> - <i>"absence is silence"</i>, the rule
/// <c>Commit</c> and <c>Nomination</c> already state. Every gate on a flight a
/// person asked for carries none, and an older control plane that never sets
/// it renders as an ordinary gate.
/// </para>
/// <para>
/// <b>And the flight's log has a kind for the incident</b>, because the
/// control plane's <c>StoryKindClosureTests</c> refuses a log kind this contract
/// has not declared: an undeclared one makes every story of that flight throw
/// rather than merely miss a line.
/// </para>
/// </remarks>
public class AGateSaysWhatMaintenanceItAsksForTests
{
    private static PendingGate AGate(GateMaintenance? maintenance = null) => new()
    {
        FlightNumber = "GG-130",
        ObligationId = "human-review",
        Approver = "an-architect",
        ManifestHash = new string('a', 64),
        Because = "this obligation declares no condition, so it always applies",
        AwaitingSince = new DateTimeOffset(2026, 9, 15, 12, 0, 0, TimeSpan.Zero),
        Attempt = 1,
        Maintenance = maintenance,
    };

    [Test]
    public async Task An_ordinary_gate_carries_no_maintenance_ask()
    {
        await Assert.That(AGate().Maintenance).IsNull()
            .Because("absence is silence: most gates are on flights a person asked for.");
    }

    [Test]
    public async Task An_agent_login_gate_names_the_runner_and_the_provider()
    {
        var gate = AGate(new GateMaintenance
        {
            Kind = GateMaintenanceKinds.AgentLogin,
            Runner = "01a0a374-0000-7000-8000-000000000000",
            RunnerLabel = "gg-pool-ui-1",
            Provider = "claude",
            Diagnosis = "the agent is not logged in and gg holds no token for it",
        });

        await Assert.That(gate.Maintenance!.Kind).IsEqualTo("agent-login");
        await Assert.That(gate.Maintenance.Runner).IsNotEmpty()
            .Because("the console reaches the runner by id to run the ceremony.");
        await Assert.That(gate.Maintenance.Provider).IsEqualTo("claude")
            .Because("which adapter's ceremony to run is the provider's to say.");
    }

    [Test]
    public async Task The_kinds_are_closed()
    {
        await Assert.That(GateMaintenanceKinds.All).IsEquivalentTo(
            (string[])[GateMaintenanceKinds.AgentLogin]);
    }

    [Test]
    public async Task Both_types_are_pinned_registered_and_declared_on_the_wire()
    {
        await Assert.That(typeof(GateMaintenance).GetCustomAttributes(typeof(PinnedIdAttribute), false))
            .IsNotEmpty();
        await Assert.That(Vocabulary.Types).Contains(typeof(GateMaintenance));

        await Assert.That(ProtocolSurface.JsonMembers[typeof(PendingGate)]).Contains("maintenance");
        await Assert.That(ProtocolSurface.JsonMembers[typeof(GateMaintenance)]).IsEquivalentTo(
            (string[])["kind", "runner", "runnerLabel", "provider", "diagnosis"]);
    }

    [Test]
    public async Task The_flights_log_has_a_kind_for_a_runner_incident()
    {
        await Assert.That(StoryKinds.All).Contains(StoryKinds.RunnerIncident);
        await Assert.That(StoryKinds.RunnerIncident).IsEqualTo("runner-incident");

        var sentence = FlightStory.Sentence(
            StoryKinds.RunnerIncident, ["gg-pool-ui-1 reports its claude agent is not logged in"]);

        await Assert.That(sentence).Contains("gg-pool-ui-1 reports its claude agent is not logged in")
            .Because("the entry's diagnosis is the whole of what a person reads.");

        // NOT A GAP: it interrupts at any stage and belongs to none of the six,
        // exactly as a pool incident does.
        await Assert.That(FlightStages.Of(StoryKinds.RunnerIncident)).IsNull();
    }
}
