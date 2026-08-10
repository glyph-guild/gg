using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// The flight read surface is part of the declared protocol now.
/// </summary>
/// <remarks>
/// <para>
/// It lived outside the governed prefixes while nothing consumed it, which was
/// honest: a declaration nobody checks is a comment. The console consumes it
/// next, so it comes in under the same closure guarantee as <c>/v1/auth</c>
/// and <c>/v1/runner</c> - a route the control plane serves inside
/// <c>/v1/flights</c> that this file does not name fails that repository's
/// build.
/// </para>
/// <para>
/// These assertions are about the DECLARATION, not about any server. What the
/// control plane actually serves is checked over there, against this same
/// artifact.
/// </para>
/// </remarks>
public class FlightSurfaceDeclarationTests
{
    private static Endpoint Declared(string method, string path) =>
        ProtocolSurface.Endpoints.Single(e => e.Method == method && e.Path == path);

    [Test]
    public async Task The_flight_prefix_is_governed()
    {
        await Assert.That(ProtocolSurface.GovernedPrefixes).Contains("/v1/flights")
            .Because("undeclared, the control plane could serve a flight route gg knows nothing about.");
    }

    [Test]
    public async Task Every_verb_landing_this_step_has_an_endpoint_to_call()
    {
        // gg fly, gg show, gg log, gg runners - in that order. A verb whose
        // endpoint is not declared is a verb that cannot work.
        await Assert.That(Declared("POST", "/v1/flights").Request).IsEqualTo(typeof(FlightLaunchRequest));
        await Assert.That(Declared("POST", "/v1/flights").Response).IsEqualTo(typeof(FlightLaunched));
        await Assert.That(Declared("GET", "/v1/flights").Response).IsEqualTo(typeof(FlightList));
        await Assert.That(Declared("GET", "/v1/flights/{ref}").Response).IsEqualTo(typeof(FlightSummary));
        await Assert.That(Declared("GET", "/v1/flights/{ref}/log").Response).IsEqualTo(typeof(FlightLog));
        await Assert.That(Declared("GET", "/v1/runners").Response).IsEqualTo(typeof(RunnerList));
    }

    [Test]
    public async Task The_whole_flight_surface_answers_to_a_developer()
    {
        // Not to a runner. A runner that could read the flight list could
        // enumerate a tenant's work from a credential that is only supposed to
        // let it hold one lease at a time.
        foreach (var endpoint in ProtocolSurface.Endpoints
                     .Where(e => e.Path.StartsWith("/v1/flights", StringComparison.Ordinal)))
        {
            await Assert.That(endpoint.Audience).IsEqualTo(Audience.Developer)
                .Because($"{endpoint.Method} {endpoint.Path} is a person's view of their own tenant.");
            await Assert.That(endpoint.RequiredHeaders).Contains(ProtocolSurface.SessionHeader);
        }
    }

    [Test]
    public async Task Reading_the_runner_fleet_is_a_developers_call_and_writing_to_it_is_not()
    {
        // Same path, two audiences, and the split is the point: a person lists
        // runners, a runner beats. Neither can do the other's call.
        await Assert.That(Declared("GET", "/v1/runners").Audience).IsEqualTo(Audience.Developer);
        await Assert.That(Declared("POST", "/v1/runners").Audience).IsEqualTo(Audience.Developer);
        await Assert.That(Declared("POST", "/v1/runners/{id}/heartbeat").Audience).IsEqualTo(Audience.Runner);
    }

    [Test]
    public async Task A_flight_reference_is_a_placeholder_the_matcher_understands()
    {
        // Both forms hit the same declared endpoint. If the matcher disagreed,
        // gg would report one of them as an undeclared path it just called.
        await Assert.That(ProtocolSurface.Find("GET", "/v1/flights/GG-42")?.Path).IsEqualTo("/v1/flights/{ref}");
        await Assert.That(ProtocolSurface.Find("GET", $"/v1/flights/{Guid.NewGuid()}")?.Path)
            .IsEqualTo("/v1/flights/{ref}");
        await Assert.That(ProtocolSurface.Find("GET", "/v1/flights/GG-42/log")?.Path)
            .IsEqualTo("/v1/flights/{ref}/log");

        // And the list is not the same endpoint as one flight.
        await Assert.That(ProtocolSurface.Find("GET", "/v1/flights")?.Path).IsEqualTo("/v1/flights");
    }

    [Test]
    public async Task Every_flight_endpoint_may_refuse_a_caller_below_the_floor()
    {
        foreach (var endpoint in ProtocolSurface.Endpoints
                     .Where(e => e.Path.StartsWith("/v1/flights", StringComparison.Ordinal)))
        {
            await Assert.That(endpoint.Statuses).Contains(ProtocolSurface.ProtocolTooOld);
            await Assert.That(endpoint.Statuses).Contains(401);
        }
    }

    [Test]
    public async Task Every_wire_type_the_declaration_names_has_declared_member_names()
    {
        // The check that caught a casing split between two green suites. A
        // response type with no entry here is one nobody is comparing.
        var undeclared = ProtocolSurface.Endpoints
            .SelectMany(e => (Type?[])[e.Request, e.Response])
            .Where(t => t is not null)
            .Distinct()
            .Where(t => !ProtocolSurface.JsonMembers.ContainsKey(t!))
            .Select(t => t!.Name)
            .ToList();

        await Assert.That(undeclared).IsEmpty()
            .Because($"these cross the wire with nothing pinning their member names: {string.Join(", ", undeclared)}");
    }

    [Test]
    public async Task Every_declared_wire_type_is_in_the_vocabulary()
    {
        // The vocabulary is what the fingerprint is computed over. A type on
        // the wire but outside it would change the protocol without moving the
        // contract version.
        var missing = ProtocolSurface.JsonMembers.Keys
            .Where(t => !Vocabulary.Types.Contains(t))
            .Select(t => t.Name)
            .ToList();

        await Assert.That(missing).IsEmpty()
            .Because($"outside the vocabulary these change the wire invisibly: {string.Join(", ", missing)}");
    }

    [Test]
    public async Task No_endpoint_anywhere_accepts_a_runner_status()
    {
        // Status is DERIVED, from heartbeat age, lease and current flight. A
        // runner that could report "busy" could report it while wedged, and a
        // wedged runner that looks busy blocks the takeover that should reclaim
        // its flight. This is structural rather than a matter of discipline:
        // the field would have to exist on a request type to be sent.
        var offenders = new List<string>();

        foreach (var request in ProtocolSurface.Endpoints
                     .Select(e => e.Request)
                     .Where(t => t is not null)
                     .Distinct())
        {
            foreach (var property in request!.GetProperties())
            {
                if (property.Name.Contains("Status", StringComparison.OrdinalIgnoreCase)
                    || property.Name.Contains("State", StringComparison.OrdinalIgnoreCase)
                    || property.Name is "Busy" or "Idle" or "Offline")
                {
                    offenders.Add($"{request.Name}.{property.Name}");
                }
            }
        }

        await Assert.That(offenders).IsEmpty()
            .Because("the control plane derives runner state; nothing may report it. Found: "
                   + string.Join(", ", offenders));
    }

    [Test]
    public async Task The_runner_list_reports_a_state_that_only_the_control_plane_can_have_decided()
    {
        // The mirror of the test above: state appears on the RESPONSE, which is
        // the direction it is allowed to travel.
        await Assert.That(ProtocolSurface.JsonMembers[typeof(RunnerSummary)]).Contains("state");
        await Assert.That(ProtocolSurface.Endpoints.Any(e => e.Response == typeof(RunnerList))).IsTrue();
    }
}
