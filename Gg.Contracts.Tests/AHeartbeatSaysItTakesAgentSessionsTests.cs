using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// A heartbeat says whether the machine takes ad hoc agent sessions, and which it holds
/// (slice seventy, ADR-0039 Decision 3).
/// </summary>
/// <remarks>
/// <b>The machine opts in, and says so where the control plane can read it.</b> The
/// control plane refuses to introduce anyone for <c>drive-an-agent</c> to a machine that
/// has not, the way it refuses <c>configure-this-runner</c> to one that does not accept
/// configuration - and the heartbeat is where it learns that, as it learns
/// <c>AcceptsConfiguration</c>. The sessions ride the same beat, so the record of who
/// used a machine and for how long is kept without a second report.
/// </remarks>
public class AHeartbeatSaysItTakesAgentSessionsTests
{
    [Test]
    public async Task The_heartbeat_declares_both_members()
    {
        await Assert.That(ProtocolSurface.JsonMembers[typeof(RunnerHeartbeat)])
            .Contains("acceptsAgentSessions");
        await Assert.That(ProtocolSurface.JsonMembers[typeof(RunnerHeartbeat)])
            .Contains("agentSessions");
        await Assert.That(ProtocolSurface.JsonMembers.ContainsKey(typeof(AgentSessionStanding)))
            .IsTrue()
            .Because("a session's standing crosses as JSON on the heartbeat, as well as in a frame.");
    }

    [Test]
    public async Task Absent_is_an_older_runner_not_a_refusal()
    {
        // NULL IS UNSAID, AcceptsConfiguration's rule. A runner from before this
        // member says nothing, and the control plane reads that as not opted in -
        // which is the safe answer - without it being a value anybody chose.
        var older = new RunnerHeartbeat { Labels = [] };

        await Assert.That(older.AcceptsAgentSessions).IsNull();
        await Assert.That(older.AgentSessions).IsNull();
    }
}
