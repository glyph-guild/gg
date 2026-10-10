using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// A person's console mints a short-lived credential for one agent session, and the gg tools
/// in that session act as the person through it (slice seventy-one, ADR-0039 Amendment 2).
/// </summary>
/// <remarks>
/// <b>Delegable is a short, named list.</b> A delegated session acts with less than the person:
/// only the routes the gg tool servers call take one, and widening the list is a change to
/// Decision 12, not a route opting in quietly - so the list is pinned here by name.
/// </remarks>
public class ASessionCanBeDelegatedTests
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    /// <summary>What `gg manage tools` and `gg itinerary tools` call, and logout, which revokes.</summary>
    private static readonly string[] ToolRoutes =
    [
        "GET /v1/auth/whoami",
        "POST /v1/auth/logout",
        "GET /v1/gates",
        "GET /v1/board",
        "POST /v1/board/{id}/decisions",
        "GET /v1/flights",
        "POST /v1/flights",
        "GET /v1/flights/{ref}",
        "GET /v1/flights/{ref}/story",
        "GET /v1/flights/{ref}/why",
        "GET /v1/flights/{ref}/log",
        "POST /v1/flights/{ref}/decisions",
        "POST /v1/flights/{ref}/grounding",
        "GET /v1/runners",
        "GET /v1/airspace/watch-standings",
        "POST /v1/airspace/names",
        "POST /v1/airspace/envelopes/{name}/retirement",
        "GET /v1/itineraries",
        "GET /v1/itineraries/{ref}",
        "GET /v1/itineraries/menu",
        "POST /v1/itineraries/check",
        "POST /v1/itineraries",
    ];

    [Test]
    public async Task Exactly_the_routes_the_gg_tools_call_take_a_delegated_session()
    {
        var delegable = ProtocolSurface.Endpoints.Where(e => e.Delegable).Select(e => $"{e.Method} {e.Path}");

        await Assert.That(delegable).IsEquivalentTo(ToolRoutes)
            .Because("credentials, keys, allowances, introductions, enrolment and minting stay refused.");
        await Assert.That(ProtocolSurface.Endpoints.Where(e => e.Delegable).All(e => e.Audience == Audience.Developer))
            .IsTrue()
            .Because("a delegated session is a person's, narrowed; no runner route takes one.");
    }

    [Test]
    public async Task A_person_mints_one_against_an_introduction()
    {
        var mint = ProtocolSurface.Endpoints.SingleOrDefault(e => e.Method == "POST" && e.Path == "/v1/auth/delegations");

        await Assert.That(mint).IsNotNull();
        await Assert.That(mint!.Audience).IsEqualTo(Audience.Developer);
        await Assert.That(mint.Delegable).IsFalse().Because("a delegated session may not mint another.");
        await Assert.That(mint.Request).IsEqualTo(typeof(AgentDelegationRequest));
        await Assert.That(mint.Response).IsEqualTo(typeof(AgentDelegation));
        await Assert.That(ProtocolSurface.JsonMembers[typeof(AgentDelegationRequest)])
            .IsEquivalentTo(["runnerId", "agentSessionId", "introductionId"]);
        await Assert.That(ProtocolSurface.JsonMembers[typeof(AgentDelegation)])
            .IsEquivalentTo(["delegationId", "token", "expiresAt"]);
        await Assert.That(Vocabulary.Types).Contains(typeof(AgentDelegationRequest)).And.Contains(typeof(AgentDelegation));
    }

    [Test]
    public async Task The_credential_crosses_in_a_frame_of_its_own()
    {
        var frame = new DelegateAgentSession { Token = "t0k3n", ExpiresAt = Noon };

        var bytes = AgentFrameCodec.Encode(frame);

        await Assert.That((int)bytes[0]).IsEqualTo(8);
        await Assert.That(AgentFrameCodec.Decode(bytes)).IsEqualTo(frame);
        await Assert.That(Vocabulary.Types).Contains(typeof(DelegateAgentSession));
    }

    [Test]
    public async Task A_machine_says_it_will_use_one_and_the_fleet_read_carries_it()
    {
        await Assert.That(ProtocolSurface.JsonMembers[typeof(RunnerHeartbeat)]).Contains("takesAgentDelegation");
        await Assert.That(ProtocolSurface.JsonMembers[typeof(RunnerSummary)]).Contains("takesAgentDelegation");

        var beat = new RunnerHeartbeat { Labels = [], TakesAgentDelegation = true };
        var row = new RunnerSummary { RunnerId = "r", Label = "l", State = "idle", TakesAgentDelegation = true };

        await Assert.That(beat.TakesAgentDelegation).IsTrue();
        await Assert.That(row.TakesAgentDelegation).IsTrue();
    }
}
