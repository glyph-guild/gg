using System.Text.Json;
using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// What Remote Control needs from the wire (slice seventy-one, ADR-0039 Amendment 1):
/// a frame that forgets an ended session, a session's end, a runner's sessions in the
/// fleet read, and a runner's recent flights.
/// </summary>
public class TheRemoteControlContractTests
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    [Test]
    [Arguments("a1b2")]
    [Arguments(null)]
    public async Task Forgetting_a_session_or_every_ended_one_round_trips(string? sessionId)
    {
        var frame = new ForgetAgentSession { SessionId = sessionId };

        var bytes = AgentFrameCodec.Encode(frame);

        await Assert.That(bytes[0]).IsEqualTo(AgentFrameKinds.Forget);
        await Assert.That(AgentFrameCodec.Decode(bytes)).IsEqualTo(frame)
            .Because("null forgets every ended session, an id forgets that one.");
    }

    [Test]
    public async Task Forget_is_a_console_to_runner_frame()
    {
        var kind = AgentFrameCodec.Encode(new ForgetAgentSession())[0];
        await Assert.That((int)kind).IsLessThan(64);
        await Assert.That(Vocabulary.Types).Contains(typeof(ForgetAgentSession));
    }

    [Test]
    public async Task A_session_says_when_it_ended()
    {
        var ended = new AgentSessionStanding { SessionId = "c3d4", StartedAt = Noon, Alive = false, EndedAt = Noon.AddHours(1) };

        await Assert.That(ProtocolSurface.JsonMembers[typeof(AgentSessionStanding)]).Contains("endedAt");
        await Assert.That(ended.EndedAt).IsEqualTo(Noon.AddHours(1));
    }

    [Test]
    public async Task The_fleet_read_carries_a_runners_sessions_and_whether_it_takes_them()
    {
        await Assert.That(ProtocolSurface.JsonMembers[typeof(RunnerSummary)])
            .Contains("acceptsAgentSessions")
            .And.Contains("agentSessions");

        var runner = new RunnerSummary
        {
            RunnerId = "runner-2",
            Label = "vmlinux002",
            State = "idle",
            AcceptsAgentSessions = true,
            AgentSessions = [new AgentSessionStanding { SessionId = "a1b2", StartedAt = Noon, Alive = true }],
        };

        await Assert.That(runner.AcceptsAgentSessions).IsTrue();
        await Assert.That(runner.AgentSessions!.Single().SessionId).IsEqualTo("a1b2");
    }

    [Test]
    public async Task A_runners_recent_flights_are_a_declared_read()
    {
        var route = ProtocolSurface.Endpoints.SingleOrDefault(e =>
            e.Method == "GET" && e.Path == "/v1/runners/{id}/flights");

        await Assert.That(route).IsNotNull();
        await Assert.That(route!.Audience).IsEqualTo(Audience.Developer);
        await Assert.That(route.Response).IsEqualTo(typeof(RunnerFlightList));
        await Assert.That(ProtocolSurface.JsonMembers[typeof(RunnerFlightList)]).IsEquivalentTo(["flights"]);
        await Assert.That(ProtocolSurface.JsonMembers[typeof(RunnerFlight)])
            .IsEquivalentTo(["flightId", "number", "kind", "state", "claimedAt", "endedAt"]);
        await Assert.That(Vocabulary.Types).Contains(typeof(RunnerFlightList)).And.Contains(typeof(RunnerFlight));
    }
}
